Option Strict On
Imports System.Threading
Imports System.Windows.Threading

Imports Clipper2Lib

Imports PolyCut.Shared

Public Class WeedingBoxGenerator
    Public Const ROOT_NAME As String = "Weeding Boxes"

    Private Shared ReadOnly Sources As New Dictionary(Of Guid, Guid())

    Private Const GeometryTolerance As Double = 0.001
    Private Const BoundaryRadius As Double = 0.001
    Private Const CoordinateScale As Double = 100000 'Scale to integer coords for Clipper2Lib

    Private ReadOnly _exteriorMargin As Double
    Private ReadOnly _minimumMargin As Double
    Private ReadOnly _stroke As Brush

    Public Sub New(Optional configuration As WeedingBoxConfiguration = Nothing)
        Dim settings = If(configuration, New WeedingBoxConfiguration())
        _exteriorMargin = settings.DesiredExteriorMargin
        _minimumMargin = settings.MinimumInterObjectMargin
        _stroke = ColorAndBrushHelpers.DeserializeBrush(settings.Colour)
    End Sub

    Public Function Generate(items As IEnumerable(Of IDrawable), relativeTo As UIElement, Optional canvasItems As IEnumerable(Of IDrawable) = Nothing, Optional skipExisting As Boolean = False) As List(Of Shape)
        Dim artwork = CaptureItems(items, relativeTo, canvasItems, skipExisting, CancellationToken.None).ToList()
        Return CreateShapes(PlanArtwork(artwork, CancellationToken.None), artwork, CancellationToken.None)
    End Function

    Public Async Function GenerateAsync(items As IEnumerable(Of IDrawable), relativeTo As UIElement, Optional canvasItems As IEnumerable(Of IDrawable) = Nothing, Optional skipExisting As Boolean = False, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of Shape))
        Dim artwork As New List(Of ArtworkItem)
        For Each item In CaptureItems(items, relativeTo, canvasItems, skipExisting, cancellationToken)
            artwork.Add(item)
            Await Dispatcher.Yield(DispatcherPriority.Background)
        Next
        Dim plans = Await Task.Run(Function() PlanArtwork(artwork, cancellationToken), cancellationToken)
        Return CreateShapes(plans, artwork, cancellationToken)
    End Function

    ' Only frozen geometry and value data cross to the calculation thread.
    Private Shared Iterator Function CaptureItems(items As IEnumerable(Of IDrawable), relativeTo As UIElement, canvasItems As IEnumerable(Of IDrawable), skipExisting As Boolean, cancellationToken As CancellationToken) As IEnumerable(Of ArtworkItem)
        relativeTo.VerifyAccess()
        cancellationToken.ThrowIfCancellationRequested()
        Dim targets = items.Where(Function(item) item IsNot Nothing AndAlso TypeOf item IsNot DrawableGroup AndAlso Not IsWeedingItem(item)).ToHashSet()
        Dim allItems = If(canvasItems, targets).ToList()
        Dim boxedSources = If(skipExisting, allItems.Where(AddressOf IsWeedingItem).SelectMany(AddressOf GetSourceIds).ToHashSet(), New HashSet(Of Guid))
        Dim leaves = allItems.Where(Function(item) item IsNot Nothing AndAlso TypeOf item IsNot DrawableGroup).
            SelectMany(Function(item) If(item.IsAnyGroup(), item.GetLeafChildren(), New List(Of IDrawable) From {item})).Distinct()
        For Each item In leaves
            cancellationToken.ThrowIfCancellationRequested()
            If IsWeedingItem(item) OrElse item.AncestorsAndSelf().Any(Function(parent) TypeOf parent IsNot DrawableGroup AndAlso parent.IsHidden) Then Continue For
            Dim source = GetLocalGeometry(item.DrawableElement)
            If source Is Nothing OrElse source.IsEmpty() Then Continue For
            Yield New ArtworkItem With {
                .Geometry = DirectCast(source.GetCurrentValueAsFrozen(), Geometry),
                .Transform = GetAccumulatedMatrix(item.DrawableElement, relativeTo),
                .SourceId = item.Id, .SourceName = item.Name,
                .HasFill = RichCanvas.DrawingManager.HasPaint(item.Fill),
                .IsTarget = Not boxedSources.Contains(item.Id) AndAlso Not RegistrationMarkHelper.IsRegistrationMark(item) AndAlso item.AncestorsAndSelf().Any(AddressOf targets.Contains)
            }
        Next
    End Function

    Private Function PlanArtwork(items As List(Of ArtworkItem), cancellationToken As CancellationToken) As List(Of CutPlan)
        Dim artwork = CaptureArtwork(items, cancellationToken)
        Return New CutPlanner(artwork, _exteriorMargin, _minimumMargin, cancellationToken).Plan()
    End Function

    Private Function CreateShapes(plans As List(Of CutPlan), items As List(Of ArtworkItem), cancellationToken As CancellationToken) As List(Of Shape)
        Dim names = items.GroupBy(Function(item) item.SourceId).ToDictionary(Function(group) group.Key, Function(group) group.First().SourceName)
        Dim shapes As New List(Of Shape)
        For Each plan In plans
            cancellationToken.ThrowIfCancellationRequested()
            shapes.Add(CreateShape(plan, names))
        Next
        cancellationToken.ThrowIfCancellationRequested()
        Return shapes
    End Function

    Private Shared Function CaptureArtwork(items As List(Of ArtworkItem), cancellationToken As CancellationToken) As List(Of ArtworkRegion)
        Dim result As New List(Of ArtworkRegion)
        For Each item In items
            cancellationToken.ThrowIfCancellationRequested()
            Dim flat = item.Geometry.GetFlattenedPathGeometry(GeometryTolerance, ToleranceType.Absolute)
            Dim matrix As Matrix = If(flat.Transform?.Value, Media.Matrix.Identity)
            matrix.Append(item.Transform)

            Dim figures As New Paths64
            Dim fill As New Paths64
            Dim closed As New Paths64
            Dim open As New Paths64

            For Each figure In flat.Figures
                cancellationToken.ThrowIfCancellationRequested()
                Dim path As New Path64 From {Scaled(matrix.Transform(figure.StartPoint))}

                For Each segment In figure.Segments
                    cancellationToken.ThrowIfCancellationRequested()
                    If TypeOf segment Is LineSegment Then
                        path.Add(Scaled(matrix.Transform(DirectCast(segment, LineSegment).Point)))
                    ElseIf TypeOf segment Is PolyLineSegment Then
                        For Each point In DirectCast(segment, PolyLineSegment).Points
                            cancellationToken.ThrowIfCancellationRequested()
                            path.Add(Scaled(matrix.Transform(point)))
                        Next
                    End If
                Next

                path = Clipper.StripDuplicates(path, figure.IsClosed)
                If path.Count < 2 Then Continue For

                figures.Add(path)
                If figure.IsFilled AndAlso item.HasFill AndAlso path.Count > 2 Then fill.Add(path)

                Dim outlines = If(figure.IsClosed AndAlso path.Count > 2, closed, open)
                outlines.Add(path)
            Next


            'Use Clipper2 abd preserve EVERY cut, even if it is cancelled by an even-odd fill. This ensures that all cuts are preserved for weeding boxes.
            Dim rule = If(flat.FillRule = Media.FillRule.EvenOdd, Clipper2Lib.FillRule.EvenOdd, Clipper2Lib.FillRule.NonZero)
            Dim offset As New ClipperOffset(10) 'hardcode?
            offset.AddPaths(closed, JoinType.Miter, EndType.Joined)
            offset.AddPaths(open, JoinType.Miter, EndType.Butt)

            Dim strokes As New Paths64
            offset.Execute(BoundaryRadius * CoordinateScale, strokes)

            Dim tree As New PolyTree64

            ' Keep fill and outline separate so even-odd cancellation never removes a cut.
            Clipper.BooleanOp(ClipType.Union, fill, strokes, tree, rule)
            Dim regions As New List(Of ArtworkRegion)
            CaptureRegions(tree, figures, item.SourceId, item.SourceName, item.IsTarget, regions, cancellationToken)

            result.AddRange(regions.OrderBy(Function(region) region.Order))
        Next

        Return result

    End Function

    Private Shared Sub CaptureRegions(parent As PolyPath64, figures As Paths64, sourceId As Guid, sourceName As String, selected As Boolean, result As List(Of ArtworkRegion), cancellationToken As CancellationToken)

        For Each node As PolyPath64 In parent
            cancellationToken.ThrowIfCancellationRequested()

            If Not node.IsHole Then

                Dim extent = Clipper.GetBounds(node.Polygon)
                Dim paths As New Paths64 From {node.Polygon}
                For Each hole As PolyPath64 In node
                    paths.Add(hole.Polygon)
                Next
                Dim bounds = Rect.Empty
                Dim firstFigure = figures.Count

                For i = 0 To figures.Count - 1
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim figure = figures(i)
                    Dim point = figure(0)

                    If point.X < extent.left OrElse
                        point.X > extent.right OrElse
                        point.Y < extent.top OrElse
                        point.Y > extent.bottom Then Continue For

                    If Clipper.PointInPolygon(point, node.Polygon) = PointInPolygonResult.IsOutside Then Continue For
                    If paths.Skip(1).Any(Function(hole) Clipper.PointInPolygon(point, hole) = PointInPolygonResult.IsInside) Then Continue For

                    bounds.Union(ToRect(Clipper.GetBounds(figure)))
                    firstFigure = Math.Min(firstFigure, i)
                Next

                If Not bounds.IsEmpty Then
                    result.Add(New ArtworkRegion With {.SourceId = sourceId, .SourceName = sourceName, .IsTarget = selected, .Bounds = bounds, .Shape = New CutObstacle(paths), .Order = firstFigure})
                End If
            End If

            CaptureRegions(node, figures, sourceId, sourceName, selected, result, cancellationToken)
        Next

    End Sub

    Private Function CreateShape(plan As CutPlan, names As Dictionary(Of Guid, String)) As Shape
        Dim box = plan.Box
        Dim shape As Shape

        If Not plan.IsMerged Then
            shape = New Rectangle()
        Else
            Dim cuts = PathGeometry.CreateFromGeometry(New RectangleGeometry(box))
            Dim border = cuts.Figures.Single()
            border.Segments.Add(New LineSegment(border.StartPoint, True))
            For Each divider In plan.Dividers
                cuts.Figures.Add(New PathFigure(divider.Start, New PathSegment() {New LineSegment(divider.Finish, True)}, False))
            Next
            ' Path coordinates must be local to the positioned WPF Shape.
            cuts.Transform = New TranslateTransform(-box.X, -box.Y)
            shape = New Shapes.Path With {.Data = cuts, .Stretch = Stretch.None}
        End If

        shape.Width = box.Width
        shape.Height = box.Height
        shape.Fill = Brushes.Transparent
        shape.Stroke = _stroke
        shape.StrokeThickness = 0.25
        shape.StrokeDashArray = New DoubleCollection({2, 2})

        Canvas.SetLeft(shape, box.X)
        Canvas.SetTop(shape, box.Y)

        shape.Uid = String.Join("_", plan.Sources.Select(Function(source) names(source)).Distinct())
        shape.Tag = plan.Sources
        Return shape

    End Function

    Public Shared Function IsWeedingItem(item As IDrawable) As Boolean
        Return item.AncestorsAndSelf().Any(Function(parent) TypeOf parent Is DrawableGroup AndAlso parent.Name = ROOT_NAME)
    End Function

    Public Shared Function GetSourceIds(box As IDrawable) As Guid()
        Dim ids As Guid() = Nothing
        Return If(Sources.TryGetValue(box.Id, ids), ids, Array.Empty(Of Guid)())
    End Function

    Public Shared Sub TrackSources(box As IDrawable, ids As Guid())
        Sources(box.Id) = ids
    End Sub

    Public Shared Sub ClearSources()
        Sources.Clear()
    End Sub

    Private Class CutPlanner

        Private ReadOnly _artwork As List(Of ArtworkRegion)
        Private ReadOnly _exteriorMargin As Double
        Private ReadOnly _minimumMargin As Double
        Private ReadOnly _clearance As Double
        Private ReadOnly _cancellationToken As CancellationToken
        Private ReadOnly _frames As New Dictionary(Of Rect, Paths64)
        Private ReadOnly _collisions As New Dictionary(Of (Box As Rect, Shape As CutObstacle), Boolean)

        Public Sub New(artwork As List(Of ArtworkRegion), exteriorMargin As Double, minimumMargin As Double, cancellationToken As CancellationToken)
            _artwork = artwork
            _exteriorMargin = exteriorMargin
            _minimumMargin = minimumMargin
            _clearance = Math.Max(BoundaryRadius, minimumMargin - BoundaryRadius * 2)
            _cancellationToken = cancellationToken
        End Sub

        Public Function Plan() As List(Of CutPlan)
            _cancellationToken.ThrowIfCancellationRequested()
            Dim candidates = _artwork.Where(Function(part) part.IsTarget).ToList()
            Dim clusters = candidates.Select(Function(part) New Cluster(part)).ToList()
            ResolveClusters(clusters, _exteriorMargin)

            ' Resolve parts of the same object and parts inside the same enclosed region separately.
            Dim nested = candidates.Select(Function(part) New Cluster(part)).ToList()
            Dim groups = candidates.GroupBy(Function(part) part.SourceId).Select(Function(group) group.ToList()).ToList()

            groups.AddRange(candidates.GroupBy(AddressOf FindEnclosingRegion).
                Where(Function(group) group.Key IsNot Nothing).Select(Function(group) group.ToList()))

            For Each group In groups.Where(Function(parts) parts.Count > 1)
                Dim clustersInGroup = group.Select(Function(part) New Cluster(part)).ToList()
                ResolveClusters(clustersInGroup, _exteriorMargin)
                nested.AddRange(clustersInGroup)
            Next

            Return AddNestedClusters(clusters, nested)

        End Function

        Private Function FindEnclosingRegion(part As ArtworkRegion) As ArtworkRegion
            _cancellationToken.ThrowIfCancellationRequested()
            Return _artwork.Where(Function(parent) parent.Bounds <> part.Bounds AndAlso parent.Bounds.Contains(part.Bounds)).
                OrderBy(Function(parent) parent.Bounds.Width * parent.Bounds.Height).
                ThenBy(Function(parent) parent.Bounds.Width + parent.Bounds.Height).FirstOrDefault()
        End Function

        Private Sub ResolveClusters(clusters As List(Of Cluster), margin As Double)
            Dim changed As Boolean
            'iteravitve convergance 
            Do
                changed = False

                For Each cluster In clusters
                    _cancellationToken.ThrowIfCancellationRequested()
                    Dim fit = FitCluster(cluster, margin)
                    If fit.Blocker IsNot Nothing Then
                        cluster.Include(fit.Blocker)
                        cluster.Box = Rect.Inflate(cluster.Bounds, margin, margin)
                        changed = True
                    Else
                        cluster.Box = fit.Box
                    End If
                Next

                If MergeClusters(clusters) Then changed = True

            Loop While changed

        End Sub

        Private Function FitCluster(cluster As Cluster, margin As Double) As (Box As Rect, Blocker As ArtworkRegion)
            Dim minimum = Rect.Inflate(cluster.Bounds, Math.Min(margin, _minimumMargin), Math.Min(margin, _minimumMargin))
            Dim box = Rect.Inflate(cluster.Bounds, margin, margin)
            Dim previous As Rect

            Do
                previous = box

                For Each obstacle In _artwork
                    _cancellationToken.ThrowIfCancellationRequested()
                    If cluster.Members.Contains(obstacle) Then Continue For
                    Dim blocked = Rect.Inflate(obstacle.Bounds, _minimumMargin, _minimumMargin)
                    If Not Overlaps(box, blocked) OrElse box.Contains(blocked) OrElse IsClear(box, obstacle.Shape) Then Continue For
                    Dim smaller = Shrink(box, minimum, blocked)
                    If smaller.IsEmpty Then smaller = ShrinkToGeometry(box, minimum, obstacle.Shape)
                    If smaller.IsEmpty Then Return (Rect.Empty, obstacle)
                    box = smaller
                Next

            Loop While box <> previous

            Return (box, Nothing)
        End Function

        Private Function MergeClusters(clusters As List(Of Cluster)) As Boolean

            Dim changed = False

            For i = clusters.Count - 1 To 0 Step -1
                For j = 0 To i - 1
                    _cancellationToken.ThrowIfCancellationRequested()
                    Dim a = clusters(i).Box
                    Dim b = clusters(j).Box
                    Dim nearby = Not a.Contains(b) AndAlso Not b.Contains(a) AndAlso Overlaps(Rect.Inflate(a, _minimumMargin, _minimumMargin), b)
                    If Not (a = b OrElse nearby OrElse clusters(i).Members.Overlaps(clusters(j).Members)) Then Continue For
                    clusters(j).Merge(clusters(i))
                    clusters(j).Box = Rect.Union(a, b)
                    clusters.RemoveAt(i)
                    changed = True
                    Exit For
                Next
            Next

            Return changed
        End Function

        'kill me
        Private Function AddNestedClusters(clusters As List(Of Cluster), candidates As List(Of Cluster)) As List(Of CutPlan)
            Dim firstNested = clusters.Count
            Dim margin = Math.Min(_exteriorMargin, _minimumMargin)
            Dim plans = clusters.Select(AddressOf CreatePlan).ToList()
            Dim outlines = plans.Select(AddressOf CreateOutline).ToList()

            Dim sourceBounds = _artwork.GroupBy(Function(part) part.SourceId).ToDictionary(Function(source) source.Key,
                Function(source) source.Select(Function(part) part.Bounds).Aggregate(Rect.Empty, Function(bounds, part) Rect.Union(bounds, part)))

            'big sorting
            For Each nested In candidates.OrderByDescending(Function(part) part.Members.Count > 1 AndAlso part.Sources.Count = 1 AndAlso part.Bounds.Contains(sourceBounds(part.Sources.First()))).
                ThenByDescending(Function(part) part.Bounds.Width * part.Bounds.Height)
                _cancellationToken.ThrowIfCancellationRequested()
                If clusters.Any(Function(cluster) cluster.Bounds = nested.Bounds) Then Continue For

                ResolveClusters(New List(Of Cluster) From {nested}, margin)
                Dim bounds = nested.Bounds

                If clusters.Any(Function(cluster) cluster.Bounds = bounds) Then Continue For
                If Not clusters.Any(Function(parent) parent.Box.Contains(nested.Box) AndAlso parent.Members.Any(Function(member) member.Bounds <> bounds AndAlso member.Bounds.Contains(bounds))) Then Continue For

                nested.Box = FitNestedBox(nested.Box, Rect.Inflate(bounds, margin, margin), nested.Members, outlines)
                If nested.Box.IsEmpty Then Continue For

                clusters.Add(nested)
                plans.Add(CreatePlan(nested))
                outlines.Add(CreateOutline(plans.Last()))
            Next

            For i = firstNested To clusters.Count - 1
                _cancellationToken.ThrowIfCancellationRequested()
                Dim cluster = clusters(i)

                Dim fit = FitCluster(cluster, _exteriorMargin)
                If fit.Blocker IsNot Nothing Then Continue For

                Dim currentOutline = outlines(i)

                Dim box = FitNestedBox(fit.Box, cluster.Box, cluster.Members, outlines.Where(Function(outline) outline IsNot currentOutline))
                If box.IsEmpty Then Continue For

                cluster.Box = box
                plans(i) = CreatePlan(cluster)
                outlines(i) = CreateOutline(plans(i))
            Next

            Return plans
        End Function

        Private Function FitNestedBox(box As Rect, minimum As Rect, members As HashSet(Of ArtworkRegion), outlines As IEnumerable(Of CutObstacle)) As Rect
            For Each outlien In outlines
                If IsClear(box, outlien) Then Continue For
                box = ShrinkToGeometry(box, minimum, outlien)
                If box.IsEmpty Then Return Rect.Empty
            Next
            If outlines.All(Function(outline) IsClear(box, outline)) AndAlso
                _artwork.All(Function(obstacle) members.Contains(obstacle) OrElse IsClear(box, obstacle.Shape)) Then Return box
            Return Rect.Empty
        End Function

        Private Function CreatePlan(cluster As Cluster) As CutPlan
            Dim dividers As New List(Of CutLine)
            Dim cells As New Queue(Of Rect)

            If cluster.Members.Count > 1 Then cells.Enqueue(cluster.Box)

            While cells.Count > 0
                _cancellationToken.ThrowIfCancellationRequested()
                Dim box = cells.Dequeue()
                Dim members = cluster.Members.Where(Function(member) box.Contains(member.Bounds)).Select(Function(member) member.Bounds).ToArray()
                Dim divider = FindDivider(members, box)
                If divider Is Nothing Then Continue While
                dividers.Add(divider)
                cells.Enqueue(New Rect(box.TopLeft, divider.Finish))
                cells.Enqueue(New Rect(divider.Start, box.BottomRight))
            End While

            Return New CutPlan With {
                .Box = cluster.Box,
                .Sources = cluster.Sources.ToArray(),
                .IsMerged = cluster.Members.Count > 1,
                .Dividers = dividers
            }
        End Function

        Private Function CreateOutline(plan As CutPlan) As CutObstacle
            _cancellationToken.ThrowIfCancellationRequested()
            Dim paths = Clipper.InflatePaths(New Paths64 From {RectanglePath(plan.Box)}, BoundaryRadius * CoordinateScale, JoinType.Miter, EndType.Joined)
            For Each divider In plan.Dividers
                _cancellationToken.ThrowIfCancellationRequested()
                paths.AddRange(Clipper.InflatePaths(New Paths64 From {New Path64 From {Scaled(divider.Start), Scaled(divider.Finish)}},
                    BoundaryRadius * CoordinateScale, JoinType.Miter, EndType.Butt))
            Next
            Return New CutObstacle(Clipper.Union(paths, Clipper2Lib.FillRule.NonZero))
        End Function

        Private Function FindDivider(members As Rect(), box As Rect) As CutLine
            If members.Length < 2 Then Return Nothing

            For Each vertical In {True, False}
                Dim sorted = members.OrderBy(Function(member) If(vertical, member.Left, member.Top)).ToArray()
                Dim edge = If(vertical, sorted(0).Right, sorted(0).Bottom)
                For Each member In sorted.Skip(1)
                    _cancellationToken.ThrowIfCancellationRequested()
                    Dim start = If(vertical, member.Left, member.Top)
                    If start - edge >= _minimumMargin * 2 Then
                        Dim middle = (edge + start) / 2
                        Dim divider = If(vertical, New CutLine(New Point(middle, box.Top), New Point(middle, box.Bottom)),
                                                   New CutLine(New Point(box.Left, middle), New Point(box.Right, middle)))
                        Dim band = If(vertical, New Rect(middle - _clearance, box.Top, _clearance * 2, box.Height),
                                                New Rect(box.Left, middle - _clearance, box.Width, _clearance * 2))
                        Dim footprint As New Paths64 From {RectanglePath(band)}
                        If Not _artwork.Any(Function(obstacle) obstacle.Shape.Bounds.IntersectsWith(band) AndAlso
                            Clipper.Intersect(footprint, obstacle.Shape.Paths, Clipper2Lib.FillRule.NonZero).Count > 0) Then Return divider
                    End If
                    edge = Math.Max(edge, If(vertical, member.Right, member.Bottom))
                Next
            Next
            Return Nothing
        End Function

        Private Shared Function Shrink(box As Rect, minimum As Rect, blocked As Rect) As Rect
            Dim candidates As New List(Of Rect)(4)

            If blocked.Left >= minimum.Right Then candidates.Add(New Rect(box.Left, box.Top, blocked.Left - box.Left, box.Height))
            If blocked.Right <= minimum.Left Then candidates.Add(New Rect(blocked.Right, box.Top, box.Right - blocked.Right, box.Height))
            If blocked.Top >= minimum.Bottom Then candidates.Add(New Rect(box.Left, box.Top, box.Width, blocked.Top - box.Top))
            If blocked.Bottom <= minimum.Top Then candidates.Add(New Rect(box.Left, blocked.Bottom, box.Width, box.Bottom - blocked.Bottom))

            Return candidates.OrderByDescending(Function(candidate) candidate.Width * candidate.Height).DefaultIfEmpty(Rect.Empty).First()
        End Function

        Private Shared Function Overlaps(a As Rect, b As Rect) As Boolean
            Return a.Left < b.Right AndAlso a.Right > b.Left AndAlso a.Top < b.Bottom AndAlso a.Bottom > b.Top
        End Function

        Private Function IsClear(box As Rect, obstacle As CutObstacle) As Boolean
            _cancellationToken.ThrowIfCancellationRequested()
            Dim blocked = Rect.Inflate(obstacle.Bounds, _minimumMargin, _minimumMargin)
            If Not Overlaps(box, blocked) OrElse box.Contains(blocked) Then Return True
            Dim key = (box, obstacle)
            Dim clear As Boolean
            If _collisions.TryGetValue(key, clear) Then Return clear
            Dim frame As Paths64 = Nothing
            If Not _frames.TryGetValue(box, frame) Then
                frame = New Paths64 From {RectanglePath(Rect.Inflate(box, _clearance, _clearance))}
                If box.Width > _clearance * 2 AndAlso box.Height > _clearance * 2 Then
                    Dim inner = RectanglePath(Rect.Inflate(box, -_clearance, -_clearance))
                    inner.Reverse()
                    frame.Add(inner)
                End If
                _frames.Add(box, frame)
            End If
            Dim extent = Clipper.GetBounds(frame(0))
            Dim nearby = Clipper.RectClip(extent, obstacle.Paths)
            clear = nearby.Count = 0 OrElse Clipper.Intersect(frame, nearby, Clipper2Lib.FillRule.NonZero).Count = 0
            _collisions.Add(key, clear)
            Return clear
        End Function

        Private Function ShrinkToGeometry(box As Rect, minimum As Rect, obstacle As CutObstacle) As Rect
            If IsClear(box, obstacle) Then Return box
            If Not IsClear(minimum, obstacle) Then Return Rect.Empty

            Dim result = minimum
            Dim lower = 0.0
            Dim upper = 1.0
            For i = 1 To 16
                Dim fraction = (lower + upper) / 2
                Dim candidate As New Rect(minimum.X + (box.X - minimum.X) * fraction, minimum.Y + (box.Y - minimum.Y) * fraction,
                                          minimum.Width + (box.Width - minimum.Width) * fraction, minimum.Height + (box.Height - minimum.Height) * fraction)
                If IsClear(candidate, obstacle) Then
                    result = candidate
                    lower = fraction
                Else
                    upper = fraction
                End If
            Next
            Return result
        End Function
    End Class

    Private Class ArtworkItem
        Public Geometry As Geometry
        Public Transform As Matrix
        Public SourceId As Guid
        Public SourceName As String
        Public HasFill As Boolean
        Public IsTarget As Boolean
    End Class

    Private Class ArtworkRegion
        Public Order As Integer
        Public SourceId As Guid
        Public SourceName As String
        Public IsTarget As Boolean
        Public Bounds As Rect
        Public Shape As CutObstacle
    End Class

    Private Class CutObstacle
        Public ReadOnly Paths As Paths64
        Public ReadOnly Bounds As Rect
        Public Sub New(polygons As Paths64)
            Paths = polygons
            Bounds = ToRect(Clipper.GetBounds(polygons))
        End Sub
    End Class

    Private Class CutLine
        Public ReadOnly Start As Point
        Public ReadOnly Finish As Point
        Public Sub New(first As Point, last As Point)
            Start = first
            Finish = last
        End Sub
    End Class

    Private Class CutPlan
        Public Box As Rect
        Public Sources As Guid()
        Public IsMerged As Boolean
        Public Dividers As List(Of CutLine)
    End Class

    Private Class Cluster
        Public ReadOnly Members As New HashSet(Of ArtworkRegion)
        Public ReadOnly Sources As New HashSet(Of Guid)
        Public Bounds As Rect = Rect.Empty
        Public Box As Rect
        Public Sub New(region As ArtworkRegion)
            Include(region)
            Sources.Add(region.SourceId)
        End Sub
        Public Sub Include(region As ArtworkRegion)
            If Members.Add(region) Then Bounds.Union(region.Bounds)
        End Sub
        Public Sub Merge(other As Cluster)
            Members.UnionWith(other.Members)
            Sources.UnionWith(other.Sources)
            Bounds.Union(other.Bounds)
        End Sub
    End Class

    Private Shared Function Scaled(point As Point) As Point64
        Return New Point64(point.X * CoordinateScale, point.Y * CoordinateScale)
    End Function

    Private Shared Function ToRect(bounds As Rect64) As Rect
        Return New Rect(CDbl(bounds.left) / CoordinateScale, CDbl(bounds.top) / CoordinateScale,
            CDbl(bounds.Width) / CoordinateScale, CDbl(bounds.Height) / CoordinateScale)
    End Function

    Private Shared Function RectanglePath(box As Rect) As Path64
        Return New Path64 From {Scaled(box.TopLeft), Scaled(box.TopRight), Scaled(box.BottomRight), Scaled(box.BottomLeft)}
    End Function
End Class

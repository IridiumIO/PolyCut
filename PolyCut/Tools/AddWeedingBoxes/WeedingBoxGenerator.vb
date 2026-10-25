Option Strict On
Imports PolyCut.Shared

Public Class WeedingBoxGenerator
    Public Const ROOT_NAME As String = "Weeding Boxes"

    Private Shared ReadOnly Sources As New Dictionary(Of Guid, Guid())


    Private Const GeometryTolerance As Double = 0.001
    Private Const BoundaryThickness As Double = 0.002
    Private Const StrokeThickness As Double = 0.25


    Private ReadOnly _exteriorMargin As Double
    Private ReadOnly _minimumMargin As Double
    Private ReadOnly _stroke As Brush

    Public Sub New(Optional configuration As WeedingBoxConfiguration = Nothing)
        Dim settings = If(configuration, New WeedingBoxConfiguration())
        _exteriorMargin = settings.DesiredExteriorMargin
        _minimumMargin = settings.MinimumInterObjectMargin
        _stroke = ColorAndBrushHelpers.DeserializeBrush(settings.Colour)
    End Sub

    Public Function Generate(items As IEnumerable(Of IDrawable), relativeTo As UIElement, Optional sceneItems As IEnumerable(Of IDrawable) = Nothing, Optional skipExisting As Boolean = False) As List(Of Shape)

        Dim targets = items.Where(Function(item) item IsNot Nothing AndAlso TypeOf item IsNot DrawableGroup AndAlso Not IsWeedingItem(item)).ToHashSet()

        Dim artwork = CollectArtwork(If(sceneItems, targets), relativeTo)
        Dim boxedSources As New HashSet(Of Guid)
        If skipExisting Then
            boxedSources.UnionWith(If(sceneItems, items).Where(AddressOf IsWeedingItem).
                SelectMany(AddressOf GetSourceIds))
        End If

        Dim clusters = artwork.Where(Function(part) Not boxedSources.Contains(part.Owner.Id) AndAlso Not RegistrationMarkHelper.IsRegistrationMark(part.Owner) AndAlso
                                         part.Owner.AncestorsAndSelf().Any(AddressOf targets.Contains)).
                                            Select(Function(part) New Cluster(part.Bounds, part.Owner)).ToList()

        ResolveClusters(clusters, artwork, _exteriorMargin)

        Return clusters.Select(Function(cluster) CreateShape(cluster, artwork)).ToList()
    End Function

    Private Function CollectArtwork(items As IEnumerable(Of IDrawable), relativeTo As UIElement) As List(Of ArtworkPart)
        Dim artwork As New List(Of ArtworkPart)

        Dim leaves = items.Where(Function(item) item IsNot Nothing AndAlso TypeOf item IsNot DrawableGroup).
            SelectMany(Function(item) If(item.IsAnyGroup(), item.GetLeafChildren(), New List(Of IDrawable) From {item})).Distinct()

        For Each item In leaves
            If IsWeedingItem(item) OrElse item.AncestorsAndSelf().Any(Function(parent) TypeOf parent IsNot DrawableGroup AndAlso parent.IsHidden) Then Continue For

            Dim source = DrawableGeometry.GetLocalGeometry(item.DrawableElement)

            If source Is Nothing OrElse source.Bounds.IsEmpty Then Continue For

            Dim world = TransformMath.GetAccumulatedMatrix(item.DrawableElement, relativeTo)
            Dim filled = RichCanvas.DrawingManager.HasPaint(item.Fill)

            For Each region In GetRegions(source)
                If region.Bounds.IsEmpty Then Continue For
                Dim fitted = If(region.Transform?.Value, Matrix.Identity)
                fitted.Append(world)

                region.Transform = New MatrixTransform(fitted)
                Dim boundary As Geometry = region.GetWidenedPathGeometry(New Pen(Brushes.Black, BoundaryThickness), GeometryTolerance, ToleranceType.Absolute)
                If filled Then boundary = Geometry.Combine(region, boundary, GeometryCombineMode.Union, Nothing, GeometryTolerance, ToleranceType.Absolute)
                artwork.Add(New ArtworkPart(item, region.Bounds, boundary))
            Next

        Next

        Return artwork

    End Function

    Private Sub ResolveClusters(clusters As List(Of Cluster), artwork As List(Of ArtworkPart), margin As Double)

        Dim changed As Boolean

        Do
            changed = False
            For Each cluster In clusters
                If FitCluster(cluster, artwork, margin) Then changed = True
            Next
            If MergeClusters(clusters) Then changed = True
        Loop While changed

    End Sub

    'Returns True if an obstacle must join the cluster rather than being avoided.
    Private Function FitCluster(cluster As Cluster, artwork As List(Of ArtworkPart), margin As Double) As Boolean

        Dim bounds = cluster.Bounds()
        Dim minimum = Rect.Inflate(bounds, Math.Min(margin, _minimumMargin * 2), Math.Min(margin, _minimumMargin * 2))
        Dim box = Rect.Inflate(bounds, margin, margin)

        Dim previous As Rect
        Do
            previous = box
            For Each obstacle In artwork
                If cluster.Members.Contains(obstacle.Bounds) OrElse IsEnclosingHole(obstacle.Bounds, obstacle.Shape, box) Then Continue For

                Dim blocked = Rect.Inflate(obstacle.Bounds, _minimumMargin * 2, _minimumMargin * 2)
                If Not Overlaps(box, blocked) OrElse box.Contains(blocked) Then Continue For

                Dim smaller = Shrink(box, minimum, blocked)
                If smaller.IsEmpty Then
                    cluster.Members.Add(obstacle.Bounds)
                    cluster.Box = box
                    Return True
                End If

                box = smaller
            Next
        Loop While box <> previous

        cluster.Box = box
        Return False
    End Function

    Private Function MergeClusters(clusters As List(Of Cluster)) As Boolean
        Dim changed = False
        For i = clusters.Count - 1 To 0 Step -1
            For j = 0 To i - 1
                Dim a = clusters(i).Box
                Dim b = clusters(j).Box
                Dim nearby = Not a.Contains(b) AndAlso Not b.Contains(a) AndAlso Overlaps(Rect.Inflate(a, _minimumMargin * 2, _minimumMargin * 2), b)
                If Not (nearby OrElse clusters(i).Members.Overlaps(clusters(j).Members)) Then Continue For
                clusters(j).Members.UnionWith(clusters(i).Members)
                clusters(j).Sources.UnionWith(clusters(i).Sources)
                clusters.RemoveAt(i)
                changed = True
                Exit For
            Next
        Next
        Return changed
    End Function

    Private Function CreateShape(cluster As Cluster, artwork As List(Of ArtworkPart)) As Shape
        Dim box = cluster.Box
        Dim shape As Shape

        If cluster.Members.Count = 1 Then
            shape = New Rectangle()
        Else
            Dim cuts = PathGeometry.CreateFromGeometry(New RectangleGeometry(box))
            Dim figure = cuts.Figures.Single()
            figure.Segments.Add(New LineSegment(figure.StartPoint, True))
            AddDividers(cuts, cluster, artwork)

            'Path coordinates must be local to the positioned WPF Shape.
            cuts.Transform = New TranslateTransform(-box.X, -box.Y)
            shape = New Shapes.Path With {.Data = cuts, .Stretch = Stretch.None}
        End If

        shape.Width = box.Width
        shape.Height = box.Height
        shape.Fill = Brushes.Transparent
        shape.Stroke = _stroke
        shape.StrokeThickness = StrokeThickness
        shape.StrokeDashArray = New DoubleCollection({2, 2})
        Canvas.SetLeft(shape, box.X)
        Canvas.SetTop(shape, box.Y)
        shape.Uid = String.Join("_", cluster.Sources.Select(Function(source) source.Name).Distinct())
        shape.Tag = cluster.Sources.Select(Function(source) source.Id).ToArray()
        Return shape
    End Function

    Private Sub AddDividers(cuts As PathGeometry, cluster As Cluster, artwork As List(Of ArtworkPart))
        Dim cells As New Queue(Of Rect)
        cells.Enqueue(cluster.Box)
        While cells.Count > 0
            Dim box = cells.Dequeue()
            Dim members = cluster.Members.Where(Function(member) box.Contains(member)).ToArray()
            Dim divider = FindDivider(members, box, artwork)
            If divider Is Nothing Then Continue While
            cuts.Figures.Add(New PathFigure(divider.StartPoint, New PathSegment() {New LineSegment(divider.EndPoint, True)}, False))
            cells.Enqueue(New Rect(box.TopLeft, divider.EndPoint))
            cells.Enqueue(New Rect(divider.StartPoint, box.BottomRight))
        End While
    End Sub


    Private Function FindDivider(members As Rect(), box As Rect, artwork As List(Of ArtworkPart)) As LineGeometry
        For Each vertical In {True, False}
            Dim sorted = members.OrderBy(Function(member) If(vertical, member.Left, member.Top)).ToArray()
            If sorted.Length < 2 Then Continue For
            Dim edge = If(vertical, sorted(0).Right, sorted(0).Bottom)
            For Each member In sorted.Skip(1)
                Dim start = If(vertical, member.Left, member.Top)
                If start - edge >= _minimumMargin * 2 Then
                    Dim middle = (edge + start) / 2
                    Dim divider = If(vertical, New LineGeometry(New Point(middle, box.Top), New Point(middle, box.Bottom)),
                                               New LineGeometry(New Point(box.Left, middle), New Point(box.Right, middle)))
                    Dim clearance = divider.GetWidenedPathGeometry(New Pen(Brushes.Black, Math.Max(BoundaryThickness, _minimumMargin * 2 - BoundaryThickness * 2)), GeometryTolerance, ToleranceType.Absolute)
                    If Not artwork.Any(Function(obstacle) obstacle.Shape.Bounds.IntersectsWith(clearance.Bounds) AndAlso
                                           obstacle.Shape.FillContainsWithDetail(clearance, GeometryTolerance, ToleranceType.Absolute) <> IntersectionDetail.Empty) Then Return divider
                End If
                edge = Math.Max(edge, If(vertical, member.Right, member.Bottom))
            Next
        Next
        Return Nothing
    End Function

    Private Function Shrink(box As Rect, minimum As Rect, blocked As Rect) As Rect
        Dim candidates As New List(Of Rect)(4)

        If blocked.Left >= minimum.Right Then candidates.Add(New Rect(box.Left, box.Top, blocked.Left - box.Left, box.Height))
        If blocked.Right <= minimum.Left Then candidates.Add(New Rect(blocked.Right, box.Top, box.Right - blocked.Right, box.Height))
        If blocked.Top >= minimum.Bottom Then candidates.Add(New Rect(box.Left, box.Top, box.Width, blocked.Top - box.Top))
        If blocked.Bottom <= minimum.Top Then candidates.Add(New Rect(box.Left, blocked.Bottom, box.Width, box.Bottom - blocked.Bottom))

        Return candidates.OrderByDescending(Function(candidate) candidate.Width * candidate.Height).DefaultIfEmpty(Rect.Empty).First()
    End Function

    Private Function Overlaps(a As Rect, b As Rect) As Boolean
        Return a.Left < b.Right AndAlso a.Right > b.Left AndAlso a.Top < b.Bottom AndAlso a.Bottom > b.Top
    End Function

    Private Function IsEnclosingHole(bounds As Rect, geometry As Geometry, box As Rect) As Boolean
        Return bounds.Contains(box) AndAlso geometry.FillContainsWithDetail(New RectangleGeometry(box), GeometryTolerance, ToleranceType.Absolute) = IntersectionDetail.Empty
    End Function

    Private Function GetRegions(source As Geometry) As List(Of PathGeometry)

        Dim flat = source.GetFlattenedPathGeometry(GeometryTolerance, ToleranceType.Absolute)
        Dim regions = flat.Figures.Where(Function(figure) Not figure.IsClosed).Select(Function(figure) New PathGeometry({figure.Clone()}, flat.FillRule, flat.Transform)).ToList()

        Dim contours = flat.Figures.Where(Function(figure) figure.IsClosed).Select(Function(figure) New PathGeometry({figure.Clone()}, flat.FillRule, flat.Transform)).ToList()

        Dim parents = contours.Select(Function(inner) Enumerable.Range(0, contours.Count).
            Where(Function(index) contours(index).Bounds <> inner.Bounds AndAlso contours(index).Bounds.Contains(inner.Bounds) AndAlso
                  contours(index).FillContainsWithDetail(inner, GeometryTolerance, ToleranceType.Absolute) = IntersectionDetail.FullyContains).
            OrderBy(Function(index) contours(index).Bounds.Width * contours(index).Bounds.Height).DefaultIfEmpty(-1).First()).ToArray()
        Dim depths(contours.Count - 1) As Integer
        For i = 0 To contours.Count - 1
            Dim parent = parents(i)
            While parent >= 0
                depths(i) += 1
                parent = parents(parent)
            End While
        Next

        For i = 0 To contours.Count - 1
            If depths(i) Mod 2 <> 0 Then Continue For
            Dim region = contours(i)
            For j = 0 To contours.Count - 1
                If parents(j) = i Then
                    region.Figures.Add(contours(j).Figures.Single().Clone())
                End If
            Next
            regions.Add(region)
        Next
        Return regions
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

    Private Class Cluster
        Public ReadOnly Members As New HashSet(Of Rect)
        Public ReadOnly Sources As New HashSet(Of IDrawable)
        Public Property Box As Rect
        Public Sub New(seed As Rect, owner As IDrawable)
            Members.Add(seed)
            Sources.Add(owner)
        End Sub

        Public Function Bounds() As Rect
            Return Members.Aggregate(Rect.Empty, Function(result, member) Rect.Union(result, member))
        End Function
    End Class

    Private Structure ArtworkPart
        Public ReadOnly Owner As IDrawable
        Public ReadOnly Bounds As Rect
        Public ReadOnly Shape As Geometry

        Public Sub New(owner As IDrawable, bounds As Rect, shape As Geometry)
            Me.Owner = owner
            Me.Bounds = bounds
            Me.Shape = shape
        End Sub
    End Structure

End Class

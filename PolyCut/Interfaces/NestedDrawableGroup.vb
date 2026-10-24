Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel

Imports PolyCut.RichCanvas

Imports PolyCut.[Shared]

Imports Svg

Public Class NestedDrawableGroup : Inherits BaseDrawable : Implements IDrawable

    'Known Not working
    '- Boolean ops with groups


    Public Property GroupChildren As New ObservableCollection(Of IDrawable)

    ' Flattened view for UI (all leaf drawables under this group, including nested groups)
    Public ReadOnly Property DisplayChildren As New ObservableCollection(Of IDrawable)

    Private Shared ReadOnly _bulkTouchedNestedGroups As New HashSet(Of NestedDrawableGroup)()
    Private Shared ReadOnly _pendingStyleNotifications As New List(Of (Group As NestedDrawableGroup, Name As String))()
    Private Shared ReadOnly _pendingStyleKeys As New HashSet(Of (NestedDrawableGroup, String))()
    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Private ReadOnly _innerCanvas As New Canvas With {.ClipToBounds = False}

    Public ReadOnly Property InnerCanvas As Canvas
        Get
            Return _innerCanvas
        End Get
    End Property

    Public Sub New(Optional groupName As String = "Group")
        Name = If(groupName, "Group")
        VisualName = "Group"

        ' Viewbox scales the inner canvas when wrapper is resized
        Dim vb As New GroupViewbox With {
            .Stretch = Stretch.Fill,
            .StretchDirection = StretchDirection.Both,
            .Child = _innerCanvas
        }

        MyBase.DrawableElement = vb

        AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
    End Sub


    Public Shared Function CreateNestedGroup(children As IEnumerable(Of IDrawable), groupName As String) As NestedDrawableGroup
        Dim grp As New NestedDrawableGroup(groupName)

        Dim items = children?.
        Where(Function(d) d IsNot Nothing AndAlso d.DrawableElement IsNot Nothing).
        ToList()

        If items Is Nothing OrElse items.Count = 0 Then Return grp

        Dim style As Style = TryCast(Application.Current?.TryFindResource("GroupChildItemStyle"), Style)

        Dim wrappers As New List(Of (drawable As IDrawable, element As FrameworkElement))

        grp.BeginBulkChildUpdate()
        Try
            For Each d In items
                Dim element = CreateChildElement(d, style)
                If element Is Nothing Then Continue For

                wrappers.Add((d, element))
                grp.AddChild(d)
            Next
        Finally
            grp.EndBulkChildUpdate()
        End Try

        Dim bounds As Rect = GetBoundsFromWrappers(wrappers.Select(Function(x) x.element))

        ' Localize wrappers into group-local space and insert into INNER canvas (not DrawableElement)
        grp.InnerCanvas.Children.Clear()
        Dim localised As New List(Of FrameworkElement)
        For Each pair In wrappers
            Dim w = pair.element

            Canvas.SetLeft(w, GetLeftSafe(w) - bounds.Left)
            Canvas.SetTop(w, GetTopSafe(w) - bounds.Top)

            localised.Add(w)
        Next
        AddPartitionedWrappers(grp.InnerCanvas, localised)

        ' IMPORTANT: set the "native" size of the inner canvas so the Viewbox has a base size to scale from
        grp.InnerCanvas.Width = bounds.Width
        grp.InnerCanvas.Height = bounds.Height

        ' Now set the Viewbox (DrawableElement) size+world position so PolyCanvas wrapper will pick it up
        Dim vb = TryCast(grp.DrawableElement, Viewbox)
        If vb IsNot Nothing Then
            vb.Width = bounds.Width
            vb.Height = bounds.Height
            Canvas.SetLeft(vb, bounds.Left)
            Canvas.SetTop(vb, bounds.Top)
        End If

        Return grp
    End Function


    Private Sub OnGroupChildrenChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If _suspendChildNotifications Then Return

        If ModelBatch.Active Then
            If _bulkTouchedNestedGroups.Add(Me) Then ModelBatch.OnEndOnce("NestedGroupChildren", AddressOf RebuildTouchedNestedGroups)
            Return
        End If

        RebuildChildren()
    End Sub

    Private Shared Sub RebuildTouchedNestedGroups()
        For Each g In _bulkTouchedNestedGroups
            g.RebuildChildren()
        Next
        _bulkTouchedNestedGroups.Clear()
    End Sub

    Private Sub RebuildChildren()
        RebuildDisplayChildren()

        For Each child In GroupChildren
            Dim npc = TryCast(child, INotifyPropertyChanged)
            If npc IsNot Nothing Then RemoveHandler npc.PropertyChanged, AddressOf OnChildPropertyChanged
        Next
        For Each child In GroupChildren
            Dim npc = TryCast(child, INotifyPropertyChanged)
            If npc IsNot Nothing Then AddHandler npc.PropertyChanged, AddressOf OnChildPropertyChanged
        Next
    End Sub

    Private Sub OnChildPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        Select Case e.PropertyName
            Case NameOf(Fill)
                RaiseStyleChanged(NameOf(Fill))
            Case NameOf(Stroke)
                RaiseStyleChanged(NameOf(Stroke))
        End Select
    End Sub

    Private Sub RaiseStyleChanged(propertyName As String)
        If Not ModelBatch.Active Then
            OnPropertyChanged(propertyName)
            Return
        End If

        If _pendingStyleKeys.Add((Me, propertyName)) Then
            _pendingStyleNotifications.Add((Me, propertyName))
            ModelBatch.OnEndOnce("NestedGroupStyleNotifications", AddressOf FlushStyleNotifications)
        End If
    End Sub

    Friend Sub RaiseStyleChangedNow(propertyName As String)
        OnPropertyChanged(propertyName)
    End Sub

    Private Shared Sub FlushStyleNotifications()
        Dim pending = _pendingStyleNotifications.ToList()
        _pendingStyleNotifications.Clear()
        _pendingStyleKeys.Clear()

        For Each entry In pending
            entry.Group.RaiseStyleChangedNow(entry.Name)
        Next
    End Sub

    Private _suspendChildNotifications As Boolean

    'Adding children one at a time rebuilt the display list and rewired every handler on every add, so building
    'a group of n children cost O(n^2). Import is the only bulk caller, so it suppresses and rebuilds once.
    Friend Sub BeginBulkChildUpdate()
        _suspendChildNotifications = True
    End Sub

    Friend Sub EndBulkChildUpdate()
        If Not _suspendChildNotifications Then Return
        _suspendChildNotifications = False
        OnGroupChildrenChanged(GroupChildren, Nothing)
    End Sub

    Private Iterator Function EnumerateLeafChildren() As IEnumerable(Of IDrawable)
        For Each ch In GroupChildren
            If ch Is Nothing Then Continue For

            Dim nested = TryCast(ch, NestedDrawableGroup)
            If nested IsNot Nothing Then
                For Each leaf In nested.EnumerateLeafChildren()
                    Yield leaf
                Next
            Else
                Yield ch
            End If
        Next
    End Function

    Public Sub RebuildDisplayChildren()
        DisplayChildren.Clear()
        For Each leaf In EnumerateLeafChildren()
            DisplayChildren.Add(leaf)
        Next
    End Sub


    Public Sub AddChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then Return

        If child.ParentGroup IsNot Nothing Then
            Dim prior = TryCast(child.ParentGroup, DrawableGroup)
            If prior IsNot Nothing Then prior.GroupChildren.Remove(child)
        End If
        child.ParentGroup = Me
        GroupChildren.Add(child)
    End Sub


    Public Function GetAllLeafChildren() As List(Of IDrawable)
        Return ProjectGraph.GetLeafChildren(Me)
    End Function



    Public Overrides Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Dim g As New SvgGroup()
        Return g
    End Function

    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement
        Dim gRoot As New SvgGroup()

        ' Each child bakes its own absolute element -> document-root matrix via the visual tree (SvgExportHelper.BakeToRoot TransformToVisual).
        ' FUTURE ME: This means groups are just organisational again, don't put cursed nested transforms here again
        For Each childDrawable In Me.GroupChildren
            If childDrawable Is Nothing Then Continue For

            Dim childSvg As SvgVisualElement = Nothing
            Try
                childSvg = childDrawable.GetTransformedSVGElement()
            Catch
                childSvg = Nothing
            End Try
            If childSvg Is Nothing Then Continue For

            Dim gChild As New SvgGroup()
            gChild.Children.Add(childSvg)
            gRoot.Children.Add(gChild)
        Next

        Return gRoot
    End Function
    Public Sub SetNativeSize(w As Double, h As Double)
        Dim inner = GetInnerCanvas()
        If inner Is Nothing Then Return

        If w >= 0 AndAlso Not Double.IsNaN(w) Then inner.Width = w
        If h >= 0 AndAlso Not Double.IsNaN(h) Then inner.Height = h
    End Sub

    Public Function GetNativeSize() As (Double, Double)
        Dim inner = GetInnerCanvas()
        If inner Is Nothing Then Return (0, 0)
        Dim host = TryCast(DrawableElement, GroupViewbox)
        Return (If(host IsNot Nothing AndAlso host.ZeroWidth, 0, inner.Width),
                If(host IsNot Nothing AndAlso host.ZeroHeight, 0, inner.Height))
    End Function

    Public Sub RebuildGroupVisualFromChildren(designerItemStyle As Style, Optional preserveLocalCoordinates As Boolean = False)
        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        If vb Is Nothing Then Return

        Dim inner = TryCast(vb.Child, Canvas)
        If inner Is Nothing Then
            inner = New Canvas With {.ClipToBounds = False}
            vb.Child = inner
        End If

        inner.Children.Clear()

        ' Compute bounds in WORLD space based on child element positions + sizes
        Dim bounds = CalculateWorldBoundsFromChildren()

        ' Set native size if not already present (needed for Viewbox scaling)
        If (inner.Width <= 0 OrElse Double.IsNaN(inner.Width)) AndAlso bounds.Width > 0 Then inner.Width = bounds.Width
        If (inner.Height <= 0 OrElse Double.IsNaN(inner.Height)) AndAlso bounds.Height > 0 Then inner.Height = bounds.Height

        ' Ensure the Viewbox has a concrete size if missing
        Dim vbe = DirectCast(vb, FrameworkElement)
        If (vbe.Width <= 0 OrElse Double.IsNaN(vbe.Width)) AndAlso bounds.Width > 0 Then vbe.Width = bounds.Width
        If (vbe.Height <= 0 OrElse Double.IsNaN(vbe.Height)) AndAlso bounds.Height > 0 Then vbe.Height = bounds.Height

        ' World position for the group element (PolyCanvas wrappering reads this)
        If Not preserveLocalCoordinates Then
            Canvas.SetLeft(vbe, bounds.Left)
            Canvas.SetTop(vbe, bounds.Top)
        End If

        ' Create child wrappers and place them in LOCAL space

        Dim rebuilt As New List(Of FrameworkElement)
        For Each child In GroupChildren
            If child?.DrawableElement Is Nothing Then Continue For

            Dim fe = child.DrawableElement

            Dim positioned = If(TryCast(fe.Parent, ContentControl), fe)
            Dim left = GetLeftSafe(positioned)
            Dim top = GetTopSafe(positioned)

            Dim element = CreateChildElement(child, designerItemStyle)
            If element Is Nothing Then Continue For

            ' Localize into group space
            Canvas.SetLeft(element, left - If(preserveLocalCoordinates, 0, bounds.Left))
            Canvas.SetTop(element, top - If(preserveLocalCoordinates, 0, bounds.Top))

            rebuilt.Add(element)

            ' Keep model links consistent
            child.ParentGroup = Me
        Next
        AddPartitionedWrappers(inner, rebuilt)
    End Sub


    'Arbitrary ranges but seem to work well enough. 
    Private Const MinLeavesPerCell As Integer = 32
    Private Const MaxCellsPerGroup As Integer = 256

    Private NotInheritable Class WrapperCell
        Public ReadOnly Bounds As Rect
        Public ReadOnly Children As List(Of FrameworkElement)

        Public Sub New(bounds As Rect, children As List(Of FrameworkElement))
            Me.Bounds = bounds
            Me.Children = children
        End Sub
    End Class

    'Split at the median on the longer axis so every cell holds a similar number of leaves, which lets dense regions end up with small cells and sparse regions stay large.
    Private Shared Sub BuildCells(items As List(Of FrameworkElement), into As List(Of WrapperCell), depth As Integer, perCell As Integer)
        If items Is Nothing OrElse items.Count = 0 Then Return

        Dim bounds As Rect = CellBounds(items)
        If items.Count <= perCell OrElse depth >= 12 OrElse bounds.Width <= 0 OrElse bounds.Height <= 0 Then
            into.Add(New WrapperCell(bounds, items))
            Return
        End If

        Dim vertical As Boolean = bounds.Width >= bounds.Height
        Dim ordered = items.OrderBy(Function(c) If(vertical, GetLeftSafe(c), GetTopSafe(c))).ToList()
        Dim half As Integer = ordered.Count \ 2
        If half <= 0 Then
            into.Add(New WrapperCell(bounds, items))
            Return
        End If

        BuildCells(ordered.Take(half).ToList(), into, depth + 1, perCell)
        BuildCells(ordered.Skip(half).ToList(), into, depth + 1, perCell)
    End Sub

    Private Shared Function LeavesPerCell(total As Integer) As Integer
        Return Math.Max(MinLeavesPerCell, CInt(Math.Ceiling(total / CDbl(MaxCellsPerGroup))))
    End Function

    Private Shared Function CellBounds(items As List(Of FrameworkElement)) As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each w In items
            Dim l = GetLeftSafe(w)
            Dim t = GetTopSafe(w)
            minX = Math.Min(minX, l)
            minY = Math.Min(minY, t)
            maxX = Math.Max(maxX, l + GetWidthSafe(w))
            maxY = Math.Max(maxY, t + GetHeightSafe(w))
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function

    ' Plain geometric leaves stay bare so large groups avoid a wrapper per shape.
    Private Shared Function CreateChildElement(child As IDrawable, designerItemStyle As Style) As FrameworkElement
        Dim fe = child?.DrawableElement
        If fe Is Nothing Then Return Nothing

        ' Reuse existing wrapper (position, size, rotation and origin)  when grouping canvas objects, including paths, rather than wrapping afresh.
        Dim existingWrapper = TryCast(fe.Parent, ContentControl)
        If existingWrapper IsNot Nothing AndAlso existingWrapper.Content Is fe Then
            Dim host = TryCast(existingWrapper.Parent, Panel)
            If host IsNot Nothing Then host.Children.Remove(existingWrapper)
            existingWrapper.IsHitTestVisible = False
            Return existingWrapper
        End If

        If TypeOf fe Is GeometricShape Then
            PerformantRasteriser.SetCacheEligible(fe, True)
            Return fe
        End If

        Dim wrapper = DrawableWrapperFactory.CreateWrapper(fe, child, designerItemStyle)
        If wrapper Is Nothing Then Return Nothing

        Canvas.SetLeft(wrapper, GetLeftSafe(fe))
        Canvas.SetTop(wrapper, GetTopSafe(fe))
        wrapper.IsHitTestVisible = False
        Return wrapper
    End Function

    'One canvas per cell, holding its leaves at cell-local offsets.
    'Absolute positions are unchanged, so (hopefully) it still fully uses the ancestor for  resolution of hit/transform gcode export still work.
    Private Shared Sub AddPartitionedWrappers(target As Canvas, wrappers As List(Of FrameworkElement))
        Dim cells As New List(Of WrapperCell)
        BuildCells(wrappers, cells, 0, LeavesPerCell(wrappers.Count))

        For Each cell In cells
            Dim host As New Canvas With {
                .ClipToBounds = False,
                .Width = Math.Max(1.0, cell.Bounds.Width),
                .Height = Math.Max(1.0, cell.Bounds.Height)
            }
            Canvas.SetLeft(host, cell.Bounds.Left)
            Canvas.SetTop(host, cell.Bounds.Top)

            For Each w In cell.Children
                Canvas.SetLeft(w, GetLeftSafe(w) - cell.Bounds.Left)
                Canvas.SetTop(w, GetTopSafe(w) - cell.Bounds.Top)
                host.Children.Add(w)
            Next

            target.Children.Add(host)
        Next
    End Sub

    Private Function GetInnerCanvas() As Canvas
        Dim vb = TryCast(Me.DrawableElement, Viewbox)
        Return TryCast(vb?.Child, Canvas)
    End Function

    Private Function CalculateWorldBoundsFromChildren() As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each child In GroupChildren
            Dim fe = child?.DrawableElement
            If fe Is Nothing Then Continue For

            Dim left = GetLeftSafe(fe)
            Dim top = GetTopSafe(fe)
            Dim w = GetWidthSafe(fe)
            Dim h = GetHeightSafe(fe)

            minX = Math.Min(minX, left)
            minY = Math.Min(minY, top)
            maxX = Math.Max(maxX, left + w)
            maxY = Math.Max(maxY, top + h)
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function

    Private Shared Function GetBoundsFromWrappers(wrappers As IEnumerable(Of FrameworkElement)) As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each w In wrappers
            If w Is Nothing Then Continue For
            Dim left = CanvasUtil.GetLeftSafe(w)
            Dim top = CanvasUtil.GetTopSafe(w)
            Dim ww = CanvasUtil.GetWidthSafe(w)
            Dim hh = CanvasUtil.GetHeightSafe(w)

            ' Wrappers are detached here, so include their render transform explicitly.
            Dim origin = w.RenderTransformOrigin
            Dim m = Matrix.Identity
            m.Translate(-origin.X * ww, -origin.Y * hh)
            m.Append(If(w.RenderTransform?.Value, Matrix.Identity))
            m.Translate(origin.X * ww + left, origin.Y * hh + top)
            Dim bounds = New MatrixTransform(m).TransformBounds(New Rect(0, 0, ww, hh))

            minX = Math.Min(minX, bounds.Left)
            minY = Math.Min(minY, bounds.Top)
            maxX = Math.Max(maxX, bounds.Right)
            maxY = Math.Max(maxY, bounds.Bottom)
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function


    Private _stroke As System.Windows.Media.Brush = Brushes.Transparent
    Private _fill As System.Windows.Media.Brush = Brushes.Transparent
    Private _strokeThickness As Double = Nothing

    Public Overrides Property Stroke As System.Windows.Media.Brush Implements IDrawable.Stroke
        Get
            ' Groups have no inherent stroke; fall back to the first child's
            If IsUsableBrush(_stroke) Then Return _stroke
            Dim first = GroupChildren.FirstOrDefault(Function(c) c IsNot Nothing)
            Return If(first IsNot Nothing, first.Stroke, _stroke)
        End Get
        Set(value As System.Windows.Media.Brush)
            _stroke = value
            For Each child As IDrawable In GroupChildren
                child.Stroke = value
            Next
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Stroke))
        End Set
    End Property

    Public Overloads Property Fill As System.Windows.Media.Brush Implements IDrawable.Fill
        Get
            ' Groups have no inherent fill; fall back to the first child's
            If IsUsableBrush(_fill) Then Return _fill
            Dim first = GroupChildren.FirstOrDefault(Function(c) c IsNot Nothing)
            Return If(first IsNot Nothing, first.Fill, _fill)
        End Get
        Set(value As System.Windows.Media.Brush)
            _fill = value
            For Each child As IDrawable In GroupChildren
                child.Fill = value
            Next
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(Fill))
        End Set
    End Property

    Private Shared Function IsUsableBrush(brush As System.Windows.Media.Brush) As Boolean
        If brush Is Nothing Then Return False
        Dim scb = TryCast(brush, System.Windows.Media.SolidColorBrush)
        If scb Is Nothing Then Return True ' gradient / non-solid -> usable as-is
        Return Not (scb.Color = System.Windows.Media.Colors.Black OrElse scb.Color = System.Windows.Media.Colors.Transparent)
    End Function

    Public Overrides Property StrokeThickness As Double Implements IDrawable.StrokeThickness
        Get
            ' Groups have no inherent fill; fall back to the first child's
            If _strokeThickness <> Nothing Then Return _strokeThickness
            Dim first = GroupChildren.FirstOrDefault(Function(c) c IsNot Nothing)
            Return If(first IsNot Nothing, first.StrokeThickness, _strokeThickness)
        End Get
        Set(value As Double)
            _strokeThickness = value
            For Each child As IDrawable In GroupChildren
                child.StrokeThickness = value
            Next
            ApplyVisualStyle()
            OnPropertyChanged(NameOf(StrokeThickness))
        End Set
    End Property

    ' Viewbox treats a zero native axis as a zero scale and erases the pen,so let's fudge it to 1 and then report back as 0 so the wrapper can still size correctly.
    Private Class GroupViewbox
        Inherits Viewbox
        Public Property ZeroWidth As Boolean
        Public Property ZeroHeight As Boolean

        Protected Overrides Function MeasureOverride(availableSize As Size) As Size
            Dim content = TryCast(Child, FrameworkElement)
            If content IsNot Nothing Then
                ZeroWidth = content.Width = 0 OrElse (ZeroWidth AndAlso content.Width = 1)
                ZeroHeight = content.Height = 0 OrElse (ZeroHeight AndAlso content.Height = 1)
                If ZeroWidth Then content.Width = 1
                If ZeroHeight Then content.Height = 1
            End If
            Dim measured = MyBase.MeasureOverride(New Size(If(ZeroWidth, 1, availableSize.Width), If(ZeroHeight, 1, availableSize.Height)))
            Return New Size(If(ZeroWidth, 0, measured.Width), If(ZeroHeight, 0, measured.Height))
        End Function

        Protected Overrides Function ArrangeOverride(finalSize As Size) As Size
            MyBase.ArrangeOverride(New Size(If(ZeroWidth AndAlso finalSize.Width = 0, 1, finalSize.Width),
                                           If(ZeroHeight AndAlso finalSize.Height = 0, 1, finalSize.Height)))
            Return finalSize
        End Function

        Protected Overrides Function GetLayoutClip(layoutSlotSize As Size) As Geometry
            Return Nothing
        End Function
    End Class

    Public Class StyleState
        Public Fill As System.Windows.Media.Brush
        Public Stroke As System.Windows.Media.Brush
        Public StrokeThickness As Double
    End Class

    Public Function CaptureOwnStyle() As StyleState
        Return New StyleState With {
            .Fill = _fill,
            .Stroke = _stroke,
            .StrokeThickness = _strokeThickness
        }
    End Function

    Public Sub RestoreOwnStyle(state As StyleState)
        If state Is Nothing Then Return

        _fill = state.Fill
        _stroke = state.Stroke
        _strokeThickness = state.StrokeThickness

        ApplyVisualStyle()
        OnPropertyChanged(NameOf(Fill))
        OnPropertyChanged(NameOf(Stroke))
        OnPropertyChanged(NameOf(StrokeThickness))
    End Sub

End Class

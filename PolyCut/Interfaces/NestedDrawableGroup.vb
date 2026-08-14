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
        Dim vb As New Viewbox With {
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

        Dim style As Style = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)

        Dim wrappers As New List(Of (drawable As IDrawable, wrapper As ContentControl))

        For Each d In items
            Dim fe = d.DrawableElement
            Dim w = DrawableWrapperFactory.CreateWrapper(fe, d, style)
            If w Is Nothing Then Continue For

            Dim left = GetLeftSafe(fe)
            Dim top = GetTopSafe(fe)

            Canvas.SetLeft(w, left)
            Canvas.SetTop(w, top)

            w.IsHitTestVisible = False

            wrappers.Add((d, w))
            grp.AddChild(d)
        Next

        Dim bounds As Rect = GetBoundsFromWrappers(wrappers.Select(Function(x) x.wrapper))

        ' Localize wrappers into group-local space and insert into INNER canvas (not DrawableElement)
        grp.InnerCanvas.Children.Clear()
        For Each pair In wrappers
            Dim w = pair.wrapper
            Dim wLeft = GetLeftSafe(w)
            Dim wTop = GetTopSafe(w)

            Canvas.SetLeft(w, wLeft - bounds.Left)
            Canvas.SetTop(w, wTop - bounds.Top)

            grp.InnerCanvas.Children.Add(w)
        Next

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
                OnPropertyChanged(NameOf(Fill))
            Case NameOf(Stroke)
                OnPropertyChanged(NameOf(Stroke))
        End Select
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

    Public Shadows Property Children As IEnumerable(Of IDrawable)
        Get
            Return GroupChildren
        End Get
        Set(value As IEnumerable(Of IDrawable))
            RemoveHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            GroupChildren.Clear()
            If value IsNot Nothing Then
                For Each c In value
                    GroupChildren.Add(c)
                Next
            End If
            AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            RebuildDisplayChildren()
        End Set
    End Property

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

    Public Sub RemoveChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then
            GroupChildren.Remove(child)
            child.ParentGroup = Nothing
        End If
    End Sub


    Public Function GetAllLeafChildren() As List(Of IDrawable)
        Return EnumerateLeafChildren().ToList()
    End Function



    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
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

        If w > 0 AndAlso Not Double.IsNaN(w) Then inner.Width = w
        If h > 0 AndAlso Not Double.IsNaN(h) Then inner.Height = h
    End Sub

    Public Function GetNativeSize() As (Double, Double)
        Dim inner = GetInnerCanvas()
        If inner Is Nothing Then Return (0, 0)
        Return (inner.Width, inner.Height)
    End Function

    Public Sub RebuildGroupVisualFromChildren(designerItemStyle As Style)
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
        Canvas.SetLeft(vbe, bounds.Left)
        Canvas.SetTop(vbe, bounds.Top)

        ' Create child wrappers and place them in LOCAL space
        For Each child In GroupChildren
            If child?.DrawableElement Is Nothing Then Continue For

            Dim fe = child.DrawableElement

            Dim left = GetLeftSafe(fe)
            Dim top = GetTopSafe(fe)

            Dim wrapper = DrawableWrapperFactory.CreateWrapper(fe, child, designerItemStyle)
            If wrapper Is Nothing Then Continue For

            ' Localize into group space
            Canvas.SetLeft(wrapper, left - bounds.Left)
            Canvas.SetTop(wrapper, top - bounds.Top)

            wrapper.IsHitTestVisible = False
            inner.Children.Add(wrapper)

            ' Keep model links consistent
            child.ParentGroup = Me
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

    Private Shared Function GetBoundsFromWrappers(wrappers As IEnumerable(Of ContentControl)) As Rect
        Dim minX = Double.MaxValue, minY = Double.MaxValue
        Dim maxX = Double.MinValue, maxY = Double.MinValue

        For Each w In wrappers
            If w Is Nothing Then Continue For
            Dim left = CanvasUtil.GetLeftSafe(w)
            Dim top = CanvasUtil.GetTopSafe(w)
            Dim ww = CanvasUtil.GetWidthSafe(w)
            Dim hh = CanvasUtil.GetHeightSafe(w)

            minX = Math.Min(minX, left)
            minY = Math.Min(minY, top)
            maxX = Math.Max(maxX, left + ww)
            maxY = Math.Max(maxY, top + hh)
        Next

        If minX = Double.MaxValue Then Return New Rect(0, 0, 0, 0)
        Return New Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY))
    End Function


    Private _stroke As System.Windows.Media.Brush = Brushes.Transparent
    Private _fill As System.Windows.Media.Brush = Brushes.Transparent
    Private _strokeThickness As Double = 0

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

            Return _strokeThickness
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


End Class

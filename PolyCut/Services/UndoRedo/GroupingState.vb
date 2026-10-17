Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

' Keep both document and WPF ownership, including imported bare paths and partition canvases.
Friend NotInheritable Class GroupingState
    Private ReadOnly _placements As List(Of Placement)
    Private ReadOnly _selection As List(Of IDrawable)

    Public Sub New(manager As IDrawableManager, drawables As IEnumerable(Of IDrawable))
        _placements = drawables.Select(Function(d) New Placement(manager, d)).ToList()
        _selection = If(PolyCanvas.SelectedItems, Enumerable.Empty(Of IDrawable)()).ToList()
    End Sub

    Public Sub Restore(manager As IDrawableManager, participants As IEnumerable(Of IDrawable))
        Using ModelBatch.Begin()
            For Each drawable In participants
                manager.RemoveDrawableFromCollection(drawable)
                If drawable.ParentGroup IsNot Nothing Then manager.RemoveChildFromGroup(drawable.ParentGroup, drawable)
                DetachVisual(VisualOf(drawable))
            Next

            For Each placement In _placements.OrderBy(Function(p) p.GroupIndex)
                If placement.Parent IsNot Nothing Then manager.AddChildToGroup(placement.Parent, placement.Drawable, placement.GroupIndex)
                placement.PrepareVisual()
            Next

            For Each placement In _placements.Where(Function(p) p.CollectionIndex < 0).OrderBy(Function(p) p.VisualIndex)
                placement.AttachVisual()
            Next
            For Each placement In _placements.Where(Function(p) p.CollectionIndex >= 0).OrderBy(Function(p) p.CollectionIndex)
                manager.AddDrawableToCollection(placement.Drawable, placement.CollectionIndex)
                ' Canvas insertion assigns its style and wires interaction; restore local state afterwards.
                placement.RestoreVisualState()
            Next

            manager.NotifyChanged()
            PolyCanvas.ApplySelection(_selection, clearExisting:=True)
        End Using
    End Sub

    Friend Shared Function VisualOf(drawable As IDrawable) As FrameworkElement
        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        Return If(wrapper IsNot Nothing AndAlso wrapper.Content Is element, DirectCast(wrapper, FrameworkElement), element)
    End Function

    Friend Shared Sub DetachVisual(visual As FrameworkElement)
        Dim host = TryCast(visual.Parent, Panel)
        If host IsNot Nothing Then host.Children.Remove(visual)
    End Sub

    Private NotInheritable Class Placement
        Public ReadOnly Drawable As IDrawable
        Public ReadOnly Parent As IDrawable
        Public ReadOnly GroupIndex As Integer
        Public ReadOnly CollectionIndex As Integer
        Public ReadOnly VisualIndex As Integer
        Private ReadOnly _visual As FrameworkElement
        Private ReadOnly _host As Panel
        Private ReadOnly _elementState As ElementState
        Private ReadOnly _visualState As ElementState

        Public Sub New(manager As IDrawableManager, item As IDrawable)
            Drawable = item
            Parent = item.ParentGroup
            GroupIndex = If(Parent IsNot Nothing, manager.IndexOfChild(Parent, item), -1)
            CollectionIndex = manager.DrawableCollection.IndexOf(item)
            _visual = VisualOf(item)
            _host = TryCast(_visual.Parent, Panel)
            VisualIndex = If(_host IsNot Nothing, _host.Children.IndexOf(_visual), -1)
            _elementState = New ElementState(item.DrawableElement)
            _visualState = New ElementState(_visual)
        End Sub

        Public Sub PrepareVisual()
            Dim element = Drawable.DrawableElement
            Dim currentWrapper = TryCast(element.Parent, ContentControl)
            If currentWrapper IsNot Nothing AndAlso currentWrapper IsNot _visual AndAlso currentWrapper.Content Is element Then
                currentWrapper.Content = Nothing
            ElseIf element Is _visual OrElse element.Parent IsNot _visual Then
                DetachVisual(element)
            End If
            DetachVisual(_visual)
            If _visual IsNot element Then DirectCast(_visual, ContentControl).Content = element
            RestoreVisualState()
        End Sub

        Public Sub AttachVisual()
            If _host Is Nothing Then Return
            _host.Children.Insert(Math.Min(Math.Max(VisualIndex, 0), _host.Children.Count), _visual)
        End Sub

        Public Sub RestoreVisualState()
            _elementState.Apply(Drawable.DrawableElement)
            _visualState.Apply(_visual)
        End Sub
    End Class

    Private NotInheritable Class ElementState
        Private ReadOnly _width As Double
        Private ReadOnly _height As Double
        Private ReadOnly _left As Double
        Private ReadOnly _top As Double
        Private ReadOnly _transform As Transform
        Private ReadOnly _origin As Point
        Private ReadOnly _style As Style
        Private ReadOnly _hitTestVisible As Boolean
        Private ReadOnly _horizontalAlignment As HorizontalAlignment

        Public Sub New(element As FrameworkElement)
            _width = element.Width
            _height = element.Height
            _left = Canvas.GetLeft(element)
            _top = Canvas.GetTop(element)
            _transform = element.RenderTransform?.CloneCurrentValue()
            _origin = element.RenderTransformOrigin
            _style = element.Style
            _hitTestVisible = element.IsHitTestVisible
            _horizontalAlignment = element.HorizontalAlignment
        End Sub

        Public Sub Apply(element As FrameworkElement)
            element.Style = _style
            element.Width = _width
            element.Height = _height
            Canvas.SetLeft(element, _left)
            Canvas.SetTop(element, _top)
            element.RenderTransform = _transform?.CloneCurrentValue()
            element.RenderTransformOrigin = _origin
            element.IsHitTestVisible = _hitTestVisible
            element.HorizontalAlignment = _horizontalAlignment
        End Sub
    End Class
End Class

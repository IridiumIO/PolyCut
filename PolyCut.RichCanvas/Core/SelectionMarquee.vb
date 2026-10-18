Imports PolyCut.Shared

' KEEP IT TO VIEWPORT. DON'T TRY TO REUSE THE DRAWABLE RECTANGLE IT FUCKS IT ALL UP.
Public NotInheritable Class SelectionMarquee
    Inherits FrameworkElement

    Private ReadOnly _canvas As PolyCanvas
    Private _start As Point
    Private _bounds As Rect = Rect.Empty
    Private _originalSelection As IDrawable()
    Private _additive As Boolean
    Private _dragging As Boolean

    Public Sub New(canvas As PolyCanvas)
        _canvas = canvas
        IsHitTestVisible = False
    End Sub

    Public ReadOnly Property IsActive As Boolean
        Get
            Return _originalSelection IsNot Nothing
        End Get
    End Property

    Public Sub Begin(start As Point, additive As Boolean)
        If IsActive Then Cancel()
        _start = start
        _additive = additive
        _originalSelection = _canvas.SelectionManager.SelectedItems.ToArray()
        _bounds = Rect.Empty
        _dragging = False
        If Not additive Then _canvas.SelectionManager.ClearSelection()
        InvalidateVisual()
    End Sub

    Public Sub Update(current As Point)
        If Not IsActive Then Return
        If Not _dragging Then
            If Math.Abs(current.X - _start.X) < SystemParameters.MinimumHorizontalDragDistance AndAlso 'how did I not know about SystemParameters before
               Math.Abs(current.Y - _start.Y) < SystemParameters.MinimumVerticalDragDistance Then Return
            _dragging = True
        End If
        _bounds = New Rect(_start, current)
        InvalidateVisual()
    End Sub

    Public Sub Complete(current As Point)
        If Not IsActive Then Return
        Update(current)
        Dim selected As New List(Of IDrawable)
        If _additive Then selected.AddRange(RetainedSelection())
        If _dragging AndAlso _bounds.Width > 0 AndAlso _bounds.Height > 0 Then
            selected.AddRange(FindContained(_bounds))
        End If
        Reset()
        _canvas.SelectionManager.ApplySelection(selected, clearExisting:=True)
    End Sub

    Public Sub Cancel(Optional restoreSelection As Boolean = True)
        If Not IsActive Then Return
        Dim original = RetainedSelection().ToArray()
        Reset()
        If restoreSelection Then _canvas.SelectionManager.ApplySelection(original, clearExisting:=True)
    End Sub

    Private Function RetainedSelection() As IEnumerable(Of IDrawable)
        Return _originalSelection.Where(Function(item) Not item.IsHidden AndAlso _canvas.ChildrenCollection IsNot Nothing AndAlso _canvas.ChildrenCollection.Contains(item))
    End Function

    Private Sub Reset()
        _originalSelection = Nothing
        _bounds = Rect.Empty
        _dragging = False
        InvalidateVisual()
    End Sub

    Private Function FindContained(bounds As Rect) As IEnumerable(Of IDrawable)
        Dim result As New List(Of IDrawable)
        If _canvas.ChildrenCollection Is Nothing Then Return result
        For Each item In _canvas.ChildrenCollection
            If item Is Nothing OrElse item.IsHidden OrElse item.DrawableElement Is Nothing Then Continue For
            Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
            ' Only interactive top-level items: (skip the drawables container, imported containers, and children of groups)
            If wrapper Is Nothing OrElse wrapper.Parent IsNot _canvas OrElse
               Not wrapper.IsVisible OrElse Not wrapper.IsHitTestVisible Then Continue For
            Try
                Dim footprint As New Rect(0, 0, wrapper.ActualWidth, wrapper.ActualHeight)
                Dim visualBounds = VisualTreeHelper.GetDescendantBounds(wrapper)
                If Not visualBounds.IsEmpty Then footprint.Union(visualBounds)
                Dim transformed = wrapper.TransformToVisual(Me).TransformBounds(footprint)
                If Not transformed.IsEmpty AndAlso bounds.Contains(transformed) Then result.Add(item)
            Catch ex As InvalidOperationException
                'idk but it can crash if you're doing a selection while the canvas is being updated. Almost never happens but can't pin it down properly
            End Try
        Next
        Return result
    End Function

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        If _bounds.IsEmpty Then Return
        Dim accent = SystemColors.HighlightColor 'TODO consolidate theme colours
        Dim fill As New SolidColorBrush(Color.FromArgb(28, accent.R, accent.G, accent.B))
        dc.DrawRectangle(fill, New Pen(SystemColors.HighlightBrush, 1), _bounds)
    End Sub
End Class

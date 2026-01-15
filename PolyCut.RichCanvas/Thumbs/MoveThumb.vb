Imports System.Windows.Controls.Primitives
Imports System.Windows.Controls
Imports PolyCut.Shared

Public Class MoveThumb
    Inherits Thumb


    Private rotateTransform As RotateTransform
    Private designerItem As ContentControl
    Private initialPosition As Point
    Private dragDirection As String

    Public Sub New()
        AddHandler DragStarted, AddressOf Me.MoveThumb_DragStarted
        AddHandler DragDelta, AddressOf Me.MoveThumb_DragDelta
        AddHandler DragCompleted, AddressOf Me.MoveThumb_DragCompleted
        AddHandler Me.MouseWheel, AddressOf Me.MoveThumb_MouseWheel
        AddHandler Me.MouseDoubleClick, AddressOf Me.MoveThumb_MouseDoubleClick
    End Sub

    Private _storedDataContext As ContentControl = Nothing


    ' Handles double-clicking to edit text boxes
    ' This really should not be here, but I don't know where else to put it
    Private Sub MoveThumb_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)

        Dim childElement As TextBox = TryCast(DataContext.Content, TextBox)
        If childElement IsNot Nothing Then
            Selector.SetIsSelected(DataContext, False)

            Dim parentPolyCanvas As PolyCanvas = TryCast(DataContext.Parent, PolyCanvas)
            _storedDataContext = DataContext
            parentPolyCanvas.Children.Remove(_storedDataContext)
            _storedDataContext.Content = Nothing
            parentPolyCanvas.Children.Add(childElement)
            Canvas.SetLeft(childElement, Canvas.GetLeft(DataContext))
            Canvas.SetTop(childElement, Canvas.GetTop(DataContext))

            childElement.Focus()
            AddHandler childElement.LostFocus, AddressOf OnTextBoxLostFocus
            e.Handled = True

            Return

        End If
    End Sub



    Private Sub OnTextBoxLostFocus(sender As Object, e As RoutedEventArgs)
        Dim textBox As TextBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then

            Dim parent As PolyCanvas = TryCast(textBox.Parent, PolyCanvas)
            parent.Children.Remove(textBox)
            _storedDataContext.Content = textBox
            _storedDataContext.Width = textBox.ActualWidth
            parent.Children.Add(_storedDataContext)

            RemoveHandler textBox.LostFocus, AddressOf OnTextBoxLostFocus
        End If
    End Sub



    Private Sub MoveThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        ' Check if this is a multi-select adorner
        Dim multiAdorner = TryCast(DataContext, MultiSelectAdorner)
        If multiAdorner IsNot Nothing Then
            ' Multi-select mode - handle differently
            HandleMultiSelectDragStart(multiAdorner)
            Return
        End If

        ' Single item mode
        Me.designerItem = TryCast(DataContext, ContentControl)

        If Me.designerItem Is Nothing Then Return

        Me.rotateTransform = TryCast(Me.designerItem.RenderTransform, RotateTransform)
        initialPosition = New Point(Canvas.GetLeft(Me.designerItem), Canvas.GetTop(Me.designerItem))
        dragDirection = Nothing

    End Sub

    Private _multiSelectItems As IReadOnlyList(Of IDrawable)
    Private _multiSelectInitialPositions As New Dictionary(Of IDrawable, Point)

    Private Sub HandleMultiSelectDragStart(adorner As MultiSelectAdorner)
        _multiSelectItems = adorner.SelectedItems
        _multiSelectInitialPositions.Clear()

        ' Store initial positions
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    _multiSelectInitialPositions(drawable) = New Point(Canvas.GetLeft(wrapper), Canvas.GetTop(wrapper))
                End If
            End If
        Next
    End Sub


    Private Sub MoveThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
        ' Check if multi-select mode
        If _multiSelectItems IsNot Nothing AndAlso _multiSelectItems.Count > 0 Then
            HandleMultiSelectDragDelta(e)
            Return
        End If

        ' Single item mode
        If Me.designerItem Is Nothing Then Return

        Dim dragDelta As New Point(e.HorizontalChange, e.VerticalChange)

        If Me.rotateTransform IsNot Nothing Then dragDelta = Me.rotateTransform.Transform(dragDelta)

        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            Dim cumulative As New Point(initialPosition.X + Canvas.GetLeft(designerItem), initialPosition.Y + Canvas.GetTop(designerItem))

            If dragDirection = Nothing Then dragDirection = If(Math.Abs(cumulative.X) > Math.Abs(cumulative.Y), "Horizontal", "Vertical")

            If dragDirection = "Horizontal" Then
                Canvas.SetTop(Me.designerItem, initialPosition.Y)
                dragDelta.Y = 0
            Else
                Canvas.SetLeft(Me.designerItem, initialPosition.X)
                dragDelta.X = 0
            End If
        Else
            dragDirection = Nothing

        End If

        Canvas.SetLeft(Me.designerItem, Canvas.GetLeft(Me.designerItem) + dragDelta.X)
        Canvas.SetTop(Me.designerItem, Canvas.GetTop(Me.designerItem) + dragDelta.Y)
    End Sub

    Private Sub HandleMultiSelectDragDelta(e As DragDeltaEventArgs)
        Dim dragDelta = New Point(e.HorizontalChange, e.VerticalChange)

        ' Handle SHIFT 
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            If dragDirection Is Nothing Then
                dragDirection = If(Math.Abs(dragDelta.X) > Math.Abs(dragDelta.Y), "Horizontal", "Vertical")
            End If

            If dragDirection = "Horizontal" Then
                dragDelta.Y = 0
            Else
                dragDelta.X = 0
            End If
        Else
            dragDirection = Nothing
        End If

        ' Move all items
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + dragDelta.X)
                    Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + dragDelta.Y)
                End If
            End If
        Next

        ' Update adorner
        Dim adorner = TryCast(DataContext, MultiSelectAdorner)
        adorner?.InvalidateArrange()
    End Sub



    Private Sub MoveThumb_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        dragDirection = Nothing
        _multiSelectItems = Nothing
        _multiSelectInitialPositions.Clear()
    End Sub



    Private Sub MoveThumb_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        PassThroughMouseWheelEvent(e)
    End Sub

    'Because the Adorner Layer is separate from the regular visual tree and you can't just "e.handled = false" your way out of this one. Ask me how long it took me to figure this out.
    Private Sub PassThroughMouseWheelEvent(e As MouseWheelEventArgs)
        ' Perform a hit test to find the underlying element
        Dim position As Point = e.GetPosition(Me)
        Dim hitTestResult As HitTestResult = VisualTreeHelper.HitTest(DataContext, position)

        If hitTestResult IsNot Nothing AndAlso hitTestResult.VisualHit IsNot Me Then
            ' Create a new MouseWheelEventArgs
            Dim newEventArgs As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) With {
                .RoutedEvent = UIElement.MouseWheelEvent,
                .Source = e.Source
            }

            ' Raise the event on the underlying element
            Dim underlyingElement As UIElement = TryCast(hitTestResult.VisualHit, UIElement)
            underlyingElement?.RaiseEvent(newEventArgs)
        End If
    End Sub

End Class


Imports System.Windows.Controls.Primitives
Imports System.Windows.Controls

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
            parent.ChildrenCollection.Add(_storedDataContext)

            RemoveHandler textBox.LostFocus, AddressOf OnTextBoxLostFocus
        End If
    End Sub



    Private Sub MoveThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        Me.designerItem = TryCast(DataContext, ContentControl)

        If Me.designerItem Is Nothing Then Return

        Me.rotateTransform = TryCast(Me.designerItem.RenderTransform, RotateTransform)
        initialPosition = New Point(Canvas.GetLeft(Me.designerItem), Canvas.GetTop(Me.designerItem))
        dragDirection = Nothing

    End Sub

    Private Sub MoveThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
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


    Private Sub MoveThumb_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        dragDirection = Nothing
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
            If underlyingElement IsNot Nothing Then
                underlyingElement.RaiseEvent(newEventArgs)
            End If
        End If
    End Sub

End Class


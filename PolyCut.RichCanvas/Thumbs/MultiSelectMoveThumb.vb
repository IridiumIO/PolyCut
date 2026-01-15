Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared

''' <summary>
''' MoveThumb for multi-select adorner - moves all selected items together
''' </summary>
Public Class MultiSelectMoveThumb : Inherits Thumb

    Private _selectedItems As IReadOnlyList(Of IDrawable)
    Private _initialPositions As New Dictionary(Of IDrawable, Point)
    Private _dragDirection As String

    Public Sub New()
        AddHandler DragStarted, AddressOf MultiSelectMoveThumb_DragStarted
        AddHandler DragDelta, AddressOf MultiSelectMoveThumb_DragDelta
        AddHandler DragCompleted, AddressOf MultiSelectMoveThumb_DragCompleted
    End Sub

    Private Sub MultiSelectMoveThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        _selectedItems = PolyCanvas.SelectedItems
        _initialPositions.Clear()
        _dragDirection = Nothing

        ' Store initial positions of all selected items
        For Each drawable In _selectedItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim initialPos = New Point(Canvas.GetLeft(wrapper), Canvas.GetTop(wrapper))
                    _initialPositions(drawable) = initialPos
                End If
            End If
        Next
    End Sub

    Private Sub MultiSelectMoveThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
        If _selectedItems Is Nothing OrElse _selectedItems.Count = 0 Then Return

        Dim dragDelta = New Point(e.HorizontalChange, e.VerticalChange)

        ' Handle SHIFT key for constrained movement (horizontal or vertical only)
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            If _dragDirection Is Nothing Then
                _dragDirection = If(Math.Abs(dragDelta.X) > Math.Abs(dragDelta.Y), "Horizontal", "Vertical")
            End If

            If _dragDirection = "Horizontal" Then
                dragDelta.Y = 0
            Else
                dragDelta.X = 0
            End If
        Else
            _dragDirection = Nothing
        End If

        ' Move all selected items by the same delta
        For Each drawable In _selectedItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + dragDelta.X)
                    Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + dragDelta.Y)
                End If
            End If
        Next

        ' Update adorner bounds
        Dim adorner = TryCast(DataContext, MultiSelectAdorner)
        adorner?.InvalidateArrange()
    End Sub

    Private Sub MultiSelectMoveThumb_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        _dragDirection = Nothing
        _initialPositions.Clear()
    End Sub

End Class

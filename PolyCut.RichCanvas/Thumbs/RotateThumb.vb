Imports System.Windows.Controls.Primitives

Public Class RotateThumb
    Inherits Thumb

    Private initialAngle As Double
    Private rotateTransform As RotateTransform
    Private startVector As Vector
    Private centerPoint As Point
    Private designerItem As ContentControl
    Private canvas As Controls.Canvas

    Public Sub New()
        AddHandler DragDelta, AddressOf Me.RotateThumb_DragDelta
        AddHandler DragStarted, AddressOf Me.RotateThumb_DragStarted
    End Sub

    Private Sub RotateThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        Me.designerItem = TryCast(DataContext, ContentControl)

        If Me.designerItem IsNot Nothing Then
            Me.canvas = TryCast(VisualTreeHelper.GetParent(Me.designerItem), Controls.Canvas)

            If Me.canvas IsNot Nothing Then
                Me.centerPoint = Me.designerItem.TranslatePoint(New Point(Me.designerItem.Width * Me.designerItem.RenderTransformOrigin.X, Me.designerItem.Height * Me.designerItem.RenderTransformOrigin.Y), Me.canvas)

                Dim startPoint As Point = Mouse.GetPosition(Me.canvas)
                Me.startVector = Point.Subtract(startPoint, Me.centerPoint)

                Me.rotateTransform = TryCast(Me.designerItem.RenderTransform, RotateTransform)
                If Me.rotateTransform Is Nothing Then
                    Me.designerItem.RenderTransform = New RotateTransform(0)
                    Me.initialAngle = 0
                Else
                    Me.initialAngle = Me.rotateTransform.Angle
                End If
            End If
        End If
    End Sub

    Private Sub RotateThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)

        If Me.designerItem Is Nothing OrElse Me.canvas Is Nothing Then Return

        Dim currentPoint As Point = Mouse.GetPosition(Me.canvas)
        Dim deltaVector As Vector = Point.Subtract(currentPoint, Me.centerPoint)

        Dim angle As Double = Vector.AngleBetween(Me.startVector, deltaVector)
        Dim totalAngle As Double = initialAngle + angle
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            ' Snap the angle to the nearest 15 degrees
            totalAngle = Math.Round((totalAngle) / 15) * 15
        Else
            totalAngle = Math.Round(totalAngle, 0)
        End If

        Dim rotateTransform As RotateTransform = TryCast(Me.designerItem.RenderTransform, RotateTransform)
        rotateTransform.Angle = totalAngle
        Me.designerItem.InvalidateVisual()
    End Sub
End Class
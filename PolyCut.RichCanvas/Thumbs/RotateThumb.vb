Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared

Public Class RotateThumb
    Inherits Thumb

    Private initialAngle As Double
    Private rotateTransform As RotateTransform
    Private startVector As Vector
    Private centerPoint As Point
    Private designerItem As ContentControl
    Private canvas As Controls.Canvas


    Private _multiSelectItems As IReadOnlyList(Of IDrawable)
    Private _multiSelectInitialAngles As New Dictionary(Of IDrawable, Double)
    Private _multiSelectInitialPositions As New Dictionary(Of IDrawable, Point)
    Private _multiSelectBoundsCenter As Point

    Public Sub New()
        AddHandler DragDelta, AddressOf Me.RotateThumb_DragDelta
        AddHandler DragStarted, AddressOf Me.RotateThumb_DragStarted
        AddHandler DragCompleted, AddressOf Me.RotateThumb_DragCompleted
    End Sub

    Private Sub RotateThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        ' Check for multi-select adorner
        Dim multiAdorner = TryCast(DataContext, MultiSelectAdorner)
        If multiAdorner IsNot Nothing Then
            HandleMultiSelectRotateStart(multiAdorner)
            Return
        End If

        ' Single item mode
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

    Private Sub HandleMultiSelectRotateStart(adorner As MultiSelectAdorner)
        _multiSelectItems = adorner.SelectedItems
        _multiSelectInitialAngles.Clear()
        _multiSelectInitialPositions.Clear()

        ' Get canvas from first item
        If _multiSelectItems.Count > 0 Then
            Dim firstWrapper = TryCast(_multiSelectItems(0).DrawableElement?.Parent, ContentControl)
            If firstWrapper IsNot Nothing Then
                Me.canvas = TryCast(VisualTreeHelper.GetParent(firstWrapper), Controls.Canvas)
            End If
        End If

        If Me.canvas Is Nothing Then Return

        ' Calculate center of bounding box
        Dim bounds = adorner.GetSelectedItemsBounds()
        If bounds.HasValue Then
            _multiSelectBoundsCenter = New Point(
                bounds.Value.Left + bounds.Value.Width / 2,
                bounds.Value.Top + bounds.Value.Height / 2
            )
        End If

        ' Store initial angles and positions for all items
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    ' Store current rotation angle
                    Dim currentRotate = TryCast(wrapper.RenderTransform, RotateTransform)
                    _multiSelectInitialAngles(drawable) = If(currentRotate IsNot Nothing, currentRotate.Angle, 0.0)

                    ' Store current position
                    Dim left = Canvas.GetLeft(wrapper)
                    Dim top = Canvas.GetTop(wrapper)
                    _multiSelectInitialPositions(drawable) = New Point(left, top)
                End If
            End If
        Next

        ' Calculate initial vector from center to mouse
        Dim startPoint = Mouse.GetPosition(Me.canvas)
        Me.startVector = Point.Subtract(startPoint, _multiSelectBoundsCenter)
        Me.initialAngle = 0 ' We track rotation delta, not absolute
    End Sub

    Private Sub RotateThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
        ' Check for multi-select mode
        If _multiSelectItems IsNot Nothing AndAlso _multiSelectItems.Count > 0 Then
            HandleMultiSelectRotate()
            Return
        End If

        ' Single item mode
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

    Private Sub HandleMultiSelectRotate()
        If Me.canvas Is Nothing Then Return

        ' Calculate current rotation delta
        Dim currentPoint = Mouse.GetPosition(Me.canvas)
        Dim deltaVector = Point.Subtract(currentPoint, _multiSelectBoundsCenter)
        Dim rotationDelta = Vector.AngleBetween(Me.startVector, deltaVector)

        ' Apply snapping if SHIFT is held
        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            rotationDelta = Math.Round(rotationDelta / 15) * 15
        Else
            rotationDelta = Math.Round(rotationDelta, 0)
        End If

        ' Rotate all items around the group center
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso _multiSelectInitialAngles.ContainsKey(drawable) AndAlso _multiSelectInitialPositions.ContainsKey(drawable) Then
                    ' Apply rotation to the item itself
                    Dim newAngle = _multiSelectInitialAngles(drawable) + rotationDelta
                    Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
                    If rotateTransform Is Nothing Then
                        rotateTransform = New RotateTransform(newAngle)
                        wrapper.RenderTransform = rotateTransform
                    Else
                        rotateTransform.Angle = newAngle
                    End If

                    ' Rotate the item's position around the group center
                    Dim initialPos = _multiSelectInitialPositions(drawable)

                    ' Get item center in initial position
                    Dim itemCenterX = initialPos.X + wrapper.ActualWidth / 2
                    Dim itemCenterY = initialPos.Y + wrapper.ActualHeight / 2

                    ' Calculate offset from group center
                    Dim offsetX = itemCenterX - _multiSelectBoundsCenter.X
                    Dim offsetY = itemCenterY - _multiSelectBoundsCenter.Y

                    ' Rotate the offset around group center
                    Dim radians = rotationDelta * Math.PI / 180.0
                    Dim cosAngle = Math.Cos(radians)
                    Dim sinAngle = Math.Sin(radians)

                    Dim rotatedOffsetX = offsetX * cosAngle - offsetY * sinAngle
                    Dim rotatedOffsetY = offsetX * sinAngle + offsetY * cosAngle

                    ' Calculate new position (subtract half width/height to get top-left)
                    Dim newLeft = _multiSelectBoundsCenter.X + rotatedOffsetX - wrapper.ActualWidth / 2
                    Dim newTop = _multiSelectBoundsCenter.Y + rotatedOffsetY - wrapper.ActualHeight / 2

                    Canvas.SetLeft(wrapper, newLeft)
                    Canvas.SetTop(wrapper, newTop)

                    wrapper.InvalidateVisual()
                End If
            End If
        Next

        ' Update adorner bounds
        Dim adorner = TryCast(DataContext, MultiSelectAdorner)
        adorner?.InvalidateArrange()
    End Sub

    Private Sub RotateThumb_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        ' Clean up multi-select state
        _multiSelectItems = Nothing
        _multiSelectInitialAngles.Clear()
        _multiSelectInitialPositions.Clear()
    End Sub



End Class

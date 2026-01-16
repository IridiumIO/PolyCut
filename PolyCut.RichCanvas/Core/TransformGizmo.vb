Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared

Public Class TransformGizmo
    Inherits FrameworkElement

    Private _handles As New Dictionary(Of String, Rect)
    Private _activeHandle As String = Nothing
    Private _dragStart As Point
    Private _initialBounds As Rect
    Private _initialTransforms As New Dictionary(Of IDrawable, TransformState)
    Private _initialSizes As New Dictionary(Of IDrawable, (Width As Double, Height As Double))
    Private _initialPositions As New Dictionary(Of IDrawable, Point)
    Private _cumulativeChangeX As Double = 0
    Private _cumulativeChangeY As Double = 0
    Private _selectionManager As SelectionManager
    Private _canvas As Canvas
    Private _rotateHandleRect As Rect
    Private _scale As Double = 1.0
    Private _lastClickTime As DateTime = DateTime.MinValue
    Private _lastClickPosition As Point
    Private Const DOUBLE_CLICK_TIME_MS As Integer = 500
    Private Const DOUBLE_CLICK_DISTANCE As Double = 5

    Private Const HANDLE_SIZE As Double = 9
    Private Const ROTATE_HANDLE_SIZE As Double = 42
    Private Const ROTATE_HANDLE_OFFSET As Double = 48
    Private Const CARDINAL_HANDLE_SIZE As Double = 9

    Private _subscribedWrappers As New List(Of ContentControl)

    Public Sub New(selectionManager As SelectionManager, canvas As Canvas)
        _selectionManager = selectionManager
        _canvas = canvas

        Me.IsHitTestVisible = True
        Me.Cursor = Cursors.Arrow
        Me.Width = Double.NaN
        Me.Height = Double.NaN

        AddHandler Me.MouseLeftButtonDown, AddressOf OnMouseDown
        AddHandler Me.MouseMove, AddressOf OnMouseMove
        AddHandler Me.MouseLeftButtonUp, AddressOf OnMouseUp
        AddHandler _selectionManager.SelectionChanged, AddressOf OnSelectionChanged

        EventAggregator.Subscribe(AddressOf OnScaleChanged)
    End Sub

    Public Property Scale As Double
        Get
            Return _scale
        End Get
        Set(value As Double)
            _scale = value
            InvalidateVisual()
        End Set
    End Property

    Private Sub OnScaleChanged(message As Object)
        If TypeOf message IsNot ScaleChangedMessage Then Return
        Scale = CType(message, ScaleChangedMessage).NewScale
    End Sub

    Private Sub OnSelectionChanged(sender As Object, e As EventArgs)
        ' Unsubscribe from old wrappers
        For Each wrapper In _subscribedWrappers
            RemoveHandler wrapper.SizeChanged, AddressOf OnWrapperPropertyChanged
            RemoveHandler wrapper.LayoutUpdated, AddressOf OnWrapperPropertyChanged
        Next
        _subscribedWrappers.Clear()


        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    AddHandler wrapper.SizeChanged, AddressOf OnWrapperPropertyChanged
                    AddHandler wrapper.LayoutUpdated, AddressOf OnWrapperPropertyChanged
                    _subscribedWrappers.Add(wrapper)
                End If
            End If
        Next

        InvalidateVisual()
    End Sub

    Private Sub OnWrapperPropertyChanged(sender As Object, e As EventArgs)
        _selectionManager.InvalidateBoundsCache()
        InvalidateVisual()
    End Sub

    Protected Overrides Sub OnRender(drawingContext As DrawingContext)
        MyBase.OnRender(drawingContext)

        Dim bounds = _selectionManager.GetUnrotatedBounds()
        If Not bounds.HasValue Then Return

        Dim rect = bounds.Value
        Dim rotationAngle = GetSelectionRotation()
        Dim hasRotation = Math.Abs(rotationAngle) > 0.01

        If hasRotation Then
            Dim boundsCenter = New Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
            drawingContext.PushTransform(New RotateTransform(rotationAngle, boundsCenter.X, boundsCenter.Y))
        End If

        Dim handleSize = HANDLE_SIZE / _scale
        Dim strokeThickness = 1.0 / _scale
        Dim rotateHandleSize = ROTATE_HANDLE_SIZE / _scale
        Dim rotateOffset = ROTATE_HANDLE_OFFSET / _scale

        Dim pen As New Pen(Brushes.Gray, strokeThickness) With {.DashStyle = DashStyles.Dash}
        drawingContext.DrawRectangle(Nothing, pen, rect)

        Dim moveFillBrush As New SolidColorBrush(Color.FromArgb(&H8, &H0, &H0, &HFF))
        drawingContext.DrawRectangle(moveFillBrush, Nothing, rect)

        _handles.Clear()

        _handles("TopLeft") = New Rect(rect.Left - handleSize / 2, rect.Top - handleSize / 2, handleSize, handleSize)
        _handles("TopRight") = New Rect(rect.Right - handleSize / 2, rect.Top - handleSize / 2, handleSize, handleSize)
        _handles("BottomLeft") = New Rect(rect.Left - handleSize / 2, rect.Bottom - handleSize / 2, handleSize, handleSize)
        _handles("BottomRight") = New Rect(rect.Right - handleSize / 2, rect.Bottom - handleSize / 2, handleSize, handleSize)

        Dim cardinalHandleSize = CARDINAL_HANDLE_SIZE / _scale
        _handles("Top") = New Rect(rect.Left + rect.Width / 2 - cardinalHandleSize / 2, rect.Top - cardinalHandleSize / 2, cardinalHandleSize, cardinalHandleSize)
        _handles("Bottom") = New Rect(rect.Left + rect.Width / 2 - cardinalHandleSize / 2, rect.Bottom - cardinalHandleSize / 2, cardinalHandleSize, cardinalHandleSize)
        _handles("Left") = New Rect(rect.Left - cardinalHandleSize / 2, rect.Top + rect.Height / 2 - cardinalHandleSize / 2, cardinalHandleSize, cardinalHandleSize)
        _handles("Right") = New Rect(rect.Right - cardinalHandleSize / 2, rect.Top + rect.Height / 2 - cardinalHandleSize / 2, cardinalHandleSize, cardinalHandleSize)

        _rotateHandleRect = New Rect(
            rect.Left + rect.Width / 2 - rotateHandleSize / 2,
            rect.Top - rotateOffset - rotateHandleSize / 2,
            rotateHandleSize,
            rotateHandleSize)

        Dim handleBrush As New RadialGradientBrush() With {
            .Center = New Point(0.2, 0.2),
            .GradientOrigin = New Point(0.2, 0.2),
            .RadiusX = 0.8,
            .RadiusY = 0.8
        }
        handleBrush.GradientStops.Add(New GradientStop(Colors.White, 0.0))
        handleBrush.GradientStops.Add(New GradientStop(Color.FromRgb(&HE2, &HE2, &HE2), 0.8))

        Dim handlePen As New Pen(Brushes.Black, strokeThickness)

        drawingContext.DrawRectangle(handleBrush, handlePen, _handles("TopLeft"))
        drawingContext.DrawRectangle(handleBrush, handlePen, _handles("TopRight"))
        drawingContext.DrawRectangle(handleBrush, handlePen, _handles("BottomLeft"))
        drawingContext.DrawRectangle(handleBrush, handlePen, _handles("BottomRight"))

        Dim edgeHandleBrush As New SolidColorBrush(Color.FromArgb(&HFF, &HA0, &HA0, &HA0))
        Dim edgePen As New Pen(Brushes.White, strokeThickness)

        drawingContext.DrawRectangle(edgeHandleBrush, edgePen, _handles("Top"))
        drawingContext.DrawRectangle(edgeHandleBrush, edgePen, _handles("Bottom"))
        drawingContext.DrawRectangle(edgeHandleBrush, edgePen, _handles("Left"))
        drawingContext.DrawRectangle(edgeHandleBrush, edgePen, _handles("Right"))

        Dim rotateBackBrush As New SolidColorBrush(Color.FromArgb(&H40, &H30, &H66, &HCC))
        Dim rotatePen As New Pen(New SolidColorBrush(Color.FromRgb(&H30, &H66, &HCC)), strokeThickness)
        drawingContext.DrawEllipse(rotateBackBrush, rotatePen, New Point(_rotateHandleRect.Left + _rotateHandleRect.Width / 2, _rotateHandleRect.Top + _rotateHandleRect.Height / 2), _rotateHandleRect.Width / 2, _rotateHandleRect.Height / 2)

        Dim iconBrush As New SolidColorBrush(Color.FromRgb(&H40, &HA0, &HE0))
        Dim iconSize = 20 / _scale
        Dim iconCenter = New Point(
            _rotateHandleRect.Left + _rotateHandleRect.Width / 2,
            _rotateHandleRect.Top + _rotateHandleRect.Height / 2)
        Dim radius = iconSize / 2.5

        Dim arcPen As New Pen(iconBrush, strokeThickness * 2.5) With {
            .StartLineCap = PenLineCap.Round,
            .EndLineCap = PenLineCap.Triangle
        }

        Dim arcGeometry As New PathGeometry()
        Dim startAngle = Math.PI / 4
        Dim endAngle = startAngle + (3 * Math.PI / 2)

        Dim startPt = New Point(
            iconCenter.X + radius * Math.Cos(startAngle),
            iconCenter.Y + radius * Math.Sin(startAngle))
        Dim endPt = New Point(
            iconCenter.X + radius * Math.Cos(endAngle),
            iconCenter.Y + radius * Math.Sin(endAngle))

        Dim figure As New PathFigure() With {.StartPoint = startPt}
        figure.Segments.Add(New ArcSegment(endPt, New Size(radius, radius), 0, True, SweepDirection.Clockwise, True))
        arcGeometry.Figures.Add(figure)
        drawingContext.DrawGeometry(Nothing, arcPen, arcGeometry)

        Dim arrowSize = 4 / _scale
        Dim arrowAngle = endAngle + Math.PI / 2
        Dim arrowTip = endPt
        Dim arrowLeft = New Point(
            arrowTip.X - arrowSize * Math.Cos(arrowAngle - 0.4),
            arrowTip.Y - arrowSize * Math.Sin(arrowAngle - 0.4))
        Dim arrowRight = New Point(
            arrowTip.X - arrowSize * Math.Cos(arrowAngle + 0.4),
            arrowTip.Y - arrowSize * Math.Sin(arrowAngle + 0.4))

        Dim arrowGeometry As New PathGeometry()
        Dim arrowFigure As New PathFigure() With {.StartPoint = arrowLeft, .IsClosed = True, .IsFilled = True}
        arrowFigure.Segments.Add(New LineSegment(arrowTip, True))
        arrowFigure.Segments.Add(New LineSegment(arrowRight, True))
        arrowGeometry.Figures.Add(arrowFigure)
        drawingContext.DrawGeometry(iconBrush, New Pen(iconBrush, strokeThickness), arrowGeometry)

        If hasRotation Then
            drawingContext.Pop()
        End If
    End Sub

    Private Function GetSelectionRotation() As Double
        If _selectionManager.Count <> 1 Then Return 0

        Dim item = _selectionManager.SelectedItems.FirstOrDefault()
        If item?.DrawableElement IsNot Nothing Then
            Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
            If wrapper IsNot Nothing Then
                Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
                If rotateTransform IsNot Nothing Then
                    Return rotateTransform.Angle
                End If
            End If
        End If

        Return 0
    End Function

    Private Sub OnMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim pos = e.GetPosition(Me)
        Dim bounds = _selectionManager.GetUnrotatedBounds()
        If Not bounds.HasValue Then Return

        Dim rotationAngle = GetSelectionRotation()
        If Math.Abs(rotationAngle) > 0.01 Then
            Dim centerPt = New Point(bounds.Value.Left + bounds.Value.Width / 2, bounds.Value.Top + bounds.Value.Height / 2)
            Dim angleRad = -rotationAngle * Math.PI / 180.0
            Dim dx = pos.X - centerPt.X
            Dim dy = pos.Y - centerPt.Y
            pos = New Point(
                centerPt.X + (dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad)),
                centerPt.Y + (dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad))
            )
        End If

        Dim timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds
        Dim distanceFromLastClick = Math.Sqrt(Math.Pow(pos.X - _lastClickPosition.X, 2) + Math.Pow(pos.Y - _lastClickPosition.Y, 2))

        If timeSinceLastClick < DOUBLE_CLICK_TIME_MS AndAlso distanceFromLastClick < DOUBLE_CLICK_DISTANCE Then
            HandleDoubleClick(pos, bounds.Value)
            _lastClickTime = DateTime.MinValue
            e.Handled = True
            Return
        End If

        _lastClickTime = DateTime.Now
        _lastClickPosition = pos

        If _rotateHandleRect.Contains(pos) Then
            StartRotate(e.GetPosition(Me))
            e.Handled = True
            Return
        End If

        For Each kvp In _handles
            If kvp.Value.Contains(pos) Then
                StartResize(kvp.Key, e.GetPosition(Me))
                e.Handled = True
                Return
            End If
        Next

        Dim hitBounds = bounds.Value
        hitBounds.Inflate(5, 5)

        If hitBounds.Contains(pos) Then
            StartMove(e.GetPosition(Me))
            e.Handled = True
        End If
    End Sub

    Private Sub HandleDoubleClick(pos As Point, bounds As Rect)
        Dim hitBounds = bounds
        hitBounds.Inflate(5, 5)

        If hitBounds.Contains(pos) AndAlso _selectionManager.Count = 1 Then
            Dim item = _selectionManager.SelectedItems.FirstOrDefault()
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso TypeOf wrapper.Content Is TextBox Then
                    Dim textBox = CType(wrapper.Content, TextBox)
                    textBox.Focus()
                End If
            End If
        End If
    End Sub

    Private Sub OnMouseMove(sender As Object, e As MouseEventArgs)
        If e.LeftButton <> MouseButtonState.Pressed Then
            UpdateCursor(e.GetPosition(Me))
            Return
        End If

        If _activeHandle Is Nothing Then Return

        Dim currentPos = e.GetPosition(_canvas)

        If _activeHandle = "Rotate" Then
            PerformRotate(currentPos)
        ElseIf _activeHandle = "Move" Then
            PerformMove(currentPos)
        Else
            PerformResize(currentPos)
        End If

        If _selectionManager.Count > 1 Then
            _selectionManager.InvalidateBoundsCache()
        End If

        InvalidateVisual()
    End Sub

    Private Sub OnMouseUp(sender As Object, e As MouseButtonEventArgs)
        _activeHandle = Nothing
        _initialTransforms.Clear()
        _initialSizes.Clear()
        _initialPositions.Clear()
        _cumulativeChangeX = 0
        _cumulativeChangeY = 0
        Me.ReleaseMouseCapture()

        _selectionManager.InvalidateBoundsCache()
        InvalidateVisual()
    End Sub

    Private Sub UpdateCursor(pos As Point)
        ' Get unrotated bounds for proper hit testing
        Dim bounds = _selectionManager.GetUnrotatedBounds()
        If Not bounds.HasValue Then Return

        ' If gizmo is rotated, transform mouse position to match
        Dim rotationAngle = GetSelectionRotation()
        If Math.Abs(rotationAngle) > 0.01 Then
            Dim centerPt = New Point(bounds.Value.Left + bounds.Value.Width / 2, bounds.Value.Top + bounds.Value.Height / 2)
            ' Inverse rotate the mouse position
            Dim angleRad = -rotationAngle * Math.PI / 180.0
            Dim dx = pos.X - centerPt.X
            Dim dy = pos.Y - centerPt.Y
            pos = New Point(
                centerPt.X + (dx * Math.Cos(angleRad) - dy * Math.Sin(angleRad)),
                centerPt.Y + (dx * Math.Sin(angleRad) + dy * Math.Cos(angleRad))
            )
        End If

        If _rotateHandleRect.Contains(pos) Then
            Me.Cursor = Cursors.Hand
            Return
        End If

        For Each kvp In _handles
            If kvp.Value.Contains(pos) Then
                Select Case kvp.Key
                    Case "TopLeft", "BottomRight"
                        Me.Cursor = Cursors.SizeNWSE
                    Case "TopRight", "BottomLeft"
                        Me.Cursor = Cursors.SizeNESW
                    Case "Top", "Bottom"
                        Me.Cursor = Cursors.SizeNS
                    Case "Left", "Right"
                        Me.Cursor = Cursors.SizeWE
                End Select
                Return
            End If
        Next

        ' Expand the hit area slightly for easier clicking
        Dim hitBounds = bounds.Value
        hitBounds.Inflate(5, 5)

        If hitBounds.Contains(pos) Then
            Me.Cursor = Cursors.SizeAll
        Else
            Me.Cursor = Cursors.Arrow
        End If
    End Sub

    Private Sub StartRotate(pos As Point)
        _activeHandle = "Rotate"
        _dragStart = pos
        _initialBounds = _selectionManager.GetSelectionBounds().GetValueOrDefault()
        CaptureInitialTransforms()

        _initialPositions.Clear()
        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    _initialPositions(item) = New Point(Canvas.GetLeft(wrapper), Canvas.GetTop(wrapper))
                End If
            End If
        Next

        Me.CaptureMouse()
    End Sub

    Private Sub StartResize(handleName As String, pos As Point)
        _activeHandle = handleName
        _dragStart = pos
        _initialBounds = _selectionManager.GetUnrotatedBounds().GetValueOrDefault()
        _cumulativeChangeX = 0
        _cumulativeChangeY = 0

        CaptureInitialTransforms()

        _initialSizes.Clear()
        _initialPositions.Clear()
        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    _initialSizes(item) = (wrapper.ActualWidth, wrapper.ActualHeight)
                    _initialPositions(item) = New Point(Canvas.GetLeft(wrapper), Canvas.GetTop(wrapper))
                End If
            End If
        Next

        Me.CaptureMouse()
    End Sub

    Private Sub StartMove(pos As Point)
        _activeHandle = "Move"
        _dragStart = pos
        CaptureInitialTransforms()
        Me.CaptureMouse()
    End Sub

    Private Sub CaptureInitialTransforms()
        _initialTransforms.Clear()
        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    _initialTransforms(item) = TransformState.FromElement(wrapper)
                End If
            End If
        Next
    End Sub

    Private Sub PerformRotate(currentPos As Point)
        Dim center = New Point(_initialBounds.Left + _initialBounds.Width / 2, _initialBounds.Top + _initialBounds.Height / 2)
        Dim startVector = Point.Subtract(_dragStart, center)
        Dim currentVector = Point.Subtract(currentPos, center)

        Dim angle = Vector.AngleBetween(startVector, currentVector)

        If Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift) Then
            angle = Math.Round(angle / 15) * 15
        Else
            angle = Math.Round(angle, 0)
        End If

        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso _initialTransforms.ContainsKey(item) AndAlso _initialPositions.ContainsKey(item) Then
                    Dim initialState = _initialTransforms(item)
                    Dim initialPos = _initialPositions(item)

                    wrapper.RenderTransform = New RotateTransform(initialState.Rotation + angle)

                    Dim initialItemCenter = New Point(
                        initialPos.X + wrapper.ActualWidth * wrapper.RenderTransformOrigin.X,
                        initialPos.Y + wrapper.ActualHeight * wrapper.RenderTransformOrigin.Y)

                    Dim offsetFromCenter = Point.Subtract(initialItemCenter, center)
                    Dim angleRad = angle * Math.PI / 180
                    Dim cosA = Math.Cos(angleRad)
                    Dim sinA = Math.Sin(angleRad)

                    Dim rotatedOffset = New Point(
                        offsetFromCenter.X * cosA - offsetFromCenter.Y * sinA,
                        offsetFromCenter.X * sinA + offsetFromCenter.Y * cosA)

                    Dim newItemCenter = Point.Add(center, CType(rotatedOffset, Vector))
                    Canvas.SetLeft(wrapper, newItemCenter.X - wrapper.ActualWidth * wrapper.RenderTransformOrigin.X)
                    Canvas.SetTop(wrapper, newItemCenter.Y - wrapper.ActualHeight * wrapper.RenderTransformOrigin.Y)
                End If
            End If
        Next
    End Sub

    Private Sub PerformMove(currentPos As Point)
        Dim delta = Point.Subtract(currentPos, _dragStart)

        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso _initialTransforms.ContainsKey(item) Then
                    Dim initialState = _initialTransforms(item)
                    Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + delta.X)
                    Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + delta.Y)
                End If
            End If
        Next

        _dragStart = currentPos
    End Sub

    Private Sub PerformResize(currentPos As Point)
        ' Calculate DELTA from last position (not cumulative!)
        Dim deltaX = currentPos.X - _dragStart.X
        Dim deltaY = currentPos.Y - _dragStart.Y
        _dragStart = currentPos

        ' For single selection, use the original ResizeThumb algorithm
        If _selectionManager.Count = 1 Then
            Dim item = _selectionManager.SelectedItems.FirstOrDefault()
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    PerformSingleItemResize(wrapper, deltaX, deltaY)
                    Return
                End If
            End If
        End If

        ' Multi-selection uses cumulative scaling
        _cumulativeChangeX += deltaX
        _cumulativeChangeY += deltaY
        PerformMultiItemResize()
    End Sub

    Private Sub PerformSingleItemResize(wrapper As ContentControl, deltaX As Double, deltaY As Double)
        Dim angle As Double = 0
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            angle = rotateTransform.Angle * Math.PI / 180.0
        End If

        Dim cosA = Math.Cos(-angle)
        Dim sinA = Math.Sin(-angle)
        Dim localDeltaX = deltaX * cosA - deltaY * sinA
        Dim localDeltaY = deltaX * sinA + deltaY * cosA

        Dim transformOrigin = wrapper.RenderTransformOrigin
        Dim deltaVertical As Double = 0
        Dim deltaHorizontal As Double = 0
        Dim verticalAlignment As VerticalAlignment = VerticalAlignment.Center
        Dim horizontalAlignment As HorizontalAlignment = HorizontalAlignment.Center

        Select Case _activeHandle
            Case "Top"
                verticalAlignment = VerticalAlignment.Top
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
            Case "Bottom"
                verticalAlignment = VerticalAlignment.Bottom
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
            Case "Left"
                horizontalAlignment = HorizontalAlignment.Left
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "Right"
                horizontalAlignment = HorizontalAlignment.Right
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "TopLeft"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "TopRight"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "BottomLeft"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "BottomRight"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
        End Select

        ' For corners, maintain aspect ratio
        Dim isCorner = (verticalAlignment = VerticalAlignment.Top OrElse verticalAlignment = VerticalAlignment.Bottom) AndAlso
                       (horizontalAlignment = HorizontalAlignment.Left OrElse horizontalAlignment = HorizontalAlignment.Right)

        If isCorner Then
            Dim aspectRatio = wrapper.ActualWidth / wrapper.ActualHeight
            wrapper.Width = wrapper.Height * aspectRatio
            deltaVertical = Math.Min(deltaVertical, wrapper.ActualHeight - wrapper.MinHeight)
            deltaHorizontal = Math.Min(deltaVertical * aspectRatio, wrapper.ActualWidth - wrapper.MinWidth)
        End If

        Dim currentTop = Canvas.GetTop(wrapper)
        Dim currentLeft = Canvas.GetLeft(wrapper)
        Dim newTop = currentTop
        Dim newLeft = currentLeft

        If verticalAlignment <> VerticalAlignment.Center Then
            newTop += GetCanvasTopOffsetForVertical(verticalAlignment, deltaVertical, angle, transformOrigin)
            newLeft += GetCanvasLeftOffsetForVertical(verticalAlignment, deltaVertical, angle, transformOrigin)
        End If

        If horizontalAlignment <> HorizontalAlignment.Center Then
            newTop += GetCanvasTopOffsetForHorizontal(horizontalAlignment, deltaHorizontal, angle, transformOrigin)
            newLeft += GetCanvasLeftOffsetForHorizontal(horizontalAlignment, deltaHorizontal, angle, transformOrigin)
        End If

        wrapper.Height -= deltaVertical
        wrapper.Width -= deltaHorizontal

        Canvas.SetTop(wrapper, newTop)
        Canvas.SetLeft(wrapper, newLeft)
    End Sub

    Public Shared Function HandleTextBoxSizeChanged(wrapper As ContentControl, e As SizeChangedEventArgs) As Boolean
        Dim textBox = TryCast(wrapper.Content, TextBox)
        If textBox Is Nothing Then Return False

        If Not (textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin) Then
            Return False
        End If

        If e.PreviousSize.Width <= 0 OrElse e.PreviousSize.Height <= 0 Then
            Return True
        End If

        Dim deltaWidth = e.NewSize.Width - e.PreviousSize.Width
        Dim deltaHeight = e.NewSize.Height - e.PreviousSize.Height

        If Math.Abs(deltaWidth) < 0.01 AndAlso Math.Abs(deltaHeight) < 0.01 Then
            Return True
        End If

        Dim angle As Double = 0
        Dim rt = TryCast(wrapper.RenderTransform, RotateTransform)
        If rt IsNot Nothing Then
            angle = rt.Angle * Math.PI / 180.0
        End If

        Dim transformOrigin = wrapper.RenderTransformOrigin

        Dim deltaHorizontal = -deltaWidth
        Dim deltaVertical = -deltaHeight

        Dim newTop = Canvas.GetTop(wrapper)
        If Double.IsNaN(newTop) Then newTop = 0
        Dim newLeft = Canvas.GetLeft(wrapper)
        If Double.IsNaN(newLeft) Then newLeft = 0

        newTop += GetCanvasTopOffsetForVertical(VerticalAlignment.Bottom, deltaVertical, angle, transformOrigin)
        newLeft += GetCanvasLeftOffsetForVertical(VerticalAlignment.Bottom, deltaVertical, angle, transformOrigin)

        newTop += GetCanvasTopOffsetForHorizontal(HorizontalAlignment.Right, deltaHorizontal, angle, transformOrigin)
        newLeft += GetCanvasLeftOffsetForHorizontal(HorizontalAlignment.Right, deltaHorizontal, angle, transformOrigin)

        Canvas.SetTop(wrapper, newTop)
        Canvas.SetLeft(wrapper, newLeft)

        Return True
    End Function

    Private Shared Function GetCanvasTopOffsetForVertical(alignment As VerticalAlignment, deltaVertical As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Cos(-angle) + (transformOrigin.Y * deltaVertical * (1 - Math.Cos(-angle)))
            Case VerticalAlignment.Bottom
                Return transformOrigin.Y * deltaVertical * (1 - Math.Cos(-angle))
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasTopOffsetForHorizontal(alignment As HorizontalAlignment, deltaHorizontal As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Sin(angle) - transformOrigin.X * deltaHorizontal * Math.Sin(angle)
            Case HorizontalAlignment.Right
                Return -transformOrigin.X * deltaHorizontal * Math.Sin(angle)
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasLeftOffsetForVertical(alignment As VerticalAlignment, deltaVertical As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Sin(-angle) - (transformOrigin.Y * deltaVertical * Math.Sin(-angle))
            Case VerticalAlignment.Bottom
                Return -deltaVertical * transformOrigin.Y * Math.Sin(-angle)
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasLeftOffsetForHorizontal(alignment As HorizontalAlignment, deltaHorizontal As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Cos(angle) + (transformOrigin.X * deltaHorizontal * (1 - Math.Cos(angle)))
            Case HorizontalAlignment.Right
                Return deltaHorizontal * transformOrigin.X * (1 - Math.Cos(angle))
            Case Else
                Return 0
        End Select
    End Function

    Private Sub PerformMultiItemResize()
        Dim scaleX As Double = 1.0
        Dim scaleY As Double = 1.0
        Dim anchorX As Double = _initialBounds.Left
        Dim anchorY As Double = _initialBounds.Top

        Select Case _activeHandle
            Case "Right"
                scaleX = (_initialBounds.Width + _cumulativeChangeX) / _initialBounds.Width
                anchorX = _initialBounds.Left
            Case "Left"
                scaleX = (_initialBounds.Width - _cumulativeChangeX) / _initialBounds.Width
                anchorX = _initialBounds.Right
            Case "Bottom"
                scaleY = (_initialBounds.Height + _cumulativeChangeY) / _initialBounds.Height
                anchorY = _initialBounds.Top
            Case "Top"
                scaleY = (_initialBounds.Height - _cumulativeChangeY) / _initialBounds.Height
                anchorY = _initialBounds.Bottom
            Case "TopLeft"
                scaleX = (_initialBounds.Width - _cumulativeChangeX) / _initialBounds.Width
                scaleY = (_initialBounds.Height - _cumulativeChangeY) / _initialBounds.Height
                anchorX = _initialBounds.Right
                anchorY = _initialBounds.Bottom
            Case "TopRight"
                scaleX = (_initialBounds.Width + _cumulativeChangeX) / _initialBounds.Width
                scaleY = (_initialBounds.Height - _cumulativeChangeY) / _initialBounds.Height
                anchorX = _initialBounds.Left
                anchorY = _initialBounds.Bottom
            Case "BottomLeft"
                scaleX = (_initialBounds.Width - _cumulativeChangeX) / _initialBounds.Width
                scaleY = (_initialBounds.Height + _cumulativeChangeY) / _initialBounds.Height
                anchorX = _initialBounds.Right
                anchorY = _initialBounds.Top
            Case "BottomRight"
                scaleX = (_initialBounds.Width + _cumulativeChangeX) / _initialBounds.Width
                scaleY = (_initialBounds.Height + _cumulativeChangeY) / _initialBounds.Height
                anchorX = _initialBounds.Left
                anchorY = _initialBounds.Top
        End Select

        Dim isCorner = _activeHandle.Contains("Top") OrElse _activeHandle.Contains("Bottom")
        If isCorner AndAlso (_activeHandle.Contains("Left") OrElse _activeHandle.Contains("Right")) Then
            Dim avgScale = (scaleX + scaleY) / 2
            scaleX = avgScale
            scaleY = avgScale
        End If

        If scaleX < 0.01 Then scaleX = 0.01
        If scaleY < 0.01 Then scaleY = 0.01

        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso _initialSizes.ContainsKey(item) AndAlso _initialPositions.ContainsKey(item) Then
                    Dim initialSize = _initialSizes(item)
                    Dim initialPos = _initialPositions(item)

                    Dim rotation As Double = 0
                    If _initialTransforms.ContainsKey(item) Then
                        rotation = _initialTransforms(item).Rotation
                    End If

                    wrapper.Width = initialSize.Width * scaleX
                    wrapper.Height = initialSize.Height * scaleY

                    Dim offsetX = initialPos.X - anchorX
                    Dim offsetY = initialPos.Y - anchorY
                    Canvas.SetLeft(wrapper, anchorX + (offsetX * scaleX))
                    Canvas.SetTop(wrapper, anchorY + (offsetY * scaleY))
                    If Math.Abs(rotation) > 0.01 Then
                        wrapper.RenderTransform = New RotateTransform(rotation)
                    Else
                        wrapper.RenderTransform = Nothing
                    End If
                End If
            End If
        Next
    End Sub

End Class

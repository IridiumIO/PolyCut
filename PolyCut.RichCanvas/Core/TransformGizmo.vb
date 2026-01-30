Imports System.ComponentModel

Imports PolyCut.Shared

Public Class TransformGizmo
    Inherits FrameworkElement

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

    Private _renderHooked As Boolean
    Private _needsRefresh As Boolean
    Private _multiBoundsDirty As Boolean

    Private Const HANDLE_SIZE As Double = 9
    Private Const ROTATE_HANDLE_SIZE As Double = 42
    Private Const ROTATE_HANDLE_OFFSET As Double = 48
    Private Const CARDINAL_HANDLE_SIZE As Double = 9

    Private _subscribedWrappers As New List(Of ContentControl)

    Private ReadOnly _styleCache As New BrushCache()
    Private ReadOnly _renderCache As New RenderCache()


    Private ReadOnly _handleRects(7) As Rect
    Private ReadOnly _handleHitRects(7) As Rect
    Private Const HANDLE_HIT_PAD As Double = 6


    Private Shared ReadOnly CanvasLeftDp As DependencyPropertyDescriptor =
    DependencyPropertyDescriptor.FromProperty(Canvas.LeftProperty, GetType(ContentControl))
    Private Shared ReadOnly CanvasTopDp As DependencyPropertyDescriptor =
    DependencyPropertyDescriptor.FromProperty(Canvas.TopProperty, GetType(ContentControl))
    Private Shared ReadOnly RenderTransformDp As DependencyPropertyDescriptor =
    DependencyPropertyDescriptor.FromProperty(UIElement.RenderTransformProperty, GetType(ContentControl))



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

        EventAggregator.Subscribe(Of ScaleChangedMessage)(AddressOf OnScaleChanged)
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
        For Each wrapper In _subscribedWrappers
            RemoveHandler wrapper.SizeChanged, AddressOf OnWrapperPropertyChanged
            CanvasLeftDp.RemoveValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)
            CanvasTopDp.RemoveValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)
            RenderTransformDp.RemoveValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)
        Next
        _subscribedWrappers.Clear()

        For Each item In _selectionManager.SelectedItems
            Dim wrapper = TryCast(item?.DrawableElement?.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            AddHandler wrapper.SizeChanged, AddressOf OnWrapperPropertyChanged
            CanvasLeftDp.AddValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)
            CanvasTopDp.AddValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)
            RenderTransformDp.AddValueChanged(wrapper, AddressOf OnWrapperPropertyChanged)

            _subscribedWrappers.Add(wrapper)
        Next

        RequestGizmoRefresh()
    End Sub




    Private Sub OnWrapperPropertyChanged(sender As Object, e As EventArgs)
        If _activeHandle IsNot Nothing Then Return
        RequestGizmoRefresh()
    End Sub

    Private Sub RequestGizmoRefresh()
        ' Coalesce multiple triggers into a single render-tick update.
        _needsRefresh = True
        If _renderHooked Then Return

        _renderHooked = True
        AddHandler CompositionTarget.Rendering, AddressOf OnRenderTick
    End Sub



    Private Sub OnRenderTick(sender As Object, e As EventArgs)
        RemoveHandler CompositionTarget.Rendering, AddressOf OnRenderTick
        _renderHooked = False

        If Not _needsRefresh Then Return
        _needsRefresh = False

        If _multiBoundsDirty OrElse _activeHandle Is Nothing Then
            _multiBoundsDirty = False
            _selectionManager.InvalidateBoundsCache()
        End If

        InvalidateVisual()
    End Sub


    Private Sub UpdateHandleRects(rect As Rect, handleSize As Double, cardinalHandleSize As Double, rotateHandleSize As Double, rotateOffset As Double)

        Dim hs = handleSize / 2
        Dim chs = cardinalHandleSize / 2

        _handleRects(HandleId.TopLeft) = New Rect(rect.Left - hs, rect.Top - hs, handleSize, handleSize)
        _handleRects(HandleId.TopRight) = New Rect(rect.Right - hs, rect.Top - hs, handleSize, handleSize)
        _handleRects(HandleId.BottomLeft) = New Rect(rect.Left - hs, rect.Bottom - hs, handleSize, handleSize)
        _handleRects(HandleId.BottomRight) = New Rect(rect.Right - hs, rect.Bottom - hs, handleSize, handleSize)

        _handleRects(HandleId.Top) = New Rect(rect.Left + rect.Width / 2 - chs, rect.Top - chs, cardinalHandleSize, cardinalHandleSize)
        _handleRects(HandleId.Bottom) = New Rect(rect.Left + rect.Width / 2 - chs, rect.Bottom - chs, cardinalHandleSize, cardinalHandleSize)
        _handleRects(HandleId.Left) = New Rect(rect.Left - chs, rect.Top + rect.Height / 2 - chs, cardinalHandleSize, cardinalHandleSize)
        _handleRects(HandleId.Right) = New Rect(rect.Right - chs, rect.Top + rect.Height / 2 - chs, cardinalHandleSize, cardinalHandleSize)

        _rotateHandleRect = New Rect(rect.Left + rect.Width / 2 - rotateHandleSize / 2, rect.Top - rotateOffset - rotateHandleSize / 2, rotateHandleSize, rotateHandleSize)

        Dim pad = HANDLE_HIT_PAD / _scale
        For i = 0 To 7
            Dim r = _handleRects(i)
            r.Inflate(pad, pad)
            _handleHitRects(i) = r
        Next
    End Sub


    Protected Overrides Sub OnRender(drawingContext As DrawingContext)
        MyBase.OnRender(drawingContext)

        _styleCache.EnsurePens(_scale)

        Dim bounds = _selectionManager.GetUnrotatedBounds()
        If Not bounds.HasValue Then Return

        Dim dpi = VisualTreeHelper.GetDpi(Me).PixelsPerDip

        Dim rect = bounds.Value
        Dim rotationAngle = GetSelectionRotation()
        Dim hasRotation = Math.Abs(rotationAngle) > 0.01

        If hasRotation Then
            Dim boundsCenter = New Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2)
            drawingContext.PushTransform(New RotateTransform(rotationAngle, boundsCenter.X, boundsCenter.Y))
        End If

        Dim handleSize = HANDLE_SIZE / _scale
        Dim cardinalHandleSize = CARDINAL_HANDLE_SIZE / _scale
        Dim rotateHandleSize = ROTATE_HANDLE_SIZE / _scale
        Dim rotateOffset = ROTATE_HANDLE_OFFSET / _scale

        UpdateHandleRects(rect, handleSize, cardinalHandleSize, rotateHandleSize, rotateOffset)

        ' Draw bounding box
        drawingContext.DrawRectangle(Nothing, _styleCache.BoundsPen, rect)
        drawingContext.DrawRectangle(_styleCache.MoveFillBrush, Nothing, rect)

        ' Draw corner handles
        drawingContext.DrawRectangle(_styleCache.HandleBrush, _styleCache.HandlePen, _handleRects(HandleId.TopLeft))
        drawingContext.DrawRectangle(_styleCache.HandleBrush, _styleCache.HandlePen, _handleRects(HandleId.TopRight))
        drawingContext.DrawRectangle(_styleCache.HandleBrush, _styleCache.HandlePen, _handleRects(HandleId.BottomLeft))
        drawingContext.DrawRectangle(_styleCache.HandleBrush, _styleCache.HandlePen, _handleRects(HandleId.BottomRight))

        ' Draw edge handles
        drawingContext.DrawRectangle(_styleCache.EdgeHandleBrush, _styleCache.EdgePen, _handleRects(HandleId.Top))
        drawingContext.DrawRectangle(_styleCache.EdgeHandleBrush, _styleCache.EdgePen, _handleRects(HandleId.Bottom))
        drawingContext.DrawRectangle(_styleCache.EdgeHandleBrush, _styleCache.EdgePen, _handleRects(HandleId.Left))
        drawingContext.DrawRectangle(_styleCache.EdgeHandleBrush, _styleCache.EdgePen, _handleRects(HandleId.Right))

        ' Draw rotate handle background
        Dim iconCenter = New Point(_rotateHandleRect.Left + _rotateHandleRect.Width / 2, _rotateHandleRect.Top + _rotateHandleRect.Height / 2)
        drawingContext.DrawEllipse(_styleCache.RotateBackBrush, _styleCache.RotatePen, iconCenter, _rotateHandleRect.Width / 2, _rotateHandleRect.Height / 2)

        ' Draw rotate handle icon
        Dim arcGeom = _renderCache.GetArcWithArrowGeometry(_scale)
        drawingContext.PushTransform(New TranslateTransform(iconCenter.X, iconCenter.Y))
        drawingContext.DrawGeometry(Nothing, _styleCache.ArcPen, arcGeom)
        drawingContext.Pop()

        ' Display rotation angle and dimensions text boxes
        RenderRotateVisual(drawingContext, rotateOffset, iconCenter, dpi)
        RenderDimensionsVisual(drawingContext, rect, dpi)

        If hasRotation Then drawingContext.Pop()

    End Sub

    Private Sub RenderRotateVisual(drawingContext As DrawingContext, rotateOffset As Double, iconCenter As Point, dpi As Double)
        ' Display current rotation angle while rotating (single selection only)
        If _activeHandle <> "Rotate" OrElse _selectionManager.Count <> 1 Then Return

        Dim angleText = $"{Math.Round(GetCurrentRotationAngle(), 1):F1}°"
        Dim ft = _renderCache.GetAngleText(angleText, 14 / _scale, dpi)

        Dim textX = iconCenter.X - ft.Width / 2
        Dim textY = _rotateHandleRect.Top - rotateOffset / 2 - ft.Height / 2
        Dim bgRect As New Rect(textX - 4 / _scale, textY - 2 / _scale, ft.Width + 8 / _scale, ft.Height + 4 / _scale)

        drawingContext.DrawRoundedRectangle(_styleCache.DimBgBrush, Nothing, bgRect, 3 / _scale, 3 / _scale)
        drawingContext.DrawText(ft, New Point(textX, textY))

    End Sub

    Private Sub RenderDimensionsVisual(drawingContext As DrawingContext, rect As Rect, dpi As Double)
        ' Display current dimensions while resizing (single selection only)
        If _activeHandle Is Nothing OrElse _activeHandle = "Rotate" OrElse _activeHandle = "Move" OrElse _selectionManager.Count <> 1 Then Return


        Dim dims = GetCurrentDimensions()
        If Not dims.HasValue Then Return

        Dim w = Math.Round(dims.Value.Width, 1)
        Dim h = Math.Round(dims.Value.Height, 1)

        Dim widthFt = _renderCache.GetWidthText($"{w:F1} mm", 12 / _scale, dpi)
        Dim widthX = rect.Left + rect.Width / 2 - widthFt.Width / 2
        Dim widthY = rect.Bottom + 8 / _scale
        Dim widthBgRect As New Rect(widthX - 4 / _scale, widthY - 2 / _scale, widthFt.Width + 8 / _scale, widthFt.Height + 4 / _scale)

        drawingContext.DrawRoundedRectangle(_styleCache.DimBgBrush, Nothing, widthBgRect, 3 / _scale, 3 / _scale)
        drawingContext.DrawText(widthFt, New Point(widthX, widthY))

        Dim heightFt = _renderCache.GetHeightText($"{h:F1} mm", 12 / _scale, dpi)
        Dim heightX = rect.Right + 8 / _scale
        Dim heightY = rect.Top + rect.Height / 2 - heightFt.Height / 2
        Dim heightBgRect As New Rect(heightX - 4 / _scale, heightY - 2 / _scale, heightFt.Width + 8 / _scale, heightFt.Height + 4 / _scale)

        drawingContext.DrawRoundedRectangle(_styleCache.DimBgBrush, Nothing, heightBgRect, 3 / _scale, 3 / _scale)
        drawingContext.DrawText(heightFt, New Point(heightX, heightY))

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

    Private Function GetCurrentRotationAngle() As Double
        ' Get the current rotation angle of the first selected item during rotation
        If _selectionManager.Count > 0 Then
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
        End If
        Return 0
    End Function

    Private Function GetCurrentDimensions() As (Width As Double, Height As Double)?
        ' Get the current dimensions of the first selected item during resize
        If _selectionManager.Count > 0 Then
            Dim item = _selectionManager.SelectedItems.FirstOrDefault()
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    ' Return dimensions in millimeters (ActualWidth/Height are in WPF units, 1 unit = 1mm)
                    Return (wrapper.ActualWidth, wrapper.ActualHeight)
                End If
            End If
        End If
        Return Nothing
    End Function

    Private Sub OnMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim pos = e.GetPosition(Me)
        Dim hit = HitTestGizmo(pos)
        If hit Is Nothing Then Return
        Dim bounds = _selectionManager.GetUnrotatedBounds()

        Dim timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds
        Dim ddx = pos.X - _lastClickPosition.X
        Dim ddy = pos.Y - _lastClickPosition.Y
        Dim dist2 = ddx * ddx + ddy * ddy
        Dim maxDist2 = DOUBLE_CLICK_DISTANCE * DOUBLE_CLICK_DISTANCE

        If timeSinceLastClick < DOUBLE_CLICK_TIME_MS AndAlso dist2 < maxDist2 Then
            HandleDoubleClick(pos, bounds.Value)
            _lastClickTime = DateTime.MinValue
            e.Handled = True
            Return
        End If
        _lastClickTime = DateTime.Now
        _lastClickPosition = pos



        Select Case hit
            Case "Rotate"
                StartRotate(e.GetPosition(Me))

            Case "Move"
                StartMove(e.GetPosition(Me))

            Case Else
                StartResize(hit, e.GetPosition(Me))
        End Select

        e.Handled = True
    End Sub


    Private Shared Function InverseRotatePoint(p As Point, center As Point, angleDeg As Double) As Point
        If Math.Abs(angleDeg) <= 0.01 Then Return p

        Dim angleRad = -angleDeg * Math.PI / 180.0
        Dim dx = p.X - center.X
        Dim dy = p.Y - center.Y
        Dim cosA = Math.Cos(angleRad)
        Dim sinA = Math.Sin(angleRad)

        Return New Point(center.X + (dx * cosA - dy * sinA),
                     center.Y + (dx * sinA + dy * cosA))
    End Function

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
            _multiBoundsDirty = True
            RequestGizmoRefresh() ' will invalidate bounds once per frame
        End If

        InvalidateVisual()
    End Sub

    Private Sub OnMouseUp(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim msg As New TransformCompletedMessage()
            For Each kvp In _initialSnapshots
                Dim item = kvp.Key
                If item?.DrawableElement IsNot Nothing Then
                    Dim beforeSnap = kvp.Value
                    Dim wrapperNow = TryCast(item.DrawableElement.Parent, ContentControl)
                    Dim afterSnap As TransformAction.Snapshot = Nothing
                    If wrapperNow IsNot Nothing Then
                        afterSnap = TransformAction.MakeSnapshotFromWrapper(wrapperNow)
                    End If

                    If beforeSnap IsNot Nothing AndAlso afterSnap IsNot Nothing Then
                        msg.Items.Add((item, CType(beforeSnap, Object), CType(afterSnap, Object)))
                    End If
                End If
            Next

            If msg.Items.Count > 0 Then
                EventAggregator.Publish(Of TransformCompletedMessage)(msg)
            End If
        Catch ex As Exception
            Debug.WriteLine($"TransformGizmo publish failed: {ex.Message}")
        End Try

        _activeHandle = Nothing

        _initialTransforms.Clear()
        _initialSizes.Clear()
        _initialPositions.Clear()
        _initialSnapshots.Clear()
        _cumulativeChangeX = 0
        _cumulativeChangeY = 0
        Me.ReleaseMouseCapture()

        _selectionManager.InvalidateBoundsCache()
        InvalidateVisual()
    End Sub

    Private Function HitTestHandle(pos As Point) As HandleId?
        ' Corners first
        If _handleHitRects(HandleId.TopLeft).Contains(pos) Then Return HandleId.TopLeft
        If _handleHitRects(HandleId.TopRight).Contains(pos) Then Return HandleId.TopRight
        If _handleHitRects(HandleId.BottomLeft).Contains(pos) Then Return HandleId.BottomLeft
        If _handleHitRects(HandleId.BottomRight).Contains(pos) Then Return HandleId.BottomRight

        ' Edges
        If _handleHitRects(HandleId.Top).Contains(pos) Then Return HandleId.Top
        If _handleHitRects(HandleId.Bottom).Contains(pos) Then Return HandleId.Bottom
        If _handleHitRects(HandleId.Left).Contains(pos) Then Return HandleId.Left
        If _handleRects(HandleId.Right).Contains(pos) Then Return HandleId.Right

        Return Nothing
    End Function

    Private Shared Function HandleIdToName(id As HandleId) As String
        Select Case id
            Case HandleId.TopLeft : Return "TopLeft"
            Case HandleId.Top : Return "Top"
            Case HandleId.TopRight : Return "TopRight"
            Case HandleId.Right : Return "Right"
            Case HandleId.BottomRight : Return "BottomRight"
            Case HandleId.Bottom : Return "Bottom"
            Case HandleId.BottomLeft : Return "BottomLeft"
            Case HandleId.Left : Return "Left"
            Case Else : Return Nothing
        End Select
    End Function


    Private Sub UpdateCursor(pos As Point)
        Dim hit = HitTestGizmo(pos)

        If hit Is Nothing Then
            Me.Cursor = Cursors.Arrow
            Return
        End If

        If hit = "Rotate" Then
            Me.Cursor = Cursors.Hand
            Return
        End If

        If hit = "Move" Then
            Me.Cursor = Cursors.SizeAll
            Return
        End If

        ' Rotation-aware resize cursors
        Dim angle = GetSelectionRotation()
        Me.Cursor = GetRotatedResizeCursor(hit, angle)
    End Sub

    Private Shared Function GetRotatedResizeCursor(handleName As String, rotationAngleDeg As Double) As Cursor

        Dim base As Integer
        Select Case handleName
            Case "Top", "Bottom"
                base = 0
            Case "TopRight", "BottomLeft"
                base = 1
            Case "Left", "Right"
                base = 2
            Case "TopLeft", "BottomRight"
                base = 3
            Case Else
                Return Cursors.Arrow
        End Select

        ' Normalize angle to [0,360)
        Dim a = rotationAngleDeg Mod 360.0
        If a < 0 Then a += 360.0

        ' Quantize to nearest 45 degrees (0..7 steps)
        Dim step45 As Integer = CInt(Math.Round(a / 45.0)) Mod 8

        Dim rotatedType As Integer = (base + step45) Mod 4

        Select Case rotatedType
            Case 0 : Return Cursors.SizeNS
            Case 1 : Return Cursors.SizeNESW
            Case 2 : Return Cursors.SizeWE
            Case 3 : Return Cursors.SizeNWSE
            Case Else : Return Cursors.Arrow
        End Select
    End Function


    Private Function HitTestGizmo(pos As Point) As String
        Dim bounds = _selectionManager.GetUnrotatedBounds()
        If Not bounds.HasValue Then Return Nothing

        ' ----- inverse rotate mouse into gizmo space -----
        Dim rotationAngle = GetSelectionRotation()
        If Math.Abs(rotationAngle) > 0.01 Then
            Dim b = bounds.Value
            Dim centerPt = New Point(b.Left + b.Width / 2, b.Top + b.Height / 2)
            pos = InverseRotatePoint(pos, centerPt, rotationAngle)
        End If

        ' ----- rotate handle first (top priority) -----
        If _rotateHandleRect.Contains(pos) Then Return "Rotate"


        ' ----- resize handles -----
        Dim h = HitTestHandle(pos)
        If h.HasValue Then Return HandleIdToName(h.Value)


        ' ----- move area (inflated bounds) -----
        Dim hitBounds = bounds.Value
        hitBounds.Inflate(5, 5)
        If hitBounds.Contains(pos) Then Return "Move"

        Return Nothing
    End Function



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
    Private _initialSnapshots As New Dictionary(Of IDrawable, TransformAction.Snapshot)
    Private Sub CaptureInitialTransforms()
        _initialTransforms.Clear()
        _initialSnapshots.Clear() ' <-- capture snapshots at the start
        For Each item In _selectionManager.SelectedItems
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    _initialTransforms(item) = TransformState.FromElement(wrapper)

                    Try
                        Dim snap = TransformAction.MakeSnapshotFromWrapper(wrapper)
                        If snap IsNot Nothing Then
                            _initialSnapshots(item) = snap
                        End If
                    Catch

                    End Try
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
                    TransformAction.ApplyRotation(wrapper, center, initialState.Rotation, angle, initialPos)
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
                    TransformAction.ApplyMove(wrapper, delta.X, delta.Y)
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
        TransformAction.ApplyResizeSingle(wrapper, _activeHandle, deltaX, deltaY)
    End Sub

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

                    TransformAction.ApplyResizeMulti(wrapper, scaleX, scaleY, anchorX, anchorY, initialSize, initialPos, rotation)
                End If
            End If
        Next
    End Sub

    Public Shared Function HandleTextBoxSizeChanged(wrapper As ContentControl, e As SizeChangedEventArgs) As Boolean
        Return TransformAction.HandleTextBoxSizeChanged(wrapper, e)
    End Function

End Class


Friend NotInheritable Class BrushCache

    ' ---- Frozen Brushes ----'
    Public ReadOnly MoveFillBrush As Brush = Freeze(New SolidColorBrush(Color.FromArgb(&H10, &H0, &H0, &HFF)))
    Public ReadOnly HandleBrush As Brush = Freeze(New SolidColorBrush(Colors.White))
    Public ReadOnly EdgeHandleBrush As Brush = Freeze(New SolidColorBrush(Color.FromArgb(&HFF, &HA0, &HA0, &HA0)))
    Public ReadOnly RotateBackBrush As Brush = Freeze(New SolidColorBrush(Color.FromArgb(&H40, &H30, &H66, &HCC)))
    Public ReadOnly RotateStrokeBrush As Brush = Freeze(New SolidColorBrush(Color.FromRgb(&H30, &H66, &HCC)))
    Public ReadOnly IconBrush As Brush = Freeze(New SolidColorBrush(Color.FromRgb(&H40, &HA0, &HE0)))
    Public ReadOnly DimBgBrush As Brush = Freeze(New SolidColorBrush(Color.FromArgb(&HC0, &H20, &H20, &H20)))

    ' ---- Scale-dependent Pens ----'
    Private _scale As Double = Double.NaN

    Public BoundsPen As Pen
    Public HandlePen As Pen
    Public EdgePen As Pen
    Public RotatePen As Pen
    Public ArcPen As Pen


    Public Sub EnsurePens(scale As Double)
        If scale = _scale Then Return
        _scale = scale

        Dim t = 1.0 / _scale

        BoundsPen = Freeze(New Pen(Brushes.Gray, t) With {.DashStyle = DashStyles.Dash})
        HandlePen = Freeze(New Pen(Brushes.Black, t))
        EdgePen = Freeze(New Pen(Brushes.White, t))
        RotatePen = Freeze(New Pen(RotateStrokeBrush, t))
        ArcPen = Freeze(New Pen(IconBrush, t * 2.5) With {.StartLineCap = PenLineCap.Round, .EndLineCap = PenLineCap.Triangle})

    End Sub

    Private Shared Function Freeze(Of T As Freezable)(obj As T) As T
        If obj.CanFreeze Then obj.Freeze()
        Return obj
    End Function

End Class

Friend NotInheritable Class RenderCache

    ' ---------- Arc + Arrow cache (scale-dependent) ----------
    Private _scale As Double = Double.NaN
    Private _arcRadius As Double
    Private _arcStartAngle As Double = Math.PI / 4
    Private _arcSweepAngle As Double = (3 * Math.PI / 2)
    Private _arcWithArrowGeom As StreamGeometry

    Public Function GetArcWithArrowGeometry(scale As Double) As StreamGeometry
        EnsureArc(scale)
        Return _arcWithArrowGeom
    End Function

    Private Sub EnsureArc(scale As Double)

        scale = Math.Round(scale, 3) 'round to avoid tiny changes causing regen

        If _arcWithArrowGeom IsNot Nothing AndAlso _scale = scale Then Return
        _scale = scale

        Dim iconSize = 20 / scale
        _arcRadius = iconSize / 2.5

        Dim r = _arcRadius
        Dim startAngle = _arcStartAngle
        Dim endAngle = startAngle + _arcSweepAngle

        Dim startPt = New Point(r * Math.Cos(startAngle), r * Math.Sin(startAngle))
        Dim endPt = New Point(r * Math.Cos(endAngle), r * Math.Sin(endAngle))

        ' Arrow head baked in 
        Dim arrowSize = 4 / scale
        Dim arrowAngle = endAngle + Math.PI / 2

        Dim leftPt = New Point(
        endPt.X - arrowSize * Math.Cos(arrowAngle - 0.7),
        endPt.Y - arrowSize * Math.Sin(arrowAngle - 0.7))

        Dim rightPt = New Point(
        endPt.X - arrowSize * Math.Cos(arrowAngle + 0.3),
        endPt.Y - arrowSize * Math.Sin(arrowAngle + 0.3))

        Dim g As New StreamGeometry()
        Using ctx = g.Open()
            ' Arc
            ctx.BeginFigure(startPt, isFilled:=False, isClosed:=False)
            ctx.ArcTo(endPt, New Size(r, r), 0, True, SweepDirection.Clockwise, True, False)

            ' Arrow head (two short strokes)
            ctx.BeginFigure(endPt, isFilled:=False, isClosed:=False)
            ctx.LineTo(leftPt, isStroked:=True, isSmoothJoin:=False)

            ctx.BeginFigure(endPt, isFilled:=False, isClosed:=False)
            ctx.LineTo(rightPt, isStroked:=True, isSmoothJoin:=False)
        End Using
        g.Freeze()

        _arcWithArrowGeom = g
    End Sub


    ' ---------- DPI-aware text cache ----------
    Private _dpi As Double = Double.NaN
    Private Shared ReadOnly _typeface As New Typeface("Segoe UI")

    Private _angleKey As String
    Private _angleFt As FormattedText

    Private _widthKey As String
    Private _widthFt As FormattedText

    Private _heightKey As String
    Private _heightFt As FormattedText

    Public Sub EnsureDpi(dpi As Double)
        If dpi = _dpi Then Return
        _dpi = dpi

        ' invalidate cached FormattedText
        _angleKey = Nothing : _angleFt = Nothing
        _widthKey = Nothing : _widthFt = Nothing
        _heightKey = Nothing : _heightFt = Nothing
    End Sub

    Private Function GetOrCreate(ByRef key As String, ByRef ft As FormattedText,
                                text As String, fontSize As Double) As FormattedText
        Dim newKey = text & "|" & fontSize.ToString("R", Globalization.CultureInfo.InvariantCulture)

        If ft IsNot Nothing AndAlso key = newKey Then Return ft

        key = newKey
        ft = New FormattedText(
            text,
            Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            fontSize,
            Brushes.White,
            _dpi)

        Return ft
    End Function

    Public Function GetAngleText(text As String, fontSize As Double, dpi As Double) As FormattedText
        EnsureDpi(dpi)
        Return GetOrCreate(_angleKey, _angleFt, text, fontSize)
    End Function

    Public Function GetWidthText(text As String, fontSize As Double, dpi As Double) As FormattedText
        EnsureDpi(dpi)
        Return GetOrCreate(_widthKey, _widthFt, text, fontSize)
    End Function

    Public Function GetHeightText(text As String, fontSize As Double, dpi As Double) As FormattedText
        EnsureDpi(dpi)
        Return GetOrCreate(_heightKey, _heightFt, text, fontSize)
    End Function

End Class


Friend Enum HandleId
    TopLeft = 0
    Top = 1
    TopRight = 2
    Right = 3
    BottomRight = 4
    Bottom = 5
    BottomLeft = 6
    Left = 7
End Enum


Imports System.Runtime.CompilerServices
Imports System.Windows.Media.Animation

Imports PolyCut.Shared

Public Enum ZoomBorderMouseAction
    None
    Move
    Reset
End Enum


''' <summary>
''' ZoomBorder control modified from https://github.com/spicermicer/ZoomBorder
''' </summary>

Public Class ZoomBorder
    Inherits Border

    Private _pendingScale As Double?
    Private _pendingZoomPoint As Point
    Private _pendingTranslation As Point?
    Private ReadOnly _viewportLimiter As InteractionRateLimiter
    Private _applyingViewport As Boolean

    Public Shared ReadOnly LeftButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(LeftButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.Move, Nothing))

    Public Property LeftButtonAction As ZoomBorderMouseAction
        Get
            Return GetValue(LeftButtonActionProperty)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(LeftButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly RightButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(RightButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.Reset, Nothing))

    Public Property RightButtonAction As ZoomBorderMouseAction
        Get
            Return GetValue(RightButtonActionProperty)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(RightButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly MiddleButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(MiddleButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.None, Nothing))

    Public Property MiddleButtonAction As ZoomBorderMouseAction
        Get
            Return GetValue(MiddleButtonActionProperty)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(MiddleButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleMaxProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleMax), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(10.0, Nothing))

    Public Property ScaleMax As Double
        Get
            Return GetValue(ScaleMaxProperty)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleMaxProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleMinProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleMin), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(0.8, Nothing))

    Public Property ScaleMin As Double
        Get
            Return GetValue(ScaleMinProperty)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleMinProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleAmountProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleAmount), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(0.005, Nothing))

    Public Property ScaleAmount As Double
        Get
            Return GetValue(ScaleAmountProperty)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleAmountProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ZoomEnabledProperty As DependencyProperty = DependencyProperty.Register(NameOf(ZoomEnabled), GetType(Boolean), GetType(ZoomBorder), New PropertyMetadata(True, Nothing))

    Public Property ZoomEnabled As Boolean
        Get
            Return GetValue(ZoomEnabledProperty)
        End Get
        Set(ByVal value As Boolean)
            SetValue(ZoomEnabledProperty, value)
        End Set
    End Property

    Public Shared ReadOnly PanEnabledProperty As DependencyProperty = DependencyProperty.Register(NameOf(PanEnabled), GetType(Boolean), GetType(ZoomBorder), New PropertyMetadata(True, Nothing))

    Public Property PanEnabled As Boolean
        Get
            Return GetValue(PanEnabledProperty)
        End Get
        Set(ByVal value As Boolean)
            SetValue(PanEnabledProperty, value)
        End Set
    End Property


    Public Shared ReadOnly ScaleProperty As DependencyProperty = DependencyProperty.Register(NameOf(Scale), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(1.0, AddressOf OnScalePropertyChanged))

    Public Property Scale As Double
        Get
            Return GetValue(ScaleProperty)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleProperty, value)
        End Set
    End Property

    Private Shared Sub OnScalePropertyChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = TryCast(d, ZoomBorder)
        If control IsNot Nothing AndAlso control.ScaleTransform IsNot Nothing Then
            control.ScaleTransform.ScaleX = e.NewValue
            control.ScaleTransform.ScaleY = e.NewValue
            If Not control._applyingViewport Then
                control.NotifyViewportChanged(CDbl(e.OldValue), New Point(control.TranslateTransform.X, control.TranslateTransform.Y))
            End If
        End If

    End Sub


    Public Shared ReadOnly ScaleChangedEvent As RoutedEvent = EventManager.RegisterRoutedEvent("ScaleChanged", RoutingStrategy.Bubble, GetType(RoutedPropertyChangedEventHandler(Of Double)), GetType(ZoomBorder))

    Public Custom Event ScaleChanged As RoutedPropertyChangedEventHandler(Of Double)
        AddHandler(value As RoutedPropertyChangedEventHandler(Of Double))
            [AddHandler](ScaleChangedEvent, value)
        End AddHandler
        RemoveHandler(value As RoutedPropertyChangedEventHandler(Of Double))
            [RemoveHandler](ScaleChangedEvent, value)
        End RemoveHandler
        RaiseEvent(sender As Object, e As RoutedEventArgs)
        End RaiseEvent
    End Event

    Protected Overridable Sub RaiseScaleChangedEvent(scale As Double)
        [RaiseEvent](New RoutedPropertyChangedEventArgs(Of Double)(GetValue(ScaleProperty), scale, ScaleChangedEvent))
    End Sub

    Public Shared ReadOnly CanvasModeProperty As DependencyProperty = DependencyProperty.Register(NameOf(CanvasMode), GetType(CanvasMode), GetType(ZoomBorder), New PropertyMetadata(CanvasMode.Selection, AddressOf OnCanvasModeChanged))

    Private Shared Sub OnCanvasModeChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        DirectCast(d, ZoomBorder).CancelMarquee()
    End Sub

    Public Property CanvasMode As CanvasMode
        Get
            Return CType(GetValue(CanvasModeProperty), CanvasMode)
        End Get
        Set(value As CanvasMode)
            SetValue(CanvasModeProperty, value)
        End Set
    End Property

    Public Shared ReadOnly CanvasTextBoxProperty As DependencyProperty = DependencyProperty.Register(NameOf(CanvasTextBox), GetType(TextBox), GetType(ZoomBorder), New PropertyMetadata(Nothing))
    Public Property CanvasTextBox As TextBox
        Get
            Return CType(GetValue(CanvasTextBoxProperty), TextBox)
        End Get
        Set(value As TextBox)
            SetValue(CanvasTextBoxProperty, value)
        End Set
    End Property


    Private origin As Point
    Private start As Point

    Private _rasteriserCache As PerformantRasteriser
    Private ReadOnly _frameMeter As New FrameMeter()


    Public Sub New()
        ClipToBounds = True
        Focusable = True
        Me.FocusVisualStyle = Nothing
        _viewportLimiter = New InteractionRateLimiter(AddressOf ApplyPendingViewport, Me)
        AddHandler Me.MouseWheel, AddressOf ZoomBorder_MouseWheel
        AddHandler Me.MouseDown, AddressOf ZoomBorder_MouseDown
        AddHandler Me.MouseUp, AddressOf ZoomBorder_MouseUp
        AddHandler Me.MouseMove, AddressOf ZoomBorder_MouseMove
        AddHandler Me.Loaded, AddressOf ZoomBorder_Loaded
        AddHandler Me.PreviewMouseDown, AddressOf ZoomBorder_PreviewMouseDown
        AddHandler Me.LostMouseCapture, AddressOf ZoomBorder_LostMouseCapture
        AddHandler Me.PreviewKeyDown, AddressOf ZoomBorder_PreviewKeyDown
        AddHandler Me.Unloaded, Sub() CancelMarquee(restoreSelection:=False)
    End Sub


    Public ReadOnly Property TranslateTransform As TranslateTransform
        Get
            Return TryCast(Child.RenderTransform, TransformGroup).Children.First(Function(tr) TypeOf tr Is TranslateTransform)
        End Get
    End Property

    Public ReadOnly Property ScaleTransform As ScaleTransform
        Get
            Return TryCast(Child.RenderTransform, TransformGroup).Children.First(Function(tr) TypeOf tr Is ScaleTransform)
        End Get
    End Property

    Public Overrides Property Child As UIElement
        Get
            Return MyBase.Child
        End Get
        Set(ByVal value As UIElement)
            If value IsNot Nothing AndAlso value IsNot Me.Child Then Me.Initialize(value)
            MyBase.Child = value
        End Set
    End Property

    Public Sub Initialize(ByVal element As UIElement)
        If element Is Nothing Then Return
        Dim group = New TransformGroup()
        group.Children.Add(New ScaleTransform())
        group.Children.Add(New TranslateTransform())
        element.RenderTransform = group
        element.RenderTransformOrigin = New Point(0.0, 0.0)

        MyBase.Child = element
    End Sub

    Private Function GetAction(ByVal button As MouseButton) As ZoomBorderMouseAction
        Select Case button
            Case MouseButton.Left
                Return LeftButtonAction
            Case MouseButton.Right
                Return RightButtonAction
            Case MouseButton.Middle
                Return MiddleButtonAction
            Case Else
                Return ZoomBorderMouseAction.None
        End Select
    End Function

    Public Async Sub Reset(Optional skipAnimation As Boolean = False)
        If Child Is Nothing Then Return
        _viewportLimiter.Flush()

        Me.UpdateLayout()

        Dim fit = TryCast(FindElementByNameInChild("mainCanvas"), FrameworkElement)
        If fit Is Nothing Then fit = TryCast(GetPolyCanvas(), FrameworkElement)
        If fit Is Nothing Then Return

        Dim w = fit.ActualWidth
        Dim h = fit.ActualHeight
        Dim vw = ActualWidth - Padding.Left - Padding.Right
        Dim vh = ActualHeight - Padding.Top - Padding.Bottom
        If w <= 0 OrElse h <= 0 OrElse vw <= 0 OrElse vh <= 0 Then Return

        Const margin As Double = 60.0
        Dim targetScale = Math.Max(ScaleMin, Math.Min(ScaleMax, Math.Min((vw - 2 * margin) / w, (vh - 2 * margin) / h)))

        Dim s0 = Scale
        Dim x0 = TranslateTransform.X
        Dim y0 = TranslateTransform.Y

        ' Measure the fitted bounds without broadcasting the temporary scale changes.
        Dim b As Rect
        _applyingViewport = True
        Try
            Scale = targetScale
            b = fit.TransformToAncestor(Me).TransformBounds(New Rect(0, 0, w, h))
        Finally
            Scale = s0
            _applyingViewport = False
        End Try
        Dim targetX = TranslateTransform.X + (Padding.Left + ((vw - b.Width) / 2.0) - b.Left)
        Dim targetY = TranslateTransform.Y + (Padding.Top + ((vh - b.Height) / 2.0) - b.Top)

        If Not skipAnimation Then
            Const durationMs As Integer = 200
            Const frames As Integer = 28
            Dim delayMs = Math.Max(1, durationMs \ frames)

            For i As Integer = 1 To frames
                Dim t = i / CDbl(frames)
                t = t * t * (3 - 2 * t) ' smoothstep easing
                ApplyViewport(s0 + ((targetScale - s0) * t), New Point(x0 + ((targetX - x0) * t), y0 + ((targetY - y0) * t)))
                Await Task.Delay(delayMs)
            Next

        End If


        ApplyViewport(targetScale, New Point(targetX, targetY))
    End Sub

    Private Function FindElementByNameInChild(elementName As String) As FrameworkElement
        If Child Is Nothing Then Return Nothing
        Return FindElementByNameRecursive(TryCast(Child, DependencyObject), elementName)
    End Function

    Private Function FindElementByNameRecursive(root As DependencyObject, elementName As String) As FrameworkElement
        If root Is Nothing Then Return Nothing

        Dim fe As FrameworkElement = TryCast(root, FrameworkElement)
        If fe IsNot Nothing AndAlso String.Equals(fe.Name, elementName, StringComparison.Ordinal) Then
            Return fe
        End If

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(root) - 1
            Dim found = FindElementByNameRecursive(VisualTreeHelper.GetChild(root, i), elementName)
            If found IsNot Nothing Then Return found
        Next

        Return Nothing
    End Function

    Private Sub MoveDown(ByVal e As MouseButtonEventArgs)
        If Not PanEnabled OrElse Child Is Nothing Then Return
        _viewportLimiter.Flush()
        start = e.GetPosition(Me)
        origin = New Point(TranslateTransform.X, TranslateTransform.Y)
        Me.Cursor = Cursors.ScrollAll
        Child.CaptureMouse()
    End Sub

    Public Sub BeginPan(e As MouseButtonEventArgs)
        If Not PanEnabled OrElse Child Is Nothing Then Return
        _viewportLimiter.Flush()
        start = e.GetPosition(Me)
        _middleMouseDownPosStart = start
        origin = New Point(TranslateTransform.X, TranslateTransform.Y)
        Me.Cursor = Cursors.ScrollAll
        Child.CaptureMouse()
    End Sub

    Public Sub UpdatePanFrom(currentPosition As Point)
        If Not ZoomEnabled OrElse Child Is Nothing OrElse Not Child.IsMouseCaptured Then Return
        _pendingTranslation = New Point(origin.X - (start.X - currentPosition.X), origin.Y - (start.Y - currentPosition.Y))
        _viewportLimiter.Request()
    End Sub

    Public Sub EndPan(e As MouseButtonEventArgs)
        If e.ChangedButton = MouseButton.Middle AndAlso DistanceTo(e.GetPosition(Me), _middleMouseDownPosStart) < 3 Then
            Reset()
        End If
        MoveUp()
    End Sub

    Private Sub MoveUp()
        If Child Is Nothing Then Return
        _viewportLimiter.Flush()
        Child.ReleaseMouseCapture()
        Me.Cursor = Nothing
    End Sub

    Public DrawingManager As New DrawingManager
    Private _selectionMarquee As SelectionMarquee

    Private Function StartMarquee(e As MouseButtonEventArgs) As Boolean
        If CanvasMode <> CanvasMode.Selection OrElse e.ChangedButton <> MouseButton.Left OrElse
           GetAction(MouseButton.Left) <> ZoomBorderMouseAction.None Then Return False
        Dim canvas = GetPolyCanvas()
        If canvas Is Nothing OrElse canvas.IsClickOnCanvasChild(TryCast(e.OriginalSource, DependencyObject)) Then Return False
        Dim marquee = canvas.GetSelectionMarquee()
        If marquee Is Nothing Then Return False
        DrawingManager.TextEditor.CommitActiveText(canvas)
        Dim start = e.GetPosition(marquee)
        If Not CaptureMouse() Then Return False
        _selectionMarquee = marquee
        marquee.Begin(start, Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift))
        Focus()
        e.Handled = True
        Return True
    End Function

    Private Sub CancelMarquee(Optional restoreSelection As Boolean = True)
        Dim marquee = _selectionMarquee
        If marquee Is Nothing Then Return
        _selectionMarquee = Nothing
        marquee.Cancel(restoreSelection)
        If IsMouseCaptured Then ReleaseMouseCapture()
    End Sub

    Private Sub ZoomBorder_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key <> Key.Escape OrElse _selectionMarquee Is Nothing Then Return
        CancelMarquee()
        e.Handled = True
    End Sub

    Private Sub ZoomBorder_MouseDown(ByVal sender As Object, ByVal e As MouseButtonEventArgs)
        HandleMouseDown(e, isPreview:=False)
    End Sub

    Private Sub ZoomBorder_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        HandleMouseDown(e, isPreview:=True)
    End Sub

    Private _middleMouseDownPosStart As Point

    Private Sub HandleMouseDown(e As MouseButtonEventArgs, isPreview As Boolean)
        If DrawingManager.TextEditor.HandleTextMouseDown(CanvasMode, e) Then Return

        If _selectionMarquee IsNot Nothing AndAlso e.ChangedButton <> MouseButton.Left Then
            CancelMarquee()
            If e.ChangedButton = MouseButton.Right Then
                e.Handled = True
                Return
            End If
        End If
        If isPreview AndAlso StartMarquee(e) Then Return

        If e.ChangedButton = MouseButton.Middle Then
            _middleMouseDownPosStart = e.GetPosition(Me)
        End If

        If CanvasMode <> CanvasMode.Selection AndAlso e.ChangedButton = MouseButton.Left Then
            Dim _polyCanvas = GetPolyCanvas()
            Dim position As Point = e.GetPosition(_polyCanvas)
            DrawingManager.StartDrawing(CanvasMode, position, _polyCanvas)
            Me.CaptureMouse()
            If isPreview Then e.Handled = True
        ElseIf Not isPreview AndAlso (e.OriginalSource Is Me OrElse e.OriginalSource Is Me.Background) Then
            Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
            If Not isShiftPressed Then
                Dim _polyCanvas = GetPolyCanvas()
                _polyCanvas?.SelectionManager.ClearSelection()
            End If
            e.Handled = True
        End If

        If e.ChangedButton = MouseButton.Right AndAlso isPreview Then

            HandleCanvasRightClick(e)
        End If

        If GetAction(e.ChangedButton) = ZoomBorderMouseAction.Move Then MoveDown(e)
    End Sub

    Private Sub HandleCanvasRightClick(e As MouseButtonEventArgs)
        ' A text box being edited owns its own context menu.
        If DrawingManager.TextEditor.ActiveTextBox IsNot Nothing Then Return

        Dim polyCanvas = GetPolyCanvas()
        If polyCanvas Is Nothing Then Return

        Dim drawable = GeometryHitTestHelper.HitTestTopmost(polyCanvas.ChildrenCollection, e.GetPosition(polyCanvas))
        If drawable Is Nothing OrElse polyCanvas.SelectionManager.SelectedItems.Contains(drawable) Then Return

        polyCanvas.SelectionManager.SelectItem(drawable, multiSelect:=False)
    End Sub

    Private Sub ZoomBorder_MouseUp(ByVal sender As Object, ByVal e As MouseButtonEventArgs)

        If _selectionMarquee IsNot Nothing AndAlso e.ChangedButton = MouseButton.Left Then
            Dim marquee = _selectionMarquee
            _selectionMarquee = Nothing
            Try
                marquee.Complete(e.GetPosition(marquee))
            Finally
                If IsMouseCaptured Then ReleaseMouseCapture()
            End Try
            e.Handled = True
            Return
        End If

        If CanvasMode <> CanvasMode.Selection AndAlso e.ChangedButton = MouseButton.Left Then
            Dim polyCanvas = GetPolyCanvas()
            DrawingManager.FinishDrawing(CanvasMode, polyCanvas, CanvasTextBox)
            Me.ReleaseMouseCapture()
            Return
        End If

        If e.ChangedButton = MouseButton.Middle AndAlso DistanceTo(e.GetPosition(Me), _middleMouseDownPosStart) < 3 Then
            Reset()
        End If


        If GetAction(e.ChangedButton) = ZoomBorderMouseAction.Move Then : MoveUp()
        ElseIf GetAction(e.ChangedButton) = ZoomBorderMouseAction.Reset Then : Reset()
        End If
    End Sub

    Private Function DistanceTo(p1 As Point, p2 As Point) As Double
        Return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2))
    End Function

    Private Sub ZoomBorder_MouseWheel(ByVal sender As Object, ByVal e As MouseWheelEventArgs)
        If _selectionMarquee IsNot Nothing Then
            e.Handled = True
            Return
        End If
        ZoomByWheel(e.Delta)
        e.Handled = False
    End Sub

    'break out from above handler so we can call it from transformgizmo (Probably cursed)
    Public Sub ZoomByWheel(delta As Integer)
        If Child Is Nothing Then Return

        Dim zoomFactor As Double = delta * ScaleAmount
        Dim currentScale = If(_pendingScale.HasValue, _pendingScale.Value, Scale)
        Dim targetScale As Double = currentScale + (zoomFactor * currentScale)

        ' Early return if zooming out too much
        If delta <= 0 AndAlso (ScaleTransform.ScaleX < ScaleMin OrElse ScaleTransform.ScaleY < ScaleMin) Then Return

        ZoomToPoint(targetScale)
    End Sub

    Public Sub ZoomByLinear(delta As Double)
        If Child Is Nothing Then Return
        ZoomToPoint(If(_pendingScale.HasValue, _pendingScale.Value, Scale) + delta)
    End Sub

    Private Sub ZoomToPoint(targetScale As Double)
        If Child Is Nothing Then Return

        _pendingScale = Math.Max(ScaleMin, Math.Min(ScaleMax, targetScale))
        _pendingZoomPoint = Mouse.GetPosition(Me)
        _viewportLimiter.Request()
    End Sub

    Private Sub ApplyPendingViewport()
        If Child Is Nothing Then
            _pendingScale = Nothing
            _pendingTranslation = Nothing
            Return
        End If
        If Not _pendingScale.HasValue AndAlso Not _pendingTranslation.HasValue Then Return

        ApplyViewport(If(_pendingScale, Scale), If(_pendingTranslation, New Point(TranslateTransform.X, TranslateTransform.Y)),
                      If(_pendingScale.HasValue, CType(_pendingZoomPoint, Point?), Nothing))
        If _pendingScale.HasValue AndAlso Child.IsMouseCaptured Then
            start = Mouse.GetPosition(Me)
            origin = New Point(TranslateTransform.X, TranslateTransform.Y)
        End If
        _pendingScale = Nothing
        _pendingTranslation = Nothing
        _frameMeter.Pulse()
    End Sub

    Private Sub ApplyViewport(scale As Double, translation As Point, Optional zoomPoint As Point? = Nothing)
        Dim oldScale = Me.Scale
        Dim transform = TranslateTransform
        Dim oldTranslation As New Point(transform.X, transform.Y)
        If scale = oldScale AndAlso translation = oldTranslation Then Return
        _applyingViewport = True
        Try
            If zoomPoint.HasValue AndAlso scale <> oldScale Then
                transform.X = translation.X
                transform.Y = translation.Y
                Dim relative = Me.TranslatePoint(zoomPoint.Value, Child)
                translation = New Point(relative.X * oldScale + translation.X - relative.X * scale,
                                        relative.Y * oldScale + translation.Y - relative.Y * scale)
            End If
            Me.Scale = scale
            transform.X = translation.X
            transform.Y = translation.Y
        Finally
            _applyingViewport = False
        End Try
        NotifyViewportChanged(oldScale, oldTranslation)
    End Sub

    Private Sub NotifyViewportChanged(oldScale As Double, oldTranslation As Point)
        Dim zoomChanged = Scale <> oldScale
        Dim transform = TranslateTransform
        Dim translation As New Point(transform.X, transform.Y)
        Dim translationChanged = translation <> oldTranslation
        If zoomChanged Then
            RaiseScaleChangedEvent(Scale)
            EventAggregator.Publish(New ScaleChangedMessage(Scale))
        End If
        If translationChanged Then EventAggregator.Publish(New TranslationChangedMessage(translation))
        If zoomChanged OrElse translationChanged Then
            UpdateRasteriser(zoomChanged)
        End If
    End Sub

    Private Sub ZoomBorder_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If _selectionMarquee IsNot Nothing Then
            If e.LeftButton = MouseButtonState.Pressed Then
                _selectionMarquee.Update(e.GetPosition(_selectionMarquee))
            Else
                CancelMarquee()
            End If
            e.Handled = True
            Return
        End If
        Dim currentPosition As Point = e.GetPosition(Me)
        If CanvasMode <> CanvasMode.Selection AndAlso e.LeftButton = MouseButtonState.Pressed Then
            Dim polyCanvas = GetPolyCanvas()
            Dim position As Point = e.GetPosition(polyCanvas)
            DrawingManager.UpdateDrawing(CanvasMode, position, Keyboard.IsKeyDown(Key.LeftShift), Keyboard.IsKeyDown(Key.LeftCtrl))
            Return
        End If

        If Not ZoomEnabled OrElse Child Is Nothing OrElse Not Child.IsMouseCaptured Then Return
        UpdatePanFrom(currentPosition)
    End Sub


    Private Sub ZoomBorder_Loaded(ByVal sender As Object, ByVal e As RoutedEventArgs)
        DrawingManager.TextEditor.AttachTextStyleSource(CanvasTextBox)
        EventAggregator.Publish(New ScaleChangedMessage(Scale))
        _frameMeter.Start()
        UpdateRasteriser(True)
    End Sub

    Private Sub ZoomBorder_LostMouseCapture(sender As Object, e As MouseEventArgs)
        If _selectionMarquee IsNot Nothing AndAlso Not IsMouseCaptured Then CancelMarquee()
        _viewportLimiter.Flush()
        If CanvasMode <> CanvasMode.Selection AndAlso DrawingManager.IsDrawing Then
            DrawingManager.CancelDrawing(GetPolyCanvas())
        End If
    End Sub


    Private Function ResolveRasteriser() As PerformantRasteriser
        If _rasteriserCache IsNot Nothing Then Return _rasteriserCache
        Dim canvas = GetPolyCanvas()
        If canvas Is Nothing Then Return Nothing
        _rasteriserCache = canvas.Rasteriser
        Return _rasteriserCache
    End Function

    Private Sub UpdateRasteriser(zoomChanged As Boolean)
        Dim rasteriser = ResolveRasteriser()
        If rasteriser Is Nothing Then Return
        rasteriser.AttachViewport(Me)
        rasteriser.ViewportChanged(Scale, zoomChanged)
    End Sub

    Private Function GetPolyCanvas() As PolyCanvas
        Dim byName As PolyCanvas = TryCast(Me.FindName("mainCanvas"), PolyCanvas)
        If byName IsNot Nothing Then Return byName
        If Child Is Nothing Then Return Nothing
        Return FindChildOfType(Of PolyCanvas)(Child)
    End Function

    Private Function FindChildOfType(Of T As DependencyObject)(root As DependencyObject) As T
        If root Is Nothing Then Return Nothing
        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(root) - 1
            Dim child = VisualTreeHelper.GetChild(root, i)
            Dim tx = TryCast(child, T)
            If tx IsNot Nothing Then Return tx
            Dim nested = FindChildOfType(Of T)(child)
            If nested IsNot Nothing Then Return nested
        Next
        Return Nothing
    End Function

End Class

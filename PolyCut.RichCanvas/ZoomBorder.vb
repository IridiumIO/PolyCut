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
            control.RaiseScaleChangedEvent(e.NewValue)
            EventAggregator.Publish(New ScaleChangedMessage(e.NewValue))
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

    Public Shared ReadOnly CanvasModeProperty As DependencyProperty = DependencyProperty.Register(NameOf(CanvasMode), GetType(CanvasMode), GetType(ZoomBorder), New PropertyMetadata(CanvasMode.Selection))

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


    Public Sub New()
        ClipToBounds = True
        AddHandler Me.MouseWheel, AddressOf ZoomBorder_MouseWheel
        AddHandler Me.MouseDown, AddressOf ZoomBorder_MouseDown
        AddHandler Me.MouseUp, AddressOf ZoomBorder_MouseUp
        AddHandler Me.MouseMove, AddressOf ZoomBorder_MouseMove
        AddHandler Me.Loaded, AddressOf ZoomBorder_Loaded
        AddHandler Me.PreviewMouseDown, AddressOf ZoomBorder_PreviewMouseDown
        AddHandler Me.LostMouseCapture, AddressOf ZoomBorder_LostMouseCapture
    End Sub


    Public ReadOnly Property TranslateTransform As TranslateTransform
        Get
            Return Child.RenderTransform.CastAs(Of TransformGroup).Children.First(Function(tr) TypeOf tr Is TranslateTransform).CastAs(Of TranslateTransform)
        End Get
    End Property

    Public ReadOnly Property ScaleTransform As ScaleTransform
        Get
            Return Child.RenderTransform.CastAs(Of TransformGroup).Children.First(Function(tr) TypeOf tr Is ScaleTransform).CastAs(Of ScaleTransform)
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

    Public Async Sub Reset()
        If Child Is Nothing Then Return

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

        ' Compute final centered translation at target scale
        Scale = targetScale
        Dim b = fit.TransformToAncestor(Me).TransformBounds(New Rect(0, 0, w, h))
        Dim targetX = TranslateTransform.X + (Padding.Left + ((vw - b.Width) / 2.0) - b.Left)
        Dim targetY = TranslateTransform.Y + (Padding.Top + ((vh - b.Height) / 2.0) - b.Top)

        ' Restore start and animate
        Scale = s0
        TranslateTransform.X = x0
        TranslateTransform.Y = y0

        Const durationMs As Integer = 200
        Const frames As Integer = 28
        Dim delayMs = Math.Max(1, durationMs \ frames)

        For i As Integer = 1 To frames
            Dim t = i / CDbl(frames)
            t = t * t * (3 - 2 * t) ' smoothstep easing
            Scale = s0 + ((targetScale - s0) * t)
            TranslateTransform.X = x0 + ((targetX - x0) * t)
            TranslateTransform.Y = y0 + ((targetY - y0) * t)
            Await Task.Delay(delayMs)
        Next

        Scale = targetScale
        TranslateTransform.X = targetX
        TranslateTransform.Y = targetY
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
        start = e.GetPosition(Me)
        origin = New Point(TranslateTransform.X, TranslateTransform.Y)
        Me.Cursor = Cursors.ScrollAll
        Child.CaptureMouse()
    End Sub

    Private Sub MoveUp()
        If Child Is Nothing Then Return
        Child.ReleaseMouseCapture()
        Me.Cursor = Nothing
    End Sub

    Public DrawingManager As New DrawingManager

    Private Sub ZoomBorder_MouseDown(ByVal sender As Object, ByVal e As MouseButtonEventArgs)
        HandleMouseDown(e, isPreview:=False)
    End Sub

    Private Sub ZoomBorder_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        HandleMouseDown(e, isPreview:=True)
    End Sub

    Private _middleMouseDownPosStart As Point

    Private Sub HandleMouseDown(e As MouseButtonEventArgs, isPreview As Boolean)
        EventAggregator.Publish(New ScaleChangedMessage(Scale))
        EventAggregator.Publish(New TranslationChangedMessage(New Point(TranslateTransform.X, TranslateTransform.Y)))

        If DrawingManager.TextEditor.HandleTextMouseDown(CanvasMode, e) Then Return

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
        ZoomByWheel(e.Delta)
        e.Handled = False
    End Sub

    'break out from above handler so we can call it from transformgizmo (Probably cursed)
    Public Sub ZoomByWheel(delta As Integer)
        If Child Is Nothing Then Return

        Dim zoomFactor As Double = delta * ScaleAmount
        Dim targetScale As Double = Scale + (zoomFactor * Scale)

        ' Early return if zooming out too much
        If delta <= 0 AndAlso (ScaleTransform.ScaleX < ScaleMin OrElse ScaleTransform.ScaleY < ScaleMin) Then Return

        ZoomToPoint(targetScale)
    End Sub

    Public Sub ZoomByLinear(delta As Double)
        If Child Is Nothing Then Return
        ZoomToPoint(Scale + delta)
    End Sub

    Private Sub ZoomToPoint(targetScale As Double)
        If Child Is Nothing Then Return

        targetScale = Math.Max(ScaleMin, Math.Min(ScaleMax, targetScale))

        Dim relative As Point = Mouse.GetPosition(Child)
        Dim absoluteX As Double = relative.X * Scale + TranslateTransform.X
        Dim absoluteY As Double = relative.Y * Scale + TranslateTransform.Y

        Scale = targetScale

        TranslateTransform.X = absoluteX - (relative.X * Scale)
        TranslateTransform.Y = absoluteY - (relative.Y * Scale)
    End Sub

    Private Sub ZoomBorder_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        Dim currentPosition As Point = e.GetPosition(Me)
        If CanvasMode <> CanvasMode.Selection AndAlso e.LeftButton = MouseButtonState.Pressed Then
            Dim polyCanvas = GetPolyCanvas()
            Dim position As Point = e.GetPosition(polyCanvas)
            DrawingManager.UpdateDrawing(CanvasMode, position, Keyboard.IsKeyDown(Key.LeftShift), Keyboard.IsKeyDown(Key.LeftCtrl))
            Return
        End If

        If Not ZoomEnabled OrElse Child Is Nothing OrElse Not Child.IsMouseCaptured Then Return
        TranslateTransform.X = origin.X - (start.X - currentPosition.X)
        TranslateTransform.Y = origin.Y - (start.Y - currentPosition.Y)
        EventAggregator.Publish(New TranslationChangedMessage(New Point(TranslateTransform.X, TranslateTransform.Y)))
    End Sub


    Private Sub ZoomBorder_Loaded(ByVal sender As Object, ByVal e As RoutedEventArgs)
        DrawingManager.TextEditor.AttachTextStyleSource(CanvasTextBox)
        EventAggregator.Publish(New ScaleChangedMessage(Scale))
    End Sub

    Private Sub ZoomBorder_LostMouseCapture(sender As Object, e As MouseEventArgs)
        If CanvasMode <> CanvasMode.Selection AndAlso DrawingManager.IsDrawing Then
            DrawingManager.CancelDrawing(GetPolyCanvas())
        End If
    End Sub


    Private Function GetPolyCanvas() As PolyCanvas
        ' Try direct name lookup first
        Dim byName As PolyCanvas = TryCast(Me.FindName("mainCanvas"), PolyCanvas)
        If byName IsNot Nothing Then Return byName

        ' If that fails, search visual tre
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

Module ExtensionMethods
    <Extension()>
    Public Function CastAs(Of T)(obj As Object) As T
        If TypeOf obj Is T Then
            Return DirectCast(obj, T)
        Else
            Return Nothing
        End If
    End Function

    <Extension()>
    Public Function TryCastAs(Of T As Class)(obj As Object) As T
        Return TryCast(obj, T)
    End Function

End Module
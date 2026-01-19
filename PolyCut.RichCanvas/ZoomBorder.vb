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

        Dim cs = CType(Child, FrameworkElement)

        Dim duration As Integer = 200 ' in milliseconds
        Dim frames As Integer = 28
        Dim deltaScale = (Scale - 2) / frames
        Dim deltaTX = (TranslateTransform.X + cs.ActualWidth / 2) / frames
        Dim deltaTY = (TranslateTransform.Y + cs.ActualHeight / 2) / frames

        Dim frame As Integer = 0
        While frame < frames
            Scale -= deltaScale
            TranslateTransform.X -= deltaTX
            TranslateTransform.Y -= deltaTY

            ' Use Task.Delay for asynchronous waiting without blocking the UI thread
            Await Task.Delay(duration / frames)

            frame += 1
        End While

        Scale = 2
        TranslateTransform.X = -cs.ActualWidth / 2
        TranslateTransform.Y = -cs.ActualHeight / 2

    End Sub

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

        EventAggregator.Publish(New ScaleChangedMessage(Scale))
        EventAggregator.Publish(New TranslationChangedMessage(New Point(TranslateTransform.X, TranslateTransform.Y)))

        If CanvasMode <> CanvasMode.Selection AndAlso e.ChangedButton = MouseButton.Left Then

            Dim _polyCanvas = CType(Me.FindName("mainCanvas"), PolyCanvas)
            Dim position As Point = e.GetPosition(_polyCanvas)
            DrawingManager.StartDrawing(CanvasMode, position, _polyCanvas)
        Else
            If e.OriginalSource Is Me OrElse e.OriginalSource Is Me.Background Then
                Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
                If Not isShiftPressed Then
                    Dim _polyCanvas = CType(Me.FindName("mainCanvas"), PolyCanvas)

                    _polyCanvas.SelectionManager.ClearSelection()
                End If
                e.Handled = True
            End If
        End If

        If GetAction(e.ChangedButton) = ZoomBorderMouseAction.Move Then MoveDown(e)
    End Sub


    Private Sub ZoomBorder_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        EventAggregator.Publish(New ScaleChangedMessage(Scale))
        EventAggregator.Publish(New TranslationChangedMessage(New Point(TranslateTransform.X, TranslateTransform.Y)))

        If CanvasMode <> CanvasMode.Selection AndAlso e.ChangedButton = MouseButton.Left Then

            Dim _polyCanvas = CType(Me.FindName("mainCanvas"), PolyCanvas)
            Dim position As Point = e.GetPosition(_polyCanvas)
            DrawingManager.StartDrawing(CanvasMode, position, _polyCanvas)
            e.Handled = True
        End If

        If GetAction(e.ChangedButton) = ZoomBorderMouseAction.Move Then MoveDown(e)
    End Sub

    Private Sub ZoomBorder_MouseUp(ByVal sender As Object, ByVal e As MouseButtonEventArgs)

        If CanvasMode <> CanvasMode.Selection AndAlso e.ChangedButton = MouseButton.Left Then
            Dim polyCanvas = CType(Me.FindName("mainCanvas"), PolyCanvas)
            DrawingManager.FinishDrawing(CanvasMode, polyCanvas, CanvasTextBox)
            Return
        End If


        If GetAction(e.ChangedButton) = ZoomBorderMouseAction.Move Then : MoveUp()
        ElseIf GetAction(e.ChangedButton) = ZoomBorderMouseAction.Reset Then : Reset()
        End If
    End Sub

    Private Sub ZoomBorder_MouseWheel(ByVal sender As Object, ByVal e As MouseWheelEventArgs)
        If Child Is Nothing Then Return

        Dim zoomFactor As Double = e.Delta * ScaleAmount
        Dim targetScale As Double = Scale + (zoomFactor * Scale)

        ' Constrain the new scale within the allowed range
        targetScale = Math.Max(ScaleMin, Math.Min(ScaleMax, targetScale))

        ' Early return if zooming out too much
        If e.Delta <= 0 AndAlso (ScaleTransform.ScaleX < 0.4 OrElse ScaleTransform.ScaleY < 0.4) Then Return

        Dim relative As Point = e.GetPosition(Child)
        ' Calculate the absolute positions based on the current scale
        Dim absoluteX As Double = relative.X * Scale + TranslateTransform.X
        Dim absoluteY As Double = relative.Y * Scale + TranslateTransform.Y


        ' Apply the new scale
        Scale = targetScale

        ' Adjust the translation based on the new scale
        TranslateTransform.X = absoluteX - (relative.X * Scale)
        TranslateTransform.Y = absoluteY - (relative.Y * Scale)

        e.Handled = False

    End Sub

    Private Sub ZoomBorder_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        Dim currentPosition As Point = e.GetPosition(Me)
        If CanvasMode <> CanvasMode.Selection AndAlso e.LeftButton = MouseButtonState.Pressed Then
            Dim polyCanvas = CType(Me.FindName("mainCanvas"), PolyCanvas)
            Dim position As Point = e.GetPosition(polyCanvas)
            DrawingManager.UpdateDrawing(CanvasMode, position, Keyboard.IsKeyDown(Key.LeftShift))
            Return
        End If

        If Not ZoomEnabled OrElse Child Is Nothing OrElse Not Child.IsMouseCaptured Then Return
        TranslateTransform.X = origin.X - (start.X - currentPosition.X)
        TranslateTransform.Y = origin.Y - (start.Y - currentPosition.Y)
        EventAggregator.Publish(New TranslationChangedMessage(New Point(TranslateTransform.X, TranslateTransform.Y)))
    End Sub


    Private Sub ZoomBorder_Loaded(ByVal sender As Object, ByVal e As RoutedEventArgs)
        EventAggregator.Publish(New ScaleChangedMessage(Scale))
    End Sub


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
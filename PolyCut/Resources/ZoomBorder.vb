Imports System
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows
Imports System.Linq
Imports System.ComponentModel
Imports System.Windows.Media.Animation
Imports System.Windows.Threading

Public Enum ZoomBorderMouseAction
    None
    Move
    Reset
End Enum


''' <summary>
''' ZoomBorder control converted from https://github.com/spicermicer/ZoomBorder
''' Added INotifyPropertyChanged interface which autoimplements with Fody.PropertyChanged
''' </summary>

Public Class ZoomBorder
    Inherits Border : Implements INotifyPropertyChanged

    Public Shared ReadOnly LeftButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(LeftButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.Move, Nothing))

    Public Property LeftButtonAction As ZoomBorderMouseAction
        Get
            Return CType(GetValue(LeftButtonActionProperty), ZoomBorderMouseAction)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(LeftButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly RightButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(RightButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.Reset, Nothing))

    Public Property RightButtonAction As ZoomBorderMouseAction
        Get
            Return CType(GetValue(RightButtonActionProperty), ZoomBorderMouseAction)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(RightButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly MiddleButtonActionProperty As DependencyProperty = DependencyProperty.Register(NameOf(MiddleButtonAction), GetType(ZoomBorderMouseAction), GetType(ZoomBorder), New PropertyMetadata(ZoomBorderMouseAction.None, Nothing))

    Public Property MiddleButtonAction As ZoomBorderMouseAction
        Get
            Return CType(GetValue(MiddleButtonActionProperty), ZoomBorderMouseAction)
        End Get
        Set(ByVal value As ZoomBorderMouseAction)
            SetValue(MiddleButtonActionProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleMaxProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleMax), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(10.0, Nothing))

    Public Property ScaleMax As Double
        Get
            Return CDbl(GetValue(ScaleMaxProperty))
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleMaxProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleMinProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleMin), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(0.8, Nothing))

    Public Property ScaleMin As Double
        Get
            Return CDbl(GetValue(ScaleMinProperty))
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleMinProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ScaleAmountProperty As DependencyProperty = DependencyProperty.Register(NameOf(ScaleAmount), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(0.005, Nothing))

    Public Property ScaleAmount As Double
        Get
            Return CDbl(GetValue(ScaleAmountProperty))
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleAmountProperty, value)
        End Set
    End Property

    Public Shared ReadOnly ZoomEnabledProperty As DependencyProperty = DependencyProperty.Register(NameOf(ZoomEnabled), GetType(Boolean), GetType(ZoomBorder), New PropertyMetadata(True, Nothing))

    Public Property ZoomEnabled As Boolean
        Get
            Return CBool(GetValue(ZoomEnabledProperty))
        End Get
        Set(ByVal value As Boolean)
            SetValue(ZoomEnabledProperty, value)
        End Set
    End Property

    Public Shared ReadOnly PanEnabledProperty As DependencyProperty = DependencyProperty.Register(NameOf(PanEnabled), GetType(Boolean), GetType(ZoomBorder), New PropertyMetadata(True, Nothing))

    Public Property PanEnabled As Boolean
        Get
            Return CBool(GetValue(PanEnabledProperty))
        End Get
        Set(ByVal value As Boolean)
            SetValue(PanEnabledProperty, value)
        End Set
    End Property

    Private _child As UIElement = Nothing
    Private origin As Point
    Private start As Point
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Shared ReadOnly ScaleProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Scale), GetType(Double), GetType(ZoomBorder), New PropertyMetadata(1.0))


    Public Property Scale As Double
        Get
            Return CType(GetValue(ScaleProperty), Double)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleProperty, value)

            _scaleTransform.ScaleX = value
            _scaleTransform.ScaleY = value

            OnScaleChanged()
        End Set
    End Property


    Public Event ScaleChanged As EventHandler(Of PropertyChangedEventArgs)

    Protected Overridable Sub OnScaleChanged()
        RaiseEvent ScaleChanged(Me, New PropertyChangedEventArgs(NameOf(Scale)))
    End Sub



    Public Sub New()
        ClipToBounds = True
    End Sub

    Public ReadOnly Property _translateTransform As TranslateTransform
        Get
            Return CType((CType(Child.RenderTransform, TransformGroup)).Children.First(Function(tr) TypeOf tr Is TranslateTransform), TranslateTransform)
        End Get
    End Property

    Private ReadOnly Property _scaleTransform As ScaleTransform
        Get
            Return CType((CType(_child.RenderTransform, TransformGroup)).Children.First(Function(tr) TypeOf tr Is ScaleTransform), ScaleTransform)
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
        Me._child = element
        If _child Is Nothing Then Return
        Dim group = New TransformGroup()
        Dim st = New ScaleTransform()
        Dim tt = New TranslateTransform()
        group.Children.Add(st)
        group.Children.Add(tt)
        _child.RenderTransform = group
        _child.RenderTransformOrigin = New Point(0.0, 0.0)
        AddHandler Me.MouseWheel, AddressOf ZoomBorder_MouseWheel
        AddHandler Me.MouseDown, AddressOf ZoomBorder_MouseDown
        AddHandler Me.MouseUp, AddressOf ZoomBorder_MouseUp
        AddHandler Me.MouseMove, AddressOf ZoomBorder_MouseMove
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
        If _child Is Nothing Then Return

        Dim duration As Integer = 200 ' in milliseconds
        Dim frames As Integer = 28
        Dim deltaScale = (Scale - 1) / frames
        Dim deltaTX = _translateTransform.X / frames
        Dim deltaTY = _translateTransform.Y / frames

        Dim frame As Integer = 0
        While frame < frames
            Scale -= deltaScale
            _translateTransform.X -= deltaTX
            _translateTransform.Y -= deltaTY

            ' Use Task.Delay for asynchronous waiting without blocking the UI thread
            Await Task.Delay(duration / frames)

            frame += 1
        End While

        Scale = 1
        _translateTransform.X = 0.0
        _translateTransform.Y = 0.0

    End Sub

    Private Sub MoveDown(ByVal e As MouseButtonEventArgs)
        If Not PanEnabled Then Return
        If _child Is Nothing Then Return
        Dim tt = _translateTransform
        start = e.GetPosition(Me)
        origin = New Point(tt.X, tt.Y)
        Me.Cursor = Cursors.ScrollAll
        _child.CaptureMouse()
    End Sub

    Private Sub MoveUp(ByVal e As MouseButtonEventArgs)
        If _child Is Nothing Then Return
        _child.ReleaseMouseCapture()
        Me.Cursor = Nothing
    End Sub

    Private Sub ZoomBorder_MouseDown(ByVal sender As Object, ByVal e As MouseButtonEventArgs)
        Select Case GetAction(e.ChangedButton)
            Case ZoomBorderMouseAction.Move
                MoveDown(e)
            Case Else
        End Select
    End Sub

    Private Sub ZoomBorder_MouseUp(ByVal sender As Object, ByVal e As MouseButtonEventArgs)
        Select Case GetAction(e.ChangedButton)
            Case ZoomBorderMouseAction.Move
                MoveUp(e)
            Case ZoomBorderMouseAction.Reset
                Reset()
            Case Else
        End Select
    End Sub

    Private Sub ZoomBorder_MouseWheel(ByVal sender As Object, ByVal e As MouseWheelEventArgs)
        If _child Is Nothing Then Return
        Dim st = _scaleTransform
        Dim tt = _translateTransform
        Dim zoom As Double = e.Delta * ScaleAmount
        Dim zoomCorrected As Double = zoom * Scale
        Dim newScale As Double = Scale + zoomCorrected
        If newScale > ScaleMax Then newScale = ScaleMax
        If newScale < ScaleMin Then newScale = ScaleMin
        If Not (e.Delta > 0) AndAlso (st.ScaleX < 0.4 OrElse st.ScaleY < 0.4) Then Return
        Dim relative As Point = e.GetPosition(_child)
        Dim absoluteX As Double
        Dim absoluteY As Double
        absoluteX = relative.X * Scale + tt.X
        absoluteY = relative.Y * Scale + tt.Y
        Scale = newScale
        tt.X = absoluteX - relative.X * Scale
        tt.Y = absoluteY - relative.Y * Scale
    End Sub

    Private Sub ZoomBorder_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs)
        If Not ZoomEnabled Then Return
        If _child Is Nothing Then Return
        If Not _child.IsMouseCaptured Then Return
        Dim tt = _translateTransform
        Dim v As Vector = start - e.GetPosition(Me)
        tt.X = origin.X - v.X
        tt.Y = origin.Y - v.Y
    End Sub
End Class


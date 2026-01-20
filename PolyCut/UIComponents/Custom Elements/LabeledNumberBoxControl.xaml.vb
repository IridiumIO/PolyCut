Partial Public Class LabeledNumberBoxControl
    Inherits UserControl

    Public Sub New()
        InitializeComponent()
    End Sub

    Public Property LabelText As String
        Get
            Return CType(GetValue(LabelTextProperty), String)
        End Get
        Set(value As String)
            SetValue(LabelTextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly LabelTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(LabelText), GetType(String), GetType(LabeledNumberBoxControl), New PropertyMetadata(String.Empty))

    Public Shared ReadOnly TextChangedEvent As RoutedEvent =
    EventManager.RegisterRoutedEvent(
        NameOf(TextChanged),
        RoutingStrategy.Bubble,
        GetType(RoutedEventHandler),
        GetType(LabeledNumberBoxControl)
    )

    Public Custom Event TextChanged As RoutedEventHandler
        AddHandler(value As RoutedEventHandler)
            MyBase.AddHandler(TextChangedEvent, value)
        End AddHandler
        RemoveHandler(value As RoutedEventHandler)
            MyBase.RemoveHandler(TextChangedEvent, value)
        End RemoveHandler
        RaiseEvent(sender As Object, e As RoutedEventArgs)
            MyBase.RaiseEvent(e)
        End RaiseEvent
    End Event

    Public Property Text As String
        Get
            Return CType(GetValue(TextProperty), String)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Text), GetType(String), GetType(LabeledNumberBoxControl), New FrameworkPropertyMetadata(String.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, AddressOf OnTextPropertyChanged))

    Private Shared Sub OnTextPropertyChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim c = DirectCast(d, LabeledNumberBoxControl)
        If Equals(e.OldValue, e.NewValue) Then Return

        c.RaiseEvent(New RoutedEventArgs(TextChangedEvent, c))
    End Sub


    Public Property UnitText As String
        Get
            Return CType(GetValue(UnitTextProperty), String)
        End Get
        Set(value As String)
            SetValue(UnitTextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly UnitTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(UnitText), GetType(String), GetType(LabeledNumberBoxControl), New PropertyMetadata(String.Empty))

    ' Width for the textbox. Use Double.NaN to let the textbox size to content (default).
    Public Property TextBoxWidth As Double
        Get
            Return CDbl(GetValue(TextBoxWidthProperty))
        End Get
        Set(value As Double)
            SetValue(TextBoxWidthProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextBoxWidthProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(TextBoxWidth), GetType(Double), GetType(LabeledNumberBoxControl), New PropertyMetadata(Double.NaN))

End Class
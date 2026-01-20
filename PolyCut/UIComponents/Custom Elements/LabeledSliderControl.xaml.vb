Partial Public Class LabeledSliderControl
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
        DependencyProperty.Register(NameOf(LabelText), GetType(String), GetType(LabeledSliderControl), New PropertyMetadata(String.Empty))

    Public Property Value As Double
        Get
            Return CDbl(GetValue(ValueProperty))
        End Get
        Set(value As Double)
            SetValue(ValueProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ValueProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Value), GetType(Double), GetType(LabeledSliderControl), New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault))

    Public Property Minimum As Double
        Get
            Return CDbl(GetValue(MinimumProperty))
        End Get
        Set(value As Double)
            SetValue(MinimumProperty, value)
        End Set
    End Property
    Public Shared ReadOnly MinimumProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Minimum), GetType(Double), GetType(LabeledSliderControl), New PropertyMetadata(0.0))

    Public Property Maximum As Double
        Get
            Return CDbl(GetValue(MaximumProperty))
        End Get
        Set(value As Double)
            SetValue(MaximumProperty, value)
        End Set
    End Property
    Public Shared ReadOnly MaximumProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Maximum), GetType(Double), GetType(LabeledSliderControl), New PropertyMetadata(100.0))

    Public Property TickFrequency As Double
        Get
            Return CDbl(GetValue(TickFrequencyProperty))
        End Get
        Set(value As Double)
            SetValue(TickFrequencyProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TickFrequencyProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(TickFrequency), GetType(Double), GetType(LabeledSliderControl), New PropertyMetadata(1.0))

    Public Property UnitText As String
        Get
            Return CType(GetValue(UnitTextProperty), String)
        End Get
        Set(value As String)
            SetValue(UnitTextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly UnitTextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(UnitText), GetType(String), GetType(LabeledSliderControl), New PropertyMetadata(String.Empty))



    Public Property NumberIsReadOnly As Boolean
        Get
            Return CBool(GetValue(NumberIsReadOnlyProperty))
        End Get
        Set(value As Boolean)
            SetValue(NumberIsReadOnlyProperty, value)
        End Set
    End Property
    Public Shared ReadOnly NumberIsReadOnlyProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(NumberIsReadOnly), GetType(Boolean), GetType(LabeledSliderControl), New PropertyMetadata(False))

End Class
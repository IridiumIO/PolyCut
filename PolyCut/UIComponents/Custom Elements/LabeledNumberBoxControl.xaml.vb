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

    Public Property Text As String
        Get
            Return CType(GetValue(TextProperty), String)
        End Get
        Set(value As String)
            SetValue(TextProperty, value)
        End Set
    End Property
    Public Shared ReadOnly TextProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Text), GetType(String), GetType(LabeledNumberBoxControl), New FrameworkPropertyMetadata(String.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault))

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
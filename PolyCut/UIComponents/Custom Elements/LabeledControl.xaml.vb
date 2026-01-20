Partial Public Class LabeledControl
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
        DependencyProperty.Register(NameOf(LabelText), GetType(String), GetType(LabeledControl), New PropertyMetadata(String.Empty))

    Public Property ValueContent As Object
        Get
            Return GetValue(ValueContentProperty)
        End Get
        Set(value As Object)
            SetValue(ValueContentProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ValueContentProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(ValueContent), GetType(Object), GetType(LabeledControl), New PropertyMetadata(Nothing))


End Class
Public Class AnnotatedColourPicker
    Inherits AnnotatedControl

    Public Sub New()
        Dim editor As New ColorPickerControl With {.LabelText = ""}
        editor.SetBinding(ColorPickerControl.CurrentColorProperty, New Binding(NameOf(Value)) With {.Source = Me, .Mode = BindingMode.TwoWay, .Converter = New BrushToHexStringConverter()})
        AddHandler editor.ColorSelected, Sub(sender, e) SetCurrentValue(ValueProperty, CStr(New BrushConverter().ConvertToString(e.SelectedBrush)))
        SetEditor(editor)
    End Sub

    Protected Overrides ReadOnly Property BoundValueProperty As DependencyProperty
        Get
            Return ValueProperty
        End Get
    End Property

    Public Shared ReadOnly ValueProperty As DependencyProperty = DependencyProperty.Register(NameOf(Value), GetType(String), GetType(AnnotatedColourPicker), New FrameworkPropertyMetadata(String.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault))
    Public Property Value As String
        Get
            Return DirectCast(GetValue(ValueProperty), String)
        End Get
        Set(value As String)
            SetValue(ValueProperty, value)
        End Set
    End Property

End Class

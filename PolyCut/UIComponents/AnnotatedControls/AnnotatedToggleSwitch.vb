Public Class AnnotatedToggleSwitch
    Inherits AnnotatedControl

    Public Sub New()
        Dim editor As New Wpf.Ui.Controls.ToggleSwitch With {.HorizontalAlignment = HorizontalAlignment.Right}
        editor.SetBinding(Primitives.ToggleButton.IsCheckedProperty, New Binding(NameOf(IsChecked)) With {.Source = Me, .Mode = BindingMode.TwoWay})
        SetEditor(editor)
    End Sub

    Protected Overrides ReadOnly Property BoundValueProperty As DependencyProperty
        Get
            Return IsCheckedProperty
        End Get
    End Property

    Protected Overrides ReadOnly Property ShowChildren As Boolean
        Get
            Return IsChecked
        End Get
    End Property

    Public Shared ReadOnly IsCheckedProperty As DependencyProperty = DependencyProperty.Register(NameOf(IsChecked), GetType(Boolean), GetType(AnnotatedToggleSwitch), New FrameworkPropertyMetadata(False, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Sub(d, e) DirectCast(d, AnnotatedToggleSwitch).RefreshChildren()))
    Public Property IsChecked As Boolean
        Get
            Return DirectCast(GetValue(IsCheckedProperty), Boolean)
        End Get
        Set(value As Boolean)
            SetValue(IsCheckedProperty, value)
        End Set
    End Property

End Class

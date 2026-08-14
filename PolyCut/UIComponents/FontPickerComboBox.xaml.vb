Public Class FontPickerComboBox
    Inherits UserControl

    Public Event DropDownClosed As EventHandler

    Public Sub New()
        InitializeComponent()
        AddHandler FontComboBox.DropDownClosed, AddressOf OnFontComboBoxDropDownClosed
    End Sub

    Private Sub OnFontComboBoxDropDownClosed(sender As Object, e As EventArgs)
        RaiseEvent DropDownClosed(Me, e)
    End Sub

    Public Shared ReadOnly SelectedFontProperty As DependencyProperty =
        DependencyProperty.Register("SelectedFont", GetType(FontFamily), GetType(FontPickerComboBox), New PropertyMetadata(Nothing))

    Public Property SelectedFont As FontFamily
        Get
            Debug.WriteLine("selected font get")
            Return CType(GetValue(SelectedFontProperty), FontFamily)
        End Get
        Set(value As FontFamily)
            Debug.WriteLine("selected font set")
            SetValue(SelectedFontProperty, value)
        End Set
    End Property
End Class
Public Class Tab_ElementProperties


    Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)
        'Need to explicitly call the `Enter` keypress as pressing `Tab` doesn't commit the new number before switching focus

        Dim numberBox As WPF.Ui.Controls.NumberBox = TryCast(sender, WPF.Ui.Controls.NumberBox)
        If numberBox IsNot Nothing Then

            numberBox.RaiseEvent(New KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(numberBox), 0, Key.Enter) With {
            .RoutedEvent = Keyboard.KeyDownEvent
        })


            Dim bindingExpression = numberBox.GetBindingExpression(WPF.Ui.Controls.NumberBox.ValueProperty)
            bindingExpression?.UpdateSource()
        End If
    End Sub


    Private Sub TextBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)

        Dim textBox As WPF.Ui.Controls.TextBox = TryCast(sender, WPF.Ui.Controls.TextBox)
        If textBox IsNot Nothing Then
            textBox.SelectAll()
            e.Handled = True
        End If
    End Sub

End Class

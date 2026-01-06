Public Class AddPrinterDialog : Inherits WPF.Ui.Controls.ContentDialog


    Public Sub New(contentPresenter As ContentPresenter)
        MyBase.New(contentPresenter)

        Me.DataContext = Me
        InitializeComponent()

        ' Validate when any button tries to close the dialog
        AddHandler Me.Closing, AddressOf ContentDialog_Closing

        ' Optional: enable/disable primary button live while typing
        AddHandler PrinterNameTextBox.TextChanged, Sub() Me.IsPrimaryButtonEnabled = Not String.IsNullOrWhiteSpace(PrinterNameTextBox.Text)


    End Sub

    Public Property PrinterName As String


    Private Sub ContentDialog_Closing(sender As Object, e As WPF.Ui.Controls.ContentDialogClosingEventArgs)
        ' Only validate when the Primary (OK) button was clicked
        If e.Result = WPF.Ui.Controls.ContentDialogResult.Primary Then
            ' Ensure binding source is updated (if UpdateSourceTrigger isn't PropertyChanged)
            Dim be = PrinterNameTextBox.GetBindingExpression(TextBox.TextProperty)
            be?.UpdateSource()

            If String.IsNullOrWhiteSpace(PrinterName) Then
                ' Cancel close and show minimal feedback
                e.Cancel = True

                ' Visual feedback: focus and highlight the textbox
                PrinterNameTextBox.Focus()
                PrinterNameTextBox.BorderBrush = System.Windows.Media.Brushes.Red

            ElseIf PrinterName.IndexOfAny(IO.Path.GetInvalidFileNameChars()) >= 0 Then
                ' Cancel close and show minimal feedback
                e.Cancel = True
                ' Visual feedback: focus and highlight the textbox
                PrinterNameTextBox.Focus()
                PrinterNameTextBox.BorderBrush = System.Windows.Media.Brushes.Red
            ElseIf Application.GetService(Of MainViewModel).Printers.Select(Function(f) f.Name).Contains(PrinterName) Then
                ' Cancel close and show minimal feedback
                e.Cancel = True
                ' Visual feedback: focus and highlight the textbox
                PrinterNameTextBox.Focus()

                PrinterNameTextBox.BorderBrush = System.Windows.Media.Brushes.Red

            Else
                ' Reset any validation visuals
                PrinterNameTextBox.ClearValue(TextBox.BorderBrushProperty)
            End If
        End If
    End Sub

End Class

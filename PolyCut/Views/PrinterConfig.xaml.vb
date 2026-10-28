Partial Public Class PrinterConfig

    Public Property ActivePrinter As Printer

    Public Property MainVM As MainViewModel

    Public Sub New(SVGPageVM As SVGPageViewModel)
        ' This call is required by the designer.
        InitializeComponent()

        Me.DataContext = SVGPageVM
        Me.MainVM = SVGPageVM.MainVM



    End Sub


    Private Sub TextBox_GotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)

        Dim textBox As WPF.Ui.Controls.TextBox = TryCast(sender, WPF.Ui.Controls.TextBox)
        If textBox IsNot Nothing Then
            textBox.SelectAll()
            e.Handled = True
        End If
    End Sub



    Private Async Sub AddPrinterBtn_Click(sender As Object, e As RoutedEventArgs)

        Dim newPrinterWindow As New AddPrinterDialog(RootContentDialog)
        Dim result = Await newPrinterWindow.ShowAsync()

        If result = WPF.Ui.Controls.ContentDialogResult.Primary Then
            Dim newPrinter As New Printer With {
                .Name = newPrinterWindow.PrinterName,
                .BedWidth = 235,
                .BedHeight = 235,
                .WorkingOffsetX = 0,
                .WorkingOffsetY = 0,
                .WorkingWidth = 235,
                .WorkingHeight = 235
            }
            MainVM.AddPrinter(newPrinter)
        End If

    End Sub



End Class

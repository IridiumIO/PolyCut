Partial Public Class PrinterConfig

    Public Property ActivePrinter As Printer

    Public Property MainVM As MainViewModel

    Private _contentDialogHost As WPF.Ui.IContentDialogService

    Public Sub New(SVGPageVM As SVGPageViewModel, contentDialogHost As WPF.Ui.IContentDialogService)
        ' This call is required by the designer.
        InitializeComponent()

        _contentDialogHost = contentDialogHost
        Me.DataContext = SVGPageVM
        Me.MainVM = SVGPageVM.MainVM

        contentDialogHost.SetDialogHost(RootContentDialog)


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



    Private Async Sub AddPrinterBtn_Click(sender As Object, e As RoutedEventArgs)

        Dim newPrinterWindow As New AddPrinterDialog(_contentDialogHost.GetDialogHost())
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

Imports WPF.Ui.Controls
Imports WPF.Ui
Public Class SnackbarService : Inherits WPF.Ui.SnackbarService

    Public Sub Generate(Title As String,
                        Subtitle As String,
                        ControlAppearance As ControlAppearance,
                        Icon As SymbolRegular,
                        Optional Duration As Integer = 3)

        MyBase.Show(Title, Subtitle, ControlAppearance, New SymbolIcon(Icon), TimeSpan.FromSeconds(Duration))

    End Sub

    Public Sub GenerateInfo(Title As String, Subtitle As String, Optional Duration As Integer = 4)
        Generate(Title, Subtitle, ControlAppearance.Info, SymbolRegular.Info32, Duration)
    End Sub

    Public Sub GenerateError(Title As String, Subtitle As String, Optional Duration As Integer = 4)
        Generate(Title, Subtitle, ControlAppearance.Danger, SymbolRegular.DismissCircle32, Duration)
    End Sub

    Public Sub GenerateSuccess(Title As String, Subtitle As String, Optional Duration As Integer = 4)
        Generate(Title, Subtitle, ControlAppearance.Success, SymbolRegular.CheckmarkCircle32, Duration)
    End Sub

    Public Sub GenerateCaution(Title As String, Subtitle As String, Optional Duration As Integer = 4)
        Generate(Title, Subtitle, ControlAppearance.Caution, SymbolRegular.Warning32, Duration)
    End Sub

End Class


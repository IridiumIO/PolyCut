Imports WPF.Ui.Controls
Imports WPF.Ui

Public Class SnackbarService : Inherits WPF.Ui.SnackbarService

    Public Sub Generate(Title As String,
                        Subtitle As String,
                        Optional ControlAppearance As ControlAppearance = ControlAppearance.Primary,
                        Optional Icon As SymbolRegular = SymbolRegular.Info32,
                        Optional Duration As Integer = 3)

        MyBase.Show(Title, Subtitle, ControlAppearance, New SymbolIcon(Icon), TimeSpan.FromSeconds(Duration))

    End Sub


    Public Sub GenerateInfo(Title As String, Subtitle As String, Optional Duration As Integer = 3)
        Dim ca As ControlAppearance = ControlAppearance.Info
        Dim ci As New SymbolIcon(SymbolRegular.Info32)
        MyBase.Show(Title, Subtitle, ca, ci, TimeSpan.FromSeconds(Duration))
    End Sub

    Public Sub GenerateError(Title As String, Subtitle As String, Optional Duration As Integer = 3)
        Dim ca As ControlAppearance = ControlAppearance.Danger
        Dim ci As New SymbolIcon(SymbolRegular.DismissCircle32)
        MyBase.Show(Title, Subtitle, ca, ci, TimeSpan.FromSeconds(Duration))
    End Sub
    Public Sub GenerateSuccess(Title As String, Subtitle As String, Optional Duration As Integer = 3)
        Dim ca As ControlAppearance = ControlAppearance.Success
        Dim ci As New SymbolIcon(SymbolRegular.CheckmarkCircle32)
        MyBase.Show(Title, Subtitle, ca, ci, TimeSpan.FromSeconds(Duration))
    End Sub
End Class

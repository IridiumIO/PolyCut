Imports Microsoft.Extensions.Logging

Imports WPF.Ui
Imports WPF.Ui.Controls
Public Class SnackbarService : Inherits WPF.Ui.SnackbarService

    Public _snackbar As Snackbar

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

    Public Sub ShowCustom(message As UIElement, title As String, appearance As ControlAppearance, Optional icon As IconElement = Nothing, Optional timeout As TimeSpan = Nothing)

        If GetSnackbarPresenter() Is Nothing Then Throw New InvalidOperationException("The SnackbarPresenter was never set")
        If _snackbar Is Nothing Then _snackbar = New Snackbar(GetSnackbarPresenter())


        _snackbar.SetCurrentValue(Snackbar.TitleProperty, title)
        _snackbar.SetCurrentValue(ContentControl.ContentProperty, message)
        _snackbar.SetCurrentValue(Snackbar.AppearanceProperty, appearance)
        _snackbar.SetCurrentValue(Snackbar.IconProperty, icon)
        _snackbar.SetCurrentValue(Snackbar.TimeoutProperty, If(timeout = Nothing, DefaultTimeOut, timeout))
        BackdropSampler.SetIsEnabled(_snackbar, True)
        BackdropSampler.SetCorner(_snackbar, BackdropSampler.SampleCorner.Bottom)
        _snackbar.Cursor = Cursors.Hand
        _snackbar.Show(True)
    End Sub

    Public Sub GenerateUpdate(newVersion As String, updateURL As String)
        Dim textBlock = New TextBlock With {.Text = "Click to view release notes and download", .TextDecorations = TextDecorations.Underline}

        Dim title As String = $"Update Available ▸ Version {newVersion}"

        ShowCustom(textBlock, title, ControlAppearance.Dark, timeout:=TimeSpan.FromSeconds(10))

        Dim handler As MouseButtonEventHandler = Nothing
        Dim closedHandler As TypedEventHandler(Of Snackbar, RoutedEventArgs) = Nothing

        handler = Sub(sender, e)
                      Process.Start(New ProcessStartInfo(updateURL) With {.UseShellExecute = True})
                      RemoveHandler Me.GetSnackbarPresenter.MouseDown, handler
                      RemoveHandler Me._snackbar.Closed, closedHandler
                  End Sub

        closedHandler = Sub(sender, e)
                            RemoveHandler Me.GetSnackbarPresenter.MouseDown, handler
                            RemoveHandler Me._snackbar.Closed, closedHandler
                        End Sub

        AddHandler Me.GetSnackbarPresenter.MouseDown, handler
        AddHandler Me._snackbar.Closed, closedHandler
    End Sub

End Class


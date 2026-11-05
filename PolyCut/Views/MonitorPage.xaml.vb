Imports System.ComponentModel
Imports System.Windows.Media.Animation

Imports Microsoft.Web.WebView2.Wpf

Imports PolyCut.Core

Imports Wpf.Ui.Abstractions.Controls
Imports Wpf.Ui.Controls

Public Class MonitorPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel

    Public ReadOnly Property BoundWebPage As Object
        Get
            Dim config = ViewModel.Printer.ExportConfig
            Dim address = config.MachineUIIP?.Trim()

            If String.IsNullOrWhiteSpace(address) OrElse config.MachineUIPort < 1 OrElse config.MachineUIPort > 65535 Then
                Return Binding.DoNothing
            End If

            If Not address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) AndAlso
               Not address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
                address = "http://" & address
            End If

            Dim destination As Uri = Nothing
            If Not Uri.TryCreate(address, UriKind.Absolute, destination) OrElse
               (destination.Scheme <> Uri.UriSchemeHttp AndAlso destination.Scheme <> Uri.UriSchemeHttps) Then
                Return Binding.DoNothing
            End If

            Dim builder As New UriBuilder(destination) With {.Port = config.MachineUIPort}
            Return builder.Uri
        End Get
    End Property

    Sub New(viewmodel As MainViewModel)
        InitializeComponent()

        ' Configure the environment before DataContext activates the Source binding.
        webView.CreationProperties = New CoreWebView2CreationProperties With {
            .UserDataFolder = IO.Path.Combine(SettingsHandler.DataFolder.FullName, "WebView")
        }

        Me.ViewModel = viewmodel
        DataContext = Me

        AddHandler viewmodel.Printer.ExportConfig.PropertyChanged, AddressOf ConfigurationPropertyChanged

    End Sub

    Private Sub ConfigurationPropertyChanged(sender As Object, e As PropertyChangedEventArgs)

        If e.PropertyName = NameOf(ExportConfiguration.MachineUIIP) OrElse
           e.PropertyName = NameOf(ExportConfiguration.MachineUIPort) Then

            webView.GetBindingExpression(WebView2.SourceProperty)?.UpdateTarget()

        End If

    End Sub
End Class

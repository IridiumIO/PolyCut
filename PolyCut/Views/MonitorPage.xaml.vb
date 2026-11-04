Imports System.Windows.Media.Animation

Imports Microsoft.Web.WebView2.Core

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

        Me.ViewModel = viewmodel
        DataContext = Me

        AddHandler viewmodel.Printer.ExportConfig.PropertyChanged, Sub(sender, e)
                                                                       If e.PropertyName = NameOf(ExportConfiguration.MachineUIIP) OrElse e.PropertyName = NameOf(ExportConfiguration.MachineUIPort) Then
                                                                           webView.GetBindingExpression(Microsoft.Web.WebView2.Wpf.WebView2.SourceProperty)?.UpdateTarget()
                                                                       End If
                                                                   End Sub

    End Sub


    Private Sub WebView_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles webView.IsVisibleChanged

        Dim animation As New DoubleAnimation(-20, TimeSpan.FromSeconds(1))
        Dim storyboard As New Storyboard()

        ' Set the target property to the Y property of the TranslateTransform
        Storyboard.SetTarget(animation, webViewTranslateTransform)
        Storyboard.SetTargetProperty(animation, New PropertyPath(TranslateTransform.YProperty))

        ' Create a Storyboard and add the animation
        storyboard.Children.Add(animation)

        ' Begin the animation
        AddHandler CompositionTarget.Rendering, Sub()
                                                    webView.UpdateWindowPos()
                                                End Sub

        storyboard.Begin()

    End Sub


    Private Async Sub Page_Initialized(sender As Object, e As EventArgs)
        If webView.CoreWebView2 Is Nothing Then
            Dim cw2Environment As CoreWebView2Environment = Await CoreWebView2Environment.CreateAsync(Nothing, System.IO.Path.GetTempPath(), New CoreWebView2EnvironmentOptions)
            ' Await webView.EnsureCoreWebView2Async(cw2Environment)
        End If

        '' Set binding
        'Dim binding As New Binding("Printer.ExportConfig.MachineUIIP")
        'binding.Source = Me.DataContext
        'binding.Mode = BindingMode.OneWay

        'BindingOperations.SetBinding(webView, Microsoft.Web.WebView2.Wpf.WebView2.SourceProperty, binding)
    End Sub
End Class

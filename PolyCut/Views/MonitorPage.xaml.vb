Imports System.Windows.Media.Animation

Imports Microsoft.Web.WebView2.Core

Imports WPF.Ui.Abstractions.Controls
Imports WPF.Ui.Controls

Class MonitorPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel


    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel
        ' This call is required by the designer.
        InitializeComponent()


        ' Add any initialization after the InitializeComponent() call.

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


    Private Sub CompositionTarget_Rendering(sender As Object, e As EventArgs)
        ' Get the current time of the animation
        webView.UpdateWindowPos()
    End Sub


    Private Async Sub Page_Initialized(sender As Object, e As EventArgs)
        If webView.CoreWebView2 Is Nothing Then
            Dim cw2Environment As CoreWebView2Environment = Await CoreWebView2Environment.CreateAsync(Nothing, System.IO.Path.GetTempPath(), New CoreWebView2EnvironmentOptions)
            Await webView.EnsureCoreWebView2Async(cw2Environment)
        End If

        ' Set binding
        Dim binding As New Binding("Configuration.ExportConfig.DestinationIP")
        binding.Source = Me.DataContext
        binding.Mode = BindingMode.OneWay

        BindingOperations.SetBinding(webView, Microsoft.Web.WebView2.Wpf.WebView2.SourceProperty, binding)
    End Sub
End Class

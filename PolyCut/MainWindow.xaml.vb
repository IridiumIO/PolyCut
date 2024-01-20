Imports SharpVectors.Renderers.Utils
Imports SharpVectors.Renderers.Wpf

Imports SharpVectors.Dom.Svg
Imports SharpVectors.Renderers
Imports CommunityToolkit.Mvvm.ComponentModel
Imports System.Xml
Imports Wpf.Ui
Imports Wpf.Ui.Controls
Class MainWindow : Implements INavigationWindow


    Private _wpfWindow As WpfSvgWindow
    Private _wpfRenderer As WpfDrawingRenderer
    Private _wpfSettings As WpfDrawingSettings

    Public Property ViewModel As MainViewModel

    Public Property svgXML As XmlDocument

    Public Sub New(mainviewmodel As MainViewModel, navigationService As INavigationService, serviceProvider As IServiceProvider, snackbarService As ISnackbarService)

        ' This call is required by the designer.


        ViewModel = mainviewmodel
        DataContext = ViewModel

        InitializeComponent()

        navigationService.SetNavigationControl(NavigationView)
        NavigationView.SetServiceProvider(serviceProvider)
        snackbarService.SetSnackbarPresenter(RootSnackbar)

    End Sub


    Public Function GetNavigation() As INavigationView Implements INavigationWindow.GetNavigation
        Throw New NotImplementedException()
    End Function

    Public Function Navigate(pageType As Type) As Boolean Implements INavigationWindow.Navigate
        Throw New NotImplementedException()
    End Function

    Public Sub SetServiceProvider(serviceProvider As IServiceProvider) Implements INavigationWindow.SetServiceProvider
        Throw New NotImplementedException()
    End Sub

    Public Sub SetPageService(pageService As IPageService) Implements INavigationWindow.SetPageService
        Throw New NotImplementedException()
    End Sub

    Public Sub ShowWindow() Implements INavigationWindow.ShowWindow
        Throw New NotImplementedException()
    End Sub

    Public Sub CloseWindow() Implements INavigationWindow.CloseWindow
        Throw New NotImplementedException()
    End Sub

    Dim svg_file As String = ""


End Class

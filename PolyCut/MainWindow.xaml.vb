Imports System.Xml

Imports SharpVectors.Renderers.Utils
Imports SharpVectors.Renderers.Wpf

Imports WPF.Ui
Imports Wpf.Ui.Controls
Imports WPF.Ui.Abstractions
Class MainWindow : Implements INavigationWindow


    'Public Property ViewModel As MainViewModel

    'Public Property svgXML As XmlDocument

    Public Sub New(navigationService As INavigationService, serviceProvider As IServiceProvider, snackbarService As SnackbarService)

        DataContext = Application.GetService(Of MainViewModel)()

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


    Public Sub ShowWindow() Implements INavigationWindow.ShowWindow
        Throw New NotImplementedException()
    End Sub

    Public Sub CloseWindow() Implements INavigationWindow.CloseWindow
        Throw New NotImplementedException()
    End Sub

    Public Sub SetPageService(navigationViewPageProvider As INavigationViewPageProvider) Implements INavigationWindow.SetPageService
        Throw New NotImplementedException()
    End Sub

    Dim svg_file As String = ""


End Class

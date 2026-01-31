Imports WPF.Ui
Imports WPF.Ui.Abstractions
Imports WPF.Ui.Controls
Class MainWindow : Implements INavigationWindow


    'Public Property ViewModel As MainViewModel

    'Public Property svgXML As XmlDocument

    Public Sub New(navigationService As INavigationService, serviceProvider As IServiceProvider, snackbarService As SnackbarService)

        DataContext = Application.GetService(Of MainViewModel)()

        InitializeComponent()

        navigationService.SetNavigationControl(NavigationView)
        NavigationView.SetServiceProvider(serviceProvider)
        snackbarService.SetSnackbarPresenter(RootSnackbar)

        AddHandler NavigationView.Navigated, Sub(s, e)
                                                 If TypeOf (e.Page) Is SVGPage Then
                                                     UndoButtonGlobal.Visibility = Visibility.Visible
                                                     RedoButtonGlobal.Visibility = Visibility.Visible
                                                 Else
                                                     UndoButtonGlobal.Visibility = Visibility.Hidden
                                                     RedoButtonGlobal.Visibility = Visibility.Hidden

                                                 End If

                                                 If TypeOf (e.Page) Is SVGPage OrElse TypeOf (e.Page) Is PreviewPage Then
                                                     GenerateGCodeButton.Visibility = Visibility.Visible
                                                 Else
                                                     GenerateGCodeButton.Visibility = Visibility.Hidden

                                                 End If

                                             End Sub

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

    Private Sub AnyPreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.F5 Then
            Dim fe = TryCast(Keyboard.FocusedElement, DependencyObject)
            Debug.WriteLine($"F5 seen. Handled={e.Handled}. Focus={TryCast(fe, FrameworkElement)?.GetType().Name}")
            Debug.WriteLine($"OriginalSource={TryCast(e.OriginalSource, FrameworkElement)?.GetType().Name}")
        End If
    End Sub


    Protected Overrides Sub OnPreviewKeyDown(e As KeyEventArgs)

        Dim k As Key = If(e.Key = Key.System, e.SystemKey, e.Key)

        If k = Key.F5 Then
            Dim vm = TryCast(DataContext, MainViewModel)
            Dim cmd = vm?.GenerateGCodeCommand

            If cmd IsNot Nothing AndAlso cmd.CanExecute(Nothing) Then
                cmd.Execute(Nothing)
                e.Handled = True
                Return
            End If
        End If

        MyBase.OnPreviewKeyDown(e)
    End Sub



    Private Sub OpenMenu(sender As Object, e As MouseButtonEventArgs)
        Dim sp = DirectCast(sender, StackPanel)
        If sp.ContextMenu IsNot Nothing Then
            sp.ContextMenu.PlacementTarget = sp
            sp.ContextMenu.IsOpen = True
        End If
    End Sub

End Class

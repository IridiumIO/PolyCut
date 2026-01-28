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

    'Add handling for keyboard to handle:
    'Ctrl + Z  => Undo
    'Ctrl + Y  => Redo
    'Ctrl + S  => Save
    'Ctrl + Shift + S => Save As
    'Ctrl + O  => Open File
    'Ctrl + N  => New File
    'Ctrl + I  => Import SVG File

    Public Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles MainWindowView.KeyDown
        Dim vm = TryCast(Me.DataContext, MainViewModel)
        If vm Is Nothing Then Return
        If e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.Z Then
            If vm.UndoCommand.CanExecute(Nothing) Then
                vm.UndoCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.Y Then
            If vm.RedoCommand.CanExecute(Nothing) Then
                vm.RedoCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.S Then
            If vm.SaveProjectCommand.CanExecute(Nothing) Then
                vm.SaveProjectCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) AndAlso e.Key = Key.S Then
            If vm.SaveProjectAsCommand.CanExecute(Nothing) Then
                vm.SaveProjectAsCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.O Then
            If vm.LoadProjectCommand.CanExecute(Nothing) Then
                vm.LoadProjectCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.N Then
            If vm.NewProjectCommand.CanExecute(Nothing) Then
                vm.NewProjectCommand.Execute(Nothing)
                e.Handled = True
            End If
        ElseIf e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso e.Key = Key.I Then
            If vm.BrowseSVGCommand.CanExecute(Nothing) Then
                vm.BrowseSVGCommand.Execute(Nothing)
                e.Handled = True
            End If
        End If

    End Sub


    Private Sub OpenMenu(sender As Object, e As MouseButtonEventArgs)
        Dim sp = DirectCast(sender, StackPanel)
        If sp.ContextMenu IsNot Nothing Then
            sp.ContextMenu.PlacementTarget = sp
            sp.ContextMenu.IsOpen = True
        End If
    End Sub

End Class

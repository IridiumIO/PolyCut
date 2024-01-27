Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Configuration
Imports System.IO
Imports System.Windows.Threading
Imports Wpf.Ui

Partial Public Class Application
    Private Shared ReadOnly _host As IHost = Host.CreateDefaultBuilder() _
        .ConfigureAppConfiguration(Sub(context, configBuilder)
                                       ' Set base path using IConfigurationBuilder
                                       configBuilder.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory))
                                   End Sub) _
        .ConfigureServices(Sub(context, services)

                               services.AddHostedService(Of ApplicationHostService)()

                               services.AddSingleton(Of CommandLineArgsService)()

                               ' Theme manipulation
                               services.AddSingleton(Of IThemeService, ThemeService)()

                               ' TaskBar manipulation
                               services.AddSingleton(Of ITaskBarService, TaskBarService)()
                               ' Service containing navigation, same as INavigationWindow... but without window
                               services.AddSingleton(Of INavigationService, NavigationService)()
                               services.AddSingleton(Of ISnackbarService, SnackbarService)()

                               ' Main window with navigation
                               services.AddSingleton(Of INavigationWindow, MainWindow)()
                               services.AddSingleton(Of MainViewModel)()
                               services.AddSingleton(Of MainWindow)()

                               ' Views and ViewModels
                               services.AddSingleton(Of SVGPage)()
                               services.AddSingleton(Of MonitorPage)()
                               services.AddSingleton(Of ExportPage)()
                               services.AddSingleton(Of ExportViewModel)()
                               services.AddSingleton(Of PreviewPage)()
                               services.AddSingleton(Of SettingsPage)()

                           End Sub) _
        .Build()

    Public Shared Function GetService(Of T As Class)() As T
        Return TryCast(_host.Services.GetService(GetType(T)), T)
    End Function

    Private Shadows Async Sub OnStartup(sender As Object, e As StartupEventArgs)
        Await _host.StartAsync()
    End Sub

    Private Shadows Async Sub OnExit(sender As Object, e As ExitEventArgs)

        Await _host.StopAsync()
        _host.Dispose()
    End Sub

    Private Sub OnDispatcherUnhandledException(sender As Object, e As DispatcherUnhandledExceptionEventArgs)
        ' For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    End Sub
End Class

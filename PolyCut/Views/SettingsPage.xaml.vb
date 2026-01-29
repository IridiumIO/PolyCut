Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports WPF.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Imports WPF.Ui.Abstractions.Controls
Class SettingsPage

    Public ReadOnly Property _viewModel As SettingsPageViewModel


    Sub New(viewmodel As SettingsPageViewModel)

        DataContext = viewmodel
        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    Private Sub ColorPickerControl_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        Dim mv As MainViewModel = Application.GetService(Of MainViewModel)()
        mv.UIConfiguration.GridConfig.GridBrush = (New BrushConverter()).ConvertToString(e.SelectedBrush)
    End Sub

    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)

    End Sub

End Class
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
        _viewModel = viewmodel
        InitializeComponent()


        If WineDetection.IsRunningUnderWine Then
            AddToStartMenuCheckBox.Visibility = Visibility.Collapsed
        End If

        Select Case _viewModel.MainVM.UIConfiguration.CanvasThemeColour
            Case "#FFFFFFFF"
                CanvasColourSelection.SelectedIndex = 0
            Case "#FF16181D"
                CanvasColourSelection.SelectedIndex = 1
            Case Else
                CanvasColourSelection.SelectedIndex = 2
        End Select

    End Sub

    Private Sub ColorPickerControl_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        _viewModel.MainVM.UIConfiguration.GridConfig.GridBrush = (New BrushConverter()).ConvertToString(e.SelectedBrush)
    End Sub

    Private Sub NumberBox_LostFocus(sender As Object, e As RoutedEventArgs)

    End Sub

    Private Sub PreviewPage_DrawingBrushColorSelector_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        _viewModel.MainVM.UIConfiguration.PreviewDrawingBrush = (New BrushConverter()).ConvertToString(e.SelectedBrush)
    End Sub

    Private Sub PreviewPage_TravelBrushColorSelector_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        _viewModel.MainVM.UIConfiguration.PreviewTravelBrush = (New BrushConverter()).ConvertToString(e.SelectedBrush)
    End Sub

    Private Sub PreviewPage_CursorBrushColorSelector_ColorSelected(sender As Object, e As ColorSelectedEventArgs)
        _viewModel.MainVM.UIConfiguration.PreviewCursorBrush = (New BrushConverter()).ConvertToString(e.SelectedBrush)
    End Sub

    Private Sub AddToStartMenuCheckBox_Checked(sender As Object, e As RoutedEventArgs)
        _viewModel.AddToStartMenu()
        _viewModel.MainVM.UIConfiguration.AddToStartMenu = True
    End Sub

    Private Sub AddToStartMenuCheckBox_Unchecked(sender As Object, e As RoutedEventArgs)
        _viewModel.RemoveFromStartMenu()
        _viewModel.MainVM.UIConfiguration.AddToStartMenu = False
    End Sub

    Private Sub ComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim selection As ComboBoxItem = TryCast(sender.SelectedItem, ComboBoxItem)

        If selection.Content.ToString() = "Light" Then
            _viewModel.MainVM.UIConfiguration.CanvasThemeColour = "#FFFFFFFF"
        ElseIf selection.Content.ToString() = "Dark" Then
            _viewModel.MainVM.UIConfiguration.CanvasThemeColour = "#FF16181D"
        Else
            _viewModel.MainVM.UIConfiguration.CanvasThemeColour = "#40646464"

        End If

    End Sub
End Class
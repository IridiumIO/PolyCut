Imports PolyCut.Shared

Public Class Tab_Configure

    Private _thresholdPreviewActive As Boolean = False

    Private _previewTextChangedHandler As RoutedEventHandler

    Private _mainVM As MainViewModel

    Public Sub New()
        InitializeComponent()
        _previewTextChangedHandler = AddressOf LabeledNumberBoxControl_TextChanged
        _mainVM = Application.GetService(Of MainViewModel)()
    End Sub

    Private Sub LabeledNumberBoxControl_MouseEnter(sender As Object, e As MouseEventArgs)

        AttachPreviewHandlers()
        BeginThresholdPreview()
        RefreshThresholdPreview()
    End Sub

    Private Sub LabeledNumberBoxControl_TextChanged(sender As Object, e As RoutedEventArgs)
        If Not _thresholdPreviewActive Then Return
        RefreshThresholdPreview()
    End Sub

    Private Sub LabeledNumberBoxControl_MouseLeave(sender As Object, e As MouseEventArgs)
        DetachPreviewHandlers()
        EndThresholdPreview()
    End Sub

    Private Sub LabeledNumberBoxControl_Unloaded(sender As Object, e As RoutedEventArgs)
        DetachPreviewHandlers()
        EndThresholdPreview()
    End Sub

    Private Sub RefreshThresholdPreview()
        If Not _thresholdPreviewActive Then Return

        ' Restore originals first
        For Each d In _mainVM.DrawableCollection
            Dim cc = TryCast(d.DrawableElement.Parent, ContentControl)
            If cc IsNot Nothing Then cc.Opacity = 1.0
        Next

        ApplyThresholdPreview()
    End Sub

    Private Sub BeginThresholdPreview()
        If _thresholdPreviewActive Then Return
        _thresholdPreviewActive = True

        ApplyThresholdPreview()
    End Sub

    Private Sub ApplyThresholdPreview()

        Dim t As Double
        Dim res = Double.TryParse(ShadingThresholdControl.Text, t)
        If Not res Then Return

        For Each d In _mainVM.DrawableCollection
            Dim brightness = GetBrightness01(d)

            If brightness < t Then
                Dim cc = TryCast(d.DrawableElement.Parent, ContentControl)
                If cc IsNot Nothing Then cc.Opacity = 0.2
            End If
        Next
    End Sub

    Private Sub EndThresholdPreview()
        If Not _thresholdPreviewActive Then Return
        _thresholdPreviewActive = False

        For Each d In _mainVM.DrawableCollection
            Dim cc = TryCast(d.DrawableElement.Parent, ContentControl)
            If cc IsNot Nothing Then cc.Opacity = 1.0
        Next

    End Sub

    Private Function GetBrightness01(d As IDrawable) As Double

        Dim color As Color = Colors.Black

        Dim brush = d.Fill
        If TypeOf brush Is SolidColorBrush Then
            color = DirectCast(brush, SolidColorBrush)?.Color
        End If
        Dim brightness = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0
        brightness = Math.Round(brightness, 3)

        Return brightness
    End Function

    Private Sub AttachPreviewHandlers()

        AddHandler ShadingThresholdControl.TextChanged, _previewTextChangedHandler

        ' Optional extra safety hooks

        AddHandler ShadingThresholdControl.Unloaded, AddressOf LabeledNumberBoxControl_Unloaded
    End Sub

    Private Sub DetachPreviewHandlers()
        If ShadingThresholdControl Is Nothing Then Return

        RemoveHandler ShadingThresholdControl.TextChanged, _previewTextChangedHandler
        RemoveHandler ShadingThresholdControl.Unloaded, AddressOf LabeledNumberBoxControl_Unloaded

    End Sub

End Class

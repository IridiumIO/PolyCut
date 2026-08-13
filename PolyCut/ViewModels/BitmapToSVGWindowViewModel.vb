Imports System.Drawing
Imports System.Threading

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input



Partial Public Class BitmapToSVGWindowViewModel : Inherits ObservableObject

    Private _WorkingSVGString As String
    Private _VTracerService As VTracerService

    <ObservableProperty> Private _OriginalImage As BitmapImage
    <ObservableProperty> Private _PreviewDrawing As Drawing
    <ObservableProperty> Private _PreviewCanvasSize As Size

    <ObservableProperty> Private _VTracerOptions As VTracerOptions

    <ObservableProperty> Private _RegionCount As Integer = 0
    <ObservableProperty> Private _NodeCount As Integer = 0

    <NotifyCanExecuteChangedFor(NameOf(UpdatePreviewCommand))>
    <ObservableProperty> Private _IsNotProcessing As Boolean = True

    <ObservableProperty> Private _BaseImagePath As String
    <ObservableProperty> Private _ResultSvgPath As String
    <ObservableProperty> Private _ExcludedRegionIndices As HashSet(Of Integer) = New HashSet(Of Integer)

    Public Event RequestClose(DialogResult As Boolean)


    Public Sub New(vtracerService As VTracerService)
        _VTracerService = vtracerService
        VTracerOptions = New VTracerOptions()
        AddHandler VTracerOptions.PropertyChanged, AddressOf OnVTracerOptionsChanged
    End Sub


    Public Async Sub Initialise()
        Dim img = New BitmapImage(New Uri(BaseImagePath))
        OriginalImage = img
        Await UpdatePreviewCommand.ExecuteAsync(Nothing)
    End Sub


    Private Async Sub OnVTracerOptionsChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        Await UpdatePreviewCommand.ExecuteAsync(Nothing)
    End Sub


    Private Function CanUpdatePreview() As Boolean
        Return IsNotProcessing
    End Function


    <RelayCommand(IncludeCancelCommand:=True)>
    Private Async Function UpdatePreview(ctx As CancellationToken) As Task
        IsNotProcessing = False

        Try
            Dim result = Await _VTracerService.RunConversionAsync(VTracerOptions, BaseImagePath, _VTracerService.GetWorkingSvgPath(), ctx)

            PreviewCanvasSize = result.CanvasSize
            PreviewDrawing = result.Drawing
            RegionCount = result.RegionCount
            NodeCount = result.NodeCount
            _WorkingSVGString = result.SvgContent

        Catch ex As OperationCanceledException
            Debug.WriteLine("Preview update canceled.")
        Catch ex As Exception
            Debug.WriteLine($"Error during preview update: {ex.Message}")
        End Try

        IsNotProcessing = True
    End Function


    <RelayCommand>
    Private Sub Finish()
        ResultSvgPath = _VTracerService.FinalizeSvg(BaseImagePath, _WorkingSVGString, ExcludedRegionIndices)
        RaiseEvent RequestClose(True)
    End Sub


End Class

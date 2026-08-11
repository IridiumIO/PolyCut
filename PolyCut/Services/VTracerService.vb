Imports System.ComponentModel
Imports System.IO

Imports CommunityToolkit.Mvvm.ComponentModel

Public Class VTracerService


    Public Function ConvertPNGToSVG(pngFilePath As String) As String


        Dim vm = Application.GetService(Of BitmapToSVGWindowViewModel)
        vm.Cleanup()
        vm.BaseImagePath = pngFilePath

        Dim window = New BitmapToSVGWindow(vm)
        vm.Initialise()
        Dim result = window.ShowDialog()

        If result Then
            Return vm.ResultSvgPath
        End If

        Return Nothing

    End Function



End Class


Public Enum ClusteringMethod
    <Description("Colour Cluster")> ColorCluster
    <Description("Watershed")> Watershed
    <Description("Black & White")> BW
End Enum

Public Enum HeiarchicalMethod
    <Description("Cutout")> Cutout
    <Description("Cutout V2")> CutoutV2
    <Description("Stacked")> Stacked
End Enum

Public Enum TracingMode
    Spline
    Polygon
    Pixel
End Enum

Public Enum OptimizationMode
    None
    QuantizeAndCleanup
    QuantizeAndCleanupAndShorthands
End Enum

Public Class VTracerOptions : Inherits ObservableObject
    <ObservableProperty> Private _Clustering As ClusteringMethod = ClusteringMethod.ColorCluster
    <ObservableProperty> Private _Hierarchical As HeiarchicalMethod = HeiarchicalMethod.CutoutV2
    <ObservableProperty> Private _Mode As TracingMode = TracingMode.Spline
    <ObservableProperty> Private _FilterSpeckle As Integer = 8
    <ObservableProperty> Private _ColorPrecision As Integer = 6
    <ObservableProperty> Private _GradientStep As Integer = 16
    <ObservableProperty> Private _Simplify As Double = 0.5 '0 = disabled
    <ObservableProperty> Private _PathPrecision As Integer = 3
    <ObservableProperty> Private _MaxColors As Integer = 0 '0 = no limit
    <ObservableProperty> Private _Optimize As OptimizationMode = OptimizationMode.QuantizeAndCleanup
    <ObservableProperty> Private _WatershedDetail As Integer = 128
    <ObservableProperty> Private _BWThreshold As Integer = 128
    <ObservableProperty> Private _AdaptiveSampling As Boolean = False
    <ObservableProperty> Private _AdaptiveSamplingWindow As Integer = 0 'px. Implies Adaptive Sampling is enabled
    <ObservableProperty> Private _AdapativeSensitivity As Integer = 16 '% below local mean, 0-100. 
End Class


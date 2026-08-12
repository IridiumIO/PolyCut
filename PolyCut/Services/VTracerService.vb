Imports System.ComponentModel
Imports System.Text.RegularExpressions
Imports System.Threading

Imports CommunityToolkit.Mvvm.ComponentModel

Imports SharpVectors.Converters
Imports SharpVectors.Renderers.Wpf

Public Class VTracerService


    ' ======================================================================
    '  UI Entry point
    ' ======================================================================


    Public Function ConvertRasterToSVG(rasterFilePath As String) As String
        CleanupWorkingFile()

        Dim vm = Application.GetService(Of BitmapToSVGWindowViewModel)
        vm.BaseImagePath = rasterFilePath

        Dim window = New BitmapToSVGWindow(vm)
        vm.Initialise()
        Dim result = window.ShowDialog()

        CleanupWorkingFile()

        If result Then Return vm.ResultSvgPath

        Return Nothing

    End Function

    ' ======================================================================
    '  Conversion Pipeline
    ' ======================================================================

    Public Async Function RunConversionAsync(options As VTracerOptions, inputPath As String, outputPath As String, ctx As CancellationToken) As Task(Of VTracerConversionResult)

        Dim args = BuildVTracerArguments(options, inputPath, outputPath)

        Dim procOutput = Await RunEmbeddedExecutable.Run("vtracer.exe", args, ctx)

        Dim svgContent = Await IO.File.ReadAllTextAsync(outputPath, ctx)

        Dim drawing = RenderSvg(outputPath)
        Dim canvasSize = GetDeclaredCanvasSize(outputPath)

        Dim analysis = AnalyseSvg(svgContent)

        Return New VTracerConversionResult With {
            .SvgContent = svgContent,
            .SvgPath = outputPath,
            .Drawing = drawing,
            .CanvasSize = canvasSize,
            .RegionCount = analysis.RegionCount,
            .NodeCount = analysis.NodeCount
        }

    End Function

    Private Function BuildVTracerArguments(opts As VTracerOptions, inputPath As String, outputPath As String) As String

        Dim clustering As String
        Select Case opts.Clustering
            Case ClusteringMethod.Watershed
                clustering = "watershed"
            Case ClusteringMethod.ColorCluster
                clustering = "color-cluster"
            Case Else
                clustering = "bw"
        End Select


        Dim args = $"
--clustering {clustering} 
--hierarchical {opts.Hierarchical.ToString().ToLower()} 
--mode {opts.Mode.ToString().ToLower()} 
--filter-speckle {opts.FilterSpeckle} 
--color-precision {opts.ColorPrecision}
--gradient-step {opts.GradientStep}
{If(opts.Simplify > 0, "--simplify " & opts.Simplify, "")} 
--path-precision {opts.PathPrecision} 
{If(opts.MaxColors > 0, "--max-colors " & opts.MaxColors, "")}
--optimize {CInt(opts.Optimize)}
--watershed-detail {opts.WatershedDetail}
--threshold {opts.BWThreshold}
{If(opts.AdaptiveSampling, "--adaptive", "")}
{If(opts.AdaptiveSampling, "--adaptive-window " & opts.AdaptiveSamplingWindow, "")}
{If(opts.AdaptiveSampling, "--adaptive-t " & opts.AdapativeSensitivity, "")}
-i ""{inputPath}"" -o ""{outputPath}"""


        args = args.Replace(Environment.NewLine, " ").Trim()
        Debug.WriteLine(args)

        Return args
    End Function


    ' ======================================================================
    '  SVG Rendering
    ' ======================================================================
    Public Function RenderSvg(svgPath As String) As Drawing
        Dim settings = New WpfDrawingSettings With {
            .IncludeRuntime = False,
            .TextAsGeometry = False
        }

        Dim converter = New FileSvgReader(settings)
        Return converter.Read(svgPath)
    End Function

    Private Function GetDeclaredCanvasSize(svgPath As String) As System.Drawing.Size
        Try
            Dim doc = XDocument.Load(svgPath)
            Dim root = doc.Root

            Dim widthAttr = root.Attribute("width")?.Value
            Dim heightAttr = root.Attribute("height")?.Value

            Dim width As Double
            Dim height As Double
            If Double.TryParse(Regex.Match(widthAttr, "[\d.]+").Value, width) AndAlso
               Double.TryParse(Regex.Match(heightAttr, "[\d.]+").Value, height) Then
                Return New System.Drawing.Size(width, height)
            End If

        Catch ex As Exception
            Debug.WriteLine($"Failed to parse SVG canvas size: {ex.Message}")
        End Try

        Return New System.Drawing.Size(0, 0)
    End Function


    ' ======================================================================
    '  SVG Analysis
    ' ======================================================================

    Public Shared Function AnalyseSvg(svgContent As String) As (RegionCount As Integer, NodeCount As Integer)
        Try
            Dim doc = XDocument.Parse(svgContent)
            Dim ns = doc.Root.GetDefaultNamespace()

            Dim pathElements = doc.Descendants(ns + "path").ToList()

            Dim totalNodes = 0
            For Each pathEl In pathElements
                Dim d = pathEl.Attribute("d")?.Value
                If Not String.IsNullOrEmpty(d) Then
                    totalNodes += CountPathNodes(d)
                End If
            Next

            Return (pathElements.Count, totalNodes)

        Catch ex As Exception
            Debug.WriteLine($"Failed to analyze SVG: {ex.Message}")
            Return (0, 0)
        End Try
    End Function

    Private Shared Function CountPathNodes(d As String) As Integer
        Dim matches = Regex.Matches(d, "[MmLlHhVvCcSsQqTtAaZz]")
        Return matches.Count
    End Function

    ' ======================================================================
    '  Finalisation and Cleanup
    ' ======================================================================
    Public Function FinalizeSvg(svgContent As String, excludedIndices As HashSet(Of Integer)) As String
        Dim tempSvgPath = IO.Path.Combine(IO.Path.GetTempPath(), $"polycut-{Guid.NewGuid:N}.svg")

        Dim svgToWrite As String
        If excludedIndices IsNot Nothing AndAlso excludedIndices.Count > 0 Then
            svgToWrite = RemoveExcludedPaths(svgContent, excludedIndices)
        Else
            svgToWrite = svgContent
        End If

        svgToWrite = AssignIncrementingPathNames(svgToWrite)

        IO.File.WriteAllText(tempSvgPath, svgToWrite)
        Return tempSvgPath
    End Function

    Private Shared Function AssignIncrementingPathNames(svgContent As String) As String
        Try
            Dim doc = XDocument.Parse(svgContent)
            Dim ns = doc.Root.GetDefaultNamespace()

            Dim counter = 1
            For Each pathEl In doc.Descendants(ns + "path")
                pathEl.SetAttributeValue("id", $"path{counter}")
                counter += 1
            Next

            Return doc.ToString()
        Catch ex As Exception
            Debug.WriteLine($"Failed to assign path names: {ex.Message}")
            Return svgContent
        End Try
    End Function

    Private Shared Function RemoveExcludedPaths(svgContent As String, excludedIndices As HashSet(Of Integer)) As String
        Try
            Dim doc = XDocument.Parse(svgContent)
            Dim ns = doc.Root.GetDefaultNamespace()

            Dim paths = doc.Descendants(ns + "path").ToList()

            ' Remove excluded indices in reverse order otherwise layer ordering will be reversed
            For Each idx In excludedIndices.OrderByDescending(Function(i) i)
                If idx >= 0 AndAlso idx < paths.Count Then
                    paths(idx).Remove()
                End If
            Next

            Return doc.ToString()
        Catch ex As Exception
            Debug.WriteLine($"Failed to remove excluded paths: {ex.Message}")
            Return svgContent
        End Try
    End Function

    ' ---- Temporary file lifecycle ---------------------------------------

    Public Function GetWorkingSvgPath() As String
        Return IO.Path.Combine(IO.Path.GetTempPath(), "polycut-working.svg")
    End Function

    Public Sub CleanupWorkingFile()
        Dim tempPath = GetWorkingSvgPath()
        If Not IO.File.Exists(tempPath) Then Return

        Try
            IO.File.Delete(tempPath)
        Catch ex As Exception
            Debug.WriteLine($"Failed to delete temporary SVG file: {ex.Message}")
        End Try

    End Sub





End Class

Public Class VTracerConversionResult
    Public Property SvgContent As String
    Public Property SvgPath As String
    Public Property Drawing As Drawing
    Public Property CanvasSize As System.Drawing.Size
    Public Property RegionCount As Integer
    Public Property NodeCount As Integer
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


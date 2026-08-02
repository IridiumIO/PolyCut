Imports System.Drawing
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Threading

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports SharpVectors.Converters
Imports SharpVectors.Renderers.Wpf

Imports Svg

Imports Vecto.Core


Public Enum ClusteringMethod
    ColorCluster
    Watershed
    BW
End Enum

Public Enum HeiarchicalMethod
    Cutout
    Stacked
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
    <ObservableProperty> Private _Hierarchical As HeiarchicalMethod = HeiarchicalMethod.Cutout
    <ObservableProperty> Private _Mode As TracingMode = TracingMode.Spline
    <ObservableProperty> Private _FilterSpeckle As Integer = 8
    <ObservableProperty> Private _ColorPrecision As Integer = 6
    <ObservableProperty> Private _GradientStep As Integer = 16
    <ObservableProperty> Private _Simplify As Double = 0.0 '0 = disabled
    <ObservableProperty> Private _PathPrecision As Integer = 3
    <ObservableProperty> Private _MaxColors As Integer = 0 '0 = no limit
    <ObservableProperty> Private _Optimize As OptimizationMode = OptimizationMode.QuantizeAndCleanup
    <ObservableProperty> Private _WatershedDetail As Integer = 128
    <ObservableProperty> Private _AdaptiveSampling As Boolean = False
    <ObservableProperty> Private _AdaptiveSamplingWindow As Integer = 0 'px. Implies Adaptive Sampling is enabled
    <ObservableProperty> Private _AdapativeSensitivity As Integer = 16 '% below local mean, 0-100. 
End Class




Partial Public Class BitmapToSVGWindowViewModel : Inherits ObservableObject

    <ObservableProperty> Private _OriginalImage As BitmapImage
    <ObservableProperty> Private _PreviewImage As DrawingImage
    <ObservableProperty> Private _PreviewDrawing As Drawing
    <ObservableProperty> Private _PreviewCanvasSize As Size

    <ObservableProperty> Private _VTracerOptions As VTracerOptions

    <ObservableProperty> Private _RegionCount As Integer = 0
    <ObservableProperty> Private _NodeCount As Integer = 0

    <ObservableProperty>
    <NotifyCanExecuteChangedFor(NameOf(UpdatePreviewCommand))>
    Private _IsNotProcessing As Boolean = True

    Public Property BaseImagePath As String
    Public Property ResultSvgPath As String


    Private _WorkingSVGString As String
    Public Event RequestClose(DialogResult As Boolean)

    Public Sub New()
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
        Dim tempSvgPath = IO.Path.Combine(IO.Path.GetTempPath(), $"polycut-working.svg")

        Dim args = BuildVTracerArgs(VTracerOptions, BaseImagePath, tempSvgPath)

        Try
            Dim result = Await RunEmbeddedExecutable.Run("vtracer.exe", args, ctx)
            Debug.WriteLine(result)

            Dim svgX = Await IO.File.ReadAllTextAsync(tempSvgPath, ctx)

            PreviewImage = Render(tempSvgPath) 'Render has a side effect of setting PreviewDrawing, used for hit testing. TODO Refactor
            AnalyzeSvg(svgX)

            _WorkingSVGString = svgX
        Catch ex As OperationCanceledException
            Debug.WriteLine("Preview update canceled.")
        Catch ex As Exception
            Debug.WriteLine($"Error during preview update: {ex.Message}")

        End Try

        IsNotProcessing = True

    End Function


    Private Function BuildVTracerArgs(opts As VTracerOptions, inputPath As String, outputPath As String) As String

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
{If(opts.AdaptiveSampling, "--adaptive", "")}
{If(opts.AdaptiveSampling, "--adaptive-window " & opts.AdaptiveSamplingWindow, "")}
{If(opts.AdaptiveSampling, "--adaptive-t " & opts.AdapativeSensitivity, "")}
-i ""{inputPath}"" -o ""{outputPath}"""


        args = args.Replace(Environment.NewLine, " ").Trim()
        Debug.WriteLine(args)

        Return args
    End Function




    Private Sub AnalyzeSvg(svgContent As String)

        Try
            Dim doc = XDocument.Parse(svgContent)
            Dim ns = doc.Root.GetDefaultNamespace()

            Dim pathElements = doc.Descendants(ns + "path").ToList()
            RegionCount = pathElements.Count

            Dim totalNodes = 0
            For Each pathEl In pathElements
                Dim d = pathEl.Attribute("d")?.Value
                If Not String.IsNullOrEmpty(d) Then
                    totalNodes += CountPathNodes(d)
                End If
            Next

            NodeCount = totalNodes

        Catch ex As Exception
            Debug.WriteLine($"Failed to analyze SVG: {ex.Message}")
            RegionCount = 0
            NodeCount = 0
        End Try

    End Sub

    Private Function CountPathNodes(d As String) As Integer
        Dim matches = Regex.Matches(d, "[MmLlHhVvCcSsQqTtAaZz]")
        Return matches.Count

    End Function



    Public Function Render(svg As String) As DrawingImage

        Dim settings = New WpfDrawingSettings()

        settings.IncludeRuntime = False
        settings.TextAsGeometry = False


        Dim converter = New FileSvgReader(settings)

        Dim drawing = converter.Read(svg)

        Dim canvasSize = GetDeclaredCanvasSize(svg)
        PreviewCanvasSize = canvasSize
        drawing = NormalizeDrawing(drawing, canvasSize.Width, canvasSize.Height) 'Pretty sure this is needed because of SharpVectors btu I can't prove it. 

        PreviewDrawing = drawing 'SIDE EFFECT: TODO refactor to make it clearer
        Debug.WriteLine($"Drawing.Bounds = {drawing.Bounds}")
        Return New DrawingImage(drawing)

    End Function

    <RelayCommand>
    Private Sub Finish()

        Dim tempSvgPath = IO.Path.Combine(IO.Path.GetTempPath(), $"polycut-{Guid.NewGuid:N}.svg")
        IO.File.WriteAllText(tempSvgPath, _WorkingSVGString)
        ResultSvgPath = tempSvgPath
        RaiseEvent RequestClose(True)
        Cleanup()
    End Sub

    Private Function GetDeclaredCanvasSize(svgPath As String) As Size
        Try
            Dim doc = XDocument.Load(svgPath)
            Dim root = doc.Root

            Dim widthAttr = root.Attribute("width")?.Value
            Dim heightAttr = root.Attribute("height")?.Value

            Dim width As Double
            Dim height As Double
            If Double.TryParse(Regex.Match(widthAttr, "[\d.]+").Value, width) AndAlso
               Double.TryParse(Regex.Match(heightAttr, "[\d.]+").Value, height) Then
                Return New Size(width, height)
            End If

        Catch ex As Exception
            Debug.WriteLine($"Failed to parse SVG canvas size: {ex.Message}")
        End Try

        Return New Size(0, 0)
    End Function

    Private _boundsSentinel As GeometryDrawing

    Private Function NormalizeDrawing(rawDrawing As Drawing, canvasWidth As Double, canvasHeight As Double) As Drawing
        If canvasWidth <= 0 OrElse canvasHeight <= 0 Then Return rawDrawing

        Dim originalBounds = rawDrawing.Bounds

        Dim shiftedContent As New DrawingGroup()
        shiftedContent.Transform = New TranslateTransform(-originalBounds.X, -originalBounds.Y)
        shiftedContent.Children.Add(rawDrawing)

        _boundsSentinel = New GeometryDrawing(
        System.Windows.Media.Brushes.Transparent,
        Nothing,
        New RectangleGeometry(New Rect(0, 0, canvasWidth, canvasHeight)))

        Dim root As New DrawingGroup()
        root.Children.Add(_boundsSentinel)
        root.Children.Add(shiftedContent)

        Return root
    End Function

    Public ReadOnly Property BoundsSentinel As GeometryDrawing
        Get
            Return _boundsSentinel
        End Get
    End Property


    Public Sub Cleanup()

        Dim tempPath = IO.Path.Combine(IO.Path.GetTempPath(), $"polycut-working.svg")

        If Not IO.File.Exists(tempPath) Then Return
        Try
            IO.File.Delete(tempPath)
        Catch ex As Exception
            Debug.WriteLine($"Failed to delete temporary SVG file: {ex.Message}")
        End Try
    End Sub

End Class


Public Module SvgHitTestHelper

    Public Function FlattenDrawing(drawing As Drawing) As List(Of (Geometry As Geometry, Source As GeometryDrawing))
        Dim results As New List(Of (Geometry, GeometryDrawing))
        If drawing IsNot Nothing Then Flatten(drawing, Matrix.Identity, results)
        Return results
    End Function

    Private Sub Flatten(drawing As Drawing, transform As Matrix, results As List(Of (Geometry, GeometryDrawing)))
        Select Case True
            Case TypeOf drawing Is DrawingGroup
                Dim group = CType(drawing, DrawingGroup)
                Dim childTransform = transform
                If group.Transform IsNot Nothing Then
                    childTransform = Matrix.Multiply(group.Transform.Value, transform)
                End If
                For Each child In group.Children
                    Flatten(child, childTransform, results)
                Next

            Case TypeOf drawing Is GeometryDrawing
                Dim gd = CType(drawing, GeometryDrawing)
                If gd.Geometry IsNot Nothing Then
                    Dim geom = gd.Geometry.Clone()
                    geom.Transform = New MatrixTransform(transform)
                    results.Add((geom, gd))
                End If
        End Select
    End Sub

End Module
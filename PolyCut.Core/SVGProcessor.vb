Imports Svg

Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Shapes

Public Class SVGProcessor


    Shared Function colourToHex(colour As Drawing.Color) As String
        If colour = Nothing Then Return Nothing
        Return "#" & colour.R.ToString("X2") & colour.G.ToString("X2") & colour.B.ToString("X2")
    End Function

    Public Shared Function SVGColorBullshitFixer(svgcolour As SvgPaintServer) As String

        Dim colour As String

        If svgcolour Is Nothing OrElse svgcolour.ToString() = "none" OrElse svgcolour.GetType().Name.Contains("None") Then
            colour = Nothing
        Else
            Dim casted = TryCast(svgcolour, SvgColourServer)?.Colour
            colour = If(casted.HasValue, colourToHex(casted), Nothing)
        End If

        Return colour
    End Function


    Shared Function CompileElementAndGetFigures(element As IPathBasedElement, svgVisualElement As SvgVisualElement, cfg As ProcessorConfiguration) As List(Of List(Of Line))


        Dim configClr = Drawing.ColorTranslator.FromHtml(cfg.ExtractionColor)

        Dim fillcolour = SVGColorBullshitFixer(svgVisualElement.Fill)
        Dim strokecolour = SVGColorBullshitFixer(svgVisualElement.Stroke)

        If cfg.ExtractOneColour = False OrElse String.IsNullOrWhiteSpace(cfg.ExtractionColor) OrElse fillcolour = colourToHex(configClr) OrElse strokecolour = colourToHex(configClr) Then
            element.CompileFromSVGElement(svgVisualElement, cfg)
            Return element.Figures
        End If

        Return New List(Of List(Of Line))

    End Function

    Shared Function CompileElementAndGetFiguresFlattened(element As IPathBasedElement, svgVisualElement As SvgVisualElement, cfg As ProcessorConfiguration) As List(Of Line)


        Dim configClr = Drawing.ColorTranslator.FromHtml(cfg.ExtractionColor)

        Dim fillcolour = SVGColorBullshitFixer(svgVisualElement.Fill)
        Dim strokecolour = SVGColorBullshitFixer(svgVisualElement.Stroke)

        If cfg.ExtractOneColour = False OrElse String.IsNullOrWhiteSpace(cfg.ExtractionColor) OrElse fillcolour = colourToHex(configClr) OrElse strokecolour = colourToHex(configClr) Then
            element.CompileFromSVGElement(svgVisualElement, cfg)
            Return element.Figures.SelectMany(Of Line)(Function(x) x).ToList
        End If
        Return New List(Of Line)

    End Function

    Shared Async Function LoopElements(element As SvgVisualElement, processorConfiguration As ProcessorConfiguration) As Task(Of List(Of List(Of Line)))

        Dim generatedLines As New List(Of List(Of Line))

        Select Case element.GetType
            Case GetType(SvgRectangle)
                generatedLines.AddRange(CompileElementAndGetFigures(New RectangleElement, element, processorConfiguration))

            Case GetType(SvgEllipse)
                generatedLines.AddRange(CompileElementAndGetFigures(New EllipseElement, element, processorConfiguration))

            Case GetType(SvgCircle)
                generatedLines.AddRange(CompileElementAndGetFigures(New CircleElement, element, processorConfiguration))

            'For SVGPath and SVGText we need to flatten the lines so that the fillprocessor can correctly remove internal elements
            'Unfortunately this means it needs to be undone in the overcut processor; need to find a better way to do this.
            Case GetType(SvgPath)
                generatedLines.Add(CompileElementAndGetFiguresFlattened(New PathElement, element, processorConfiguration))

            Case GetType(SvgText)
                generatedLines.Add(CompileElementAndGetFiguresFlattened(New TextElement, element, processorConfiguration))
            Case GetType(SvgLine)
                generatedLines.AddRange(CompileElementAndGetFigures(New LineElement, element, processorConfiguration))
            Case Else
                Debug.WriteLine(element.GetType.ToString)

        End Select


        For Each child In element.Children.Where(Function(c) TypeOf (c) Is SvgVisualElement)

            If element.Transforms IsNot Nothing Then
                child.Transforms = If(child.Transforms, New Transforms.SvgTransformCollection)
                child.Transforms.InsertRange(0, element.Transforms)
                'child.Transforms.AddRange(element.Transforms)

            End If

            generatedLines.AddRange(Await LoopElements(child, processorConfiguration))

        Next

        Return generatedLines

    End Function



    Shared Async Function ProcessSVGVisualElements(elements As List(Of SvgVisualElement), cfg As ProcessorConfiguration) As Task(Of List(Of List(Of Line)))

        Dim compiledLines As New List(Of List(Of Line))

        For Each element In elements
            compiledLines.AddRange(Await LoopElements(element, cfg))
        Next

        compiledLines.RemoveAll(Function(lset) lset.Count = 0)
        Return compiledLines

    End Function


    'We need to apply transforms from the parent document to the child elements.
    'Note we do NOT loop through the children of the child elements here, as they will be processed in the main loop.
    Private Shared Sub ApplyTransformsToChildren(ByRef svgDoc As SvgDocument)

        For Each child In svgDoc.Children.OfType(Of SvgVisualElement)
            If svgDoc.Transforms IsNot Nothing Then
                child.Transforms = If(child.Transforms, New Transforms.SvgTransformCollection)
                child.Transforms.InsertRange(0, svgDoc.Transforms)
                ' child.Transforms.AddRange(svgDoc.Transforms)
            End If
        Next

    End Sub

    Public Shared Async Function ProcessSVG(svgText As String, config As ProcessorConfiguration) As Task(Of List(Of List(Of Line)))

        Dim svgDoc = SvgDocument.FromSvg(Of SvgDocument)(svgText)

        ApplyTransformsToChildren(svgDoc)

        Dim visualElements As List(Of SvgVisualElement) = svgDoc.Children.OfType(Of SvgVisualElement).ToList

        Dim compiledLines = Await ProcessSVGVisualElements(visualElements, config)

        Return compiledLines

    End Function

    Public Shared Function ProcessSVGFile(svgFile As String, config As ProcessorConfiguration)
        Dim svgText = IO.File.ReadAllText(svgFile)
        Return ProcessSVG(svgText, config)
    End Function


End Class

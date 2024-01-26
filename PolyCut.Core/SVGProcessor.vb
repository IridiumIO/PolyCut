﻿Imports Svg

Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Shapes

Public Class SVGProcessor


    Shared Function CompileElementAndGetFigures(element As IPathBasedElement, svgVisualElement As SvgVisualElement, cfg As ProcessorConfiguration) As List(Of List(Of Line))

        element.CompileFromSVGElement(svgVisualElement, cfg)
        Return element.Figures

    End Function

    Shared Function CompileElementAndGetFiguresFlattened(element As IPathBasedElement, svgVisualElement As SvgVisualElement, cfg As ProcessorConfiguration) As List(Of Line)

        element.CompileFromSVGElement(svgVisualElement, cfg)
        Return element.Figures.SelectMany(Of Line)(Function(x) x).ToList

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
            Case GetType(SvgPath)
                generatedLines.Add(CompileElementAndGetFiguresFlattened(New PathElement, element, processorConfiguration))

            Case GetType(SvgText)
                generatedLines.Add(CompileElementAndGetFiguresFlattened(New TextElement, element, processorConfiguration))

            Case Else
                Debug.WriteLine(element.GetType.ToString)

        End Select


        For Each child In element.Children.Where(Function(c) TypeOf (c) Is SvgVisualElement)

            If element.Transforms IsNot Nothing Then
                child.Transforms = If(child.Transforms, New Transforms.SvgTransformCollection)
                child.Transforms.InsertRange(0, element.Transforms)
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


        Return compiledLines

    End Function


    'We need to apply transforms from the parent document to the child elements.
    'Note we do NOT loop through the children of the child elements here, as they will be processed in the main loop.
    Private Shared Sub ApplyTransformsToChildren(ByRef svgDoc As SvgDocument)

        For Each child In svgDoc.Children.OfType(Of SvgVisualElement)
            If svgDoc.Transforms IsNot Nothing Then
                child.Transforms = If(child.Transforms, New Transforms.SvgTransformCollection)
                child.Transforms.InsertRange(0, svgDoc.Transforms)
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
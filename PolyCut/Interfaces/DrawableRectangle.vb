Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Class DrawableRectangle : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Rectangle)
        DrawableElement = element
        VisualName = "Rectangle"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Rectangle)

        Dim fillServer As SvgColourServer = Nothing
        Dim strokeServer As SvgColourServer = Nothing
        Dim strokeW As Single = 0.001F

        Try
            fillServer = SvgHelpers.BrushToSvgColourServer(Me.Fill)
        Catch
        End Try

        ' Only set stroke if thickness > 0 and stroke is not Nothing
        If Me.StrokeThickness > 0.001 AndAlso Me.Stroke IsNot Nothing Then
            Try
                strokeServer = SvgHelpers.BrushToSvgColourServer(Me.Stroke)
                strokeW = CSng(Me.StrokeThickness)
            Catch
            End Try
        End If

        Dim rect As New SvgRectangle With {
            .X = 0,
            .Y = 0,
            .Width = rt.ActualWidth,
            .Height = rt.ActualHeight,
            .FillOpacity = 0.001,
            .Fill = If(fillServer, New SvgColourServer(System.Drawing.Color.White)),
            .StrokeLineCap = SvgStrokeLineCap.Round
        }

        ' Only set stroke properties if we have a stroke
        If strokeServer IsNot Nothing Then
            rect.Stroke = strokeServer
            rect.StrokeWidth = strokeW
        End If

        Return rect

    End Function


    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class


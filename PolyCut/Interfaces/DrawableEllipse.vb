Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Class DrawableEllipse : Inherits BaseDrawable : Implements IDrawable

    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName

    Public Sub New(element As Ellipse)
        DrawableElement = element
        VisualName = "Ellipse"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim rt = CType(DrawableElement, Ellipse)

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

        Dim ellipse As New SvgEllipse With {
            .CenterX = rt.ActualWidth / 2,
            .CenterY = rt.ActualHeight / 2,
            .RadiusX = DrawableElement.ActualWidth / 2,
            .RadiusY = DrawableElement.ActualHeight / 2,
            .FillOpacity = 0.001,
            .Fill = If(fillServer, Nothing),
            .StrokeLineCap = SvgStrokeLineCap.Round
        }

        ' Only set stroke properties if we have a stroke
        If strokeServer IsNot Nothing Then
            ellipse.Stroke = strokeServer
            ellipse.StrokeWidth = strokeW
        End If

        Return ellipse

    End Function

    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Dim sx = component.BakeTransforms(DrawableElement)
        Return sx

    End Function
End Class


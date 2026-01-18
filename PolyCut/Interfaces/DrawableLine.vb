Imports PolyCut.Shared

Imports Svg

Public Class DrawableLine : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Line)
        DrawableElement = element
        VisualName = "Line"
        Name = VisualName
    End Sub

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim ln = CType(DrawableElement, Line)

        Dim strokeServer As SvgColourServer = Nothing
        Dim strokeW As Single = 0.001F

        ' Lines must have a stroke to be visible
        If Me.StrokeThickness > 0.001 AndAlso Me.Stroke IsNot Nothing Then
            Try
                strokeServer = SvgHelpers.BrushToSvgColourServer(Me.Stroke)
                strokeW = CSng(Me.StrokeThickness)
            Catch
                strokeServer = New SvgColourServer(System.Drawing.Color.Black)
            End Try
        Else
            ' Default stroke for lines (they need one to be visible)
            strokeServer = New SvgColourServer(System.Drawing.Color.Black)
            strokeW = CSng(If(Me.StrokeThickness > 0, Me.StrokeThickness, 0.001))
        End If

        Dim svgLine As New SvgLine With {
            .StartX = ln.X1,
            .StartY = ln.Y1,
            .EndX = ln.X2,
            .EndY = ln.Y2,
            .Stroke = strokeServer,
            .StrokeWidth = strokeW
        }

        Return svgLine

    End Function




    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = DrawingToSVG().DeepCopy

        Return component.BakeTransforms(DrawableElement)

    End Function
End Class

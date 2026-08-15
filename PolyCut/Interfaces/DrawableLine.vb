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

        ' Lines must have a stroke to be visible - fall back to black when the drawable
        ' stroke can't be converted (transparent/none/thickness 0).
        Dim strokeServer As SvgColourServer = CreateSvgStrokeServer()
        Dim strokeW As Single = CSng(If(Me.StrokeThickness > 0, Me.StrokeThickness, 0.001))
        If strokeServer Is Nothing Then
            strokeServer = New SvgColourServer(System.Drawing.Color.Black)
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




End Class

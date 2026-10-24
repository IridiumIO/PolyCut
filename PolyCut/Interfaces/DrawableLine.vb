Imports PolyCut.Shared

Imports Svg

Public Class DrawableLine : Inherits BaseDrawable : Implements IDrawable


    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
    Public Sub New(element As Shape)
        DrawableElement = GeometricShape.FromShape(element)
        VisualName = "Line"
        Name = VisualName
    End Sub

    Public Overrides Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG

        Dim geometric = TryCast(DrawableElement, GeometricShape)
        Dim points = DirectCast(geometric.LocalGeometry(), LineGeometry)

        ' Lines must have a stroke to be visible - fall back to black when the drawable
        ' stroke can't be converted (transparent/none/thickness 0).
        Dim strokeServer As SvgColourServer = CreateSvgStrokeServer()
        Dim strokeW As Single = CSng(Me.StrokeThickness)
        If strokeServer Is Nothing Then
            strokeServer = New SvgColourServer(System.Drawing.Color.Black)
        End If

        Dim svgLine As New SvgLine With {
            .StartX = points.Transform.Transform(points.StartPoint).X,
            .StartY = points.Transform.Transform(points.StartPoint).Y,
            .EndX = points.Transform.Transform(points.EndPoint).X,
            .EndY = points.Transform.Transform(points.EndPoint).Y,
            .Stroke = strokeServer,
            .StrokeWidth = strokeW
        }

        Return svgLine

    End Function




End Class

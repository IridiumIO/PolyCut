Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.[Shared]

Imports Svg

Public Class CircleElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of GeoLine) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of GeoLine)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As List(Of List(Of GeoLine)) Implements IPathBasedElement.Figures

    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled

    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim circleElement = DirectCast(element, SvgCircle)
        Config = cfg

        Dim fillcolor = ColorAndBrushHelpers.SVGPaintServerToString(element.Fill)

        Dim eGeo As New EllipseGeometry(New Point(circleElement.CenterX, circleElement.CenterY), circleElement.Radius, circleElement.Radius)
        Geo = eGeo.GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Figures = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()

        For fi = 0 To Figures.Count - 1
            For li = 0 To Figures(fi).Count - 1
                Dim ln = Figures(fi)(li)
                ln = ln.WithTag(fillcolor)
                Figures(fi)(li) = ln
            Next
        Next


    End Sub
End Class

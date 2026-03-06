Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Imports PolyCut.[Shared]

Imports Svg

Public Class EllipseElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of GeoLine) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of GeoLine)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As List(Of List(Of GeoLine)) Implements IPathBasedElement.Figures
    Public Property IsFilled As Boolean = False Implements IPathBasedElement.IsFilled
    Public Property FillColor As String Implements IPathBasedElement.FillColor

    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim ellipse = DirectCast(element, SvgEllipse)
        Config = cfg
        Dim eGeo As New EllipseGeometry(New Point(ellipse.CenterX, ellipse.CenterY), ellipse.RadiusX, ellipse.RadiusY)

        FillColor = ColorAndBrushHelpers.SVGPaintServerToString(element.Fill)

        Geo = eGeo.GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Figures = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()

    End Sub



End Class
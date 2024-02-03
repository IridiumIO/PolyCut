Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports Svg

Public Class CircleElement : Implements IPathBasedElement

    Public ReadOnly Property FlattenedLines As List(Of Line) Implements IPathBasedElement.FlattenedLines
        Get
            Return Figures.SelectMany(Of Line)(Function(x) x).ToList
        End Get
    End Property
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config
    Public Property Figures As List(Of List(Of Line)) Implements IPathBasedElement.Figures

    Public Property Stroke As System.Drawing.Color = Nothing
    Public Property Fill As System.Drawing.Color = Nothing


    Public Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileFromSVGElement
        Dim circleElement = DirectCast(element, SvgCircle)
        Config = cfg


        Dim eGeo As New EllipseGeometry(New Point(circleElement.CenterX, circleElement.CenterY), circleElement.Radius, circleElement.Radius)

        If circleElement.Fill IsNot Nothing Then Fill = CType(circleElement.Fill, SvgColourServer)?.Colour
        If circleElement.Stroke IsNot Nothing Then Stroke = CType(circleElement.Stroke, SvgColourServer)?.Colour




        Geo = eGeo.GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Figures = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Figures = Figures.Select(Function(fig) TransformLines(fig, element.Transforms.GetMatrix).ToList).ToList()
    End Sub
End Class

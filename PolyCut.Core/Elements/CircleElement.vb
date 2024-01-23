Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports Svg

Public Class CircleElement : Implements IPathBasedElement

    Public Property Lines As List(Of Line) Implements IPathBasedElement.Lines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config

    Public Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileElement
        Dim circleElement = DirectCast(element, SvgCircle)
        Config = cfg

        Dim eGeo As EllipseGeometry = New EllipseGeometry(New Point(circleElement.CenterX, circleElement.CenterY), circleElement.Radius, circleElement.Radius)
        Geo = eGeo.GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Lines = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Lines = TransformLines(Lines, element.Transforms.GetMatrix)
        Lines = OffsetProcessor.ProcessOffsets(Lines, Config.Offset, Config.Overcut)

    End Sub
End Class

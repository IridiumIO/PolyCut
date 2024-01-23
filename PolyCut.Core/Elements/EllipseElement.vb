Imports Svg
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class EllipseElement : Implements IPathBasedElement

    Public Property Lines As List(Of Line) Implements IPathBasedElement.Lines
    Public Property Geo As PathGeometry Implements IPathBasedElement.Geo
    Public Property Config As ProcessorConfiguration Implements IPathBasedElement.Config

    Public Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration) Implements IPathBasedElement.CompileElement
        Dim ellipse = DirectCast(element, SvgEllipse)
        Config = cfg
        Dim eGeo As EllipseGeometry = New EllipseGeometry(New Point(ellipse.CenterX, ellipse.CenterY), ellipse.RadiusX, ellipse.RadiusY)

        Geo = eGeo.GetFlattenedPathGeometry(Config.Tolerance, ToleranceType.Absolute)

        Lines = BuildLinesFromGeometry(Geo, Config.Tolerance)
        Lines = TransformLines(Lines, element.Transforms.GetMatrix)

        Lines = OffsetProcessor.ProcessOffsets(Lines, Config.Offset, Config.Overcut)
    End Sub



End Class
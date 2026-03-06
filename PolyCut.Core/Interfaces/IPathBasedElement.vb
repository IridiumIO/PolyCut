Imports Svg
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Interface IPathBasedElement

    ReadOnly Property FlattenedLines As List(Of GeoLine)

    Property Geo As PathGeometry
    Property Figures As List(Of List(Of GeoLine))
    Property IsFilled As Boolean
    Property FillColor As String
    Property Config As ProcessorConfiguration
    Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration)
End Interface

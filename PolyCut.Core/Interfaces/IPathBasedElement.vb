Imports Svg
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Interface IPathBasedElement

    ReadOnly Property FlattenedLines As List(Of Line)

    Property Geo As PathGeometry
    Property Figures As List(Of List(Of Line))
    Property Config As ProcessorConfiguration
    Sub CompileFromSVGElement(element As SvgVisualElement, cfg As ProcessorConfiguration)

    'TODO: Add property for the fill of the element to be used in the fill processor
End Interface

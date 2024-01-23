Imports Svg
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Interface IPathBasedElement

    Property Lines As List(Of Line)

    Property Geo As PathGeometry

    Property Config As ProcessorConfiguration
    Sub CompileElement(element As SvgVisualElement, cfg As ProcessorConfiguration)


End Interface

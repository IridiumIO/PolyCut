Imports Svg
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Interface IPathBasedElement
    ReadOnly Property FlattenedLines As List(Of GeoLine)
    Property Figures As List(Of List(Of GeoLine))
    Property IsFilled As Boolean
    Property FillColor As String
End Interface

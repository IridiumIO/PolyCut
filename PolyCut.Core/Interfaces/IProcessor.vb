Imports System.Windows.Shapes

Public Interface IProcessor
    Function Process(lines As List(Of GeoLine), cfg As ProcessorConfiguration) As List(Of GeoLine)
End Interface

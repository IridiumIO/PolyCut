Imports System.Windows.Shapes

Public Interface IProcessor
    Function Process(elements As List(Of IPathBasedElement), cfg As ProcessorConfiguration) As List(Of GeoLine)
End Interface

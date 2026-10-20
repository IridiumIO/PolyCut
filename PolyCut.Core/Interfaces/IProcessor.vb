Imports System.Windows.Shapes

Public Interface IProcessor
    Function Process(elements As List(Of PathBasedElement), cfg As ProcessorConfiguration) As List(Of GeoLine)
End Interface

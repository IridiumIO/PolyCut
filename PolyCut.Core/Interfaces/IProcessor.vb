Imports System.Windows.Shapes

Public Interface IProcessor
    Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line)
End Interface

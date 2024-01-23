Public Interface IExporter

    ReadOnly Property ProcessorConfiguration As ProcessorConfiguration

    'Return 0 for success, 1 for failure
    Function Export(gcodes As List(Of GCode), fileName As String) As Task(Of Integer)

End Interface

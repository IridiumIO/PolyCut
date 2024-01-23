Imports PolyCut.Core

Public Interface IGenerator

    Property Configuration As ProcessorConfiguration
    Property Printer As Printer
    Property GCodes As List(Of GCode)

    Function GenerateGcodeAsync() As Task(Of (StatusCode As Integer, Message As String))
    Function GetGCode() As List(Of GCode)

End Interface

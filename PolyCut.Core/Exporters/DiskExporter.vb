Imports System.IO

Public Class DiskExporter : Implements IExporter

    Public ReadOnly Property ProcessorConfiguration As ProcessorConfiguration Implements IExporter.ProcessorConfiguration

    Public Sub New(config As ProcessorConfiguration)
        ProcessorConfiguration = config
    End Sub

    Public Async Function Export(gcodes As List(Of GCode), fileName As String) As Task(Of Integer) Implements IExporter.Export

        If gcodes Is Nothing OrElse gcodes.Count = 0 Then Return 1

        Dim flattenedGCode As String = ""
        For Each gcode In gcodes
            flattenedGCode &= gcode.ToString & Environment.NewLine
        Next

        Await File.WriteAllTextAsync(fileName, flattenedGCode)

        Return 0

    End Function
End Class

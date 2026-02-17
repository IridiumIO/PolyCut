Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Shapes

Public Class ProcessorManager

    Private ReadOnly Property ProcessorConfiguration As ProcessorConfiguration

    Public Sub New(ProcessorConfiguration As ProcessorConfiguration)
        Me.ProcessorConfiguration = ProcessorConfiguration
    End Sub

    Public Function Process(figures As List(Of IPathBasedElement)) As List(Of GeoLine)
        Dim processor = GetProcessor()
        Return processor.Process(figures, ProcessorConfiguration)
    End Function

    Private Function GetProcessor() As IProcessor
        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut
                Return New CutProcessor()
            Case ProcessorConfiguration.ToolMode.Draw
                Return New FillProcessor()
            Case Else
                Throw New NotImplementedException("Tool Mode not implemented")
        End Select
    End Function

    Public Function GenerateGCode(lines As List(Of GeoLine)) As GCodeData
        Dim GCodeData = GCodeGenerator.GenerateWithMetadata(lines, ProcessorConfiguration)
        Return GCodeData
        ' Note: Lead-in post-processor code below is unreachable (Return happens first)
        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : GCodeData = New GCodeLeadInPostProcessor().Process(GCodeData, ProcessorConfiguration)
            Case ProcessorConfiguration.ToolMode.Draw : GCodeData = GCodeData
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select
    End Function

End Class

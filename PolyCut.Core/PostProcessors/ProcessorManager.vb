Imports System.Windows.Shapes

Public Class ProcessorManager

    Private ReadOnly Property ProcessorConfiguration As ProcessorConfiguration


    Public Sub New(ProcessorConfiguration As ProcessorConfiguration)
        Me.ProcessorConfiguration = ProcessorConfiguration
    End Sub


    Public Function Process(figures As List(Of List(Of Line))) As List(Of Line)

        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : Return ProcessCutting(figures)
            Case ProcessorConfiguration.ToolMode.Draw : Return ProcessDrawing(figures)
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select

    End Function


    Private Function ProcessCutting(figures As List(Of List(Of Line))) As List(Of Line)

        Dim overcutProcessedFigures As New List(Of List(Of Line))

        For Each figure In figures
            overcutProcessedFigures.Add(New OvercutProcessor().Process(figure, ProcessorConfiguration))
        Next

        Dim offsetProcessedFigures As New List(Of List(Of Line))

        For Each figure In overcutProcessedFigures
            offsetProcessedFigures.Add(New OffsetProcessor().Process(figure, ProcessorConfiguration))
        Next

        Return offsetProcessedFigures.SelectMany(Of Line)(Function(x) x).ToList

    End Function


    Private Function ProcessDrawing(figures As List(Of List(Of Line))) As List(Of Line)

        Dim fillProcessedFigures As New List(Of List(Of Line))

        For Each figure In figures
            fillProcessedFigures.Add(New FillProcessor().Process(figure, ProcessorConfiguration))
        Next


        Return fillProcessedFigures.SelectMany(Of Line)(Function(x) x).ToList

    End Function

    Public Function GenerateGCode(lines As List(Of Line)) As GCodeData
        Return GCodeGenerator.GenerateWithMetadata(lines, ProcessorConfiguration)
    End Function


End Class

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

            Dim closedLoops = GetClosedPaths(figure)
            For Each closedloop In closedLoops
                overcutProcessedFigures.Add(New OvercutProcessor().Process(closedloop, ProcessorConfiguration))

            Next

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


    Public Function GetClosedPaths(figures As List(Of Line)) As List(Of List(Of Line))
        Dim closedPaths As New List(Of List(Of Line))

        Dim workingPath As New List(Of Line)

        Dim currentLoopStartPoint As Windows.Point = figures.First.StartPoint
        Dim workingEndPoint As Windows.Point = figures.First.EndPoint

        workingPath.Add(figures(0))

        For Each line In figures

            If line.StartPoint = workingEndPoint Then
                workingPath.Add(line)
                workingEndPoint = line.EndPoint
            Else
                If workingEndPoint = currentLoopStartPoint Then
                    closedPaths.Add(workingPath)
                    workingPath = New List(Of Line)
                    currentLoopStartPoint = line.StartPoint
                    workingEndPoint = line.EndPoint
                    workingPath.Add(line)
                End If

            End If
        Next

        If workingPath.Count > 0 Then
            closedPaths.Add(workingPath)
        End If

        Return closedPaths

    End Function
End Class

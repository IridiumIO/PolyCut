Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Shapes

Public Class ProcessorManager

    Private ReadOnly Property ProcessorConfiguration As ProcessorConfiguration


    Public Sub New(ProcessorConfiguration As ProcessorConfiguration)
        Me.ProcessorConfiguration = ProcessorConfiguration
    End Sub


    Public Function Process(figures As List(Of IPathBasedElement)) As List(Of GeoLine)

        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : Return ProcessCutting(figures)
            Case ProcessorConfiguration.ToolMode.Draw : Return ProcessDrawing(figures)
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select

    End Function


    Private Function ProcessCutting(figures As List(Of IPathBasedElement)) As List(Of GeoLine)
        ' Reorder top-level figures as groups (greedy nearest-first) if enabled
        Dim workFigures As List(Of IPathBasedElement) = If(ProcessorConfiguration.OptimisedToolPath, figures.ReorderFiguresGreedy(), figures)

        ' Flatten all closed loops from all figures
        Dim unprocessedLoops = workFigures.SelectMany(Function(figure) GetClosedPaths(figure.FlattenedLines)).ToList()

        Dim processedLoops As New List(Of List(Of GeoLine))
        Dim lastBladeDir As Vector? = Nothing

        Const LookaheadCount As Integer = 3

        While unprocessedLoops.Any()

            'Step 1: examine next LookaheadCount loops for best blade aligmnent
            Dim bestIndex As Integer = 0
            If lastBladeDir.HasValue Then
                Dim limit As Integer = Math.Min(LookaheadCount, unprocessedLoops.Count)
                Dim candidates = unprocessedLoops.Take(limit).Select(Function(loopCandidate, idx) New With {
                    idx,
                    Key .score = Vector.Multiply(lastBladeDir.Value, loopCandidate(0).Direction()) * loopCandidate(0).Length()
                }).ToList()

                If candidates.Any() Then
                    bestIndex = candidates.OrderByDescending(Function(x) x.score).First().idx
                End If
            End If

            Dim selectedLoop As List(Of GeoLine) = unprocessedLoops(bestIndex)
            unprocessedLoops.RemoveAt(bestIndex)

            'Step 2: pre-offset alignment within loop
            Dim alignedPreOffsetLoop As List(Of GeoLine) = OffsetProcessor.ReorderLoopForBladeAlignment(selectedLoop, lastBladeDir)
            'Step 3: offset + overcut
            Dim offsetLoop As List(Of GeoLine) = New OffsetProcessor().Process(alignedPreOffsetLoop, ProcessorConfiguration)

            Dim overcutLoop As List(Of GeoLine) = New OvercutProcessor().Process(offsetLoop, ProcessorConfiguration)

            processedLoops.Add(overcutLoop)
            lastBladeDir = overcutLoop.LastDirection()

        End While

        ' Flatten to single list
        Return processedLoops.SelectMany(Function(x) x).ToList()
    End Function





    Private Function ProcessDrawing(figures As List(Of IPathBasedElement)) As List(Of GeoLine)

        ' Reorder top-level figures as groups (greedy nearest-first) if enabled
        Dim workFigures As List(Of IPathBasedElement) = If(ProcessorConfiguration.OptimisedToolPath, figures.ReorderFiguresGreedy(), figures)

        Dim fillProcessedFigures = workFigures.Select(Function(figure) New FillProcessor().Process(figure.FlattenedLines, ProcessorConfiguration)).ToList()

        Return fillProcessedFigures.SelectMany(Of GeoLine)(Function(x) x).ToList

    End Function




    Public Function GenerateGCode(lines As List(Of GeoLine)) As GCodeData

        Dim GCodeData = GCodeGenerator.GenerateWithMetadata(lines, ProcessorConfiguration)
        Return GCodeData
        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : GCodeData = New GCodeLeadInPostProcessor().Process(GCodeData, ProcessorConfiguration)
            Case ProcessorConfiguration.ToolMode.Draw : GCodeData = GCodeData
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select


    End Function


    Public Function GetClosedPaths(figures As List(Of GeoLine)) As List(Of List(Of GeoLine))
        If figures Is Nothing OrElse figures.Count = 0 Then
            Return New List(Of List(Of GeoLine))
        End If
        Dim closedPaths As New List(Of List(Of GeoLine))

        Dim workingPath As New List(Of GeoLine)

        Dim currentLoopStartPoint As System.Windows.Point = figures.First.StartPoint.ToPoint
        Dim workingEndPoint As System.Windows.Point = figures.First.EndPoint.ToPoint

        workingPath.Add(figures(0))

        For Each line In figures

            If line.StartPoint.X = workingEndPoint.X AndAlso line.StartPoint.Y = workingEndPoint.Y Then
                workingPath.Add(line)
                workingEndPoint = line.EndPoint.ToPoint
            Else
                If workingEndPoint.X = currentLoopStartPoint.X AndAlso workingEndPoint.Y = currentLoopStartPoint.Y Then
                    closedPaths.Add(workingPath)
                    workingPath = New List(Of GeoLine)
                    currentLoopStartPoint = line.StartPoint.ToPoint
                    workingEndPoint = line.EndPoint.ToPoint
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

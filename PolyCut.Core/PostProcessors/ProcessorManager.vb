Imports System.Windows
Imports System.Windows.Documents
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
        ' Reorder top-level figures as groups (greedy nearest-first) if enabled
        Dim workFigures As List(Of List(Of Line)) = If(ProcessorConfiguration.OptimisedToolPath, figures.ReorderFiguresGreedy, New List(Of List(Of Line))(figures))

        ' Flatten all closed loops from all figures
        Dim unprocessedLoops = workFigures.SelectMany(Function(figure) GetClosedPaths(figure)).ToList()

        Dim processedLoops As New List(Of List(Of Line))
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

            Dim selectedLoop As List(Of Line) = unprocessedLoops(bestIndex)
            unprocessedLoops.RemoveAt(bestIndex)

            'Step 2: pre-offset alignment within loop
            Dim alignedPreOffsetLoop As List(Of Line) = OffsetProcessor.ReorderLoopForBladeAlignment(selectedLoop, lastBladeDir)

            'Step 3: offset + overcut
            Dim offsetLoop As List(Of Line) = New OffsetProcessor().Process(alignedPreOffsetLoop, ProcessorConfiguration)

            Dim overcutLoop As List(Of Line) = New OvercutProcessor().Process(offsetLoop, ProcessorConfiguration)

            processedLoops.Add(overcutLoop)
            lastBladeDir = overcutLoop.LastDirection()

        End While

        ' Flatten to single list
        Return processedLoops.SelectMany(Function(x) x).ToList()
    End Function





    Private Function ProcessDrawing(figures As List(Of List(Of Line))) As List(Of Line)

        ' Reorder top-level figures as groups (greedy nearest-first) if enabled
        Dim workFigures As List(Of List(Of Line)) = If(ProcessorConfiguration.OptimisedToolPath, figures.ReorderFiguresGreedy, New List(Of List(Of Line))(figures))

        Dim fillProcessedFigures = workFigures.Select(Function(figure) New FillProcessor().Process(figure, ProcessorConfiguration)).ToList()

        Return fillProcessedFigures.SelectMany(Of Line)(Function(x) x).ToList

    End Function




    Public Function GenerateGCode(lines As List(Of Line)) As GCodeData

        Dim GCodeData = GCodeGenerator.GenerateWithMetadata(lines, ProcessorConfiguration)
        Return GCodeData
        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : GCodeData = New GCodeLeadInPostProcessor().Process(GCodeData, ProcessorConfiguration)
            Case ProcessorConfiguration.ToolMode.Draw : GCodeData = GCodeData
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select


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

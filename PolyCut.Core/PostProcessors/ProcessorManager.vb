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
        Dim workFigures As List(Of List(Of Line)) = If(ProcessorConfiguration.OptimisedToolPath, ReorderFiguresGreedy(figures), New List(Of List(Of Line))(figures))



        Dim offsetProcessedFigures As New List(Of List(Of Line))

        For Each figure In workFigures

            Dim closedLoops = GetClosedPaths(figure)
            For Each closedloop In closedLoops
                ' First offset the closed loop so arcs are generated from the original loop geometry.
                Dim offsetLoop As List(Of Line) = New OffsetProcessor().Process(closedloop, ProcessorConfiguration)

                ' Then apply overcut to the offset loop so any overcut segments are added after offset geometry.
                Dim overcutLoop As List(Of Line) = New OvercutProcessor().Process(offsetLoop, ProcessorConfiguration)

                offsetProcessedFigures.Add(overcutLoop)

            Next

        Next


        Return offsetProcessedFigures.SelectMany(Of Line)(Function(x) x).ToList

    End Function


    Private Function ProcessDrawing(figures As List(Of List(Of Line))) As List(Of Line)

        ' Reorder top-level figures as groups (greedy nearest-first) if enabled
        Dim workFigures As List(Of List(Of Line)) = If(ProcessorConfiguration.OptimisedToolPath, ReorderFiguresGreedy(figures), New List(Of List(Of Line))(figures))

        Dim fillProcessedFigures As New List(Of List(Of Line))

        For Each figure In workFigures
            fillProcessedFigures.Add(New FillProcessor().Process(figure, ProcessorConfiguration))
        Next


        Return fillProcessedFigures.SelectMany(Of Line)(Function(x) x).ToList

    End Function


    'Reorder figures to minimise travel distance between them. I really need to get away from List(of List(of Line)) at some point...
    Private Function ReorderFiguresGreedy(figures As List(Of List(Of Line))) As List(Of List(Of Line))
        If figures Is Nothing OrElse figures.Count = 0 Then
            Return New List(Of List(Of Line))()
        End If

        Dim remaining As New List(Of List(Of Line))(figures)
        Dim orderedFigures As New List(Of List(Of Line))

        ' Start from centre of work area for reasonable initial head position
        Dim currentPoint As Windows.Point = New Windows.Point(ProcessorConfiguration.WorkAreaWidth / 2.0, ProcessorConfiguration.WorkAreaHeight / 2.0)

        While remaining.Count > 0
            Dim bestIdx As Integer = -1
            Dim bestDistSq As Double = Double.MaxValue

            For i = 0 To remaining.Count - 1
                Dim rep = GetFigureRepresentative(remaining(i))
                Dim dx = rep.X - currentPoint.X
                Dim dy = rep.Y - currentPoint.Y
                Dim distSq = dx * dx + dy * dy
                If distSq < bestDistSq Then
                    bestDistSq = distSq
                    bestIdx = i
                End If
            Next

            If bestIdx = -1 Then
                orderedFigures.AddRange(remaining)
                Exit While
            End If

            Dim chosen = remaining(bestIdx)
            orderedFigures.Add(chosen)
            remaining.RemoveAt(bestIdx)

            ' update currentPoint to the representative of chosen group (keeps continuity for greedy selection)
            currentPoint = GetFigureRepresentative(chosen)
        End While

        Return orderedFigures
    End Function

    Private Function GetFigureRepresentative(figure As List(Of Line)) As Windows.Point
        If figure Is Nothing OrElse figure.Count = 0 Then
            Return New Windows.Point(0, 0)
        End If

        Dim sumX As Double = 0
        Dim sumY As Double = 0
        Dim count As Integer = 0

        For Each ln In figure
            Dim mx = (ln.X1 + ln.X2) / 2.0
            Dim my = (ln.Y1 + ln.Y2) / 2.0
            sumX += mx
            sumY += my
            count += 1
        Next

        Return New Windows.Point(sumX / count, sumY / count)
    End Function

    Public Function GenerateGCode(lines As List(Of Line)) As GCodeData

        Dim GCodeData = GCodeGenerator.GenerateWithMetadata(lines, ProcessorConfiguration)

        Select Case ProcessorConfiguration.SelectedToolMode
            Case ProcessorConfiguration.ToolMode.Cut : GCodeData = New GCodeLeadInPostProcessor().Process(GCodeData, ProcessorConfiguration)
            Case ProcessorConfiguration.ToolMode.Draw : GCodeData = GCodeData
            Case Else : Throw New NotImplementedException("Tool Mode not implemented")
        End Select

        Return GCodeData
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

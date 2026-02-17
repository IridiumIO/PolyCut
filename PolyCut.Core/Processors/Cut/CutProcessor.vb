Imports System.Windows
Imports System.Windows.Documents

Public Class CutProcessor : Implements IProcessor

    Public Function Process(elements As List(Of IPathBasedElement), cfg As ProcessorConfiguration) As List(Of GeoLine) Implements IProcessor.Process
        If elements Is Nothing OrElse elements.Count = 0 Then
            Return New List(Of GeoLine)
        End If

        ' Reorder elements if optimization enabled
        Dim workElements = If(cfg.OptimisedToolPath, elements.ReorderFiguresGreedy(), elements)

        ' Flatten all loops from all elements for global optimization
        Dim unprocessedLoops = workElements.SelectMany(Function(elem) GetClosedPaths(elem.FlattenedLines)).ToList()

        Dim processedLoops As New List(Of List(Of GeoLine))
        Dim lastBladeDir As Vector? = Nothing

        Const LookaheadCount As Integer = 3 'why 3? good enough, not too costly

        While unprocessedLoops.Any()
            ' Step 1: Select best next loop (blade alignment optimization)
            Dim bestIndex = SelectBestLoopIndex(unprocessedLoops, lastBladeDir, LookaheadCount)
            Dim selectedLoop = unprocessedLoops(bestIndex)
            unprocessedLoops.RemoveAt(bestIndex)

            ' Step 2: Pre-offset blade alignment within loop
            Dim alignedLoop = OffsetGenerator.ReorderLoopForBladeAlignment(selectedLoop, lastBladeDir)

            ' Step 3: Generate offset arcs for tool radius
            Dim offsetLoop = OffsetGenerator.CreateOffsetArcs(alignedLoop, cfg.CuttingConfig.ToolRadius)

            ' Step 4: Add overcut to close loop cleanly
            Dim overcutLoop = OvercutGenerator.CreateOvercuts(offsetLoop, cfg.CuttingConfig.Overcut)

            processedLoops.Add(overcutLoop)
            lastBladeDir = overcutLoop.LastDirection()
        End While

        ' Flatten all processed loops into single line list
        Return processedLoops.SelectMany(Function(x) x).ToList()
    End Function


    Private Shared Function SelectBestLoopIndex(loops As List(Of List(Of GeoLine)), lastBladeDir As Vector?, lookahead As Integer) As Integer
        If Not lastBladeDir.HasValue OrElse loops.Count = 0 Then Return 0


        Dim limit = Math.Min(lookahead, loops.Count)
        Dim candidates = loops.Take(limit).Select(Function(loopCandidate, idx) New With {
                idx,
                Key .score = Vector.Multiply(lastBladeDir.Value, loopCandidate(0).Direction()) * loopCandidate(0).Length()
            }).ToList()

        Return If(candidates.Any(),
                 candidates.OrderByDescending(Function(x) x.score).First().idx,
                 0)
    End Function


    Private Shared Function GetClosedPaths(figures As List(Of GeoLine)) As List(Of List(Of GeoLine))
        If figures Is Nothing OrElse figures.Count = 0 Then
            Return New List(Of List(Of GeoLine))
        End If

        Dim closedPaths As New List(Of List(Of GeoLine))
        Dim workingPath As New List(Of GeoLine)

        Dim currentLoopStart = figures.First.StartPoint.ToPoint
        Dim workingEnd = figures.First.EndPoint.ToPoint

        workingPath.Add(figures(0))

        For Each line In figures
            If line.StartPoint.X = workingEnd.X AndAlso line.StartPoint.Y = workingEnd.Y Then
                workingPath.Add(line)
                workingEnd = line.EndPoint.ToPoint
            Else
                If workingEnd.X = currentLoopStart.X AndAlso workingEnd.Y = currentLoopStart.Y Then
                    closedPaths.Add(workingPath)
                    workingPath = New List(Of GeoLine)
                    currentLoopStart = line.StartPoint.ToPoint
                    workingEnd = line.EndPoint.ToPoint
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

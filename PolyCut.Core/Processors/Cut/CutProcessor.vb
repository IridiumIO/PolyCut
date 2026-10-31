Imports System.Windows
Imports System.Windows.Documents

Public Class CutProcessor : Implements IProcessor

    Public Function Process(elements As List(Of PathBasedElement), cfg As ProcessorConfiguration) As List(Of GeoLine) Implements IProcessor.Process
        If elements Is Nothing OrElse elements.Count = 0 Then
            Return New List(Of GeoLine)
        End If

        ' Reorder elements if optimization enabled TODO: Add this to the config sidebar for cutting options
        Dim workElements = If(cfg.OptimisedToolPath, elements.ReorderFiguresGreedy(), elements)

        ' Compensate each element's region before separating its loops, so holes offset correctly.
        Dim unprocessedLoops = workElements.SelectMany(Function(elem) CutWidthCompensationProcessor.Process(
            elem.Figures, cfg.CuttingConfig.CutWidthCompensation)).
            Where(Function(figure) figure.Count > 0).ToList()

        Dim processedLoops As New List(Of List(Of GeoLine))
        Dim lastBladeDir As Vector? = Nothing

        Const LookaheadCount As Integer = 3 'why 3? good enough, not too costly

        While unprocessedLoops.Any()
            ' Step 1: Select best next loop (blade alignment optimization)
            Dim bestIndex = SelectBestLoopIndex(unprocessedLoops, lastBladeDir, LookaheadCount)
            Dim selectedLoop = unprocessedLoops(bestIndex)
            unprocessedLoops.RemoveAt(bestIndex)

            ' Step 2: Pre-offset blade alignment within loop
            Dim alignedLoop = If(selectedLoop(0).StartPoint = selectedLoop.Last().EndPoint,
                                 OffsetGenerator.ReorderLoopForBladeAlignment(selectedLoop, lastBladeDir), selectedLoop)

            ' Step 3: Generate offset arcs for tool radius
            Dim offsetLoop = OffsetGenerator.CreateOffsetArcs(alignedLoop, cfg.CuttingConfig.ToolRadius, cfg.CuttingConfig.BladePathNoiseTolerance)

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


End Class

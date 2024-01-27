Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes


Public Class OffsetProcessor : Implements IProcessor


    Private Shared Function CreateOffsetArcs(lines As List(Of Line), toolRadius As Double) As List(Of Line)

        Dim processedLines As New List(Of Line)

        'Flag to note if the last processed line was continuous so we don't double up on adding a shortened l2
        Dim previousWasContinuous As Boolean = False
        Dim outerlooped As Boolean = False



        For i As Integer = 0 To lines.Count - 1
            If outerlooped Then Exit For

            Dim l1 = lines(i)
            Dim l2 = lines((i + 1) Mod lines.Count) 'If lines(i) = lines.count - 1 then we need to check its position relative to the first line to close the shape


            If Not l1.IsContinuousWith(l2) OrElse l1.IsCollinearWith(l2, 40) Then
                previousWasContinuous = False

                If Not processedLines.Contains(l1) Then
                    processedLines.Add(l1)

                End If

                Continue For
            End If


            'TODO
            'TODO
            'TODO
            'Add an arc before the first line because I'm too lazy to work out the math to continue from the last known point 
            'TODO
            'TODO
            'TODO



            'Extend the first line by the tool radius so the blade point is exactly on the corner
            Dim theta = l1.AngleR
            Dim xOffset = toolRadius * Math.Cos(theta)
            Dim yOffset = toolRadius * Math.Sin(theta)
            l1.X2 += xOffset
            l1.Y2 += yOffset

            Dim epsilon = l2.AngleR
            Dim xOffset2 = toolRadius * Math.Cos(epsilon)
            Dim yOffset2 = toolRadius * Math.Sin(epsilon)


            Dim rollingIndex As Integer = i + 1

            Dim arcStartP = l1.EndPoint
            Dim arcCenterP = l2.StartPoint
            Dim arcEndP As New Point(l2.X1 + xOffset2, l2.Y1 + yOffset2)

            Dim geom As New PathGeometry
            Dim pfig As New PathFigure With {.StartPoint = arcStartP}
            Dim bzc = BezierArcApproximation(arcStartP, arcEndP, arcCenterP, toolRadius)
            pfig.Segments.Add(bzc)

            While lines(rollingIndex Mod lines.Count).Length < toolRadius

                Dim l = lines(rollingIndex Mod lines.Count)
                Dim ltheta = l.AngleR

                'Generate a new offset point that is the tool radius away from the line as the new arc start point. 
                Dim lxOffset = toolRadius * Math.Cos(ltheta)
                Dim lyOffset = toolRadius * Math.Sin(ltheta)

                arcCenterP = New Point(l.X2, l.Y2)
                arcStartP = New Point(l.X2 + lxOffset, l.Y2 + lyOffset)
                pfig.Segments.Add(New LineSegment(arcStartP, True))

                Dim lplus1 = lines((rollingIndex + 1) Mod lines.Count)
                Dim lplus1theta = lplus1.AngleR
                Dim lplus1xOffset = toolRadius * Math.Cos(lplus1theta)
                Dim lplus1yOffset = toolRadius * Math.Sin(lplus1theta)

                arcEndP = New Point(lplus1.X1 + lplus1xOffset, lplus1.Y1 + lplus1yOffset)

                Dim bzn = BezierArcApproximation(arcStartP, arcEndP, arcCenterP, toolRadius)
                pfig.Segments.Add(bzn)

                rollingIndex += 1

                If rollingIndex = lines.Count Then
                    rollingIndex = 0
                    outerlooped = True
                    Exit While
                End If

            End While

            geom.Figures.Add(pfig)

            Dim flattened = geom.GetFlattenedPathGeometry(0.01, ToleranceType.Absolute)
            Dim builtLines = BuildLinesFromGeometry(flattened, 0.01).SelectMany(Of Line)(Function(x) x).ToList()

            'Ensure after arc is built that the first line of the arc is continuous with l1
            builtLines(0).X1 = l1.X2
            builtLines(0).Y1 = l1.Y2

            If Not previousWasContinuous Then
                processedLines.Add(l1)

                i = rollingIndex
            Else


            End If

            Dim lf = lines((rollingIndex) Mod lines.Count)
            Dim lftheta = lf.AngleR
            Dim lfxOffset = toolRadius * Math.Cos(lftheta)
            Dim lfyOffset = toolRadius * Math.Sin(lftheta)
            lf.X1 += lfxOffset
            lf.Y1 += lfyOffset


            lf.X1 = builtLines.Last.X2
            lf.Y1 = builtLines.Last.Y2

            processedLines.AddRange(builtLines)


            If rollingIndex Mod lines.Count = 0 Then
                outerlooped = True
            End If

            If Not outerlooped Then
                processedLines.Add(lf)

                i = rollingIndex - 1

            Else
                i = rollingIndex

            End If
            previousWasContinuous = True

        Next


        Return processedLines

    End Function

    Public Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line) Implements IProcessor.Process
        Return CreateOffsetArcs(lines, cfg.CuttingConfig.ToolDiameter / 2)
    End Function
End Class

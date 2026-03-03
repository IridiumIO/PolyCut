Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes


Public Class OffsetGenerator

    Private Const COLLINEARITYANGLE_SIN = 0.08715572466147 ' 5 degrees in radians

    Public Shared Function CreateOffsetArcs(lines As List(Of GeoLine), toolRadius As Double) As List(Of GeoLine)

        If toolRadius <= 0 Then Return lines

        Dim processedLines As New List(Of GeoLine)

        'Flag to note if the last processed line was continuous so we don't double up on adding a shortened l2
        Dim previousWasContinuous As Boolean = False
        Dim outerlooped As Boolean = False


        For i As Integer = 0 To lines.Count - 1
            If outerlooped Then Exit For

            Dim l1 = lines(i)
            Dim l2 = lines((i + 1) Mod lines.Count) 'If lines(i) = lines.count - 1 then we need to check its position relative to the first line to close the shape

            If Not l1.IsContinuousWith(l2) OrElse l1.IsCollinearWith(l2, COLLINEARITYANGLE_SIN) Then
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
            l1 = New GeoLine(l1.X1, l1.Y1, l1.X2 + xOffset, l1.Y2 + yOffset)
            lines(i) = l1

            ' If this line was already added in a previous iteration (previousWasContinuous = True),
            ' we need to update it in processedLines with the extended version
            If previousWasContinuous AndAlso processedLines.Count > 0 Then
                processedLines(processedLines.Count - 1) = l1
            End If

            Dim epsilon = l2.AngleR
            Dim xOffset2 = toolRadius * Math.Cos(epsilon)
            Dim yOffset2 = toolRadius * Math.Sin(epsilon)


            Dim rollingIndex As Integer = i + 1

            Dim arcStartP = l1.EndPoint.ToPoint
            Dim arcCenterP = l2.StartPoint.ToPoint
            Dim arcEndP As New Point(l2.X1 + xOffset2, l2.Y1 + yOffset2)

            Dim geom As New PathGeometry
            Dim pfig As New PathFigure With {.StartPoint = arcStartP}
            Dim bzc = BezierArcApproximation(arcStartP, arcEndP, arcCenterP, toolRadius)
            pfig.Segments.Add(bzc)

            While lines(rollingIndex Mod lines.Count).Length < toolRadius AndAlso lines(rollingIndex Mod lines.Count).IsContinuousWith(lines((rollingIndex + 1) Mod lines.Count))

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

                If rollingIndex >= lines.Count Then
                    rollingIndex = 0
                    outerlooped = True
                    Exit While
                End If

            End While

            geom.Figures.Add(pfig)

            Dim flattened = geom.GetFlattenedPathGeometry(0.01, ToleranceType.Absolute)
            Dim builtLines = BuildLinesFromGeometry(flattened, 0.01).SelectMany(Of GeoLine)(Function(x) x).ToList()

            'Ensure after arc is built that the first line of the arc is continuous with l1
            builtLines(0) = New GeoLine(l1.X2, l1.Y2, builtLines(0).X2, builtLines(0).Y2)

            If Not previousWasContinuous Then
                processedLines.Add(l1)

                i = rollingIndex
            Else


            End If

            Dim lfIndex = rollingIndex Mod lines.Count
            Dim lf = lines(lfIndex)
            lf = New GeoLine(builtLines.Last.X2, builtLines.Last.Y2, lf.X2, lf.Y2)

            lines(lfIndex) = lf

            processedLines.AddRange(builtLines)


            If rollingIndex Mod lines.Count = 0 Then
                outerlooped = True
            End If

            If Not outerlooped Then
                processedLines.Add(lf)

                i = rollingIndex - 1

            Else

                If processedLines.Count > 0 AndAlso lfIndex = 0 Then
                    For pi = 0 To Math.Min(10, processedLines.Count - 1)
                        If processedLines(pi).X2 = lf.X2 AndAlso processedLines(pi).Y2 = lf.Y2 Then
                            processedLines(pi) = lf
                            Exit For
                        End If
                    Next
                End If

                i = rollingIndex

            End If
            previousWasContinuous = True

        Next


        Return processedLines

    End Function


    ' cos(25 deg)
    Const AlignmentThresholdCos As Double = 0.906

    Public Shared Function ReorderLoopForBladeAlignment(loopLines As List(Of GeoLine), lastBladeDir As Vector?) As List(Of GeoLine)
        If loopLines Is Nothing OrElse loopLines.Count < 2 Then Return loopLines

        Dim n = loopLines.Count

        Dim scored = loopLines.Select(Function(l, idx)
                                          Dim predIdx = (idx - 1 + n) Mod n
                                          Dim predDir = loopLines(predIdx).Direction()
                                          Dim currDir = l.Direction()

                                          ' Strongly prefer entries where the loop predecessor already points the same way.
                                          Dim hasFreeEntry As Boolean = Vector.Multiply(predDir, currDir) >= AlignmentThresholdCos

                                          ' Secondary score: alignment with the last known blade direction.
                                          Dim dirScore As Double = If(lastBladeDir.HasValue,
                                                                    Vector.Multiply(lastBladeDir.Value, currDir),
                                                                    0.0)

                                          Return New With {idx, Key .score = If(hasFreeEntry, 10.0, 0.0) + dirScore}
                                      End Function).OrderByDescending(Function(x) x.score).ToList()

        Return loopLines.RotateStartAt(scored(0).idx)
    End Function

End Class

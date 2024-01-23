Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes


Public Class OffsetProcessor


    Public Shared Function ProcessOffsets(lines As List(Of Line), toolRadius As Double, overcut As Double) As List(Of Line)

        Dim processedLines As New List(Of Line)

        'Flag to note if the last processed line was continuous so we don't double up on adding a shortened l2
        Dim previousWasContinuous As Boolean = False
        For i As Integer = 0 To lines.Count - 1
            Dim l1 = lines(i)

            Dim l2 As Line

            'If lines(i) = lines.count - 1 then we need to check its position relative to the first line to close the shape
            If i = lines.Count - 1 Then
                l2 = lines(0)
            Else
                l2 = lines(i + 1)
            End If

            'If the two lines are continuous then we can work out an offset
            'If the two lines are almost collinear within a tolerance (angle as degrees), we don't need to adjust the offset
            If IsContinuous(l1, l2) And Not IsCollinear(l1, l2, 40) Then

                'What will happen if the length of l2 < toolRadius? Fucked if I know right now

                'Extend the first line by the tool radius so the blade point is exactly on the corner
                Dim theta = GetLineAngle(l1)
                Dim xOffset = toolRadius * Math.Cos(theta)
                Dim yOffset = toolRadius * Math.Sin(theta)
                l1.X2 = l1.X2 + xOffset
                l1.Y2 = l1.Y2 + yOffset

                'Shorten the second line by the tool radius 
                Dim epsilon = GetLineAngle(l2)
                Dim xOffset2 = toolRadius * Math.Cos(epsilon)
                Dim yOffset2 = toolRadius * Math.Sin(epsilon)
                l2.X1 += xOffset2
                l2.Y1 += yOffset2

                Dim bz = BezierArcApproximation(New Point(l1.X2, l1.Y2), New Point(l2.X1, l2.Y1), New Point(l2.X1 - xOffset2, l2.Y1 - yOffset2), toolRadius)

                Dim pg As New PathGeometry
                Dim pf As New PathFigure With {
                    .StartPoint = New Point(l1.X2, l1.Y2),
                    .Segments = New PathSegmentCollection From {
                        bz
                    }
                }
                pg.Figures.Add(pf)

                Dim flat = pg.GetFlattenedPathGeometry(0.01, ToleranceType.Absolute)
                Dim builtLines = BuildLinesFromGeometry(flat, 0.01)
                If Not previousWasContinuous Then
                    processedLines.Add(l1)
                End If
                processedLines.AddRange(builtLines)

                If Not i = lines.Count - 1 Then
                    processedLines.Add(l2)

                End If

                previousWasContinuous = True
            Else
                previousWasContinuous = False
                processedLines.Add(l1)

            End If

        Next

        Dim overcutlines As New List(Of Line)

        'If overcut <> 0 Then

        '    Dim remainder As Double = overcut

        '    Dim i = 0
        '    While remainder > 0

        '        Dim workingL = processedLines(i)
        '        Dim lineLength As Double = GetLineLength(workingL)
        '        remainder -= lineLength

        '        Dim p1 = New Point(workingL.X1, workingL.Y1)
        '        Dim theta = GetLineAngle(workingL)

        '        Dim p2 As New Point

        '        If remainder > 0 Then
        '            p2 = New Point(workingL.X2, workingL.Y2)
        '        Else
        '            p2 = New Point((remainder + lineLength) * Math.Cos(theta) + p1.X, (remainder + lineLength) * Math.Sin(theta) + p1.Y)
        '        End If

        '        Dim overcutLine As New Line With {
        '                            .X1 = p1.X,
        '                            .Y1 = p1.Y,
        '                            .X2 = p2.X,
        '                            .Y2 = p2.Y
        '                        }

        '        overcutlines.Add(overcutLine)
        '        i += 1

        '        'Thought I wouldn't catch the edge case where you loop the full geometry multiple times? Think again
        '        If i = processedLines.Count Then i = 0

        '    End While

        'End If

        processedLines.AddRange(overcutlines)

        Return processedLines

    End Function

    Private Shared Function IsCollinear(line1 As Line, line2 As Line, Optional tolerance As Double = 0) As Boolean

        Dim radians = Math.PI / 180 * tolerance

        Dim slope1 = (line1.Y2 - line1.Y1) / (line1.X2 - line1.X1)
        Dim slope2 = (line2.Y2 - line2.Y1) / (line2.X2 - line2.X1)

        Dim TwoAngle = GetTwoLineAngle(line1, line2)

        Dim withinTolerance = Between(TwoAngle, Math.PI - radians, Math.PI + radians)

        Return withinTolerance

    End Function

    Private Shared Function Between(n, bound1, bound2) As Boolean
        Return (n >= bound1 And n <= bound2) Or (n >= bound2 And n <= bound1)
    End Function

    Private Shared Function IsContinuous(line1 As Line, line2 As Line) As Boolean
        Return line1.X2 = line2.X1 AndAlso line1.Y2 = line2.Y1
    End Function

    Public Shared Function GetLineAngle(line As Line) As Double

        Dim angle As Double = Math.Atan2(line.Y2 - line.Y1, line.X2 - line.X1)

        Return angle

    End Function

    Public Shared Function GetTwoLineAngle(line1 As Line, line2 As Line) As Double
        Dim p1 As Point = New Point(line1.X1, line1.Y1)
        Dim p2 As Point = New Point(line1.X2, line1.Y2)
        Dim p3 As Point = New Point(line2.X2, line2.Y2)

        Dim line3 As New Line() With {.X1 = p1.X, .Y1 = p1.Y, .X2 = p3.X, .Y2 = p3.Y}

        Dim lenA As Double = GetLineLength(line1)
        Dim lenb As Double = GetLineLength(line2)
        Dim lenC As Double = GetLineLength(line3)

        Dim Angle = Math.Acos((lenA ^ 2 + lenb ^ 2 - lenC ^ 2) / (2 * lenA * lenb))

        Debug.WriteLine(Angle * 180 / Math.PI)

        Return Angle


    End Function

    Public Shared Function GetLineLength(line As Line) As Double

        Return Math.Sqrt((line.X2 - line.X1) ^ 2 + (line.Y2 - line.Y1) ^ 2)

    End Function

    Public Shared Function BezierArcApproximation(startP As Point, endP As Point, centerP As Point, radius As Double)

        Dim startAngle = Math.Atan2(startP.Y - centerP.Y, startP.X - centerP.X)
        Dim endAngle = Math.Atan2(endP.Y - centerP.Y, endP.X - centerP.X)

        '' Calculate the angle of the arc.
        '' Don't ask me how I decided on these angle adjustments. I have no idea.
        'Dim arcAngle = endAngle - startAngle
        'If arcAngle < 0 Then
        '    arcAngle += 2 * Math.PI
        '    If arcAngle > Math.PI Then
        '        arcAngle = -(2 * Math.PI - arcAngle)
        '    End If
        'Else
        '    If arcAngle > Math.PI Then
        '        arcAngle = -(2 * Math.PI - arcAngle)
        '    End If
        'End If

        'I'm an idiot. This is much simpler
        Dim arcAngle = Math.IEEERemainder(endAngle - startAngle, 2 * Math.PI)



        ' Magic control point distance
        Dim f = 4 / 3 * Math.Tan(arcAngle / 4)

        ' Calculate the control points
        Dim controlPoint1 As New Point(startP.X - radius * f * Math.Sin(startAngle), startP.Y + radius * f * Math.Cos(startAngle))
        Dim controlPoint2 As New Point(endP.X + radius * f * Math.Sin(endAngle), endP.Y - radius * f * Math.Cos(endAngle))

        ' Create the Bezier segment
        Dim bezierSegment As New BezierSegment(controlPoint1, controlPoint2, endP, True)

        Return bezierSegment


    End Function

End Class

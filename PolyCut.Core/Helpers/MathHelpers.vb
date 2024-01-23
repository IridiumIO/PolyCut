Imports System.Windows
Imports System.Windows.Media

Public Module MathHelpers
    Public Function Between(n, bound1, bound2) As Boolean
        Return (n >= bound1 And n <= bound2) Or (n >= bound2 And n <= bound1)
    End Function



    Public Function BezierArcApproximation(startP As Point, endP As Point, centerP As Point, radius As Double)

        Dim startAngle = Math.Atan2(startP.Y - centerP.Y, startP.X - centerP.X)
        Dim endAngle = Math.Atan2(endP.Y - centerP.Y, endP.X - centerP.X)

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

End Module

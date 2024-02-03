Imports System.Runtime.CompilerServices
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class Geo

End Class


Public Class GeoLine


    Public Property StartPoint As Point
    Public Property EndPoint As Point


    Public Sub New(startP As Point, endP As Point)
        StartPoint = startP
        EndPoint = endP
    End Sub

    Public Sub New(X1 As Double, Y1 As Double, X2 As Double, Y2 As Double)
        StartPoint = New Point(X1, Y1)
        EndPoint = New Point(X2, Y2)
    End Sub

    Public Sub New(startP As Point, length As Double, angleR As Double)
        StartPoint = startP
        EndPoint = New Point(startP.X + length * Math.Cos(angleR), startP.Y + length * Math.Sin(angleR))
    End Sub


    Public ReadOnly Property XLength As Double
        Get
            Return EndPoint.X - StartPoint.X
        End Get
    End Property

    Public ReadOnly Property YLength As Double
        Get
            Return EndPoint.Y - StartPoint.Y
        End Get
    End Property

    Public ReadOnly Property Length As Double
        Get
            Return Math.Sqrt(XLength ^ 2 + YLength ^ 2)
        End Get
    End Property


    Public ReadOnly Property Slope As Double
        Get
            Return YLength / XLength
        End Get
    End Property

    Public ReadOnly Property AngleR As Double
        Get
            Return Math.Atan2(YLength, XLength)
        End Get
    End Property


    Public Function Transform(transforms As System.Drawing.Drawing2D.Matrix) As GeoLine

        Dim wpfMatrix As New Matrix(transforms.Elements(0), transforms.Elements(1), transforms.Elements(2), transforms.Elements(3), transforms.OffsetX, transforms.OffsetY)

        Dim p1 = wpfMatrix.Transform(StartPoint)
        Dim p2 = wpfMatrix.Transform(EndPoint)

        Return New GeoLine(p1, p2)

    End Function


    Public Function IsContinuousWith(otherline As GeoLine) As Boolean
        Return EndPoint.X = otherline.StartPoint.X AndAlso EndPoint.Y = otherline.StartPoint.Y
    End Function


    Public Function IsCollinearWith(otherline As GeoLine, Optional tolerance As Double = 0) As Boolean
        Dim radians = Math.PI / 180 * tolerance
        Dim TwoAngle = GetAngleBetween(otherline)
        Dim withinTolerance = MathHelpers.Between(TwoAngle, Math.PI - radians, Math.PI + radians)
        Return withinTolerance
    End Function

    Public Function GetAngleBetween(otherline As GeoLine) As Double

        Dim hypot As GeoLine = New GeoLine(StartPoint, otherline.EndPoint)

        Dim lenA As Double = Length
        Dim lenb As Double = otherline.Length
        Dim lenC As Double = hypot.Length

        Dim Angle = Math.Acos((lenA ^ 2 + lenb ^ 2 - lenC ^ 2) / (2 * lenA * lenb))

        Return Angle

    End Function


    Public Function GetIntersectionPointWith(otherline As GeoLine) As Nullable(Of Point)

        Dim Ax As Double = StartPoint.X
        Dim Ay As Double = StartPoint.Y
        Dim Bx As Double = EndPoint.X
        Dim By As Double = EndPoint.Y

        Dim Cx As Double = otherline.StartPoint.X
        Dim Cy As Double = otherline.StartPoint.Y
        Dim Dx As Double = otherline.EndPoint.X
        Dim Dy As Double = otherline.EndPoint.Y

        Dim denominator As Double = (Dy - Cy) * (Bx - Ax) - (Dx - Cx) * (By - Ay)
        Dim numerator1 As Double = (Dx - Cx) * (Ay - Cy) - (Dy - Cy) * (Ax - Cx)
        Dim numerator2 As Double = (Cy - Ay) * (Ax - Bx) - (Cx - Ax) * (Ay - By)

        If denominator = 0 Then Return Nothing

        Dim t = numerator1 / denominator
        Dim u = numerator2 / denominator

        If t < 0 OrElse t > 1 OrElse u < 0 OrElse u > 1 Then Return Nothing

        Return New Point(Ax, Ay).Lerp(New Point(Bx, By), t)

    End Function

    Public Function IntersectsWithShape(shapeBoundaries As List(Of GeoLine)) As Boolean

        Dim intersectionPoints As New List(Of Point)
        For Each segment In shapeBoundaries
            Dim intersection = GetIntersectionPointWith(segment)
            If intersection IsNot Nothing Then
                Return True
            End If
        Next

        Return False
    End Function



End Class



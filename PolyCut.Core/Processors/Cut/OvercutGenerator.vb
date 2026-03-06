Imports System.Windows
Imports System.Windows.Shapes

Public Class OvercutGenerator

    Public Shared Function CreateOvercuts(lines As List(Of GeoLine), overcut As Double) As List(Of GeoLine)

        If lines Is Nothing OrElse lines.Count = 0 OrElse overcut <= 0 Then Return lines

        ' Ensure the provided lines form a continuous closed loop.
        Dim isContinuous As Boolean = True
        For ix = 1 To lines.Count - 1
            If Not lines(ix - 1).IsContinuousWith(lines(ix)) Then
                isContinuous = False
                Exit For
            End If
        Next

        ' Not a closed continuous loop -> nothing to overcut.
        If Not isContinuous OrElse Not lines(lines.Count - 1).IsContinuousWith(lines(0)) Then Return lines

        Dim totalPerimeter As Double = lines.Sum(Function(l) l.Length)
        Dim requestedOvercut As Double = Math.Min(overcut, totalPerimeter)

        Dim overcutlines As New List(Of GeoLine)
        Dim remainder As Double = requestedOvercut
        Dim i = 0

        While remainder > 0
            Dim workingL = lines(i)
            Dim lineLength As Double = workingL.Length
            remainder -= lineLength

            Dim p1 = workingL.StartPoint
            Dim theta = workingL.AngleR

            Dim p2 = If(remainder > 0,
                        workingL.EndPoint,
                        New Numerics.Vector2((remainder + lineLength) * Math.Cos(theta) + p1.X, (remainder + lineLength) * Math.Sin(theta) + p1.Y))

            Dim overcutLine As GeoLine = p1.LineTo(p2)
            overcutlines.Add(overcutLine)

            i += 1
            If i = lines.Count Then i = 0 ' wrapping is only safe because we validated a closed loop
        End While

        lines.AddRange(overcutlines)
        Return lines
    End Function

End Class

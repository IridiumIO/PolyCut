Imports System.Windows
Imports System.Windows.Shapes

Public Class OvercutProcessor : Implements IProcessor


    Public Shared Function CreateOvercuts(lines As List(Of Line), overcut As Double) As List(Of Line)


        If lines Is Nothing OrElse lines.Count = 0 OrElse overcut <= 0 Then Return lines

        ' Ensure the provided lines form a continuous closed loop.
        Dim isContinuous As Boolean = True
        For ix = 1 To lines.Count - 1
            If lines(ix).StartPoint <> lines(ix - 1).EndPoint Then
                isContinuous = False
                Exit For
            End If
        Next

        ' Not a closed continuous loop -> nothing to overcut.
        If Not isContinuous OrElse lines.Last.EndPoint <> lines.First.StartPoint Then Return lines

        Dim totalPerimeter As Double = lines.Sum(Function(l) l.Length)
        Dim requestedOvercut As Double = Math.Min(overcut, totalPerimeter)

        Dim overcutlines As New List(Of Line)
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
                        New Point((remainder + lineLength) * Math.Cos(theta) + p1.X, (remainder + lineLength) * Math.Sin(theta) + p1.Y))

            Dim overcutLine As Line = p1.LineTo(p2)
            overcutlines.Add(overcutLine)

            i += 1
            If i = lines.Count Then i = 0 ' wrapping is only safe because we validated a closed loop
        End While

        lines.AddRange(overcutlines)
        Return lines
    End Function

    Public Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line) Implements IProcessor.Process
        Return CreateOvercuts(lines, cfg.CuttingConfig.Overcut)
    End Function
End Class

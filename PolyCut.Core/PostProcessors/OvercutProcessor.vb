Imports System.Windows
Imports System.Windows.Shapes

Public Class OvercutProcessor : Implements IProcessor


    Public Shared Function CreateOvercuts(lines As List(Of Line), overcut As Double) As List(Of Line)

        Dim overcutlines As New List(Of Line)

        If overcut <> 0 Then

            Dim remainder As Double = overcut

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

                If i = lines.Count Then i = 0

            End While

        End If

        lines.AddRange(overcutlines)

        Return lines

    End Function

    Public Function Process(lines As List(Of Line), cfg As ProcessorConfiguration) As List(Of Line) Implements IProcessor.Process
        Return CreateOvercuts(lines, cfg.CuttingConfig.Overcut)
    End Function
End Class

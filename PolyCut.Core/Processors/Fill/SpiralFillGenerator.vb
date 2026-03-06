Imports System.Numerics
Imports System.Windows

Public Class SpiralFillGenerator
    Friend Shared Function Generate(lines As List(Of GeoLine), density As Double, fillangle As Double, cfg As ProcessorConfiguration) As List(Of List(Of GeoLine))
        Dim outSegs As New List(Of List(Of GeoLine))()
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return outSegs

        Dim snapTol2 As Double = density * density
        Dim flatTol As Double = Math.Max(0.001, cfg.Tolerance) * FillProcessor.DefaultScalingFactor

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) / 2
        Dim centerY = (bounds.Top + bounds.Bottom) / 2
        Dim center As New Vector2(CSng(centerX), CSng(centerY))

        Dim maxRadius As Double = 0
        For Each ln In lines
            Dim d1 = Vector2.Distance(center, ln.StartPoint)
            Dim d2 = Vector2.Distance(center, ln.EndPoint)
            If d1 > maxRadius Then maxRadius = d1
            If d2 > maxRadius Then maxRadius = d2
        Next

        Dim b As Double = density / (2 * Math.PI)
        Dim bSq As Double = b * b
        Dim thetaOffset As Double = Math.PI * fillangle / 180
        Dim maxRadiusExtended = maxRadius + density


        Dim ctx As ShapeGridContext = BuildShapeGrid(lines, density)

        Dim theta As Double = 0
        Dim prevPoint As Vector2 = Nothing
        Dim havePrev As Boolean = False
        Dim currentSeg As List(Of GeoLine) = Nothing

        While True
            Dim r = b * theta
            If r > maxRadiusExtended Then Exit While

            Dim ang = theta + thetaOffset
            Dim p As New Vector2(
            center.X + CSng(r * Math.Cos(ang)),
            center.Y + CSng(r * Math.Sin(ang)))

            If havePrev Then
                Dim spiralEdge As New GeoLine(prevPoint, p)

                Dim clipped As List(Of List(Of GeoLine)) = ClipLinesAgainstShape(ctx, spiralEdge, isSegment:=True)
                StitchClippedPieces(clipped, currentSeg, outSegs, snapTol2)
            End If

            prevPoint = p
            havePrev = True

            'adaptive stepping 
            Dim rSq As Double = r * r
            Dim rSqPlusBSq As Double = rSq + bSq
            Dim ds As Double = Math.Sqrt(rSqPlusBSq)
            Dim rho As Double = (rSqPlusBSq * ds) / (rSq + 2.0 * bSq)
            Dim maxChord As Double = 2.0 * Math.Sqrt(2.0 * rho * flatTol)

            Dim dTheta As Double = If(ds > 0, maxChord / ds, 0.5)
            dTheta = Math.Clamp(dTheta, 0.01, 0.5)

            theta += dTheta
        End While

        If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
            outSegs.Add(currentSeg)
        End If

        Return outSegs
    End Function


    Private Shared Sub StitchClippedPieces(clipped As List(Of List(Of GeoLine)), ByRef currentSeg As List(Of GeoLine), outSegs As List(Of List(Of GeoLine)), snapTol2 As Double)
        If clipped.Count = 0 Then
            If currentSeg IsNot Nothing AndAlso currentSeg.Count > 0 Then
                outSegs.Add(currentSeg)
                currentSeg = Nothing
            End If
            Return
        End If

        For Each piece In clipped
            If piece Is Nothing OrElse piece.Count = 0 Then Continue For

            If currentSeg Is Nothing Then
                currentSeg = New List(Of GeoLine)(128)
                currentSeg.AddRange(piece)
                Continue For
            End If

            Dim curEnd As Vector2 = currentSeg(currentSeg.Count - 1).EndPoint
            Dim pieceStart As Vector2 = piece(0).StartPoint

            If curEnd.DistanceToSquaredG(pieceStart) <= snapTol2 Then
                currentSeg.AddRange(piece)
            Else
                outSegs.Add(currentSeg)
                currentSeg = New List(Of GeoLine)(128)
                currentSeg.AddRange(piece)
            End If
        Next
    End Sub


End Class

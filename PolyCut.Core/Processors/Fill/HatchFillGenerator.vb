Imports System.Windows

Public Class HatchFillGenerator

    Public Shared Function GenerateHatch(lines As List(Of GeoLine), density As Double, fillangle As Double) As List(Of List(Of GeoLine))
        Dim segments As New List(Of List(Of GeoLine))()
        If lines Is Nothing OrElse lines.Count = 0 OrElse density <= 0 Then Return segments

        Dim traverseAngleRad = Math.PI * fillangle / 180 + (Math.PI / 2)
        Dim fillAngleRad = Math.PI * fillangle / 180

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) * 0.5
        Dim centerY = (bounds.Top + bounds.Bottom) * 0.5

        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top

        Dim maxExtent = Math.Sqrt(dx * dx + dy * dy)
        Dim radius As Double = 0.5 * maxExtent
        If radius <= 0 Then Return segments

        Dim cosTrav = Math.Cos(traverseAngleRad)
        Dim sinTrav = Math.Sin(traverseAngleRad)
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        Dim ctx As ShapeGridContext = BuildShapeGrid(lines, density)

        For traversePosition As Double = -maxExtent To maxExtent Step density
            Dim sx As Double = centerX + traversePosition * cosTrav
            Dim sy As Double = centerY + traversePosition * sinTrav

            Dim ray As New GeoLine(
                X1:=sx - radius * cosFill,
                Y1:=sy - radius * sinFill,
                X2:=sx + radius * cosFill,
                Y2:=sy + radius * sinFill
            )

            segments.AddRange(ClipLinesAgainstShape(ctx, ray, isSegment:=False))
        Next

        Return segments
    End Function


    Friend Shared Function GenerateTriangularHatch(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of List(Of GeoLine))

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of List(Of GeoLine))()

        Dim result As New List(Of List(Of GeoLine))()

        ' Triangular lattice: perpendicular spacing = S * sin(60) = S * √3/2
        Const Sin60 As Double = 0.86602540378444
        Dim densityEff As Double = baseSpacing * Sin60

        result.AddRange(GenerateAlignedHatchFillCore(lines, densityEff, fillangle))
        result.AddRange(GenerateAlignedHatchFillCore(lines, densityEff, fillangle + 60))
        result.AddRange(GenerateAlignedHatchFillCore(lines, densityEff, fillangle + 120))

        Return result
    End Function


    Friend Shared Function GenerateDiamondCrossHatch(lines As List(Of GeoLine), baseSpacing As Double, fillangle As Double) As List(Of List(Of GeoLine))

        If lines Is Nothing OrElse lines.Count = 0 OrElse baseSpacing <= 0 Then Return New List(Of List(Of GeoLine))()

        Dim result As New List(Of List(Of GeoLine))()

        For Each angle In {fillangle, fillangle + 45, fillangle + 90, fillangle + 135}

            Dim fillAngleRad = Math.PI * angle / 180.0
            Dim cosFill = Math.Cos(fillAngleRad)
            Dim sinFill = Math.Sin(fillAngleRad)

            ' Square lattice factor: 0/90 => 1, 45/135 => 1/√2
            Dim factor As Double = Math.Max(Math.Abs(sinFill), Math.Abs(cosFill))
            Dim densityEff As Double = baseSpacing * factor
            If densityEff <= 0 Then densityEff = baseSpacing

            result.AddRange(GenerateAlignedHatchFillCore(lines, densityEff, angle))
        Next

        Return result

    End Function


    Private Shared Function GenerateAlignedHatchFillCore(lines As List(Of GeoLine), densityEff As Double, fillangle As Double) As List(Of List(Of GeoLine))

        Dim fills As New List(Of List(Of GeoLine))
        If lines Is Nothing OrElse lines.Count = 0 OrElse densityEff <= 0 Then Return fills

        Dim fillAngleRad = Math.PI * fillangle / 180.0

        ' Fill direction u
        Dim cosFill = Math.Cos(fillAngleRad)
        Dim sinFill = Math.Sin(fillAngleRad)

        ' Traverse normal n
        Dim nx As Double = -sinFill
        Dim ny As Double = cosFill

        Dim bounds As Rect = ComputeBounds(lines)
        Dim centerX = (bounds.Left + bounds.Right) * 0.5
        Dim centerY = (bounds.Top + bounds.Bottom) * 0.5

        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top

        Dim maxExtent = Math.Sqrt(dx * dx + dy * dy)
        Dim radius As Double = 0.5 * maxExtent
        If radius <= 0 Then Return fills

        ' Anchor at center 
        Dim anchorX As Double = centerX
        Dim anchorY As Double = centerY

        ' Traverse coordinate of center and anchor
        Dim tCenter As Double = centerX * nx + centerY * ny
        Dim tAnchor As Double = anchorX * nx + anchorY * ny

        Dim tMin As Double = tCenter - maxExtent
        Dim tMax As Double = tCenter + maxExtent

        Dim kMin As Integer = CInt(Math.Floor((tMin - tAnchor) / densityEff))
        Dim kMax As Integer = CInt(Math.Ceiling((tMax - tAnchor) / densityEff))
        If kMin > kMax Then
            Dim tmp = kMin : kMin = kMax : kMax = tmp
        End If

        Dim ctx As ShapeGridContext = BuildShapeGrid(lines, densityEff)

        For k As Integer = kMin To kMax
            Dim off As Double = k * densityEff
            Dim sx As Double = anchorX + off * nx
            Dim sy As Double = anchorY + off * ny

            Dim ray As New GeoLine(
            X1:=sx - radius * cosFill,
            Y1:=sy - radius * sinFill,
            X2:=sx + radius * cosFill,
            Y2:=sy + radius * sinFill
        )

            fills.AddRange(ClipLinesAgainstShape(ctx, ray, isSegment:=False))
        Next

        Return fills
    End Function



End Class

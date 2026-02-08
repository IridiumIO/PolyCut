Imports System.Numerics
Imports System.Windows

Public Class RadialFillGenerator
    Public Shared Function Generate(lines As List(Of GeoLine), spacing As Double, angleDeg As Double) As List(Of List(Of GeoLine))
        Dim fills As New List(Of List(Of GeoLine))
        If lines Is Nothing OrElse lines.Count = 0 OrElse spacing <= 0 Then Return fills

        Dim bounds As Rect = ComputeBounds(lines)
        Dim cx As Double = (bounds.Left + bounds.Right) * 0.5
        Dim cy As Double = (bounds.Top + bounds.Bottom) * 0.5
        Dim center As New Vector2(CSng(cx), CSng(cy))

        Dim dx = bounds.Right - bounds.Left
        Dim dy = bounds.Bottom - bounds.Top

        Dim radius As Double = 0.5 * Math.Sqrt(dx * dx + dy * dy)
        If radius <= 0 Then Return fills

        Dim dTheta As Double = spacing / radius
        dTheta = Math.Clamp(dTheta, 0.01, 0.5)

        Dim theta0 As Double = Math.PI * angleDeg / 180.0

        ' Cover full circle.  include endpoint so pattern is stable.
        Dim steps As Integer = Math.Max(1, CInt(Math.Ceiling((2.0 * Math.PI) / dTheta)))

        Dim ctx As ShapeGridContext = BuildShapeGrid(lines, spacing)

        For i As Integer = 0 To steps - 1
            Dim theta As Double = theta0 + i * (2.0 * Math.PI / steps)

            Dim ux As Double = Math.Cos(theta)
            Dim uy As Double = Math.Sin(theta)

            Dim p1 As New Vector2(CSng(cx - radius * ux), CSng(cy - radius * uy))
            Dim p2 As New Vector2(CSng(cx + radius * ux), CSng(cy + radius * uy))

            Dim ray As New GeoLine(p1, p2)

            fills.AddRange(ClipLinesAgainstShape(ctx, ray, isSegment:=False))
        Next

        Return fills
    End Function
End Class

Imports System.Windows.Media
Imports Svg

Public Module SvgHelpers
    Public Function BrushToSvgColourServer(brush As Brush) As SvgColourServer
        If brush Is Nothing Then Return Nothing
        Dim scb = TryCast(brush, SolidColorBrush)
        If scb Is Nothing Then Return Nothing
        Dim c = scb.Color

        Dim alpha As Byte = c.A

        If alpha = 0 Then
            ' Fully transparent
            Return Nothing
        End If

        Return New SvgColourServer(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B))
    End Function
End Module

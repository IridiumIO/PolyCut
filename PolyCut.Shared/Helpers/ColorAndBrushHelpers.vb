Imports System.Windows.Media

Imports Svg

Public NotInheritable Class ColorAndBrushHelpers

    Shared Function ColourToHex(colour As System.Drawing.Color, Optional returnAlpha As Boolean = False) As String
        If colour = Nothing Then Return Nothing
        If returnAlpha Then
            Return "#" & colour.A.ToString("X2") & colour.R.ToString("X2") & colour.G.ToString("X2") & colour.B.ToString("X2")
        Else
            Return "#" & colour.R.ToString("X2") & colour.G.ToString("X2") & colour.B.ToString("X2")
        End If
    End Function


    Public Shared Function SVGPaintServerToString(paintServer As SvgPaintServer) As String

        Dim colour As String

        If paintServer Is Nothing OrElse paintServer.ToString() = "none" OrElse paintServer.GetType().Name.Contains("None") Then
            colour = Nothing
        Else
            Dim casted = TryCast(paintServer, SvgColourServer)?.Colour
            colour = If(casted.HasValue, ColourToHex(casted), Nothing)
        End If

        Return colour
    End Function


    Public Shared Function BrushToSvgColourServer(brush As Brush) As SvgColourServer
        If brush Is Nothing Then Return Nothing
        Dim scb = TryCast(brush, SolidColorBrush)
        If scb Is Nothing Then Return Nothing

        Dim c = scb.Color
        If c.A = 0 Then Return Nothing 'Transparent

        Return New SvgColourServer(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B))
    End Function



    ' ---- Serialization Helpers ----
    Public Shared Function SerializeBrush(brush As Brush) As String
        If brush Is Nothing Then Return Nothing
        If TypeOf brush Is SolidColorBrush Then
            Dim solidBrush = CType(brush, SolidColorBrush)
            Return solidBrush.Color.ToString()
        End If
        Return Nothing
    End Function

    Public Shared Function DeserializeBrush(colorString As String) As Brush
        If String.IsNullOrEmpty(colorString) Then Return Brushes.Black
        Try
            Dim converter As New BrushConverter()
            Return CType(converter.ConvertFromString(colorString), Brush)
        Catch
            Return Brushes.Black
        End Try
    End Function





End Class


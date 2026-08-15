Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Module TextGeometryHelper

    Public Function GetPixelsPerDip(textBox As TextBox) As Double
        If textBox Is Nothing Then Return 1.0
        Try
            Return VisualTreeHelper.GetDpi(textBox).PixelsPerDip
        Catch
            Return 1.0
        End Try
    End Function

    Public Function CreateFormattedText(textBox As TextBox, text As String, Optional pixelsPerDip As Double? = Nothing,
                                        Optional trimming As TextTrimming = TextTrimming.None,
                                        Optional textAlignment As TextAlignment = TextAlignment.Left) As FormattedText
        If textBox Is Nothing Then Return Nothing

        Dim value As String = If(String.IsNullOrEmpty(text), " ", text)
        Dim ppd As Double = If(pixelsPerDip.HasValue, pixelsPerDip.Value, GetPixelsPerDip(textBox))

        Return New FormattedText(
            value,
            CultureInfo.CurrentCulture,
            textBox.FlowDirection,
            New Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
            textBox.FontSize,
            Brushes.Black,
            ppd) With {
                .Trimming = trimming,
                .TextAlignment = textAlignment
            }
    End Function

    Public Function TryGetContentOrigin(textBox As TextBox, ByRef origin As Point) As Boolean
        If textBox Is Nothing Then
            origin = New Point(0, 0)
            Return False
        End If

        Try
            Dim r As Rect = textBox.GetRectFromCharacterIndex(0, False)
            If Not r.IsEmpty AndAlso Not Double.IsNaN(r.X) AndAlso Not Double.IsNaN(r.Y) Then
                origin = New Point(r.X, r.Y)
                Return True
            End If
        Catch
        End Try

        origin = New Point(0, 0)
        Return False
    End Function

    Public Function GetContentOrigin(textBox As TextBox, Optional fallback As Point = Nothing) As Point
        Dim origin As Point
        If TryGetContentOrigin(textBox, origin) Then Return origin
        Return fallback
    End Function

    Public Function BuildTextGeometry(textBox As TextBox, Optional text As String = Nothing, Optional origin As Point? = Nothing,
                                      Optional pixelsPerDip As Double? = Nothing,
                                      Optional trimming As TextTrimming = TextTrimming.None,
                                      Optional textAlignment As TextAlignment = TextAlignment.Left) As Geometry
        If textBox Is Nothing Then Return Nothing

        Dim value As String = If(text, textBox.Text)
        If String.IsNullOrEmpty(value) Then Return Nothing

        Dim ft = CreateFormattedText(textBox, value, pixelsPerDip, trimming, textAlignment)
        If ft Is Nothing Then Return Nothing

        Dim o As Point = If(origin.HasValue, origin.Value, GetContentOrigin(textBox, New Point(0, 0)))
        Return ft.BuildGeometry(o)
    End Function

    Public Function MeasureTextWidth(textBox As TextBox, text As String, Optional pixelsPerDip As Double? = Nothing) As Double
        Dim ft = CreateFormattedText(textBox, text, pixelsPerDip)
        If ft Is Nothing Then Return 0
        Return ft.Width
    End Function

End Module

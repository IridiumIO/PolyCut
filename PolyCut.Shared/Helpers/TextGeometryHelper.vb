Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Module TextGeometryHelper

    ' Report the em height in document space. The perpendicular height (area divided by baseline length) excludes rotation, horizontal stretching, and baseline shear.
    Public Function GetEffectiveFontSize(textBox As TextBox, Optional documentRoot As UIElement = Nothing) As Double
        If textBox Is Nothing Then Return 0
        Return textBox.FontSize * GetFontHeightScale(textBox, documentRoot)
    End Function

    Public Function SetEffectiveFontSize(textBox As TextBox, size As Double, Optional documentRoot As UIElement = Nothing) As Boolean
        If textBox Is Nothing OrElse Not Double.IsFinite(size) OrElse size <= 0 Then Return False

        textBox.UpdateLayout()
        Dim scale = GetFontHeightScale(textBox, documentRoot)
        If scale <= 0 Then Return False
        Dim wrapper = TryCast(textBox.Parent, ContentControl)
        If wrapper Is Nothing Then
            If Not TextBox.FontSizeProperty.IsValidValue(size) Then Return False
            textBox.FontSize = size
        Else
            ' Resize the whole object, including its chrome and glyph outline. 
            Dim ratio = size / (textBox.FontSize * scale)
            If Not Double.IsFinite(ratio) OrElse ratio <= 0 Then Return False
            Dim state = TransformState.FromWrapper(wrapper)
            Dim affine = state.AffineTransform
            affine.Scale(ratio, ratio)
            TransformState.SetAffineTransform(wrapper, affine)
        End If
        Return True
    End Function

    Private Function GetFontHeightScale(textBox As TextBox, documentRoot As UIElement) As Double
        If documentRoot Is Nothing Then
            ' The outermost canvas is the document; viewport zoom lives above it.
            Dim current = VisualTreeHelper.GetParent(textBox)
            While current IsNot Nothing
                If TypeOf current Is Canvas Then documentRoot = DirectCast(current, Canvas)
                current = VisualTreeHelper.GetParent(current)
            End While
        End If
        If documentRoot Is Nothing Then Return 1

        Dim m = TransformMath.GetAccumulatedMatrix(textBox, documentRoot)
        Dim baseline = Math.Sqrt(m.M11 * m.M11 + m.M12 * m.M12)
        If baseline <= Double.Epsilon Then Return 1
        Return Math.Abs(m.Determinant) / baseline
    End Function

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
    Public Sub FitToInk(textBox As TextBox)
        If textBox Is Nothing Then Return
        If String.IsNullOrEmpty(textBox.Text) Then Return
        If textBox.TextWrapping <> TextWrapping.NoWrap Then Return

        Dim ft = CreateFormattedText(textBox, textBox.Text, , TextTrimming.None, textBox.TextAlignment)
        If ft Is Nothing Then Return

        Dim geometry = ft.BuildGeometry(New Point(0, 0))
        If geometry Is Nothing Then Return

        Dim inset As Double = 0
        Dim host = TryCast(textBox.Template.FindName("PART_ContentHost", textBox), ScrollViewer)
        If host IsNot Nothing Then
            Dim hostOrigin = host.TransformToAncestor(textBox).Transform(New Point(0, 0))
            Dim contentOrigin As Point
            If TryGetContentOrigin(textBox, contentOrigin) Then inset = Math.Max(0.0, contentOrigin.X - hostOrigin.X)
        End If

        Dim chrome = textBox.BorderThickness.Left + textBox.BorderThickness.Right
        textBox.Width = chrome + (3 * inset) + Math.Max(ft.WidthIncludingTrailingWhitespace, geometry.Bounds.Right) + 2
    End Sub
    Public Sub UseDefaultContextMenuFont(textBox As TextBox)
        If textBox Is Nothing Then Return

        Dim menu = TryCast(textBox.FindResource("TextEditContextMenu"), ContextMenu)
        If menu IsNot Nothing Then textBox.ContextMenu = menu

        RemoveHandler textBox.ContextMenuOpening, AddressOf OnTextBoxContextMenuOpening
        AddHandler textBox.ContextMenuOpening, AddressOf OnTextBoxContextMenuOpening
    End Sub

    Private Sub OnTextBoxContextMenuOpening(sender As Object, e As ContextMenuEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox Is Nothing OrElse textBox.ContextMenu Is Nothing Then Return

        textBox.ContextMenu.PlacementTarget = textBox
        textBox.ContextMenu.IsOpen = True
        e.Handled = True
    End Sub

End Module

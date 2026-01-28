Imports System.Reflection
Imports System.Windows
Imports System.Windows.Media

Public Class ColorPreviewWindow
    Private Const OffsetX As Integer = 10
    Private Const OffsetY As Integer = 10

    Public Sub UpdateAtCursor(screenX As Integer, screenY As Integer, c As Color)

        Swatch.Background = New SolidColorBrush(c)
        HexText.Text = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
        NamedText.Text = ColorNameHelper.GetColorNameIdentifier(c)

        Left = screenX + OffsetX
        Top = screenY + OffsetY
    End Sub

    Private Sub Window_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Escape Then
            Me.Close()
            e.Handled = True
        End If
    End Sub



End Class


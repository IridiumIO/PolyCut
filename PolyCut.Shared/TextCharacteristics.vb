Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

'Snapshot of a text element's font properties. Consolidates FontFamily / FontStyle /FontWeight / FontSize so they can be passed around as one unit
Public Class TextCharacteristics

    Public Property FontFamily As FontFamily
    Public Property FontStyle As FontStyle
    Public Property FontWeight As FontWeight
    Public Property FontSize As Double

    Public Shared Function FromTextBox(textBox As TextBox) As TextCharacteristics
        If textBox Is Nothing Then Return New TextCharacteristics()
        Return New TextCharacteristics With {
            .FontFamily = textBox.FontFamily,
            .FontStyle = textBox.FontStyle,
            .FontWeight = textBox.FontWeight,
            .FontSize = textBox.FontSize
        }
    End Function

    Public Sub ApplyTo(textBox As TextBox)
        If textBox Is Nothing Then Return
        If FontFamily IsNot Nothing Then textBox.FontFamily = FontFamily
        textBox.FontStyle = FontStyle
        textBox.FontWeight = FontWeight
        textBox.FontSize = FontSize
    End Sub

    'Value comparison - "did anything change" checks. 
    Public Function SameAs(other As TextCharacteristics) As Boolean
        If other Is Nothing Then Return False
        If Not String.Equals(FontFamily?.Source, other.FontFamily?.Source, StringComparison.OrdinalIgnoreCase) Then Return False
        If Not FontStyle.Equals(other.FontStyle) Then Return False
        If Not FontWeight.Equals(other.FontWeight) Then Return False
        Return FontSize = other.FontSize
    End Function

End Class

Imports System.Windows

Public Class ColorPickerControl
    Inherits UserControl

    ' Label text property
    Public Shared ReadOnly LabelTextProperty As DependencyProperty =
        DependencyProperty.Register("LabelText", GetType(String), GetType(ColorPickerControl), New PropertyMetadata("Color"))

    Public Property LabelText As String
        Get
            Return CStr(GetValue(LabelTextProperty))
        End Get
        Set(value As String)
            SetValue(LabelTextProperty, value)
        End Set
    End Property

    ' Current color property
    Public Shared ReadOnly CurrentColorProperty As DependencyProperty =
        DependencyProperty.Register("CurrentColor", GetType(Brush), GetType(ColorPickerControl),
            New PropertyMetadata(Brushes.Black, AddressOf OnCurrentColorChanged))

    Public Property CurrentColor As Brush
        Get
            Return CType(GetValue(CurrentColorProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(CurrentColorProperty, value)
        End Set
    End Property

    Private Shared Sub OnCurrentColorChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = TryCast(d, ColorPickerControl)
        If control Is Nothing Then Return
        control.UpdateHexFromColor()
    End Sub

    ' Hex color property
    Public Shared ReadOnly HexColorProperty As DependencyProperty =
        DependencyProperty.Register("HexColor", GetType(String), GetType(ColorPickerControl),
            New PropertyMetadata("#FF000000", AddressOf OnHexColorChanged))

    Public Property HexColor As String
        Get
            Return CStr(GetValue(HexColorProperty))
        End Get
        Set(value As String)
            SetValue(HexColorProperty, value)
        End Set
    End Property

    Private Shared Sub OnHexColorChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = TryCast(d, ColorPickerControl)
        If control Is Nothing OrElse control._updatingHex Then Return
        control.UpdateColorFromHex()
    End Sub

    ' Event for color selection
    Public Event ColorSelected As EventHandler(Of ColorSelectedEventArgs)

    ' Event fired when popup opens (to capture old color for undo)
    Public Event PopupOpening As EventHandler

    Private _updatingHex As Boolean = False

    Private Sub ColorButton_Click(sender As Object, e As RoutedEventArgs)
        RaiseEvent PopupOpening(Me, EventArgs.Empty)
        UpdateHexFromColor()
        ColorPopup.IsOpen = True
    End Sub

    Private Sub ColorMenuItem_Click(sender As Object, e As RoutedEventArgs)
        Dim button = TryCast(sender, Button)
        If button Is Nothing Then Return

        Dim brush = TryCast(button.Tag, Brush)
        If brush Is Nothing Then Return

        ColorPopup.IsOpen = False
        RaiseEvent ColorSelected(Me, New ColorSelectedEventArgs(brush))
    End Sub

    Private Sub HexTextBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Enter Then
            UpdateColorFromHex()
            If IsValidHex(HexColor) Then
                ColorPopup.IsOpen = False
            End If
            e.Handled = True
        ElseIf e.Key = Key.Escape Then
            ColorPopup.IsOpen = False
            e.Handled = True
        End If
    End Sub

    Private Sub HexTextBox_LostFocus(sender As Object, e As RoutedEventArgs)
        UpdateColorFromHex()
    End Sub

    Private Sub UpdateHexFromColor()
        _updatingHex = True
        Try
            Dim solidBrush = TryCast(CurrentColor, SolidColorBrush)
            If solidBrush IsNot Nothing Then
                Dim c = solidBrush.Color
                HexColor = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
            Else
                HexColor = "#FF000000"
            End If
        Finally
            _updatingHex = False
        End Try
    End Sub

    Private Sub UpdateColorFromHex()
        If String.IsNullOrWhiteSpace(HexColor) Then Return

        Dim hexValue = HexColor.Trim()
        If Not hexValue.StartsWith("#") Then
            hexValue = "#" & hexValue
        End If

        If IsValidHex(hexValue) Then
            Try
                Dim color = CType(ColorConverter.ConvertFromString(hexValue), Color)
                Dim brush As New SolidColorBrush(color)
                RaiseEvent ColorSelected(Me, New ColorSelectedEventArgs(brush))
            Catch
                ' Invalid hex, revert to current color
                UpdateHexFromColor()
            End Try
        End If
    End Sub

    Private Function IsValidHex(hex As String) As Boolean
        If String.IsNullOrWhiteSpace(hex) Then Return False
        If Not hex.StartsWith("#") Then Return False

        Dim hexDigits = hex.Substring(1)
        Return (hexDigits.Length = 6 OrElse hexDigits.Length = 8) AndAlso
               hexDigits.All(Function(c) "0123456789ABCDEFabcdef".Contains(c))
    End Function
End Class

Public Class ColorSelectedEventArgs
    Inherits EventArgs

    Public Property SelectedBrush As Brush

    Public Sub New(brush As Brush)
        SelectedBrush = brush
    End Sub
End Class


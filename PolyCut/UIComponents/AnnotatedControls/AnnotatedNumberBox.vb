Imports System.Globalization

Public Class AnnotatedNumberBox
    Inherits AnnotatedControl

    'Still using Textbox because I'm scare that WpfUI's Numberbox is still broken

    Protected ReadOnly InputBox As New Wpf.Ui.Controls.TextBox With {.ClearButtonEnabled = False, .VerticalContentAlignment = VerticalAlignment.Center}

    Public Sub New()
        InputBox.Padding = New Thickness(8, 0, 35, 0)
        InputBox.SetBinding(WidthProperty, New Binding(NameOf(EditorWidth)) With {.Source = Me})
        InputBox.SetBinding(TextBox.TextProperty, New Binding(NameOf(Value)) With {.Source = Me, .Mode = BindingMode.TwoWay, .UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, .Converter = New NumberConverter(), .ConverterParameter = Me})
        AddHandler InputBox.GotKeyboardFocus, Sub() InputBox.SelectAll()
        AddHandler InputBox.TextChanged, Sub() RaiseEvent TextChanged(Me, New RoutedEventArgs())
        Dim editor As New Grid()
        editor.Children.Add(InputBox)
        Dim unit As New TextBlock With {.HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 10, 0), .IsHitTestVisible = False, .Opacity = 0.5}
        unit.SetBinding(TextBlock.TextProperty, New Binding(NameOf(UnitText)) With {.Source = Me})
        editor.Children.Add(unit)
        SetEditor(editor)
    End Sub

    Public Event TextChanged As RoutedEventHandler

    Public ReadOnly Property Text As String
        Get
            Return InputBox.Text
        End Get
    End Property

    Protected Overrides ReadOnly Property BoundValueProperty As DependencyProperty
        Get
            Return ValueProperty
        End Get
    End Property

    Public Shared ReadOnly ValueProperty As DependencyProperty = DependencyProperty.Register(NameOf(Value), GetType(Double), GetType(AnnotatedNumberBox), New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault))
    Public Property Value As Double
        Get
            Return DirectCast(GetValue(ValueProperty), Double)
        End Get
        Set(value As Double)
            SetValue(ValueProperty, value)
        End Set
    End Property

    Public Shared ReadOnly AllowZeroProperty As DependencyProperty = DependencyProperty.Register(NameOf(AllowZero), GetType(Boolean), GetType(AnnotatedNumberBox), New PropertyMetadata(True))
    Public Property AllowZero As Boolean
        Get
            Return DirectCast(GetValue(AllowZeroProperty), Boolean)
        End Get
        Set(value As Boolean)
            SetValue(AllowZeroProperty, value)
        End Set
    End Property

    Public Shared ReadOnly AllowNegativeProperty As DependencyProperty = DependencyProperty.Register(NameOf(AllowNegative), GetType(Boolean), GetType(AnnotatedNumberBox), New PropertyMetadata(False))
    Public Property AllowNegative As Boolean
        Get
            Return DirectCast(GetValue(AllowNegativeProperty), Boolean)
        End Get
        Set(value As Boolean)
            SetValue(AllowNegativeProperty, value)
        End Set
    End Property

    Private Class NumberConverter
        Implements IValueConverter

        'TODO: Maybe I should move this to the converters or avoid calling a converter from a converter
        Private Shared ReadOnly Units As New InputToMillimetresConverter()

        Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
            Return Units.Convert(value, targetType, Nothing, culture)
        End Function

        Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
            If String.IsNullOrWhiteSpace(TryCast(value, String)) Then Return DependencyProperty.UnsetValue
            Dim parsed = Units.ConvertBack(value, GetType(Double), "allownegative", culture)
            If parsed Is DependencyProperty.UnsetValue Then Return parsed
            Dim editor = DirectCast(parameter, AnnotatedNumberBox)
            Dim number = CDbl(parsed)
            If Not Double.IsFinite(number) OrElse (number = 0 AndAlso Not editor.AllowZero) OrElse (number < 0 AndAlso Not editor.AllowNegative) Then Return DependencyProperty.UnsetValue
            Return number
        End Function
    End Class
End Class

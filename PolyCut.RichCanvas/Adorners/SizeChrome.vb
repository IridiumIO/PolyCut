Imports System.Globalization

Public Class SizeChrome
    Inherits Control

    Shared Sub New()
        FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(GetType(SizeChrome), New FrameworkPropertyMetadata(GetType(SizeChrome)))
    End Sub
End Class

Public Class DoubleFormatConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim d As Double = CType(value, Double)
        Return Math.Round(d)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return Nothing
    End Function
End Class
Imports System.Data
Imports System.Globalization
Imports System.Text.RegularExpressions

Imports PolyCut.Core
Imports PolyCut.Shared

Public Class ZoomFactorToThicknessConverter
    Implements IMultiValueConverter

    Public Function Convert(values As Object(), targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert
        If values IsNot Nothing AndAlso values.Length > 0 AndAlso TypeOf values(0) Is Double Then
            Dim zoomFactor As Double = DirectCast(values(0), Double)
            Dim baseThickness As Double = 2 ' Adjust this value according to your needs
            Return New Thickness(baseThickness / zoomFactor)
        End If
        Return Binding.DoNothing
    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type(), parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function


End Class

Public Class InputToMillimetresConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Return CDec(value)
    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim input As String = TryCast(value, String)

        If input Is Nothing Then
            Return DependencyProperty.UnsetValue
        End If
        input = input.Replace(" ", "")
        Dim operators = {"+", "-", "*", "/"}

        If operators.Any(Function(op) input.Contains(op)) Then

            Dim componentStrings() = input.Split(operators, StringSplitOptions.None)
            Dim resultArray As String() = Regex.Split(input, $"({String.Join("|", {"\+", "-", "\*", "/"})})")

            For i = 0 To resultArray.Length - 1
                If Not operators.Contains(resultArray(i)) Then
                    resultArray(i) = CStr(unitConverter(resultArray(i)))
                End If
            Next



            Dim expression As String = String.Join("", resultArray)

            Try
                Dim dataTable As New DataTable()
                Dim result As Object = dataTable.Compute(expression, String.Empty)
                Debug.WriteLine(result)
                input = result
            Catch ex As Exception
                ' Handle invalid expressions or other exceptions
                Debug.WriteLine("error")
                input = 0
            End Try

        End If

        Dim convertedDecimal As Decimal = unitConverter(input)
        If convertedDecimal <= 0 Then Return 0
        Return convertedDecimal


    End Function

    Private Shared Function unitConverter(value As String) As Decimal
        value = value.ToLower.Trim
        If Not IsNumeric(value) Then
            If value.EndsWith("in") OrElse value.EndsWith("inches") Then
                value = ExtractDecimalNumber(value)
                value *= 25.4
            ElseIf value.EndsWith("cm") OrElse value.EndsWith("centimetres") Then
                value = ExtractDecimalNumber(value)
                value *= 10
            ElseIf value.EndsWith("mm") OrElse value.EndsWith("millimetres") Then
                value = ExtractDecimalNumber(value)
            Else
                value = ExtractDecimalNumber(value)
            End If
        End If

        Return Math.Round(CDbl(value), 4)

    End Function

    Private Shared Function ExtractDecimalNumber(Input As String) As Decimal

        Dim pattern As String = "[-+]?\d*\.?\d+([eE][-+]?\d+)?"

        Dim match As Match = Regex.Match(Input, pattern)

        If match.Success Then
            Dim matchedValue As String = match.Value

            Dim parsedValue As Decimal
            If Decimal.TryParse(matchedValue, NumberStyles.Any, CultureInfo.InvariantCulture, parsedValue) Then
                Return parsedValue
            End If
        End If

        Return Nothing
    End Function

End Class



Public Class AnimationFactorToValueConverter
    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert
        If TypeOf values(0) IsNot Double Then
            Return 0.0
        End If

        Dim completeValue As Double = DirectCast(values(0), Double)

        If TypeOf values(1) IsNot Double Then
            Return 0.0
        End If

        Dim factor As Double = DirectCast(values(1), Double)

        If parameter IsNot Nothing AndAlso parameter.ToString() = "negative" Then
            factor = -factor
        End If

        Return factor * completeValue
    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

Public Class InverseBoolToVisConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim vx = CBool(value)

        If vx = True Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Dim vx = CBool(value)

        If vx = True Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

End Class



Public Class InverseBoolConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim vx = CBool(value)
        Return Not vx
    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException
    End Function

End Class



Public Class ToolModeToVisConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim vx = CInt(value)

        If vx = True Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Dim vx = CBool(value)

        If vx = True Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

End Class

Public Class NullableIntConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Return value

    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        If value = "" Then
            Return Nothing
        End If

        Dim int As Integer = Nothing

        If Integer.TryParse(value, int) Then
            Return CInt(value)
        End If

        Return Nothing

    End Function

End Class

Public Class RadioButtonConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Return CType(value, ProcessorConfiguration.ToolMode).HasFlag(CType(parameter, ProcessorConfiguration.ToolMode))

        Return False
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return If(value.Equals(True), parameter, Binding.DoNothing)
    End Function
End Class


Public Class PathTrimmerConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is String AndAlso targetType Is GetType(String) Then
            Dim path As String = DirectCast(value, String)
            Dim maxLength As Integer = 45

            If path.Length > maxLength Then
                Dim parts As String() = path.Split("\"c)
                Dim firstPart As String = parts.FirstOrDefault()
                Dim lastPart As String = parts.LastOrDefault()

                If String.IsNullOrEmpty(firstPart) OrElse String.IsNullOrEmpty(lastPart) Then Return path

                If lastPart.Length > maxLength - firstPart.Length - 5 Then
                    lastPart = lastPart.Substring(0, maxLength - firstPart.Length - 10)

                End If

                If firstPart.Length + lastPart.Length + 5 <= maxLength Then ' 5 for the "...\"
                    Return $"{firstPart}\...\{lastPart}"
                Else
                    Dim ellipsisLength As Integer = Math.Max(0, maxLength - lastPart.Length - 5)
                    Dim ret = $"{firstPart.Substring(0, ellipsisLength)}\...\{lastPart}"
                    Return $"fuck"
                End If
            Else
                Return path
            End If
        End If

        Return value
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class ComparisonConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.Convert
        Return value?.Equals(parameter)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        If value?.Equals(True) Then
            Return parameter
        Else
            Return Binding.DoNothing
        End If
    End Function
End Class


Public Class CanvasToolModeCursorConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.Convert
        Dim toolMode As CanvasMode = CType(value, CanvasMode)
        Dim cursor As String = "Arrow"

        Select Case toolMode
            Case CanvasMode.Selection
                cursor = "Arrow"
            Case CanvasMode.Rectangle
                cursor = "Cross"
            Case CanvasMode.Line
                cursor = "Cross"
            Case CanvasMode.Ellipse
                cursor = "Cross"
            Case CanvasMode.Text
                cursor = "IBeam"
        End Select

        Return cursor
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

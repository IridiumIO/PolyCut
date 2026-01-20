Imports System.Data
Imports System.Globalization
Imports System.Reflection
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
        If value IsNot Nothing AndAlso IsNumeric(value) AndAlso CStr(value).ToLower <> "nan" Then
            Return Math.Round(CDec(value), 4)
        End If
        Return 0
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim input As String = TryCast(value, String)

        If input Is Nothing Then
            Return DependencyProperty.UnsetValue
        End If
        input = input.Replace(" ", "")
        Dim operators = New List(Of Char) From {"+", "-", "*", "/"}

        If operators.Exists(Function(op) input.Contains(op)) Then

            Dim resultArray As String() = Regex.Split(input, $"({String.Join("|", "\+", "-", "\*", "/")})")

            For i = 0 To resultArray.Length - 1
                If Not operators.Contains(resultArray(i)) Then
                    resultArray(i) = CStr(UnitConverter(resultArray(i)))
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
                Return DependencyProperty.UnsetValue
            End Try

        End If

        Dim convertedDecimal As Decimal = UnitConverter(input)
        If convertedDecimal <= 0D Then

            Return DependencyProperty.UnsetValue
        End If

        If targetType Is GetType(Double) OrElse targetType Is GetType(Double?) Then
            Return CDbl(convertedDecimal)
        End If

        If targetType Is GetType(Decimal) OrElse targetType Is GetType(Decimal?) Then
            Return convertedDecimal
        End If

        Return CDbl(convertedDecimal)


    End Function

    Private Shared Function UnitConverter(value As String) As Decimal
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

        If CBool(value) Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        If CBool(value) Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

End Class



Public Class InverseBoolConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Return Not CBool(value)
    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException
    End Function

End Class



Public Class ToolModeToVisConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If CInt(value) Then
            Return Visibility.Hidden
        End If

        Return Visibility.Visible

    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        If CBool(value) Then
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
            Dim maxLength As Integer = 40

            If path.Length <= maxLength Then
                Return path
            End If

            Dim parts As String() = path.Split(New Char() {"\"c, "/"c}, StringSplitOptions.None)
            Dim firstPart As String = parts.FirstOrDefault()
            Dim lastPart As String = parts.LastOrDefault()

            If String.IsNullOrEmpty(firstPart) OrElse String.IsNullOrEmpty(lastPart) Then
                Dim take = Math.Max(0, maxLength - 3)
                Return path.Substring(0, Math.Min(path.Length, take)) & "..."
            End If

            If firstPart.Length + 5 + lastPart.Length <= maxLength Then
                Return $"{firstPart}\...\{lastPart}"
            End If

            Dim availableForLast = maxLength - firstPart.Length - 5 ' 5 for "\...\"
            If availableForLast > 0 Then
                Dim truncatedLast = lastPart.Substring(0, Math.Min(lastPart.Length, availableForLast))
                Return $"{firstPart}\...\{truncatedLast}"
            End If

            Dim takeFirst = Math.Max(0, maxLength - 3)
            Return firstPart.Substring(0, Math.Min(firstPart.Length, takeFirst)) & "..."

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
            Case CanvasMode.Text
                cursor = "IBeam"
            Case Else
                cursor = "Cross"
        End Select

        Return cursor
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

Public Class SelectedObjectIsTextboxToVisConverter
    Implements IValueConverter
    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf (value) Is TextBox Then
            Return Visibility.Visible
        Else
            Return Visibility.Hidden
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class

Public Class GCodeToFlowDocumentConverter
    Implements IValueConverter

    Private Shared ReadOnly TokenRegex As New Regex(
        "(?ix)
            (?<Comment>        ;.*$ )
          | (?<ParenComment>   \(.*?\) )
          | (?<KlipperExpr>    \[[^\]]+\] )
          | (?<KlipperParam>   \b[A-Z_][A-Z0-9_]*=[^\s]+ )
          | (?<GCode>          \b[GM]\d+(?:\.\d+)?\b )
          | (?<Axis>           \b[XYZ][+-]?\d+(?:\.\d+)?\b )
          | (?<Feed>           \b[FSE][+-]?\d+(?:\.\d+)?\b )
          | (?<Macro>          \b[A-Z_]{2,}[A-Z0-9_]*\b )
          | (?<Number>         [+-]?\d+(?:\.\d+)? )
        ",
        RegexOptions.Compiled)

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim doc As New FlowDocument With {.PagePadding = New Thickness(0), .TextAlignment = TextAlignment.Left}

        Dim text = If(value?.ToString(), "")
        If text.Length = 0 Then Return doc

        For Each line In text.Replace(vbCr, "").Split(vbLf)
            Dim para As New Paragraph With {.Margin = New Thickness(0)}
            Dim last = 0

            If line.Trim() = ";######################################" Then
                ' Add a horizontal rule
                Dim hr As New Border With {
                        .BorderBrush = Brushes.Gray,
                        .BorderThickness = New Thickness(0, 0, 0, 1),
                        .Height = 4,
                        .Margin = New Thickness(0, 4, 0, 10)
                    }
                doc.Blocks.Add(New BlockUIContainer(hr))
                Continue For ' skip normal tokenization
            End If


            For Each m As Match In TokenRegex.Matches(line)

                If m.Index > last Then
                    para.Inlines.Add(New Run(line.Substring(last, m.Index - last)))
                End If

                If m.Groups("Comment").Success Then
                    ' Add any text before the comment
                    If m.Index > last Then
                        para.Inlines.Add(New Run(line.Substring(last, m.Index - last)))
                    End If

                    ' Strip leading ';'
                    Dim commentText = m.Value.Substring(1).TrimStart
                    para.Inlines.Add(New Run(";") With {.Foreground = Brushes.Transparent, .FontSize = 1})
                    para.Inlines.Add(New Run(commentText) With {.Foreground = Brushes.Gray})

                    last = line.Length ' prevetnt trailing text from being added
                    Exit For
                End If

                Dim run As New Run(m.Value)

                Select Case True

                    Case m.Groups("GCode").Success
                        run.Foreground = New SolidColorBrush(Color.FromRgb(&H59, &HAF, &HEF))
                        run.FontWeight = FontWeights.Bold

                    Case m.Groups("Macro").Success
                        run.Foreground = Brushes.OrangeRed
                        run.FontWeight = FontWeights.Bold

                    Case m.Groups("KlipperParam").Success
                        run.Foreground = New SolidColorBrush(Color.FromRgb(&H2E, &H8B, &H57))

                    Case m.Groups("KlipperExpr").Success
                        run.Foreground = New SolidColorBrush(Color.FromRgb(&HCC, &H78, &H32))

                    Case m.Groups("Axis").Success
                        run.Foreground = New SolidColorBrush(Color.FromRgb(&HE0, &H6C, &H75))

                    Case m.Groups("Feed").Success
                        run.Foreground = New SolidColorBrush(Color.FromRgb(&HC7, &H6B, &H0))

                    Case m.Groups("Number").Success
                        run.Foreground = Brushes.DarkCyan

                    Case Else
                        run.Foreground = Brushes.Black
                End Select

                para.Inlines.Add(run)
                last = m.Index + m.Length
            Next

            If last < line.Length Then
                para.Inlines.Add(New Run(line.Substring(last)))
            End If

            doc.Blocks.Add(para)
        Next

        Return doc
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object _
        Implements IValueConverter.ConvertBack

        Throw New NotImplementedException()
    End Function
End Class

Public Class SelectedPageIsTypeConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Try
            If value Is Nothing OrElse parameter Is Nothing Then
                Return Visibility.Collapsed
            End If

            ' parameter is expected to be a Type (from {x:Type local:SVGPage})
            Dim expectedType As Type = TryCast(parameter, Type)
            If expectedType Is Nothing Then
                Return Visibility.Collapsed
            End If

            ' SelectedItem is a NavigationViewItem; try to read its TargetPageType property via reflection
            Dim prop As PropertyInfo = value.GetType().GetProperty("TargetPageType")
            If prop Is Nothing Then
                Return Visibility.Collapsed
            End If

            Dim pageTypeObj = prop.GetValue(value)
            Dim pageType As Type = TryCast(pageTypeObj, Type)
            If pageType Is Nothing Then
                Return Visibility.Collapsed
            End If

            Return If(pageType Is expectedType, Visibility.Visible, Visibility.Collapsed)
        Catch
            Return Visibility.Collapsed
        End Try
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function
End Class


'Converter to return true only if count = 1. This implementation is cursed and only for the visibility of transform boxes
'because it technically uses the fact that the SelectedDrawables.Count() in MainVM doesn't update before the binding is evaluated
'Should I fix it? Yes. Will I? Probably not.
Public Class CountEqualsOneToBoolConverter
    Implements IValueConverter
    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is Integer Then
            Return CInt(value) = 0
        End If
        Return False
    End Function
    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function
End Class
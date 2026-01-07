

Public NotInheritable Class InlineBuilder
        Private Sub New()
        End Sub

        ' Public simple DTO used by tokenizer -> UI
        Public Class TokenDto
            Public Property Type As Integer
            Public Property Text As String
            Public Sub New(t As Integer, txt As String)
                Type = t
                Text = txt
            End Sub
        End Class

        Public Class LineTokens
            Public Property IsHorizontalRule As Boolean = False
            Public Property Tokens As List(Of TokenDto) = New List(Of TokenDto)()
        End Class

        Public Shared ReadOnly TokensProperty As DependencyProperty =
            DependencyProperty.RegisterAttached(
                "Tokens",
                GetType(Object),
                GetType(InlineBuilder),
                New PropertyMetadata(Nothing, AddressOf OnTokensChanged))

        Public Shared Sub SetTokens(target As DependencyObject, value As Object)
            target.SetValue(TokensProperty, value)
        End Sub

        Public Shared Function GetTokens(target As DependencyObject) As Object
            Return target.GetValue(TokensProperty)
        End Function

        ' Shared brushes (frozen where possible)
        Private Shared ReadOnly brushGCode As SolidColorBrush = New SolidColorBrush(Color.FromRgb(&H59, &HAF, &HEF))
        Private Shared ReadOnly brushMacro As SolidColorBrush = Brushes.OrangeRed
        Private Shared ReadOnly brushKlipperParam As SolidColorBrush = New SolidColorBrush(Color.FromRgb(&H2E, &H8B, &H57))
        Private Shared ReadOnly brushKlipperExpr As SolidColorBrush = New SolidColorBrush(Color.FromRgb(&HCC, &H78, &H32))
        Private Shared ReadOnly brushAxis As SolidColorBrush = New SolidColorBrush(Color.FromRgb(&HE0, &H6C, &H75))
        Private Shared ReadOnly brushFeed As SolidColorBrush = New SolidColorBrush(Color.FromRgb(&HC7, &H6B, &H0))
        Private Shared ReadOnly brushNumber As SolidColorBrush = Brushes.DarkCyan
        Private Shared ReadOnly brushComment As SolidColorBrush = Brushes.Gray
        Private Shared ReadOnly brushDefault As SolidColorBrush = Brushes.Black
        Private Shared ReadOnly transparentSemicolon As SolidColorBrush = New SolidColorBrush(Colors.Transparent)

        Shared Sub New()
            ' Freeze brushes to improve rendering perf
            For Each f As Freezable In New Freezable() {brushGCode, brushKlipperParam, brushKlipperExpr, brushAxis, brushFeed, brushNumber, brushComment, brushDefault, transparentSemicolon}
                If f IsNot Nothing AndAlso f.CanFreeze Then
                    Try
                        f.Freeze()
                    Catch
                    End Try
                End If
            Next
        End Sub

        Private Shared Sub OnTokensChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim tb = TryCast(d, TextBlock)
            If tb Is Nothing Then Return

            tb.Inlines.Clear()

            Dim lt = TryCast(e.NewValue, LineTokens)
            If lt Is Nothing Then
                ' Fallback: maybe tokens provided as IEnumerable(Of TokenDto)
                Dim enumObj = TryCast(e.NewValue, System.Collections.IEnumerable)
                If enumObj Is Nothing Then
                    Dim s = TryCast(e.NewValue, String)
                    If s IsNot Nothing Then
                        tb.Inlines.Add(New Run(s) With {.Foreground = brushDefault})
                    End If
                    Return
                End If

                ' Convert generic enumerable to TokenDto list
                Dim list As New List(Of TokenDto)
                For Each o In enumObj
                    Dim tProp = o.GetType().GetProperty("Type")
                    Dim txtProp = o.GetType().GetProperty("Text")
                    Dim tVal As Integer = If(tProp IsNot Nothing, CInt(tProp.GetValue(o, Nothing)), 0)
                    Dim txtVal As String = If(txtProp IsNot Nothing, CStr(txtProp.GetValue(o, Nothing)), o.ToString())
                    list.Add(New TokenDto(tVal, txtVal))
                Next

                BuildRuns(tb, list)
                Return
            End If

            If lt.IsHorizontalRule Then
                ' nothing to render in TextBlock for horizontal rule; the DataTemplate shows a Border instead
                Return
            End If

            BuildRuns(tb, lt.Tokens)
        End Sub

        Private Shared Sub BuildRuns(tb As TextBlock, tokens As List(Of TokenDto))
            If tokens Is Nothing OrElse tokens.Count = 0 Then
                Return
            End If

            For Each tk In tokens
                Select Case tk.Type
                    Case 1 ' Comment (use same behaviour as converter: small transparent ';' + grey comment)
                        If tk.Text.Length > 0 AndAlso tk.Text(0) = ";"c Then
                            tb.Inlines.Add(New Run(";"c) With {.Foreground = transparentSemicolon, .FontSize = 1})
                            Dim commentBody = tk.Text.Substring(1).TrimStart()
                            tb.Inlines.Add(New Run(commentBody) With {.Foreground = brushComment})
                        Else
                            tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushComment})
                        End If
                    Case 5 ' GCode
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushGCode, .FontWeight = FontWeights.Bold})
                    Case 8 ' Macro
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushMacro, .FontWeight = FontWeights.Bold})
                    Case 4 ' KlipperParam
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushKlipperParam})
                    Case 3 ' KlipperExpr
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushKlipperExpr})
                    Case 6 ' Axis
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushAxis})
                    Case 7 ' Feed
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushFeed})
                    Case 9 ' Number
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushNumber})
                    Case Else
                        tb.Inlines.Add(New Run(tk.Text) With {.Foreground = brushDefault})
                End Select
            Next
        End Sub
    End Class

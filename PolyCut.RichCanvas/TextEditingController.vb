Imports System.ComponentModel
Imports System.Windows.Threading

Imports PolyCut.Shared

Public Class TextEditingController
    Private _activeTextBox As TextBox
    Private _styleSource As TextBox
    Private _sessionStartChars As TextCharacteristics
    Private _sessionStartText As String
    Private _suppressStyleSourceApply As Boolean

    Public ReadOnly Property ActiveTextBox As TextBox
        Get
            Return _activeTextBox
        End Get
    End Property

    Public Property SuppressStyleSourceApply As Boolean
        Get
            Return _suppressStyleSourceApply
        End Get
        Set(value As Boolean)
            _suppressStyleSourceApply = value
        End Set
    End Property

    Public Event SessionFinished(element As UIElement)
    Public Event EditRequested(sender As Object, textBox As TextBox)
    Public Event TextEdited(sender As Object, textBox As TextBox, oldText As String, oldChars As TextCharacteristics, newText As String, newChars As TextCharacteristics)

    Public Sub BeginEditingText(textBox As TextBox, pcanvas As PolyCanvas)
        If textBox Is Nothing OrElse _activeTextBox Is textBox Then Return
        BeginSession(textBox, pcanvas)
        FocusTextBox(textBox)
    End Sub

    Public Sub StartNewText(startPos As Point, chars As TextCharacteristics, pcanvas As PolyCanvas)
        If _activeTextBox IsNot Nothing Then Return
        Dim textBox = CreateTextBox(startPos, chars)
        pcanvas.Children.Add(textBox)
        BeginSession(textBox, pcanvas)
        FocusTextBox(textBox)
    End Sub

    Public Sub CommitActiveText(pcanvas As PolyCanvas)
        EndSession(pcanvas, raiseSessionFinished:=True)
    End Sub

    Public Sub CancelActiveText(pcanvas As PolyCanvas)
        EndSession(pcanvas, raiseSessionFinished:=False)
    End Sub

    Public Sub EvaluateTextCommit()
        If _activeTextBox Is Nothing OrElse IsFocusRetained() Then Return
        CommitActiveText(TryCast(_activeTextBox.Parent, PolyCanvas))
    End Sub

    Public Sub RefocusActiveTextBox()
        If _activeTextBox Is Nothing Then Return
        FocusTextBox(_activeTextBox)
    End Sub

    Public Sub AttachTextStyleSource(source As TextBox)
        If source Is Nothing OrElse _styleSource Is source Then Return
        _styleSource = source
        DependencyPropertyDescriptor.FromProperty(TextBox.FontSizeProperty, GetType(TextBox)).AddValueChanged(source, AddressOf OnStyleSourceSizeChanged)
        DependencyPropertyDescriptor.FromProperty(TextBox.FontFamilyProperty, GetType(TextBox)).AddValueChanged(source, AddressOf OnStyleSourceFamilyChanged)
        DependencyPropertyDescriptor.FromProperty(TextBox.FontStyleProperty, GetType(TextBox)).AddValueChanged(source, AddressOf OnStyleSourceStyleChanged)
        DependencyPropertyDescriptor.FromProperty(TextBox.FontWeightProperty, GetType(TextBox)).AddValueChanged(source, AddressOf OnStyleSourceWeightChanged)
    End Sub



    Public Function HandleTextMouseDown(mode As CanvasMode, e As MouseButtonEventArgs) As Boolean
        If mode <> CanvasMode.Text OrElse e.ChangedButton <> MouseButton.Left Then Return False
        Dim source = TryCast(e.OriginalSource, DependencyObject)
        If source Is Nothing Then Return False
        Dim textBox = FindEditableTextBox(source)
        If textBox Is Nothing Then Return False
        If textBox IsNot _activeTextBox Then
            RaiseEditRequested(textBox)
            e.Handled = True
        End If
        Return True
    End Function

    Public Sub RaiseEditRequested(textBox As TextBox)
        RaiseEvent EditRequested(Me, textBox)
    End Sub

    Public Function FindEditableTextBox(start As DependencyObject) As TextBox
        Dim current = start
        While current IsNot Nothing
            Dim textBox = TryCast(current, TextBox)
            If textBox IsNot Nothing Then Return textBox
            Dim wrapper = TryCast(current, ContentControl)
            If wrapper IsNot Nothing AndAlso TypeOf wrapper.Content Is TextBox Then
                Return CType(wrapper.Content, TextBox)
            End If
            current = VisualTreeHelper.GetParent(current)
        End While
        Return Nothing
    End Function

    Private Sub BeginSession(textBox As TextBox, pcanvas As PolyCanvas)
        CommitActiveText(pcanvas)
        _activeTextBox = textBox
        _sessionStartChars = TextCharacteristics.FromTextBox(textBox)
        _sessionStartText = textBox.Text
        TextEditHelper.SetIsEditing(textBox, True)
        textBox.Background = Brushes.Transparent
        textBox.Cursor = Cursors.IBeam
        AddHandler textBox.LostFocus, AddressOf OnActiveTextBoxLostFocus
        AddHandler textBox.PreviewKeyDown, AddressOf OnActiveTextBoxKeyDown
    End Sub

    Private Sub EndSession(pcanvas As PolyCanvas, raiseSessionFinished As Boolean)
        If _activeTextBox Is Nothing Then Return
        Dim textBox = _activeTextBox
        textBox.Cursor = Cursors.Arrow
        Dim wasEditingExisting = TypeOf textBox.Parent Is ContentControl
        _activeTextBox = Nothing
        RemoveHandler textBox.LostFocus, AddressOf OnActiveTextBoxLostFocus
        RemoveHandler textBox.PreviewKeyDown, AddressOf OnActiveTextBoxKeyDown
        TextEditHelper.SetIsEditing(textBox, False)
        If pcanvas IsNot Nothing AndAlso pcanvas.Children.Contains(textBox) Then
            pcanvas.Children.Remove(textBox)
        End If
        If wasEditingExisting AndAlso SessionChanged(textBox) Then
            RaiseEvent TextEdited(Me, textBox, _sessionStartText, _sessionStartChars, textBox.Text, TextCharacteristics.FromTextBox(textBox))
        End If
        _sessionStartChars = Nothing
        _sessionStartText = Nothing
        If raiseSessionFinished AndAlso Not wasEditingExisting AndAlso Not String.IsNullOrEmpty(textBox.Text) Then
            RaiseEvent SessionFinished(textBox)
        End If
    End Sub

    Private Function SessionChanged(textBox As TextBox) As Boolean
        If Not String.Equals(_sessionStartText, textBox.Text, StringComparison.Ordinal) Then Return True
        If _sessionStartChars Is Nothing Then Return False
        Return Not _sessionStartChars.SameAs(TextCharacteristics.FromTextBox(textBox))
    End Function

    Private Shared Sub FocusTextBox(textBox As TextBox)
        If Not textBox.Focus() Then
            textBox.Dispatcher.BeginInvoke(Sub() textBox.Focus(), DispatcherPriority.Input)
        End If
    End Sub

    Private Sub OnActiveTextBoxLostFocus(sender As Object, e As RoutedEventArgs)
        Dim textBox = DirectCast(sender, TextBox)
        If _activeTextBox IsNot textBox Then Return
        If IsFocusRetained() Then
            If Keyboard.FocusedElement Is textBox.Parent Then textBox.Focus()
            Return
        End If
        CommitActiveText(TryCast(textBox.Parent, PolyCanvas))
    End Sub

    Private Sub OnActiveTextBoxKeyDown(sender As Object, e As KeyEventArgs)
        Dim textBox = DirectCast(sender, TextBox)
        If e.Key = Key.Escape Then
            e.Handled = True
            CancelActiveText(TryCast(textBox.Parent, PolyCanvas))
        ElseIf e.Key = Key.Enter AndAlso Not textBox.AcceptsReturn Then
            e.Handled = True
            CommitActiveText(TryCast(textBox.Parent, PolyCanvas))
        End If
    End Sub

    Private Sub OnStyleSourceSizeChanged(sender As Object, e As EventArgs)
        If _activeTextBox Is Nothing OrElse _suppressStyleSourceApply Then Return
        _activeTextBox.FontSize = _styleSource.FontSize
    End Sub

    Private Sub OnStyleSourceFamilyChanged(sender As Object, e As EventArgs)
        If _activeTextBox Is Nothing OrElse _suppressStyleSourceApply Then Return
        _activeTextBox.FontFamily = _styleSource.FontFamily
    End Sub

    Private Sub OnStyleSourceStyleChanged(sender As Object, e As EventArgs)
        If _activeTextBox Is Nothing OrElse _suppressStyleSourceApply Then Return
        _activeTextBox.FontStyle = _styleSource.FontStyle
    End Sub

    Private Sub OnStyleSourceWeightChanged(sender As Object, e As EventArgs)
        If _activeTextBox Is Nothing OrElse _suppressStyleSourceApply Then Return
        _activeTextBox.FontWeight = _styleSource.FontWeight
    End Sub

    Private Function IsFocusRetained() As Boolean
        If _activeTextBox Is Nothing Then Return False
        If _activeTextBox.IsKeyboardFocusWithin Then Return True
        If IsFocusWithinTextStyleHost() Then Return True
        Return Keyboard.FocusedElement Is _activeTextBox.Parent
    End Function

    Private Shared Function IsFocusWithinTextStyleHost() As Boolean
        Dim focused = Keyboard.FocusedElement
        While focused IsNot Nothing
            If TextEditHelper.GetIsTextStyleHost(focused) Then Return True
            focused = VisualTreeHelper.GetParent(focused)
        End While
        Return False
    End Function

    Private Shared Function CreateTextBox(p As Point, chars As TextCharacteristics) As TextBox
        Dim tb As New TextBox With {
            .Width = Double.NaN,
            .Height = Double.NaN,
            .Background = Brushes.Transparent,
            .BorderBrush = Brushes.Transparent,
            .Foreground = Brushes.Black,
            .BorderThickness = New Thickness(1),
            .Style = Nothing,
            .Text = "",
            .AcceptsReturn = False,
            .AcceptsTab = True,
            .Padding = New Thickness(0)
        }

        chars?.ApplyTo(tb)

        Canvas.SetLeft(tb, p.X)
        Canvas.SetTop(tb, p.Y)

        Return tb
    End Function
End Class

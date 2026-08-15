Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class TextEditAction : Implements IUndoableAction

    Private ReadOnly _textBox As System.Windows.Controls.TextBox
    Private ReadOnly _oldText As String
    Private ReadOnly _oldChars As TextCharacteristics
    Private ReadOnly _newText As String
    Private ReadOnly _newChars As TextCharacteristics

    Public Sub New(textBox As System.Windows.Controls.TextBox, oldText As String, oldChars As TextCharacteristics, newText As String, newChars As TextCharacteristics)
        _textBox = textBox
        _oldText = oldText
        _oldChars = oldChars
        _newText = newText
        _newChars = newChars
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return "Text edit"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _textBox Is Nothing Then Return False
        If HasChanges(_newText, _newChars) Then
            Apply(_newText, _newChars)
            Return True
        End If
        Return False
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _textBox Is Nothing Then Return
        Apply(_oldText, _oldChars)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        If _textBox Is Nothing Then Return
        Apply(_newText, _newChars)
    End Sub

    Private Function HasChanges(text As String, chars As TextCharacteristics) As Boolean
        If Not String.Equals(_oldText, text, StringComparison.Ordinal) Then Return True
        If _oldChars Is Nothing Then Return chars IsNot Nothing
        Return Not _oldChars.SameAs(chars)
    End Function

    Private Sub Apply(text As String, chars As TextCharacteristics)
        _textBox.Text = text
        chars?.ApplyTo(_textBox)
        RefreshWrapperLayout()
    End Sub

    Private Sub RefreshWrapperLayout()
        Dim wrapper = TryCast(_textBox.Parent, System.Windows.Controls.ContentControl)
        If wrapper Is Nothing Then Return

        ' A rotated wrapper pivots around its center (RenderTransformOrigin 0.5,0.5), so a
        Dim rotate = TryCast(wrapper.RenderTransform, RotateTransform)
        Dim preserveCenter As Boolean = rotate IsNot Nothing AndAlso Math.Abs(rotate.Angle) > 0.01
        Dim centerX As Double = 0
        Dim centerY As Double = 0
        If preserveCenter Then
            Dim left = Canvas.GetLeft(wrapper)
            Dim top = Canvas.GetTop(wrapper)
            If Double.IsNaN(left) Then left = 0
            If Double.IsNaN(top) Then top = 0
            centerX = left + wrapper.ActualWidth / 2
            centerY = top + wrapper.ActualHeight / 2
        End If

        ' Mirror the creation path: auto-size the textbox to its content so the
        ' wrapper can grow/shrink to fit the restored text, then fix the size.
        wrapper.Width = Double.NaN
        wrapper.Height = Double.NaN
        _textBox.Width = Double.NaN
        _textBox.Height = Double.NaN
        wrapper.UpdateLayout()

        Dim contentWidth As Double = wrapper.ActualWidth
        Dim contentHeight As Double = wrapper.ActualHeight

        wrapper.Width = contentWidth
        wrapper.Height = contentHeight
        wrapper.UpdateLayout()

        If preserveCenter Then
            Canvas.SetLeft(wrapper, centerX - contentWidth / 2)
            Canvas.SetTop(wrapper, centerY - contentHeight / 2)
        End If

        MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))
        PolyCanvas.ActiveInstance?.RefreshTextWrapper(wrapper)
    End Sub

End Class

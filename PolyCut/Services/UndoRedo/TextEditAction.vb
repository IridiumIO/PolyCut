Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class TextEditAction : Implements IUndoableAction
    Private ReadOnly _textBox As System.Windows.Controls.TextBox
    Private ReadOnly _before As TextEditState
    Private ReadOnly _after As TextEditState

    Public Sub New(textBox As System.Windows.Controls.TextBox, before As TextEditState, after As TextEditState)
        _textBox = textBox
        _before = before
        _after = after
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return "Text edit"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        ' The editor has already applied this change. Recording it must not resize it again.
        Return _textBox IsNot Nothing AndAlso _before IsNot Nothing AndAlso _before.HasChanges(_after)
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        Restore(_before)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Restore(_after)
    End Sub

    Private Sub Restore(state As TextEditState)
        If _textBox Is Nothing OrElse state Is Nothing Then Return
        state.ApplyTo(_textBox)
        Dim wrapper = TryCast(_textBox.Parent, System.Windows.Controls.ContentControl)
        PolyCanvas.ActiveInstance?.RefreshTextWrapper(wrapper)
    End Sub
End Class

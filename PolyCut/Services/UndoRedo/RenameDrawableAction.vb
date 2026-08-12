Imports PolyCut.Shared

Public Class RenameDrawableAction : Implements IUndoableAction

    Private ReadOnly _drawable As IDrawable
    Private ReadOnly _oldName As String
    Private ReadOnly _newName As String

    Public Sub New(drawable As IDrawable, oldName As String, newName As String)
        _drawable = drawable
        _oldName = oldName
        _newName = newName
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Rename: {_newName}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _drawable Is Nothing Then Return False
        If Not String.Equals(_drawable.Name, _newName) Then
            _drawable.Name = _newName
        End If
        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _drawable Is Nothing Then Return
        _drawable.Name = _oldName
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        If _drawable Is Nothing Then Return
        _drawable.Name = _newName
    End Sub

End Class
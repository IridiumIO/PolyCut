


Imports PolyCut.Shared

Public Class CompositeAction
    Implements IUndoableAction

    Private ReadOnly _actions As New List(Of IUndoableAction)

    Public Sub New(actions As IEnumerable(Of IUndoableAction))
        If actions IsNot Nothing Then
            _actions.AddRange(actions)
        End If
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Composite ({_actions.Count} actions)"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        For i As Integer = _actions.Count - 1 To 0 Step -1
            Try
                _actions(i).Undo()
            Catch ex As Exception
            End Try
        Next
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        For Each a In _actions
            Try
                a.Redo()
            Catch ex As Exception
            End Try
        Next
    End Sub
End Class

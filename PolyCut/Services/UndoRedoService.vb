
Imports PolyCut.Shared

Public Class UndoRedoService

    Private ReadOnly _undoStack As New Stack(Of IUndoableAction)()
    Private ReadOnly _redoStack As New Stack(Of IUndoableAction)()

    Public Event CanUndoChanged As EventHandler
    Public Event CanRedoChanged As EventHandler

    Private _isExecuting As Boolean = False
    Private _suspendRecording As Boolean = False

    Public ReadOnly Property IsExecuting As Boolean
        Get
            Return _isExecuting
        End Get
    End Property

    Public ReadOnly Property CanUndo As Boolean
        Get
            Return _undoStack.Count > 0
        End Get
    End Property

    Public ReadOnly Property CanRedo As Boolean
        Get
            Return _redoStack.Count > 0
        End Get
    End Property

    Public Sub Push(action As IUndoableAction)
        If action Is Nothing Then Return

        If _isExecuting OrElse _suspendRecording Then
            Return
        End If

        _undoStack.Push(action)
        _redoStack.Clear()
        RaiseEvent CanUndoChanged(Me, EventArgs.Empty)
        RaiseEvent CanRedoChanged(Me, EventArgs.Empty)
    End Sub

    Public Sub Undo()
        If _undoStack.Count = 0 Then Return

        Dim act = _undoStack.Pop()
        Try
            _isExecuting = True
            act.Undo()
        Finally
            _isExecuting = False
        End Try

        _redoStack.Push(act)
        RaiseEvent CanUndoChanged(Me, EventArgs.Empty)
        RaiseEvent CanRedoChanged(Me, EventArgs.Empty)
    End Sub

    Public Sub Redo()
        If _redoStack.Count = 0 Then Return

        Dim act = _redoStack.Pop()
        Try
            _isExecuting = True
            act.Redo()
        Finally
            _isExecuting = False
        End Try

        _undoStack.Push(act)
        RaiseEvent CanUndoChanged(Me, EventArgs.Empty)
        RaiseEvent CanRedoChanged(Me, EventArgs.Empty)
    End Sub

    Public Sub Clear()
        _undoStack.Clear()
        _redoStack.Clear()
        RaiseEvent CanUndoChanged(Me, EventArgs.Empty)
        RaiseEvent CanRedoChanged(Me, EventArgs.Empty)
    End Sub

    Public Sub ExecuteWithoutRecording(action As Action)
        If action Is Nothing Then Return
        Try
            _suspendRecording = True
            action()
        Finally
            _suspendRecording = False
        End Try
    End Sub
End Class

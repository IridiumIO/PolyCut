Public Interface IUndoableAction
    ReadOnly Property Description As String
    Function Execute() As Boolean
    Sub Undo()
    Sub Redo()
End Interface



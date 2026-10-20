Option Strict On
Imports PolyCut.[Shared]
Imports PolyCut.RichCanvas

Public Class RemoveDrawableAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _selectionManager As SelectionManager
    Private ReadOnly _drawable As IDrawable
    Private ReadOnly _parent As IDrawable
    Private ReadOnly _indexInCollection As Integer
    Private ReadOnly _indexInGroup As Integer
    Private ReadOnly _transformSnapshot As TransformState

    Private Shared ReadOnly _restoredForSelection As New List(Of IDrawable)

    Public Sub New(manager As IDrawableManager, drawable As IDrawable, selectionManager As SelectionManager)
        _manager = manager
        _selectionManager = selectionManager
        _drawable = drawable

        _parent = drawable.ParentGroup
        _indexInCollection = If(_manager.DrawableCollection IsNot Nothing, _manager.DrawableCollection.IndexOf(drawable), -1)
        _indexInGroup = If(_parent IsNot Nothing, _manager.IndexOfChild(_parent, drawable), -1)

        Try
            _transformSnapshot = TransformAction.CaptureState(_drawable)
        Catch
            _transformSnapshot = Nothing
        End Try
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Remove: {_drawable?.Name}"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _drawable Is Nothing Then Return False

        Dim deferring As Boolean = _manager.IsRemovalDeferred
        If Not deferring Then
            _selectionManager.DeselectItem(_drawable)
            _manager.RemoveDrawableFromCollection(_drawable)
        End If

        If _parent IsNot Nothing Then
            _manager.RemoveChildFromGroup(_parent, _drawable)
        End If

        _manager.ClearDrawableParent(_drawable)

        If Not _manager.IsRemovalDeferred Then _manager.NotifyChanged()

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _drawable Is Nothing Then Return

        Try
            If _transformSnapshot IsNot Nothing AndAlso _drawable.DrawableElement IsNot Nothing AndAlso TransformAction.GetWrapper(_drawable) Is Nothing Then
                Dim child = TryCast(_drawable.DrawableElement, FrameworkElement)
                If child IsNot Nothing Then
                    If _transformSnapshot.Width > 0 Then child.Width = _transformSnapshot.Width
                    If _transformSnapshot.Height > 0 Then child.Height = _transformSnapshot.Height
                End If
            End If
        Catch
        End Try

        If _parent IsNot Nothing Then
            If Not _manager.ContainsChild(_parent, _drawable) Then
                _manager.AddChildToGroup(_parent, _drawable, _indexInGroup)
            End If

            If Not _manager.DrawableCollection.Contains(_drawable) Then
                RestoreToCollection()
            End If
        Else
            RestoreToCollection()
        End If
    End Sub


    Private Sub RestoreToCollection()
        Dim snapshot = _transformSnapshot
        ModelBatch.RestoreToCollection(_manager, _drawable, _indexInCollection,
            Sub()
                Try
                    TransformAction.ApplyState(snapshot, _drawable)
                Catch
                End Try
            End Sub)

        QueueSelectionRestore(_drawable)
    End Sub

    'Queue reselection for performance
    Private Sub QueueSelectionRestore(drawable As IDrawable)
        If drawable Is Nothing Then Return

        If Not ModelBatch.Active Then
            Try
                _selectionManager.ApplySelection({drawable}, Nothing, False)
            Catch
            End Try
            Return
        End If

        _restoredForSelection.Add(drawable)
        ModelBatch.OnEndOnce("RemoveDrawable.RestoreSelection", AddressOf FlushRestoredSelection)
    End Sub

    Private Sub FlushRestoredSelection()
        If _restoredForSelection.Count = 0 Then Return

        Dim restored = _restoredForSelection.ToList()
        _restoredForSelection.Clear()

        Try
            _selectionManager.ApplySelection(restored, Nothing, False)
        Catch
        End Try
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class


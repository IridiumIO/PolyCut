Imports PolyCut.[Shared]
Imports PolyCut.RichCanvas

Public Class RemoveDrawableAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _drawable As IDrawable
    Private ReadOnly _parentGroup As DrawableGroup
    Private ReadOnly _indexInCollection As Integer
    Private ReadOnly _indexInGroup As Integer
    Private ReadOnly _transformSnapshot As TransformAction.Snapshot

    Public Sub New(manager As IDrawableManager, drawable As IDrawable)
        _manager = manager
        _drawable = drawable

        _parentGroup = TryCast(drawable.ParentGroup, DrawableGroup)
        _indexInCollection = If(_manager.DrawableCollection IsNot Nothing, _manager.DrawableCollection.IndexOf(drawable), -1)
        _indexInGroup = If(_parentGroup IsNot Nothing, _parentGroup.GroupChildren.IndexOf(drawable), -1)

        Try
            _transformSnapshot = TransformAction.MakeSnapshotFromDrawable(_drawable)
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

        Dim mainVM = TryCast(_manager, MainViewModel)

        Dim deferring As Boolean = mainVM IsNot Nothing AndAlso mainVM.IsDrawableRemovalDeferred
        If Not deferring Then
            If mainVM IsNot Nothing Then
                PolyCanvas.RemoveFromSelection(_drawable)
            End If

            _manager.RemoveDrawableFromCollection(_drawable)
        End If

        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_drawable)
        End If

        _manager.ClearDrawableParent(_drawable)

        If mainVM IsNot Nothing AndAlso Not mainVM.IsDrawableRemovalDeferred Then
            mainVM.NotifyCollectionsChanged()
        End If

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _drawable Is Nothing Then Return

        Try
            If _transformSnapshot IsNot Nothing AndAlso _drawable.DrawableElement IsNot Nothing Then
                Dim child = TryCast(_drawable.DrawableElement, FrameworkElement)
                If child IsNot Nothing Then
                    If _transformSnapshot.Width > 0 Then child.Width = _transformSnapshot.Width
                    If _transformSnapshot.Height > 0 Then child.Height = _transformSnapshot.Height
                End If
            End If
        Catch
        End Try

        If _parentGroup IsNot Nothing Then
            If Not _parentGroup.GroupChildren.Contains(_drawable) Then
                _parentGroup.AddChild(_drawable, _indexInGroup)
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
                    TransformAction.ApplySnapshot(snapshot, _drawable)
                Catch
                End Try
            End Sub)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class


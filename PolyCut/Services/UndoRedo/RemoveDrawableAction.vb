Imports PolyCut.[Shared]
Imports PolyCut.RichCanvas
Imports System.Windows.Controls

Public Class RemoveDrawableAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _drawable As IDrawable
    Private ReadOnly _parentGroup As DrawableGroup
    Private ReadOnly _indexInCollection As Integer
    Private ReadOnly _transformSnapshot As TransformAction.Snapshot

    Public Sub New(manager As IDrawableManager, drawable As IDrawable)
        _manager = manager
        _drawable = drawable

        _parentGroup = TryCast(drawable.ParentGroup, DrawableGroup)
        _indexInCollection = If(_manager.DrawableCollection IsNot Nothing, Math.Max(0, _manager.DrawableCollection.IndexOf(drawable)), -1)

        Try
            Dim wrapper = TryCast(_drawable?.DrawableElement?.Parent, ContentControl)
            If wrapper IsNot Nothing Then
                _transformSnapshot = TransformAction.MakeSnapshotFromWrapper(wrapper)
            Else
                _transformSnapshot = Nothing
            End If
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
        If mainVM IsNot Nothing Then
            PolyCanvas.RemoveFromSelection(_drawable)
        End If

        _manager.RemoveDrawableFromCollection(_drawable)

        If _parentGroup IsNot Nothing Then
            _parentGroup.RemoveChild(_drawable)
            _manager.CleanupEmptyGroup(_parentGroup)
        End If

        _manager.ClearDrawableParent(_drawable)

        If mainVM IsNot Nothing Then
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
                _parentGroup.AddChild(_drawable)
            End If

            If Not _manager.DrawableCollection.Contains(_drawable) Then
                _manager.DrawableCollection.Add(_drawable)
            End If
        Else
            _manager.AddDrawableToCollection(_drawable, _indexInCollection)
        End If

        Try
            If _transformSnapshot IsNot Nothing Then
                Dim wrapper = TryCast(_drawable.DrawableElement?.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Canvas.SetLeft(wrapper, _transformSnapshot.Left)
                    Canvas.SetTop(wrapper, _transformSnapshot.Top)
                    If Not Double.IsNaN(_transformSnapshot.Width) AndAlso _transformSnapshot.Width > 0 Then wrapper.Width = _transformSnapshot.Width
                    If Not Double.IsNaN(_transformSnapshot.Height) AndAlso _transformSnapshot.Height > 0 Then wrapper.Height = _transformSnapshot.Height
                    wrapper.RenderTransform = _transformSnapshot.RenderTransform
                End If
            End If
        Catch
        End Try
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class



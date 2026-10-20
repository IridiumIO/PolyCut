Option Strict On 'do not remove, slowly migrate to strict mode to help catch issues
Imports System.Collections.ObjectModel
Imports System.ComponentModel

Imports CodingNinja.Wpf.ObjectModel

Imports PolyCut.[Shared]

Public Class Document : Implements IDrawableManager, INotifyPropertyChanged 'do not use <ObservableProperty> as that fires notifications we do not want

    Public Const DrawingGroupName As String = "Drawing Group"

    Private ReadOnly _drawables As ObservableCollection(Of IDrawable) = New WpfObservableRangeCollection(Of IDrawable)
    Private ReadOnly _importRoots As New ObservableCollection(Of DrawableGroup)
    Private _drawingGroup As DrawableGroup
    Private _removalDeferDepth As Integer
    Private _notifyQueued As Boolean

    Public Event Changed As EventHandler
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub NotifyChanged() Implements IDrawableManager.NotifyChanged
        If _notifyQueued Then Return
        _notifyQueued = True

        If ModelBatch.Active Then
            ModelBatch.OnEndOnce("DocumentChanged", AddressOf RaiseChanged)
            Return
        End If

        RaiseChanged()
    End Sub

    Private Sub RaiseChanged()
        _notifyQueued = False
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(HasDrawables)))
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Public ReadOnly Property HasDrawables As Boolean
        Get
            Return _drawables.Any(Function(d) TypeOf d IsNot DrawableGroup)
        End Get
    End Property

    Public ReadOnly Property Drawables As ObservableCollection(Of IDrawable) Implements IDrawableManager.DrawableCollection
        Get
            Return _drawables
        End Get
    End Property

    Public ReadOnly Property ImportRoots As ObservableCollection(Of DrawableGroup)
        Get
            Return _importRoots
        End Get
    End Property

    Public Property DrawingGroup As DrawableGroup
        Get
            Return _drawingGroup
        End Get
        Set(value As DrawableGroup)
            _drawingGroup = value
        End Set
    End Property

    Public ReadOnly Property IsRemovalDeferred As Boolean Implements IDrawableManager.IsRemovalDeferred
        Get
            Return _removalDeferDepth > 0
        End Get
    End Property

    Public Sub BeginRemovalDeferral()
        _removalDeferDepth += 1
    End Sub

    Public Sub EndRemovalDeferral()
        If _removalDeferDepth > 0 Then _removalDeferDepth -= 1
    End Sub

    Public Sub AddDrawableToCollection(drawable As IDrawable, index As Integer) Implements IDrawableManager.AddDrawableToCollection
        If drawable Is Nothing OrElse _drawables.Contains(drawable) Then Return

        If index >= 0 AndAlso index <= _drawables.Count Then
            _drawables.Insert(index, drawable)
        Else
            _drawables.Add(drawable)
        End If
    End Sub

    Public Sub RemoveDrawableFromCollection(drawable As IDrawable) Implements IDrawableManager.RemoveDrawableFromCollection
        If drawable IsNot Nothing Then _drawables.Remove(drawable)
    End Sub

    Public Sub AddChildToGroup(parent As IDrawable, child As IDrawable, Optional index As Integer = -1) Implements IDrawableManager.AddChildToGroup
        If parent Is Nothing OrElse child Is Nothing Then Return

        Dim children = ChildrenOf(parent)
        If children Is Nothing Then Return

        DetachFromParent(child)
        child.ParentGroup = parent
        If children.Contains(child) Then Return

        If index >= 0 AndAlso index < children.Count Then
            children.Insert(index, child)
        Else
            children.Add(child)
        End If
    End Sub

    Public Sub RemoveChildFromGroup(parent As IDrawable, child As IDrawable) Implements IDrawableManager.RemoveChildFromGroup
        If parent Is Nothing OrElse child Is Nothing Then Return

        Dim children = ChildrenOf(parent)
        If children Is Nothing OrElse Not children.Remove(child) Then Return

        If Object.ReferenceEquals(child.ParentGroup, parent) Then child.ParentGroup = Nothing
    End Sub

    Public Function ContainsChild(parent As IDrawable, child As IDrawable) As Boolean Implements IDrawableManager.ContainsChild
        Dim children = ChildrenOf(parent)
        Return children IsNot Nothing AndAlso child IsNot Nothing AndAlso children.Contains(child)
    End Function

    Public Function IndexOfChild(parent As IDrawable, child As IDrawable) As Integer Implements IDrawableManager.IndexOfChild
        Dim children = ChildrenOf(parent)
        If children Is Nothing OrElse child Is Nothing Then Return -1
        Return children.IndexOf(child)
    End Function

    Public Sub AddImportRoot(root As IDrawable, Optional index As Integer = -1) Implements IDrawableManager.AddImportRoot
        Dim grp = TryCast(root, DrawableGroup)
        If grp Is Nothing OrElse _importRoots.Contains(grp) Then Return

        If index >= 0 AndAlso index <= _importRoots.Count Then
            _importRoots.Insert(index, grp)
        Else
            _importRoots.Add(grp)
        End If
    End Sub

    Public Function ContainsImportRoot(root As IDrawable) As Boolean Implements IDrawableManager.ContainsImportRoot
        Dim grp = TryCast(root, DrawableGroup)
        Return grp IsNot Nothing AndAlso _importRoots.Contains(grp)
    End Function

    Public Function IndexOfImportRoot(root As IDrawable) As Integer Implements IDrawableManager.IndexOfImportRoot
        Dim grp = TryCast(root, DrawableGroup)
        If grp Is Nothing Then Return -1
        Return _importRoots.IndexOf(grp)
    End Function

    Public Function ParentGroupOf(item As IDrawable) As IDrawable Implements IDrawableManager.ParentGroupOf
        If item Is Nothing Then Return Nothing

        Dim direct = TryCast(item.ParentGroup, DrawableGroup)
        If direct IsNot Nothing Then Return direct

        Return _importRoots.FirstOrDefault(Function(g) g.GroupChildren.Contains(item))
    End Function

    Public Function IsAncestorOf(ancestor As IDrawable, node As IDrawable) As Boolean Implements IDrawableManager.IsAncestorOf
        If ancestor Is Nothing OrElse node Is Nothing Then Return False

        Dim current = node
        While current IsNot Nothing
            If current Is ancestor Then Return True
            current = current.ParentGroup
        End While
        Return False
    End Function

    Public Sub RemoveImportRoot(root As IDrawable) Implements IDrawableManager.RemoveImportRoot
        Dim grp = TryCast(root, DrawableGroup)
        If grp IsNot Nothing Then _importRoots.Remove(grp)
    End Sub

    Public Function TopLevelGroupOf(item As IDrawable) As IDrawable Implements IDrawableManager.TopLevelGroupOf
        Dim current = item
        While current IsNot Nothing AndAlso current.ParentGroup IsNot Nothing
            current = current.ParentGroup
        End While
        Return current
    End Function

    Public Function IsDrawingGroup(item As IDrawable) As Boolean Implements IDrawableManager.IsDrawingGroup
        If item Is Nothing Then Return False
        Return item Is _drawingGroup OrElse String.Equals(item.Name, DrawingGroupName, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub DetachFromParent(child As IDrawable)
        Dim previous = child.ParentGroup
        If previous Is Nothing Then Return

        Dim children = ChildrenOf(previous)
        If children IsNot Nothing Then children.Remove(child)
    End Sub

    Public Sub AddDrawablesToCollection(drawables As IEnumerable(Of IDrawable)) Implements IDrawableManager.AddDrawablesToCollection
        Dim batch = MissingFrom(drawables)
        If batch.Count = 0 Then Return

        Dim range = TryCast(_drawables, WpfObservableRangeCollection(Of IDrawable))
        If range IsNot Nothing Then
            range.AddRange(batch)
        Else
            For Each d In batch
                _drawables.Add(d)
            Next
        End If
    End Sub

    Public Sub RemoveDrawablesFromCollection(drawables As IEnumerable(Of IDrawable)) Implements IDrawableManager.RemoveDrawablesFromCollection
        Dim batch = PresentIn(drawables)
        If batch.Count = 0 Then Return

        Dim range = TryCast(_drawables, WpfObservableRangeCollection(Of IDrawable))
        If range IsNot Nothing Then
            range.RemoveRange(batch)
        Else
            For Each d In batch
                _drawables.Remove(d)
            Next
        End If
    End Sub

    Public Sub ClearDrawableParent(drawable As IDrawable) Implements IDrawableManager.ClearDrawableParent
        If drawable IsNot Nothing Then drawable.ParentGroup = Nothing
    End Sub

    Public Sub CleanupEmptyGroup(group As IDrawable) Implements IDrawableManager.CleanupEmptyGroup
        Dim grp = TryCast(group, DrawableGroup)
        If grp Is Nothing OrElse IsDrawingGroup(grp) OrElse grp.GroupChildren.Any() Then Return

        RemoveImportRoot(grp)
        RemoveDrawableFromCollection(grp)

        Dim parent = TryCast(grp.ParentGroup, DrawableGroup)
        If parent IsNot Nothing Then
            RemoveChildFromGroup(parent, grp)
            CleanupEmptyGroup(parent)
        End If
    End Sub

    Public ReadOnly Property ManagerImportRoots As IEnumerable(Of IDrawable) Implements IDrawableManager.ImportRoots
        Get
            Return _importRoots
        End Get
    End Property

    Public Property ManagerDrawingGroup As IDrawable Implements IDrawableManager.DrawingGroup
        Get
            Return _drawingGroup
        End Get
        Set(value As IDrawable)
            _drawingGroup = TryCast(value, DrawableGroup)
        End Set
    End Property

    Private Function MissingFrom(drawables As IEnumerable(Of IDrawable)) As List(Of IDrawable)
        Dim result As New List(Of IDrawable)()
        If drawables Is Nothing Then Return result

        Dim seen As New HashSet(Of IDrawable)()
        For Each d In drawables
            If d IsNot Nothing AndAlso seen.Add(d) AndAlso Not _drawables.Contains(d) Then result.Add(d)
        Next
        Return result
    End Function

    Private Function PresentIn(drawables As IEnumerable(Of IDrawable)) As List(Of IDrawable)
        Dim result As New List(Of IDrawable)()
        If drawables Is Nothing Then Return result

        Dim seen As New HashSet(Of IDrawable)()
        For Each d In drawables
            If d IsNot Nothing AndAlso seen.Add(d) AndAlso _drawables.Contains(d) Then result.Add(d)
        Next
        Return result
    End Function

    Private Shared Function ChildrenOf(item As IDrawable) As ObservableCollection(Of IDrawable)
        Dim group = TryCast(item, DrawableGroup)
        If group IsNot Nothing Then Return group.GroupChildren

        Dim nested = TryCast(item, NestedDrawableGroup)
        If nested IsNot Nothing Then Return nested.GroupChildren

        Return Nothing
    End Function

End Class

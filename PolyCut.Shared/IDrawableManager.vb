Option Strict On
Imports System.Collections.ObjectModel

Public Interface IDrawableManager
    ReadOnly Property DrawableCollection As ObservableCollection(Of IDrawable)
    Sub AddDrawableToCollection(drawable As IDrawable, index As Integer)
    Sub RemoveDrawableFromCollection(drawable As IDrawable)
    Sub AddChildToGroup(parent As IDrawable, child As IDrawable, Optional index As Integer = -1)
    Sub RemoveChildFromGroup(parent As IDrawable, child As IDrawable)
    Function ContainsChild(parent As IDrawable, child As IDrawable) As Boolean
    Function IndexOfChild(parent As IDrawable, child As IDrawable) As Integer

    ReadOnly Property IsRemovalDeferred As Boolean

    Sub NotifyChanged()

    ReadOnly Property ImportRoots As IEnumerable(Of IDrawable)
    Property DrawingGroup As IDrawable

    Sub AddImportRoot(root As IDrawable, Optional index As Integer = -1)
    Sub RemoveImportRoot(root As IDrawable)
    Function ContainsImportRoot(root As IDrawable) As Boolean
    Function IndexOfImportRoot(root As IDrawable) As Integer

    Sub AddDrawablesToCollection(drawables As IEnumerable(Of IDrawable))
    Sub RemoveDrawablesFromCollection(drawables As IEnumerable(Of IDrawable))

    Function TopLevelGroupOf(item As IDrawable) As IDrawable
    Function ParentGroupOf(item As IDrawable) As IDrawable
    Function IsAncestorOf(ancestor As IDrawable, node As IDrawable) As Boolean
    Function IsDrawingGroup(item As IDrawable) As Boolean

    Sub ClearDrawableParent(drawable As IDrawable)
    Sub CleanupEmptyGroup(group As IDrawable)
End Interface





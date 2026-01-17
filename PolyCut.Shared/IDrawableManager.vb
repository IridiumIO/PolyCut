Imports System.Collections.ObjectModel

Public Interface IDrawableManager
    ReadOnly Property DrawableCollection As ObservableCollection(Of IDrawable)
    Sub AddDrawableToCollection(drawable As IDrawable, index As Integer)
    Sub RemoveDrawableFromCollection(drawable As IDrawable)
    Sub ClearDrawableParent(drawable As IDrawable)
    Sub CleanupEmptyGroup(group As IDrawable)
End Interface





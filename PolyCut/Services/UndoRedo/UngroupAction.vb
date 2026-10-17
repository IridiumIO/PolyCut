Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class UngroupAction : Implements IUndoableAction
    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _selectedItems As List(Of IDrawable)
    Private _participants As List(Of IDrawable)
    Private _before As GroupingState
    Private _after As GroupingState

    Public Sub New(manager As IDrawableManager, selectedItems As IEnumerable(Of IDrawable))
        _manager = manager
        _selectedItems = selectedItems.Where(Function(d) d IsNot Nothing).Distinct().ToList()
    End Sub

    Public ReadOnly Property Description As String = "Ungroup" Implements IUndoableAction.Description

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _after IsNot Nothing Then
            Redo()
            Return True
        End If
        Dim groups = _selectedItems.OfType(Of NestedDrawableGroup)().
            Where(Function(g) _manager.DrawableCollection.Contains(g)).ToList()
        Dim canvas = PolyCanvas.ActiveInstance
        If groups.Count = 0 OrElse canvas Is Nothing Then Return False
        canvas.UpdateLayout()

        _participants = groups.Cast(Of IDrawable)().Concat(groups.SelectMany(Function(g) g.GroupChildren)).ToList()
        _before = New GroupingState(_manager, _participants)

        ' Capture placements before detaching the old visual tree, including its scale and shear.
        Dim placements As New Dictionary(Of IDrawable, TransformState)
        For Each group In groups
            For Each child In group.GroupChildren
                Dim source = GroupingState.VisualOf(child)
                Dim m = TransformMath.GeneralTransformToMatrix(source.TransformToVisual(canvas))
                Dim size = TransformMath.GetWrapperSize(source)
                Dim angle = Math.Atan2(m.M12, m.M11) * 180 / Math.PI
                Dim pivot As New Point(size.Width / 2, size.Height / 2)
                Dim center = m.Transform(pivot)
                m.OffsetX = 0
                m.OffsetY = 0
                m.Rotate(-angle)
                placements(child) = New TransformState With {
                    .Translation = New Point(center.X - pivot.X, center.Y - pivot.Y),
                    .Width = size.Width,
                    .Height = size.Height,
                    .Rotation = angle,
                    .AffineTransform = m,
                    .TransformOrigin = New Point(0.5, 0.5)
                }
            Next
        Next

        Dim released = _selectedItems.Where(Function(d) Not groups.Contains(TryCast(d, NestedDrawableGroup))).ToList()
        Using ModelBatch.Begin()
            For Each group In groups
                Dim parent = If(group.ParentGroup, _manager.DrawingGroup)
                Dim index = _manager.DrawableCollection.IndexOf(group)
                Dim childIndex = _manager.IndexOfChild(parent, group)
                Dim children = group.GroupChildren.ToList()

                _manager.RemoveDrawableFromCollection(group)
                _manager.RemoveChildFromGroup(parent, group)

                For Each child In children
                    Dim element = child.DrawableElement
                    Dim source = GroupingState.VisualOf(child)
                    GroupingState.DetachVisual(source)
                    If source Is element Then
                        ' A bare imported path's transform is now part of its new wrapper placement.
                        element.Width = placements(child).Width
                        element.Height = placements(child).Height
                        element.RenderTransform = Transform.Identity
                        element.RenderTransformOrigin = New Point(0, 0)
                    End If
                    element.IsHitTestVisible = True

                    _manager.AddChildToGroup(parent, child, childIndex)
                    _manager.AddDrawableToCollection(child, index)
                    placements(child).ApplyToWrapper(TransformAction.GetWrapper(child), applyScale:=False)
                    released.Add(child)
                    index += 1
                    If childIndex >= 0 Then childIndex += 1
                Next
            Next
            _manager.NotifyChanged()
            PolyCanvas.ApplySelection(released, clearExisting:=True)
            _after = New GroupingState(_manager, _participants.Where(Function(d) Not groups.Contains(TryCast(d, NestedDrawableGroup))))
        End Using
        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        If _after IsNot Nothing Then _before.Restore(_manager, _participants)
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        _after?.Restore(_manager, _participants)
    End Sub
End Class

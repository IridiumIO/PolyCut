Option Strict On
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class PasteDrawablesAction : Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _projectService As ProjectSerializationService
    Private ReadOnly _projectData As ProjectData
    Private ReadOnly _designerStyle As Style

    Private _built As RuntimeProjectModel
    Private _addedToCollection As New List(Of IDrawable)
    Private _addedImportedGroups As New List(Of IDrawable)
    Private _attachedToDrawingGroup As New List(Of IDrawable)
    Private _reattachedToExistingImportRoot As New List(Of (Host As IDrawable, Child As IDrawable))

    Public Sub New(manager As IDrawableManager, projectService As ProjectSerializationService, projectData As ProjectData, designerStyle As Style)
        _manager = manager
        _projectService = projectService
        _projectData = projectData
        _designerStyle = designerStyle
    End Sub


    Public ReadOnly Property Description As String = "Paste" Implements IUndoableAction.Description


    Public Sub Undo() Implements IUndoableAction.Undo
        For Each t In _reattachedToExistingImportRoot
            Try
                _manager.RemoveChildFromGroup(t.Host, t.Child)
            Catch
            End Try
        Next

        ' Detach from DrawingGroup first
        For Each d In _attachedToDrawingGroup
            Try
                _manager.RemoveChildFromGroup(_manager.DrawingGroup, d)
            Catch
            End Try
        Next

        ' Remove from collection
        Try
            _manager.RemoveDrawablesFromCollection(_addedToCollection)
        Catch
        End Try

        ' Remove from ImportedGroups
        For Each g In _addedImportedGroups
            Try
                _manager.RemoveImportRoot(g)
            Catch
            End Try
        Next

        _manager.NotifyChanged()
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        ApplyInstances()
    End Sub

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _projectData Is Nothing Then Return False

        PolyCanvas.ClearSelection()

        _built = _projectService.BuildRuntimeModel(_projectData, _designerStyle)
        If _built Is Nothing Then Return False

        ' ---- compute existingByName + _reattachedToExistingImportRoot ----
        Dim existingByName As New Dictionary(Of String, IDrawable)(StringComparer.OrdinalIgnoreCase)
        For Each g In _manager.ImportRoots
            If Not String.IsNullOrWhiteSpace(g.Name) Then existingByName(g.Name) = g
        Next

        For Each g In _built.Groups

            If g Is Nothing OrElse g.ParentGroup IsNot Nothing Then Continue For

            Dim dg = TryCast(g, DrawableGroup)

            ' Non-DrawableGroup roots: always just add to ImportedGroups
            If dg Is Nothing Then
                If Not _manager.ContainsImportRoot(g) Then
                    _manager.AddImportRoot(g)
                    _addedImportedGroups.Add(g)
                End If
                Continue For
            End If

            ' DrawableGroup roots: apply special exclusions
            If _manager.IsDrawingGroup(dg) Then Continue For

            Dim existing As IDrawable = Nothing

            ' If it matches an existing import root, reattach children and don't add dg as a new imported group
            If existingByName.TryGetValue(dg.Name, existing) Then
                For Each ch In ProjectGraph.GetGroupChildren(dg)?.Where(Function(c) c IsNot Nothing).ToList()
                    If ch Is Nothing Then Continue For
                    _manager.AddChildToGroup(existing, ch)
                    _reattachedToExistingImportRoot.Add((existing, ch))
                Next
                Continue For
            End If

            ' Otherwise it's a new imported root group
            If Not _manager.ContainsImportRoot(dg) Then
                _manager.AddImportRoot(dg)
                _addedImportedGroups.Add(dg)
            End If

        Next


        ' ---- populate _addedToCollection ----

        Dim toAdd As New List(Of IDrawable)
        For Each g In ProjectSerializationService.CanvasItemsInDocumentOrder(_projectData, _built)
            If g Is Nothing Then Continue For

            Do While PastedNameExistsInDrawableCollection(g.Name)
                g.Name = GetIncrementedName(g.Name)
            Loop

            toAdd.Add(g)
            _addedToCollection.Add(g)
        Next

        _manager.AddDrawablesToCollection(toAdd)

        ' ---- populate _attachedToDrawingGroup ----
        If _manager.DrawingGroup IsNot Nothing Then
            For Each d In _built.Drawables
                If d Is Nothing Then Continue For
                If d.ParentGroup IsNot Nothing Then Continue For
                _manager.AddChildToGroup(_manager.DrawingGroup, d)
                _attachedToDrawingGroup.Add(d)
            Next
        End If

        ' ---- Apply wrapper state + notify (same path as Redo) ----
        Application.Current.Dispatcher.Invoke(Sub() ApplyInstances(), Threading.DispatcherPriority.Normal)

        Return True
    End Function


    Private Function GetIncrementedName(og As String) As String
        Dim newName As String = og
        Dim indexofUnderscore = newName.LastIndexOf("_"c)
        If indexofUnderscore >= 0 Then
            Dim baseName = newName.Substring(0, indexofUnderscore)
            Dim suffix = newName.Substring(indexofUnderscore + 1)
            Dim num As Integer
            If Integer.TryParse(suffix, num) Then
                newName = $"{baseName}_{num + 1}"
            Else
                newName = $"{newName}_1"
            End If
        Else
            newName = $"{newName}_1"
        End If
        Return newName
    End Function


    Private Function PastedNameExistsInDrawableCollection(name As String) As Boolean
        For Each d In _manager.DrawableCollection
            If String.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function


    Private Sub ApplyInstances()
        ' Imported root groups
        For Each g In _addedImportedGroups
            If Not _manager.ContainsImportRoot(g) Then
                _manager.AddImportRoot(g)
            End If
        Next

        ' Canvas collection items
        _manager.AddDrawablesToCollection(_addedToCollection)

        ' DrawingGroup attachments
        If _manager.DrawingGroup IsNot Nothing Then
            For Each d In _attachedToDrawingGroup
                If Not _manager.ContainsChild(_manager.DrawingGroup, d) Then
                    _manager.AddChildToGroup(_manager.DrawingGroup, d)
                End If
            Next
        End If

        ' Reuse existing imported roots: child reattachments
        For Each t In _reattachedToExistingImportRoot
            Try
                If Not _manager.ContainsChild(t.Host, t.Child) Then
                    _manager.AddChildToGroup(t.Host, t.Child)
                End If
            Catch
            End Try
        Next

        ' Apply wrapper state + notify (keep deterministic; no BeginInvoke here)
        ProjectApplyHelper.ApplyWrapperStateForPaste(_projectData, _built)
        _manager.NotifyChanged()
    End Sub

End Class

Friend NotInheritable Class ProjectApplyHelper

    Public Shared Sub ApplyWrapperStateForPaste(pd As ProjectData, built As RuntimeProjectModel)
        If pd Is Nothing OrElse built Is Nothing Then Return

        Dim toSelect As New List(Of IDrawable)

        ' Groups
        For Each gd In pd.Groups
            Dim grp As IDrawable = Nothing
            If Not built.GroupById.TryGetValue(gd.Id, grp) Then Continue For
            If grp?.DrawableElement Is Nothing Then Continue For

            If grp.HasNestedAncestor() Then Continue For

            Dim wrapper = TryCast(grp.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            wrapper.Visibility = If(gd.IsHidden, Visibility.Collapsed, Visibility.Visible)
            If grp.ParentGroup Is Nothing OrElse TypeOf (grp.ParentGroup) Is DrawableGroup Then
                toSelect.Add(grp)
            End If
            Dim groupState As New TransformState With {
                .Translation = New Point(gd.Left, gd.Top),
                .Width = If(gd.Width > 0, gd.Width, Double.NaN),
                .Height = If(gd.Height > 0, gd.Height, Double.NaN),
                .Rotation = gd.RotationAngle,
                .Scale = New Point(If(gd.ScaleX = 0, 1.0, gd.ScaleX), If(gd.ScaleY = 0, 1.0, gd.ScaleY))
            }
            groupState.ApplyToWrapper(wrapper, applyScale:=True)
            Panel.SetZIndex(wrapper, gd.ZIndex)
            MetadataHelper.SetOriginalDimensions(wrapper, (gd.Width, gd.Height))

        Next

        ' Drawables
        For Each dd In pd.Drawables
            Dim d As IDrawable = Nothing
            If Not built.DrawableById.TryGetValue(dd.Id, d) Then Continue For
            If d?.DrawableElement Is Nothing Then Continue For

            If d.HasNestedAncestor() Then Continue For

            Dim wrapper = TryCast(d.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For
            If d.ParentGroup Is Nothing OrElse TypeOf (d.ParentGroup) Is DrawableGroup Then
                toSelect.Add(d)
            End If
            Dim drawState As New TransformState With {
                .Translation = New Point(dd.Left, dd.Top),
                .Width = If(dd.Width > 0, dd.Width, Double.NaN),
                .Height = If(dd.Height > 0, dd.Height, Double.NaN),
                .Rotation = dd.RotationAngle
            }
            drawState.ApplyToWrapper(wrapper, applyScale:=False)
            Panel.SetZIndex(wrapper, dd.ZIndex)
            MetadataHelper.SetOriginalDimensions(wrapper, (dd.Width, dd.Height))

        Next
        PolyCanvas.ApplySelection(toSelect, Nothing, True)

    End Sub

End Class


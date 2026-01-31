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
    Private _reattachedToExistingImportRoot As New List(Of (Host As DrawableGroup, Child As IDrawable))

    Public Sub New(manager As IDrawableManager, projectService As ProjectSerializationService, projectData As ProjectData, designerStyle As Style)
        _manager = manager
        _projectService = projectService
        _projectData = projectData
        _designerStyle = designerStyle
    End Sub


    Public ReadOnly Property Description As String = "Paste" Implements IUndoableAction.Description


    Public Sub Undo() Implements IUndoableAction.Undo
        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM Is Nothing Then Return

        For Each t In _reattachedToExistingImportRoot
            Try
                t.Host.RemoveChild(t.Child)
                t.Child.ParentGroup = Nothing
            Catch
            End Try
        Next

        ' Detach from DrawingGroup first
        For Each d In _attachedToDrawingGroup
            Try
                mainVM.DrawingGroup?.RemoveChild(d)
                d.ParentGroup = Nothing
            Catch
            End Try
        Next

        ' Remove from collection
        For Each item In _addedToCollection
            Try
                _manager.RemoveDrawableFromCollection(item)
            Catch
            End Try
        Next

        ' Remove from ImportedGroups
        For Each g In _addedImportedGroups
            Try
                If mainVM.ImportedGroups.Contains(g) Then
                    mainVM.ImportedGroups.Remove(g)
                End If
            Catch
            End Try
        Next

        mainVM.NotifyCollectionsChanged()
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM Is Nothing Then Return
        ApplyInstances(mainVM)
    End Sub

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _projectData Is Nothing Then Return False
        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM Is Nothing Then Return False

        PolyCanvas.ClearSelection()

        _built = _projectService.BuildRuntimeModel(_projectData, _designerStyle)
        If _built Is Nothing Then Return False

        ' ---- compute existingByName + _reattachedToExistingImportRoot ----
        Dim existingByName As New Dictionary(Of String, DrawableGroup)(StringComparer.OrdinalIgnoreCase)
        For Each g In mainVM.ImportedGroups.OfType(Of DrawableGroup)()
            If Not String.IsNullOrWhiteSpace(g.Name) Then existingByName(g.Name) = g
        Next

        For Each g In _built.Groups

            If g Is Nothing OrElse g.ParentGroup IsNot Nothing Then Continue For

            Dim dg = TryCast(g, DrawableGroup)

            ' Non-DrawableGroup roots: always just add to ImportedGroups
            If dg Is Nothing Then
                If Not mainVM.ImportedGroups.Contains(g) Then
                    mainVM.ImportedGroups.Add(g)
                    _addedImportedGroups.Add(g)
                End If
                Continue For
            End If

            ' DrawableGroup roots: apply special exclusions
            If String.Equals(dg.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then Continue For

            Dim existing As DrawableGroup = Nothing

            ' If it matches an existing import root, reattach children and don't add dg as a new imported group
            If existingByName.TryGetValue(dg.Name, existing) Then
                For Each ch In ProjectGraph.GetGroupChildren(dg)?.Where(Function(c) c IsNot Nothing).ToList()
                    If ch Is Nothing Then Continue For
                    existing.AddChild(ch)
                    ch.ParentGroup = existing
                    _reattachedToExistingImportRoot.Add((existing, ch))
                Next
                Continue For
            End If

            ' Otherwise it's a new imported root group
            If Not mainVM.ImportedGroups.Contains(dg) Then
                mainVM.ImportedGroups.Add(dg)
                _addedImportedGroups.Add(dg)
            End If

        Next


        ' ---- populate _addedToCollection ----
        For Each g In _built.Groups
            If g Is Nothing Then Continue For
            If TypeOf g Is NestedDrawableGroup AndAlso Not HasNestedAncestor(g) Then

                Do While PastedNameExistsInDrawableCollection(g.Name)
                    g.Name = GetIncrementedName(g.Name)
                Loop


                _manager.AddDrawableToCollection(g, -1)
                _addedToCollection.Add(g)
            End If
        Next

        For Each d In _built.Drawables
            If d Is Nothing Then Continue For
            If Not HasNestedAncestor(d) Then

                Do While PastedNameExistsInDrawableCollection(d.Name)
                    d.Name = GetIncrementedName(d.Name)
                Loop

                _manager.AddDrawableToCollection(d, -1)
                _addedToCollection.Add(d)
            End If
        Next

        ' ---- populate _attachedToDrawingGroup ----
        If mainVM.DrawingGroup IsNot Nothing Then
            For Each d In _built.Drawables
                If d Is Nothing Then Continue For
                If d.ParentGroup IsNot Nothing Then Continue For
                mainVM.DrawingGroup.AddChild(d)
                d.ParentGroup = mainVM.DrawingGroup
                _attachedToDrawingGroup.Add(d)
            Next
        End If

        ' ---- Apply wrapper state + notify (same path as Redo) ----
        Application.Current.Dispatcher.Invoke(
        Sub() ApplyInstances(mainVM),
        Threading.DispatcherPriority.Normal)

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


    Private Sub ApplyInstances(mainVM As MainViewModel)
        ' Imported root groups
        For Each g In _addedImportedGroups
            If Not mainVM.ImportedGroups.Contains(g) Then
                mainVM.ImportedGroups.Add(g)
            End If
        Next

        ' Canvas collection items
        For Each item In _addedToCollection
            If Not _manager.DrawableCollection.Contains(item) Then
                _manager.AddDrawableToCollection(item, -1)
            End If
        Next

        ' DrawingGroup attachments
        If mainVM.DrawingGroup IsNot Nothing Then
            For Each d In _attachedToDrawingGroup
                If Not mainVM.DrawingGroup.GroupChildren.Contains(d) Then
                    mainVM.DrawingGroup.AddChild(d)
                End If
                d.ParentGroup = mainVM.DrawingGroup
            Next
        End If

        ' Reuse existing imported roots: child reattachments
        For Each t In _reattachedToExistingImportRoot
            Try
                If Not t.Host.GroupChildren.Contains(t.Child) Then
                    t.Host.AddChild(t.Child)
                End If
                t.Child.ParentGroup = t.Host
            Catch
            End Try
        Next

        ' Apply wrapper state + notify (keep deterministic; no BeginInvoke here)
        ProjectApplyHelper.ApplyWrapperStateForPaste(_projectData, _built)
        mainVM.NotifyCollectionsChanged()
    End Sub





End Class

Friend NotInheritable Class ProjectApplyHelper

    Public Shared Sub ApplyWrapperStateForPaste(pd As ProjectData, built As RuntimeProjectModel)
        If pd Is Nothing OrElse built Is Nothing Then Return



        ' Groups
        For Each gd In pd.Groups
            Dim grp As IDrawable = Nothing
            If Not built.GroupById.TryGetValue(gd.Id, grp) Then Continue For
            If grp?.DrawableElement Is Nothing Then Continue For

            Dim wrapper = TryCast(grp.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            wrapper.Visibility = If(gd.IsHidden, Visibility.Collapsed, Visibility.Visible)
            If grp.ParentGroup Is Nothing OrElse TypeOf (grp.ParentGroup) Is DrawableGroup Then
                PolyCanvas.AddToSelection(grp)
                grp.IsSelected = True
            End If
            ApplyWrapperState(wrapper,
                                New WrapperState With {
                                    .Left = gd.Left,
                                    .Top = gd.Top,
                                    .Width = gd.Width,
                                    .Height = gd.Height,
                                    .ZIndex = gd.ZIndex,
                                    .RotationAngle = gd.RotationAngle,
                                    .ScaleX = gd.ScaleX,
                                    .ScaleY = gd.ScaleY
                                },
                                applyScaleToContent:=True)


        Next

        ' Drawables
        For Each dd In pd.Drawables
            Dim d As IDrawable = Nothing
            If Not built.DrawableById.TryGetValue(dd.Id, d) Then Continue For
            If d?.DrawableElement Is Nothing Then Continue For

            Dim wrapper = TryCast(d.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For
            If d.ParentGroup Is Nothing OrElse TypeOf (d.ParentGroup) Is DrawableGroup Then
                PolyCanvas.AddToSelection(d)
                d.IsSelected = True
            End If
            ApplyWrapperState(wrapper,
                              New WrapperState With {
                                  .Left = dd.Left,
                                  .Top = dd.Top,
                                  .Width = dd.Width,
                                  .Height = dd.Height,
                                  .ZIndex = dd.ZIndex,
                                  .RotationAngle = dd.RotationAngle
                              })

        Next

        Debug.WriteLine(Application.GetService(Of MainViewModel).SelectedDrawables.Count)






    End Sub


    Public Shared Sub ApplyWrapperState(wrapper As ContentControl, wrapperState As WrapperState, Optional applyScaleToContent As Boolean = False)

        wrapper.Width = wrapperState.Width
        wrapper.Height = wrapperState.Height
        Canvas.SetLeft(wrapper, wrapperState.Left)
        Canvas.SetTop(wrapper, wrapperState.Top)
        Panel.SetZIndex(wrapper, wrapperState.ZIndex)

        wrapper.RenderTransform = If(Math.Abs(wrapperState.RotationAngle) > 0.01, New RotateTransform(wrapperState.RotationAngle), Nothing)

        If applyScaleToContent Then
            Dim contentFe = TryCast(wrapper.Content, FrameworkElement)
            If contentFe IsNot Nothing Then
                If Math.Abs(wrapperState.ScaleX - 1.0) > 0.0001 OrElse Math.Abs(wrapperState.ScaleY - 1.0) > 0.0001 Then
                    Dim tg = TryCast(contentFe.RenderTransform, TransformGroup)
                    If tg Is Nothing Then
                        tg = New TransformGroup()
                        contentFe.RenderTransform = tg
                    End If

                    Dim st = tg.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                    If st Is Nothing Then
                        st = New ScaleTransform(1, 1)
                        tg.Children.Add(st)
                    End If

                    st.ScaleX = wrapperState.ScaleX
                    st.ScaleY = wrapperState.ScaleY
                    contentFe.RenderTransformOrigin = New Point(0.5, 0.5)
                End If
            End If
        End If

        MetadataHelper.SetOriginalDimensions(wrapper, (wrapperState.Width, wrapperState.Height))
        wrapper.InvalidateMeasure()
        wrapper.InvalidateArrange()
    End Sub

End Class


Friend NotInheritable Class WrapperState
    Public Property Left As Double
    Public Property Top As Double
    Public Property Width As Double
    Public Property Height As Double
    Public Property ZIndex As Integer
    Public Property RotationAngle As Double
    Public Property ScaleX As Double = 1.0
    Public Property ScaleY As Double = 1.0
End Class

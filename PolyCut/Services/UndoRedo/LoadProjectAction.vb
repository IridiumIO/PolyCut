Imports System.Collections.Generic
Imports System.Linq

Imports PolyCut.RichCanvas
Imports PolyCut.Shared
Imports PolyCut.Shared.Project

Namespace Services.UndoRedo

    Public Class LoadProjectAction
        Implements IUndoableAction

        Private ReadOnly _manager As IDrawableManager
        Private ReadOnly _projectService As ProjectSerializationService
        Private ReadOnly _filePath As String
        Private _projectData As ProjectData
        Private _previousState As ProjectSnapshot

        Public Sub New(manager As IDrawableManager, projectService As ProjectSerializationService, filePath As String)
            _manager = manager
            _projectService = projectService
            _filePath = filePath
        End Sub

        Public ReadOnly Property Description As String Implements IUndoableAction.Description
            Get
                Return $"Load Project: {IO.Path.GetFileName(_filePath)}"
            End Get
        End Property

        Public Function Execute() As Boolean Implements IUndoableAction.Execute

            _previousState = ProjectSnapshot.Capture(_manager)

            _projectData = _projectService.LoadProject(_filePath)
            If _projectData Is Nothing Then Return False

            ClearCurrentProject()
            Return RestoreProjectState(_projectData)
        End Function

        Public Sub Undo() Implements IUndoableAction.Undo
            If _previousState Is Nothing Then Return

            ClearCurrentProject()
            _previousState.Restore(_manager, _projectService)
        End Sub

        Public Sub Redo() Implements IUndoableAction.Redo
            If _projectData Is Nothing Then Return

            ClearCurrentProject()
            RestoreProjectState(_projectData)
        End Sub

        Private Sub ClearCurrentProject()
            ' Clear DrawingGroup children first
            Dim mainVM = TryCast(_manager, MainViewModel)
            If mainVM IsNot Nothing AndAlso mainVM.DrawingGroup IsNot Nothing Then
                mainVM.DrawingGroup.GroupChildren.Clear()
            End If

            ' Clear all drawables
            Dim drawablesToRemove = _manager.DrawableCollection.ToList()
            For Each drawable In drawablesToRemove
                _manager.RemoveDrawableFromCollection(drawable)
            Next

            ' Clear imported groups
            If mainVM IsNot Nothing Then
                mainVM.ImportedGroups.Clear()
            End If
        End Sub

        Private Function RestoreProjectState(projectData As ProjectData) As Boolean
            Try
                Dim mainVM = TryCast(_manager, MainViewModel)

                ' Step 1: Restore all groups first
                Dim groupIdMap As New Dictionary(Of Guid, DrawableGroup)

                For Each groupData In projectData.Groups
                    Dim group As New DrawableGroup(groupData.Name)
                    groupIdMap(groupData.Id) = group
                Next

                ' Step 2:  parent-child relationships between groups
                For Each groupData In projectData.Groups
                    Dim group = groupIdMap(groupData.Id)

                    If groupData.ParentGroupId.HasValue AndAlso groupIdMap.ContainsKey(groupData.ParentGroupId.Value) Then
                        Dim parentGroup = groupIdMap(groupData.ParentGroupId.Value)
                        parentGroup.AddChild(group)
                    End If
                Next

                ' Step 3: Add top-level groups to manager and iportedGroups
                If mainVM IsNot Nothing Then
                    For Each kvp In groupIdMap
                        Dim group = kvp.Value

                        ' Only add top-level groups  to ImportedGroups
                        If group.ParentGroup Is Nothing Then
                            _manager.AddDrawableToCollection(group, -1)

                            If Not mainVM.ImportedGroups.Contains(group) Then
                                mainVM.ImportedGroups.Add(group)
                            End If

                            ' Set DrawingGroup if this is the Drawing Group
                            If String.Equals(group.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then
                                mainVM.DrawingGroup = group
                            End If
                        End If
                    Next

                    ' Ensure DrawingGroup exists even if not in saved data
                    If mainVM.DrawingGroup Is Nothing Then
                        mainVM.DrawingGroup = New DrawableGroup("Drawing Group")
                        _manager.AddDrawableToCollection(mainVM.DrawingGroup, 0)

                        If Not mainVM.ImportedGroups.Contains(mainVM.DrawingGroup) Then
                            mainVM.ImportedGroups.Add(mainVM.DrawingGroup)
                        End If
                    End If
                End If

                ' Step 4: Create drawables and map them by ID
                Dim drawableIdMap As New Dictionary(Of Guid, IDrawable)
                Dim drawableDataMap As New Dictionary(Of IDrawable, DrawableData)

                For Each drawableData In projectData.Drawables
                    Dim element = _projectService.DeserializeDrawable(drawableData)
                    If element Is Nothing Then Continue For

                    ' Pre-set position on element (before wrapper is created)
                    Canvas.SetLeft(element, drawableData.Left)
                    Canvas.SetTop(element, drawableData.Top)

                    ' For TextBox, set dimensions on the element itself
                    If TypeOf element Is TextBox Then
                        element.Width = drawableData.Width
                        element.Height = drawableData.Height
                    End If

                    ' Create drawable from element
                    Dim drawable As IDrawable = Nothing

                    If TypeOf element Is System.Windows.Shapes.Path Then
                        drawable = New DrawablePath(CType(element, System.Windows.Shapes.Path))
                    ElseIf TypeOf element Is Rectangle Then
                        drawable = New DrawableRectangle(CType(element, Rectangle))
                    ElseIf TypeOf element Is Ellipse Then
                        drawable = New DrawableEllipse(CType(element, Ellipse))
                    ElseIf TypeOf element Is Line Then
                        drawable = New DrawableLine(CType(element, Line))
                    ElseIf TypeOf element Is TextBox Then
                        drawable = New DrawableText(CType(element, TextBox))
                    End If

                    If drawable IsNot Nothing Then
                        drawable.IsHidden = drawableData.IsHidden
                        drawableIdMap(drawableData.Id) = drawable
                        drawableDataMap(drawable) = drawableData
                    End If

                    drawable.Name = drawableData.Name
                Next

                ' Step 5: Add drawables to their correct parent groups
                For Each drawableData In projectData.Drawables
                    If Not drawableIdMap.ContainsKey(drawableData.Id) Then Continue For

                    Dim drawable = drawableIdMap(drawableData.Id)
                    Dim targetGroup As DrawableGroup = Nothing

                    ' Find the correct parent group
                    If drawableData.ParentGroupId.HasValue AndAlso groupIdMap.ContainsKey(drawableData.ParentGroupId.Value) Then
                        targetGroup = groupIdMap(drawableData.ParentGroupId.Value)
                    ElseIf mainVM IsNot Nothing Then
                        ' No parent specified, add to DrawingGroup
                        targetGroup = mainVM.DrawingGroup
                    End If

                    If targetGroup IsNot Nothing Then
                        targetGroup.AddChild(drawable)
                    End If

                    ' Add to collection (triggers PolyCanvas to create wrapper)
                    _manager.AddDrawableToCollection(drawable, -1)
                Next

                ' Step 6: Configure wrappers in next dispatcher cycle
                Application.Current.Dispatcher.BeginInvoke(New Action(Sub()
                                                                          For Each kvp In drawableDataMap
                                                                              Dim drawable = kvp.Key
                                                                              Dim drawableData = kvp.Value

                                                                              If drawable?.DrawableElement Is Nothing Then Continue For
                                                                              Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                                                                              If wrapper Is Nothing Then Continue For

                                                                              ' Apply wrapper-specific properties
                                                                              wrapper.Width = drawableData.Width
                                                                              wrapper.Height = drawableData.Height

                                                                              Canvas.SetLeft(wrapper, drawableData.Left)
                                                                              Canvas.SetTop(wrapper, drawableData.Top)
                                                                              Panel.SetZIndex(wrapper, drawableData.ZIndex)

                                                                              If Math.Abs(drawableData.RotationAngle) > 0.01 Then
                                                                                  wrapper.RenderTransform = New RotateTransform(drawableData.RotationAngle)
                                                                              End If

                                                                              MetadataHelper.SetOriginalDimensions(wrapper, (drawableData.Width, drawableData.Height))

                                                                              wrapper.InvalidateMeasure()
                                                                              wrapper.InvalidateArrange()
                                                                              wrapper.UpdateLayout()
                                                                          Next

                                                                          If mainVM IsNot Nothing Then
                                                                              mainVM.NotifyCollectionsChanged()
                                                                          End If
                                                                      End Sub), Threading.DispatcherPriority.Loaded)

                Return True
            Catch ex As Exception
                Debug.WriteLine($"Failed to restore project state: {ex.Message}")
                Return False
            End Try
        End Function

    End Class


    Public Class ProjectSnapshot
        Public Property DrawableSnapshots As List(Of (Id As Guid, Data As DrawableData))
        Public Property GroupSnapshots As List(Of GroupData)

        Public Shared Function Capture(manager As IDrawableManager) As ProjectSnapshot
            Dim snapshot As New ProjectSnapshot With {
                .DrawableSnapshots = New List(Of (Guid, DrawableData)),
                .GroupSnapshots = New List(Of GroupData)
            }

            ' Capture all current drawables
            Dim projectService As New ProjectSerializationService()
            Dim drawableIdMap As New Dictionary(Of IDrawable, Guid)

            For Each drawable In manager.DrawableCollection
                Dim id = Guid.NewGuid()
                drawableIdMap(drawable) = id

                snapshot.DrawableSnapshots.Add((id, Nothing))
            Next

            Return snapshot
        End Function

        Public Sub Restore(manager As IDrawableManager, projectService As ProjectSerializationService)

            For Each snap In DrawableSnapshots
                If snap.Data IsNot Nothing Then
                    Dim element = projectService.DeserializeDrawable(snap.Data)
                End If
            Next
        End Sub
    End Class

End Namespace

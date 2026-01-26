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
                Dim designerStyle = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)
                Dim built As RuntimeProjectModel = _projectService.BuildRuntimeModel(projectData, designerStyle)

                Dim allGroups As List(Of IDrawable) = built.Groups
                Dim allDrawables As List(Of IDrawable) = built.Drawables

                ' Populate VM group list (top-level groups only)
                If mainVM IsNot Nothing Then
                    mainVM.ImportedGroups.Clear()

                    For Each g In allGroups
                        If g.ParentGroup Is Nothing Then
                            mainVM.ImportedGroups.Add(g)
                        End If
                    Next
                End If

                ' --- Ensure DrawingGroup exists 
                If mainVM IsNot Nothing Then
                    mainVM.DrawingGroup = Nothing

                    ' Prefer: find by name among existing DrawableGroup groups
                    For Each g In allGroups.OfType(Of DrawableGroup)()
                        If String.Equals(g.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then
                            mainVM.DrawingGroup = g
                            Exit For
                        End If
                    Next

                    If mainVM.DrawingGroup Is Nothing Then
                        mainVM.DrawingGroup = New DrawableGroup("Drawing Group")
                        mainVM.ImportedGroups.Add(mainVM.DrawingGroup)
                        allGroups.Add(mainVM.DrawingGroup)
                    End If
                End If

                For Each g In allGroups
                    If TypeOf g Is NestedDrawableGroup AndAlso Not HasNestedAncestor(g) Then
                        _manager.AddDrawableToCollection(g, -1)
                    End If
                Next

                For Each d In allDrawables
                    If Not HasNestedAncestor(d) Then
                        _manager.AddDrawableToCollection(d, -1)
                    End If
                Next

                Application.Current.Dispatcher.BeginInvoke(New Action(Sub()

                                                                          ' --- Apply GROUP wrapper state (includes NestedDrawableGroup wrapper) ---
                                                                          For Each gd In projectData.Groups
                                                                              Dim grp As IDrawable = Nothing
                                                                              If Not built.GroupById.TryGetValue(gd.Id, grp) Then Continue For
                                                                              If grp?.DrawableElement Is Nothing Then Continue For

                                                                              Dim groupWrapper = TryCast(grp.DrawableElement.Parent, ContentControl)
                                                                              If groupWrapper Is Nothing Then Continue For

                                                                              groupWrapper.Visibility = If(gd.IsHidden, Visibility.Collapsed, Visibility.Visible)
                                                                              ApplyWrapperState(groupWrapper, gd.Left, gd.Top, gd.Width, gd.Height, gd.ZIndex, gd.RotationAngle)
                                                                          Next

                                                                          ' --- Apply DRAWABLE wrapper state ---
                                                                          For Each dd In projectData.Drawables
                                                                              Dim d As IDrawable = Nothing
                                                                              If Not built.DrawableById.TryGetValue(dd.Id, d) Then Continue For
                                                                              If d?.DrawableElement Is Nothing Then Continue For

                                                                              Dim wrapper = TryCast(d.DrawableElement.Parent, ContentControl)
                                                                              If wrapper Is Nothing Then Continue For

                                                                              ApplyWrapperState(wrapper, dd.Left, dd.Top, dd.Width, dd.Height, dd.ZIndex, dd.RotationAngle)
                                                                          Next

                                                                          Try : Application.Current.MainWindow?.UpdateLayout() : Catch : End Try
                                                                          mainVM?.NotifyCollectionsChanged()
                                                                      End Sub), Threading.DispatcherPriority.Loaded)

                Return True

            Catch ex As Exception
                Debug.WriteLine($"Failed to restore project state: {ex.Message}")
                Return False
            End Try
        End Function



        Private Shared Sub ApplyWrapperState(wrapper As ContentControl, left As Double, top As Double, width As Double, height As Double, z As Integer, rotationAngle As Double)

            wrapper.Width = width
            wrapper.Height = height
            Canvas.SetLeft(wrapper, left)
            Canvas.SetTop(wrapper, top)
            Panel.SetZIndex(wrapper, z)

            If Math.Abs(rotationAngle) > 0.01 Then wrapper.RenderTransform = New RotateTransform(rotationAngle)
            MetadataHelper.SetOriginalDimensions(wrapper, (width, height))

            wrapper.InvalidateMeasure()
            wrapper.InvalidateArrange()
        End Sub


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
                    Dim element = DrawableCodec.DeserializeDrawable(snap.Data)
                End If
            Next
        End Sub
    End Class

End Namespace

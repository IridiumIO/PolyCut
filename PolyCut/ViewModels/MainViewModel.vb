Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Net.WebRequestMethods

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.Core
Imports PolyCut.RichCanvas
Imports PolyCut.Services.UndoRedo
Imports PolyCut.Shared

Imports Svg

Imports WPF.Ui
Partial Public Class MainViewModel
    Inherits ObservableObject
    Implements IDrawableManager

    ' Services
    Private ReadOnly _snackbarService As SnackbarService
    Private ReadOnly _navigationService As INavigationService
    Private ReadOnly _argsService As CommandLineArgsService
    Private ReadOnly _svgImportService As ISvgImportService
    Private ReadOnly _undoRedoService As UndoRedoService
    Private ReadOnly _projectService As ProjectSerializationService

    ' State / configuration
    <ObservableProperty> Private _UsingGCodePlot As Boolean
    <ObservableProperty> Private _Printers As ObservableCollection(Of Printer)
    <ObservableProperty> Private _Printer As Printer
    <ObservableProperty> Private _CuttingMats As ObservableCollection(Of CuttingMat)
    <ObservableProperty> Private _Configuration As ProcessorConfiguration
    <ObservableProperty> Private _UIConfiguration As UIConfiguration

    <ObservableProperty> Private _GCode As String = Nothing
    <ObservableProperty> Private _GCodeGeometry As GCodeGeometry
    <ObservableProperty> Private _GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()
    <ObservableProperty> Private _GeneratedGCode As List(Of GCode)

    <ObservableProperty>
    <ImplementsProperty(GetType(IDrawableManager), NameOf(IDrawableManager.DrawableCollection))>
    Private _DrawableCollection As ObservableCollection(Of IDrawable) = New ObservableCollection(Of IDrawable)
    <ObservableProperty> Private _ImportedGroups As New ObservableCollection(Of DrawableGroup)

    Friend DrawingGroup As DrawableGroup

    ' Suspension flag for transform message handling
    Private _suspendTransformMessageHandling As Boolean = False

    ' UI meta properties
    <NotifyPropertyChangedFor(NameOf(LogarithmicPreviewSpeed))>
    <ObservableProperty> Private _PreviewRenderSpeed As Double = 0.48

    Private Sub OnPreviewRenderSpeedChanged(oldValue As Double, newValue As Double)
        LogarithmicPreviewSpeed = CInt(30 * (Math.Exp(6 * newValue) - 1) + 30)
    End Sub

    <ObservableProperty> Private _LogarithmicPreviewSpeed As Integer = 500


    ' Convenience / computed properties
    Public ReadOnly Property SelectedDrawable As IDrawable
        Get
            Return PolyCanvas.SelectedItems?.FirstOrDefault()
        End Get
    End Property

    Public ReadOnly Property SelectedWrapper As ContentControl
        Get
            Return PolyCanvas.CurrentSelected
        End Get
    End Property

    Public ReadOnly Property SelectedDrawables As IEnumerable(Of IDrawable)
        Get
            Return DrawableCollection.Where(Function(d) d.IsSelected)
        End Get
    End Property

    Public ReadOnly Property HasMultipleSelected As Boolean
        Get
            Return SelectedDrawables.Count() > 1
        End Get
    End Property

    Public ReadOnly Property PolyCutDocumentName As String
        Get
            If ImportedGroups.Count <> 0 AndAlso ImportedGroups.First.Name <> "Drawing Group" Then
                Return ImportedGroups.First.Name.Replace(".svg", ".gcode")
            Else
                Return "PolyCut1.gcode"
            End If
        End Get
    End Property

    Public Event PrinterConfigOpened()
    Public Event PrinterConfigClosed()

    ' Commands

    <RelayCommand> Public Sub MainViewLoaded()
        If _argsService.Args.Length > 0 Then DragSVGs(_argsService.Args)
    End Sub

    <RelayCommand> Public Sub MainViewClosing()
        SettingsHandler.WriteConfiguration(Configuration)
        SettingsHandler.WriteUIConfiguration(UIConfiguration)
    End Sub

    <RelayCommand> Public Sub CopyGCodeToClipboard()
        If Not String.IsNullOrEmpty(GCode) Then
            Clipboard.SetText(GCode)
        End If
    End Sub

    <RelayCommand> Public Sub UnionShapes()
        BooleanOperation(GeometryCombineMode.Union)
    End Sub

    <RelayCommand> Public Sub IntersectShapes()
        BooleanOperation(GeometryCombineMode.Intersect)
    End Sub

    <RelayCommand> Public Sub SubtractShapes()
        BooleanOperation(GeometryCombineMode.Exclude)
    End Sub

    <RelayCommand> Public Sub XorShapes()
        BooleanOperation(GeometryCombineMode.Xor)
    End Sub

    <RelayCommand> Public Sub Undo()
        _undoRedoService.Undo()
    End Sub

    <RelayCommand> Public Sub Redo()
        _undoRedoService.Redo()
    End Sub


    ' Constructor & initialization
    Public Sub New(snackbarService As SnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService, svgImportService As ISvgImportService, undoRedoService As UndoRedoService, projectService As ProjectSerializationService)
        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService
        _svgImportService = svgImportService
        _undoRedoService = undoRedoService
        _projectService = projectService


        AddHandler PolyCanvas.SelectionCountChanged, AddressOf OnCanvasSelectionChanged
        AddHandler PolyCanvas.CurrentSelectedChanged, AddressOf OnCurrentSelectedChanged
        EventAggregator.Subscribe(Of TransformCompletedMessage)(AddressOf OnTransformCompleted)
        Initialise()
    End Sub

    Private Sub Initialise()
        CuttingMats = SettingsHandler.GetCuttingMats
        Printers = SettingsHandler.GetPrinters

        For Each p In Printers
            If p Is Nothing Then Continue For

            If p.CuttingMat IsNot Nothing Then
                Dim match As CuttingMat = Nothing

                If p.CuttingMat.Id <> Guid.Empty Then
                    match = CuttingMats.FirstOrDefault(Function(cm) cm.Id = p.CuttingMat.Id)
                End If

                If match Is Nothing Then
                    match = CuttingMats.FirstOrDefault(Function(cm) _
                        String.Equals(cm.Name, p.CuttingMat.Name, StringComparison.OrdinalIgnoreCase) AndAlso
                        cm.Width = p.CuttingMat.Width AndAlso
                        cm.Height = p.CuttingMat.Height AndAlso
                        String.Equals(cm.SVGSource, p.CuttingMat.SVGSource, StringComparison.OrdinalIgnoreCase))
                End If

                If match IsNot Nothing Then
                    p.CuttingMat = match
                Else
                    If p.CuttingMat.Id = Guid.Empty Then p.CuttingMat.Id = Guid.NewGuid()
                    CuttingMats.Add(p.CuttingMat)
                End If
            Else
                If CuttingMats.Count > 0 Then p.CuttingMat = CuttingMats.First()
            End If
        Next

        Printer = Printers.First
        Configuration = (SettingsHandler.GetConfigurations).First
        UIConfiguration = SettingsHandler.GetUIConfiguration()

        If DrawingGroup Is Nothing Then
            DrawingGroup = New DrawableGroup("Drawing Group")
            DrawableCollection.Add(DrawingGroup)
            ImportedGroups.Add(DrawingGroup)
        End If

        For Each grp In DrawableCollection.OfType(Of DrawableGroup)().ToList()
            If grp.Name <> "Drawing Group" Then
                DrawableCollection.Remove(grp)
                If Not ImportedGroups.Contains(grp) Then ImportedGroups.Add(grp)
            End If
        Next
    End Sub


    ' -----------------
    ' Selection / Transform event handlers
    ' -----------------
    Private Sub OnCanvasSelectionChanged(sender As Object, e As EventArgs)
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
    End Sub

    Private Sub OnCurrentSelectedChanged(sender As Object, e As EventArgs)
        OnPropertyChanged(NameOf(SelectedWrapper))
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
    End Sub

    Private Sub OnTransformCompleted(msg As TransformCompletedMessage)
        If _suspendTransformMessageHandling Then
            Debug.WriteLine("[MainViewModel] TransformCompleted ignored because transform recording is suspended")
            Return
        End If

        If msg?.Items Is Nothing OrElse msg.Items.Count = 0 Then Return

        Dim items = msg.Items.
            Where(Function(it) it.Drawable IsNot Nothing).
            Select(Function(it) (drawable:=it.Drawable, before:=TryCast(it.Before, TransformAction.Snapshot), after:=TryCast(it.After, TransformAction.Snapshot))).
            Where(Function(t) t.before IsNot Nothing AndAlso t.after IsNot Nothing AndAlso Not IsTransformUnchanged(t.before, t.after)).
            Select(Function(t) (t.drawable, t.before, t.after)).
            ToList()

        If items.Count > 0 Then
            _undoRedoService.Push(New TransformAction(items))
        End If
    End Sub

    Private Function IsTransformUnchanged(before As TransformAction.Snapshot, after As TransformAction.Snapshot) As Boolean
        Return Math.Abs(before.Left - after.Left) < 0.01 AndAlso
               Math.Abs(before.Top - after.Top) < 0.01 AndAlso
               Math.Abs(before.Width - after.Width) < 0.01 AndAlso
               Math.Abs(before.Height - after.Height) < 0.01 AndAlso
               ((before.RenderTransform Is Nothing AndAlso after.RenderTransform Is Nothing) OrElse
                (before.RenderTransform IsNot Nothing AndAlso after.RenderTransform IsNot Nothing AndAlso before.RenderTransform.Value = after.RenderTransform.Value))
    End Function

    ' -----------------
    ' Printer management
    ' -----------------
    <RelayCommand>
    Public Sub AddPrinter(newPrinter As Printer)
        Printers.Add(newPrinter)
        Printer = newPrinter
        SettingsHandler.WritePrinter(Printer)
        _snackbarService.GenerateSuccess("Added Preset", Printer.Name)
    End Sub

    <RelayCommand>
    Public Sub SavePrinter(newName As String)
        Dim nameToSave = If(String.IsNullOrWhiteSpace(newName), Printer?.Name, newName)

        If nameToSave Is Nothing Then
            _snackbarService.GenerateError("Error", "Printer name is empty", 3)
            Return
        End If

        Dim existingPrinter = Printers.FirstOrDefault(Function(p) p.Name = nameToSave)

        If existingPrinter IsNot Nothing Then
            Dim result = MessageBox.Show($"A printer with the name '{nameToSave}' already exists. Do you want to overwrite it?", "Overwrite Printer?", MessageBoxButton.YesNo, MessageBoxImage.Exclamation)
            If result = MessageBoxResult.No Then
                _snackbarService.GenerateError("Error", "Printer not saved. Name already exists.", 3)
                Return
            End If

            existingPrinter.CopyFrom(Printer)
            existingPrinter.Name = newName
            Printer = existingPrinter
        Else
            Dim newPrinter As Printer = Printer.Clone()
            Printers.Add(newPrinter)
            newPrinter.Name = nameToSave
            Printer = newPrinter
        End If

        SettingsHandler.WritePrinter(Printer)
        _snackbarService.GenerateSuccess("Saved Preset", Printer.Name)
    End Sub

    <RelayCommand>
    Public Sub ConfigurePrinter()
        Dim printerConfigWindow = Application.GetService(Of PrinterConfig)
        RaiseEvent PrinterConfigOpened()
        Dim result = printerConfigWindow.ShowDialog()
        RaiseEvent PrinterConfigClosed()

        For Each p In Printers
            SettingsHandler.WritePrinter(p)
        Next
    End Sub

    <RelayCommand>
    Public Sub DeletePrinter()
        If Printers.Count > 1 Then
            Dim toRemove = Printer
            Printer = Printers((Printers.IndexOf(toRemove) + 1) Mod (Printers.Count))
            Printers.Remove(toRemove)
            SettingsHandler.DeletePrinter(toRemove)
        End If
    End Sub

    ' -----------------
    ' Import / SVG handling
    ' -----------------
    <RelayCommand>
    Private Sub BrowseSVG()
        Dim fs As New Microsoft.Win32.OpenFileDialog With {
            .Filter = "*.svg|*.svg",
            .Multiselect = True
        }
        If fs.ShowDialog Then
            Dim actions As New List(Of IUndoableAction)()

            For Each fl In fs.FileNames
                Dim action As New ImportSVGAction(Me, _svgImportService, fl)
                If action.Execute() Then
                    actions.Add(action)
                End If
            Next

            If actions.Count > 0 Then
                _undoRedoService.Push(New CompositeAction(actions))
            End If
        End If
    End Sub

    Public Sub DragSVGs(x As String())
        Dim actions As New List(Of IUndoableAction)()

        For Each file In x
            Dim finfo As New FileInfo(file)
            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                Dim action As New ImportSVGAction(Me, _svgImportService, file)
                If action.Execute() Then
                    actions.Add(action)
                End If
            End If
        Next

        If actions.Count > 0 Then
            _undoRedoService.Push(New CompositeAction(actions))
        End If
    End Sub

    Public Sub UpdateSVGFiles()
        For Each grp In ImportedGroups.ToList()
            For Each ch In grp.GroupChildren
                If Not DrawableCollection.Contains(ch) Then
                    DrawableCollection.Add(ch)
                End If
            Next
        Next
        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(SelectedDrawable))
    End Sub

    ' -----------------
    ' Drawable collection management (IDrawableManager)
    ' -----------------
    Public Sub AddDrawableToCollection(drawable As IDrawable, index As Integer) Implements IDrawableManager.AddDrawableToCollection
        If drawable Is Nothing Then Return
        If DrawableCollection.Contains(drawable) Then Return

        If index >= 0 AndAlso index <= DrawableCollection.Count Then
            DrawableCollection.Insert(index, drawable)
        Else
            DrawableCollection.Add(drawable)
        End If
    End Sub

    Public Sub RemoveDrawableFromCollection(drawable As IDrawable) Implements IDrawableManager.RemoveDrawableFromCollection
        If drawable Is Nothing Then Return
        If DrawableCollection.Contains(drawable) Then
            DrawableCollection.Remove(drawable)
        End If
    End Sub

    Public Sub ClearDrawableParent(drawable As IDrawable) Implements IDrawableManager.ClearDrawableParent
        If drawable IsNot Nothing Then
            drawable.ParentGroup = Nothing
        End If
    End Sub

    Public Sub CleanupEmptyGroup(group As IDrawable) Implements IDrawableManager.CleanupEmptyGroup
        Dim drawableGroup = TryCast(group, DrawableGroup)
        If drawableGroup Is Nothing OrElse IsDrawingGroup(drawableGroup) Then Return

        If Not drawableGroup.GroupChildren.Any() Then
            If ImportedGroups.Contains(drawableGroup) Then
                ImportedGroups.Remove(drawableGroup)
            End If

            RemoveDrawableFromCollection(drawableGroup)

            Dim parentGroup = TryCast(drawableGroup.ParentGroup, DrawableGroup)
            If parentGroup IsNot Nothing Then
                parentGroup.RemoveChild(drawableGroup)
                CleanupEmptyGroup(parentGroup)
            End If
        End If
    End Sub

    Public Sub AddDrawableElement(element As FrameworkElement)
        Dim action As New AddDrawableAction(Me, element)
        If action.Execute() Then
            _undoRedoService.Push(action)
        End If
    End Sub

    Friend Sub NotifyCollectionsChanged()
        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(DrawableCollection))
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
    End Sub

    Public Sub RemoveSelectedDrawables()
        Dim selectedItems = SelectedDrawables.ToList()
        If selectedItems.Count = 0 Then Return

        PerformRemoveDrawables(selectedItems)
        PolyCanvas.ClearSelection()
    End Sub

    Private Sub PerformRemoveDrawables(drawables As List(Of IDrawable))
        Dim actions As New List(Of IUndoableAction)()
        Dim parentGroups = New HashSet(Of DrawableGroup)(drawables.Select(Function(d) GetParentGroup(d)).Where(Function(pg) pg IsNot Nothing))

        _suspendTransformMessageHandling = True

        For Each drawable In drawables
            Dim action As New RemoveDrawableAction(Me, drawable)
            If action.Execute() Then actions.Add(action)
        Next

        For Each grp In parentGroups.Where(Function(g) Not IsDrawingGroup(g) AndAlso Not g.GroupChildren.Any())
            Dim removeGroupAction As New RemoveGroupAction(Me, grp)
            If removeGroupAction.Execute() Then actions.Add(removeGroupAction)
        Next

        _suspendTransformMessageHandling = False

        If actions.Count > 0 Then
            _undoRedoService.Push(New CompositeAction(actions))
        End If
    End Sub

    <RelayCommand>
    Private Sub RemoveGroup(group As DrawableGroup)
        If group Is Nothing Then Return

        If IsDrawingGroup(group) Then
            Dim children = group.GroupChildren.ToList()
            If children.Count = 0 Then
                _snackbarService.GenerateError("Error", "Drawing Group is already empty", 3)
                Return
            End If
            PerformRemoveDrawables(children)
        Else
            Dim action As New RemoveGroupAction(Me, group)
            If action.Execute() Then
                _undoRedoService.Push(action)
            End If
        End If
    End Sub

    Friend Function GetTopLevelGroup(g As DrawableGroup) As DrawableGroup
        If g Is Nothing Then Return Nothing
        Dim cur As DrawableGroup = g
        While cur IsNot Nothing AndAlso cur.ParentGroup IsNot Nothing
            cur = TryCast(cur.ParentGroup, DrawableGroup)
        End While
        Return cur
    End Function

    Friend Function GetParentGroup(drawable As IDrawable) As DrawableGroup
        If drawable Is Nothing Then Return Nothing
        Dim pg = TryCast(drawable.ParentGroup, DrawableGroup)
        If pg Is Nothing Then
            pg = ImportedGroups.FirstOrDefault(Function(g) g.GroupChildren.Contains(drawable))
        End If
        Return pg
    End Function

    Friend Function IsAncestorOf(potentialAncestor As DrawableGroup, group As DrawableGroup) As Boolean
        If potentialAncestor Is Nothing OrElse group Is Nothing Then Return False
        Dim cur = group
        While cur IsNot Nothing
            If cur Is potentialAncestor Then Return True
            cur = TryCast(cur.ParentGroup, DrawableGroup)
        End While
        Return False
    End Function

    Private Function IsDrawingGroup(group As DrawableGroup) As Boolean
        Return group Is DrawingGroup OrElse String.Equals(group?.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function ValidateShapesForBoolean(shapes As List(Of IDrawable)) As (isValid As Boolean, errorMessage As String)
        For Each drawable In shapes
            Dim element = drawable.DrawableElement

            If TypeOf element Is Line Then
                Return (False, "Cannot perform boolean operations on open paths (Lines). All shapes must be closed.")
            End If

            If TypeOf element Is System.Windows.Shapes.Path Then
                Dim path = CType(element, System.Windows.Shapes.Path)
                Dim pathGeo = TryCast(path.Data, PathGeometry)
                If pathGeo IsNot Nothing Then
                    For Each figure In pathGeo.Figures
                        If Not figure.IsClosed Then
                            Return (False, "Cannot perform boolean operations on open paths. All shapes must be closed.")
                        End If
                    Next
                End If
            End If
        Next
        Return (True, Nothing)
    End Function

    ' -----------------
    ' GCode Generation
    ' -----------------
    <RelayCommand>
    Private Async Function GenerateGCode() As Task
        Configuration.WorkAreaHeight = Printer.BedHeight
        Configuration.WorkAreaWidth = Printer.BedWidth
        Configuration.SoftwareVersion = SettingsHandler.Version

        Dim generator As IGenerator = If(UsingGCodePlot,
            New GCodePlotGenerator(Configuration, Printer, GenerateSVGText),
            New PolyCutGenerator(Configuration, Printer, GenerateSVGText))

        Dim retcode = Await generator.GenerateGcodeAsync

        If retcode.StatusCode = 1 Then
            _snackbarService.GenerateError("Error", retcode.Message, 5)
            Return
        End If

        GeneratedGCode = generator.GetGCode
        Dim compiledGCodeString = BuildStringFromGCodes(GeneratedGCode)

        GCode = compiledGCodeString
        GCodeGeometry = New GCodeGeometry(GeneratedGCode)
        OnPropertyChanged(NameOf(GCode))
        _navigationService.Navigate(GetType(PreviewPage))
    End Function

    Private Function BuildStringFromGCodes(GeneratedGCode As List(Of GCode)) As String
        Dim stringBuilder As New Text.StringBuilder()
        For Each gc In GeneratedGCode
            stringBuilder.AppendLine(gc.ToString())
            If gc?.Comment?.Equals("Custom Start GCode") Then
                stringBuilder.Append(Printer.StartGCode.Trim)
            ElseIf gc?.Comment?.Equals("Custom End GCode") Then
                stringBuilder.Append(Printer.EndGCode.Trim)
            End If
        Next
        Return stringBuilder.ToString()
    End Function

    Function GenerateSVGText() As String
        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)
        }

        For Each drawableL In DrawableCollection.Where(Function(d) Not d.IsHidden)
            Dim finalElement = drawableL?.GetTransformedSVGElement

            If finalElement?.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) Then
                outDoc.Children.Add(finalElement)
            ElseIf TypeOf (drawableL) Is DrawablePath Then
                If drawableL?.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) Then
                    outDoc.Children.Add(finalElement)
                End If
            End If
        Next

        Return SVGImportService.SVGDocumentToString(outDoc)
    End Function

    ' -----------------
    ' Boolean / Geometry operations
    ' -----------------
    Private Sub BooleanOperation(combineMode As GeometryCombineMode)
        Dim selectedItems = SelectedDrawables.ToList()
        If selectedItems.Count < 2 Then
            _snackbarService.GenerateError("Error", "Select at least 2 shapes to combine", 3)
            Return
        End If

        Dim validation = ValidateShapesForBoolean(selectedItems)
        If Not validation.isValid Then
            _snackbarService.GenerateError("Error", validation.errorMessage, 4)
            Return
        End If

        _suspendTransformMessageHandling = True
        Dim action As New BooleanOperationAction(Me, selectedItems, combineMode)
        If action.Execute() Then
            _undoRedoService.Push(action)
        Else
            _snackbarService.GenerateError("Error", GetBooleanErrorMessage(combineMode), 4)
        End If
        _suspendTransformMessageHandling = False
    End Sub

    Private Function GetBooleanErrorMessage(mode As GeometryCombineMode) As String
        Select Case mode
            Case GeometryCombineMode.Union
                Return "Union operation resulted in an empty geometry. This should not happen."
            Case GeometryCombineMode.Intersect
                Return "No intersection found. The selected shapes do not overlap."
            Case GeometryCombineMode.Exclude
                Return "Subtraction resulted in an empty geometry. The shapes may not overlap or the result is fully subtracted."
            Case GeometryCombineMode.Xor
                Return "XOR operation resulted in an empty geometry. The shapes may be identical."
            Case Else
                Return "Boolean operation failed."
        End Select
    End Function

    <NotifyPropertyChangedFor(NameOf(CurrentProjectName))>
    <ObservableProperty> Private _currentProjectPath As String = Nothing

    Public ReadOnly Property CurrentProjectName As String
        Get
            Return Path.GetFileNameWithoutExtension(_currentProjectPath)
        End Get
    End Property

    <RelayCommand>
    Private Sub SaveProject()
        If String.IsNullOrEmpty(CurrentProjectPath) Then
            SaveProjectAs()
        Else
            If _projectService.SaveProject(CurrentProjectPath, DrawableCollection, ImportedGroups) Then
                _snackbarService.GenerateSuccess("Project Saved", IO.Path.GetFileName(CurrentProjectPath))
            Else
                _snackbarService.GenerateError("Error", "Failed to save project", 3)
            End If
        End If
    End Sub

    <RelayCommand>
    Private Sub SaveProjectAs()
        Dim saveDialog As New Microsoft.Win32.SaveFileDialog With {
            .Filter = "PolyCut Project (*.polycut)|*.polycut|All Files (*.*)|*.*",
            .DefaultExt = ".polycut"
        }

        If saveDialog.ShowDialog() = True Then
            CurrentProjectPath = saveDialog.FileName
            If _projectService.SaveProject(CurrentProjectPath, DrawableCollection, ImportedGroups) Then
                _snackbarService.GenerateSuccess("Project Saved", IO.Path.GetFileName(CurrentProjectPath))
            Else
                _snackbarService.GenerateError("Error", "Failed to save project", 3)
            End If
        End If
    End Sub

    <RelayCommand>
    Private Async Sub LoadProject()
        ' Warn if unsaved changes
        If DrawableCollection.Count > 1 Then

            Dim nx As New WPF.Ui.Controls.MessageBox With {
                .Title = "Load Project",
                .Content = "Loading a project will clear the current workspace. Continue?",
                .IsPrimaryButtonEnabled = True,
                .PrimaryButtonText = "Yes",
                .CloseButtonText = "No",
                .Owner = Application.Current.MainWindow,
                .WindowStartupLocation = WindowStartupLocation.CenterOwner}


            Dim res = Await nx.ShowDialogAsync()


            If res <> WPF.Ui.Controls.MessageBoxResult.Primary Then Return
        End If

        Dim openDialog As New Microsoft.Win32.OpenFileDialog With {
            .Filter = "PolyCut Project (*.polycut)|*.polycut|All Files (*.*)|*.*",
            .CheckFileExists = True
        }

        If openDialog.ShowDialog() = True Then
            Dim action As New LoadProjectAction(Me, _projectService, openDialog.FileName)
            If action.Execute() Then
                _undoRedoService.Clear()
                _undoRedoService.Push(action)
                CurrentProjectPath = openDialog.FileName
                _snackbarService.GenerateSuccess("Project Loaded", IO.Path.GetFileName(CurrentProjectPath))
                NotifyCollectionsChanged()
            Else
                _snackbarService.GenerateError("Error", "Failed to load project", 3)
            End If
        End If
    End Sub

    <RelayCommand>
    Private Async Sub NewProject()
        If DrawableCollection.Count > 1 Then
            Dim nx As New WPF.Ui.Controls.MessageBox With {
                .Title = "New Project",
                .Content = "Creating a new project will clear the current workspace. Continue?",
                .IsPrimaryButtonEnabled = True,
                .PrimaryButtonText = "Yes",
                .CloseButtonText = "No",
                .Owner = Application.Current.MainWindow,
                .WindowStartupLocation = WindowStartupLocation.CenterOwner}


            Dim res = Await nx.ShowDialogAsync()


            If res <> WPF.Ui.Controls.MessageBoxResult.Primary Then Return

        End If

        ' Clear everything
        _suspendTransformMessageHandling = True

        ' Clear DrawingGroup children
        If DrawingGroup IsNot Nothing Then
            DrawingGroup.GroupChildren.Clear()
        End If

        ' Clear imported groups
        ImportedGroups.Clear()

        ' Remove all drawables
        Dim drawablesToRemove = DrawableCollection.ToList()
        For Each drawable In drawablesToRemove
            RemoveDrawableFromCollection(drawable)
        Next

        _suspendTransformMessageHandling = False
        CurrentProjectPath = Nothing

        ' Reinitialize drawing group
        DrawingGroup = New DrawableGroup("Drawing Group")
        DrawableCollection.Add(DrawingGroup)
        ImportedGroups.Add(DrawingGroup)

        NotifyCollectionsChanged()
        _snackbarService.GenerateSuccess("New Project", "Workspace cleared")
    End Sub


    Public Property CuttingMatVisibility As Boolean
        Get
            Return UIConfiguration.ShowCuttingMat
        End Get
        Set(value As Boolean)
            UIConfiguration.ShowCuttingMat = value
        End Set
    End Property

    Public Property WorkingAreaVisibility As Boolean
        Get
            Return UIConfiguration.ShowWorkArea
        End Get
        Set(value As Boolean)
            UIConfiguration.ShowWorkArea = value
        End Set
    End Property


    Public Property IsGridVisible As Boolean
        Get
            If UIConfiguration.ShowGrid Then
                Application.GetService(Of SVGPageViewModel).GridLineBrush = New SolidColorBrush(Color.FromArgb(&H80, &HFF, &HFF, &HFF))
            Else
                Application.GetService(Of SVGPageViewModel).GridLineBrush = Brushes.Transparent
            End If
            Return UIConfiguration.ShowGrid
        End Get
        Set(value As Boolean)
            UIConfiguration.ShowGrid = value
            If value Then
                Application.GetService(Of SVGPageViewModel).GridLineBrush = New SolidColorBrush(Color.FromArgb(&H80, &HFF, &HFF, &HFF))
            Else
                Application.GetService(Of SVGPageViewModel).GridLineBrush = Brushes.Transparent
            End If
            Application.GetService(Of SVGPageViewModel).NotifyPropertyChangedForGrid()
        End Set
    End Property


End Class
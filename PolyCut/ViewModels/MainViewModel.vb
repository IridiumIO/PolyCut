Option Strict On
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.IO
Imports System.Net.WebRequestMethods

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.Core
Imports PolyCut.RichCanvas
Imports PolyCut.Services.UndoRedo
Imports PolyCut.Shared

Imports Svg

Imports Wpf.Ui
Partial Public Class MainViewModel : Inherits ObservableObject

    ' Services
    Private ReadOnly _snackbarService As SnackbarService
    Private ReadOnly _navigationService As INavigationService
    Private ReadOnly _argsService As CommandLineArgsService
    Private ReadOnly _svgImportService As ISvgImportService
    Private ReadOnly _undoRedoService As UndoRedoService
    Private ReadOnly _projectService As ProjectSerializationService

    'Operating System detection
    <ObservableProperty> Private _IsWindows As Boolean = Not WineDetection.IsRunningUnderWine

    ' State / configuration
    <ObservableProperty> Private _Printers As ObservableCollection(Of Printer)
    <ObservableProperty> Private _Printer As Printer
    <ObservableProperty> Private _Configuration As ProcessorConfiguration
    <ObservableProperty> Private _UIConfiguration As UIConfiguration

    <ObservableProperty> Private _GCode As String = Nothing
    <ObservableProperty> Private _GCodeGeometry As GCodeGeometry
    <ObservableProperty> Private _GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()
    <ObservableProperty> Private _GeneratedGCode As List(Of GCode)

    Private ReadOnly _document As New Document()

    Public ReadOnly Property Document As Document
        Get
            Return _document
        End Get
    End Property

    Public ReadOnly Property DrawableCollection As ObservableCollection(Of IDrawable)
        Get
            Return _document.Drawables
        End Get
    End Property

    Public ReadOnly Property ImportedGroups As ObservableCollection(Of DrawableGroup)
        Get
            Return _document.ImportRoots
        End Get
    End Property

    <ObservableProperty>
    Private _FlatSidebarItems As New ObservableCollection(Of SidebarItemVM)
    Private _sidebarRebuildQueued As Boolean

    Private Sub RebuildFlatSidebarItems()
        Dim d = Application.Current?.Dispatcher
        If d Is Nothing OrElse Not d.CheckAccess() Then
            RebuildFlatSidebarItemsNow()
            Return
        End If
        If _sidebarRebuildQueued Then Return
        _sidebarRebuildQueued = True
        d.BeginInvoke(New Action(AddressOf RebuildFlatSidebarItemsNow), Threading.DispatcherPriority.Background)
    End Sub

    Private Sub RebuildFlatSidebarItemsNow()
        _sidebarRebuildQueued = False
        FlatSidebarItems.Clear()

        For i = ImportedGroups.Count - 1 To 0 Step -1
            Dim grp = ImportedGroups(i)
            If grp Is Nothing Then Continue For

            Dim children = grp.DisplayChildren
            If children Is Nothing Then Continue For

            ' Hide Drawing Group if empty
            If _document.IsDrawingGroup(grp) AndAlso children.Count = 0 Then Continue For

            For j = children.Count - 1 To 0 Step -1
                Dim ch = children(j)
                If ch Is Nothing Then Continue For
                If TypeOf ch Is DrawableGroup Then Continue For
                FlatSidebarItems.Add(New SidebarItemVM(grp, ch))
            Next
        Next
    End Sub

    Friend Property DrawingGroup As DrawableGroup
        Get
            Return _document.DrawingGroup
        End Get
        Set(value As DrawableGroup)
            _document.DrawingGroup = value
        End Set
    End Property

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
            Return If(PolyCanvas.SelectedItems, Enumerable.Empty(Of IDrawable)())?.FirstOrDefault()
        End Get
    End Property

    Public ReadOnly Property SelectedWrapper As ContentControl
        Get
            Return PolyCanvas.CurrentSelected
        End Get
    End Property

    Public ReadOnly Property SelectedDrawables As IEnumerable(Of IDrawable)
        Get
            Return If(PolyCanvas.SelectedItems, Enumerable.Empty(Of IDrawable)())
        End Get
    End Property

    Public ReadOnly Property HasSelection As Boolean
        Get
            Return PolyCut.RichCanvas.PolyCanvas.SelectedItems IsNot Nothing AndAlso PolyCanvas.SelectedItems.Where(Function(f) TypeOf (f) IsNot DrawableGroup).Any()
        End Get
    End Property

    Public ReadOnly Property HasDrawables As Boolean
        Get
            Return DrawableCollection.Any(Function(d) TypeOf (d) IsNot DrawableGroup)
        End Get
    End Property

    Public ReadOnly Property HasMultipleSelected As Boolean
        Get
            Return SelectedDrawables.Where(Function(f) TypeOf (f) IsNot DrawableGroup).Count() > 1
        End Get
    End Property

    Public ReadOnly Property PolyCutDocumentName As String
        Get
            If ImportedGroups.Count <> 0 AndAlso Not _document.IsDrawingGroup(ImportedGroups.First) Then
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

    <RelayCommand> Public Async Function MainViewClosing() As Task
        SettingsHandler.WriteConfiguration(Configuration)
        Await SettingsHandler.WriteUIConfiguration(UIConfiguration)
    End Function

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
        PolyCanvas.ActiveInstance?.CommitActiveText()
        _undoRedoService.Undo()
    End Sub

    <RelayCommand> Public Sub Redo()
        PolyCanvas.ActiveInstance?.CommitActiveText()
        _undoRedoService.Redo()
    End Sub


    ' ----- Sidebar reordering -----

    <RelayCommand> Public Sub MoveSelectedItemUp(item As IDrawable)
        If Keyboard.IsKeyDown(Key.LeftCtrl) Then
            ReorderSelectedItem(item, Integer.MaxValue)
        Else
            ReorderSelectedItem(item, 1)
        End If
    End Sub

    <RelayCommand> Public Sub MoveSelectedItemDown(item As IDrawable)
        If Keyboard.IsKeyDown(Key.LeftCtrl) Then
            ReorderSelectedItem(item, Integer.MinValue)
        Else
            ReorderSelectedItem(item, -1)
        End If
    End Sub

    <RelayCommand> Public Sub MoveSelectedItemsCanvasUp()
        ReorderSelectedItems(SelectedDrawables, 1)
    End Sub

    <RelayCommand> Public Sub MoveSelectedItemsCanvasDown()
        ReorderSelectedItems(SelectedDrawables, -1)
    End Sub

    <RelayCommand> Public Sub MoveSelectedItemsCanvasToBottom()
        ReorderSelectedItems(SelectedDrawables, Integer.MinValue)
    End Sub

    <RelayCommand> Public Sub MoveSelectedItemsCanvasToTop()
        ReorderSelectedItems(SelectedDrawables, Integer.MaxValue)
    End Sub

    Private Sub ReorderSelectedItem(item As IDrawable, direction As Integer)
        If item Is Nothing OrElse TypeOf item Is DrawableGroup Then Return
        ReorderSelectedItems({item}, direction)
    End Sub

    Private Sub ReorderSelectedItems(items As IEnumerable(Of IDrawable), direction As Integer)
        Dim actions = ReorderPlanner.Move(_document, items, direction, AddressOf GetParentGroup)
        If actions.Count > 0 Then
            _undoRedoService.Push(New CompositeAction(actions))
        End If
    End Sub

    Public Sub ReorderSelectedItems(sidebarOrder As IEnumerable(Of IDrawable))
        Using ModelBatch.Begin()
            Dim actions As New List(Of IUndoableAction)
            For Each siblings In sidebarOrder.GroupBy(AddressOf GetParentGroup)
                If siblings.Key Is Nothing Then Continue For
                Dim target = siblings.Key.GroupChildren.Count - 1
                For Each item In siblings
                    actions.AddRange(ReorderPlanner.Move(_document, {item}, target - siblings.Key.GroupChildren.IndexOf(item), AddressOf GetParentGroup))
                    target -= 1
                Next
            Next
            If actions.Count > 0 Then _undoRedoService.Push(New CompositeAction(actions))
        End Using
        RebuildFlatSidebarItems()
    End Sub

    ' Constructor & initialization
    Public Sub New(snackbarService As SnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService, svgImportService As ISvgImportService, undoRedoService As UndoRedoService, projectService As ProjectSerializationService)
        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService
        _svgImportService = svgImportService
        _undoRedoService = undoRedoService
        _projectService = projectService


        AddHandler PolyCanvas.CurrentSelectedChanged, AddressOf OnCurrentSelectedChanged
        EventAggregator.Subscribe(Of TransformCompletedMessage)(AddressOf OnTransformCompleted)
        AddHandler _document.Changed, AddressOf OnDocumentChanged
        Initialise()
        WireFlatSidebarInvalidation()
    End Sub

    Private Sub AnyDisplayChildrenChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        RebuildFlatSidebarItems()
    End Sub

    Private Sub WireFlatSidebarInvalidation()
        AddHandler ImportedGroups.CollectionChanged, AddressOf ImportedGroups_CollectionChanged

        ' existing
        For Each g In ImportedGroups
            If g Is Nothing Then Continue For
            AddHandler g.DisplayChildren.CollectionChanged, AddressOf AnyDisplayChildrenChanged
        Next

        RebuildFlatSidebarItems()

    End Sub

    Private Sub ImportedGroups_CollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If e.NewItems IsNot Nothing Then
            For Each g As DrawableGroup In e.NewItems
                AddHandler g.DisplayChildren.CollectionChanged, AddressOf AnyDisplayChildrenChanged
            Next
        End If
        If e.OldItems IsNot Nothing Then
            For Each g As DrawableGroup In e.OldItems
                RemoveHandler g.DisplayChildren.CollectionChanged, AddressOf AnyDisplayChildrenChanged
            Next
        End If

        RebuildFlatSidebarItems()
    End Sub

    Private Sub OnUIConfigurationChanged(sender As Object, e As System.ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(UIConfiguration.UseRasterisedRendering) Then ApplyRasterisedRenderingSetting()
    End Sub

    Private Sub ApplyRasterisedRenderingSetting()
        PerformantRasteriser.Enabled = UIConfiguration.UseRasterisedRendering
    End Sub

    Private Sub Initialise()
        Printers = SettingsHandler.GetPrinters

        Printer = Printers.First
        Configuration = (SettingsHandler.GetConfigurations).First
        UIConfiguration = SettingsHandler.GetUIConfiguration()
        ApplyRasterisedRenderingSetting()
        AddHandler UIConfiguration.PropertyChanged, AddressOf OnUIConfigurationChanged

        If DrawingGroup Is Nothing Then
            DrawingGroup = New DrawableGroup(Document.DrawingGroupName)
            DrawableCollection.Add(DrawingGroup)
            ImportedGroups.Add(DrawingGroup)
        End If

        For Each grp In DrawableCollection.OfType(Of DrawableGroup)().ToList()
            If Not _document.IsDrawingGroup(grp) Then
                DrawableCollection.Remove(grp)
                If Not ImportedGroups.Contains(grp) Then ImportedGroups.Add(grp)
            End If
        Next
    End Sub

    ' -----------------
    ' Clipboard operations
    ' -----------------
    <RelayCommand>
    Private Sub CopySelection()
        Application.GetService(Of ClipboardService)().CopySelectionToClipboard()
    End Sub

    <RelayCommand>
    Private Sub CutSelection()
        Application.GetService(Of ClipboardService)().CutSelectionToClipboard()
    End Sub
    <RelayCommand>
    Private Sub PasteClipboard()
        Application.GetService(Of ClipboardService)().PasteFromClipboard()
    End Sub

    <RelayCommand>
    Private Sub SelectAll()
        PolyCanvas.ApplySelection(DrawableCollection.Where(Function(f) TypeOf f IsNot DrawableGroup))
    End Sub


    ' -----------------
    ' Selection / Transform event handlers
    ' -----------------
    Private Sub OnCurrentSelectedChanged(sender As Object, e As EventArgs)
        OnPropertyChanged(NameOf(SelectedWrapper))
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
        OnPropertyChanged(NameOf(HasSelection))
        OnPropertyChanged(NameOf(HasDrawables))
    End Sub

    Private Sub OnTransformCompleted(msg As TransformCompletedMessage)
        If _suspendTransformMessageHandling Then
            Debug.WriteLine("[MainViewModel] TransformCompleted ignored because transform recording is suspended")
            Return
        End If


        If msg?.Items Is Nothing OrElse msg.Items.Count = 0 Then Return

        Dim items = msg.Items.
            Where(Function(it) it.Drawable IsNot Nothing).
            Select(Function(it) (drawable:=it.Drawable, before:=TryCast(it.Before, TransformState), after:=TryCast(it.After, TransformState))).
            Where(Function(t) t.before IsNot Nothing AndAlso t.after IsNot Nothing AndAlso Not IsTransformUnchanged(t.before, t.after)).
            Select(Function(t) (t.drawable, t.before, t.after)).
            ToList()

        If items.Count > 0 Then
            _undoRedoService.Push(New TransformAction(items))
        End If
    End Sub


    Private Function IsTransformUnchanged(before As TransformState, after As TransformState) As Boolean
        If before Is Nothing OrElse after Is Nothing Then Return True
        If before Is Nothing OrElse after Is Nothing Then Return True

        Return before.Matches(after)
    End Function

    ' -----------------
    ' Printer management
    ' -----------------
    <RelayCommand>
    Public Sub AddPrinter(newPrinter As Printer)
        Printers.Add(newPrinter)
        Printer = newPrinter
        SettingsHandler.WritePrinter(Printer)
        _snackbarService.GenerateSuccess("Added Preset".LT(), Printer.Name)
    End Sub

    <RelayCommand>
    Public Sub SavePrinter(newName As String)
        Dim nameToSave = If(String.IsNullOrWhiteSpace(newName), Printer?.Name, newName)

        If nameToSave Is Nothing Then
            _snackbarService.GenerateError("Error".LT(), "Printer name is empty".LT(), 3)
            Return
        End If

        Dim existingPrinter = Printers.FirstOrDefault(Function(p) p.Name = nameToSave)

        If existingPrinter IsNot Nothing Then
            Dim result = MessageBox.Show("A printer with the name '{0}' already exists. Do you want to overwrite it?".LTF(nameToSave), "Overwrite Printer?".LT(), MessageBoxButton.YesNo, MessageBoxImage.Exclamation)
            If result = MessageBoxResult.No Then
                _snackbarService.GenerateError("Error".LT(), "Printer not saved. Name already exists.".LT(), 3)
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
        _snackbarService.GenerateSuccess("Saved Preset".LT(), Printer.Name)
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
            .Filter = "Image Files (*.svg;*.png;*.bmp;*.jpg;*.jpeg)|*.svg;*.png;*.bmp;*.jpg;*.jpeg|All Files (*.*)|*.*",
            .Multiselect = True
        }
        If fs.ShowDialog Then
            Dim actions As New List(Of IUndoableAction)()

            Dim vtracer = Application.GetService(Of VTracerService)

            For Each fl In fs.FileNames

                If IO.Path.GetExtension(fl).ToLower() <> ".svg" Then
                    Dim svgPath = vtracer.ConvertRasterToSVG(fl)
                    If svgPath Is Nothing Then
                        Continue For
                    End If
                    fl = svgPath
                End If


                Dim action As New ImportSVGAction(_document, _svgImportService, fl)
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
                Dim action As New ImportSVGAction(_document, _svgImportService, file)
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
        OnPropertyChanged(NameOf(FlatSidebarItems))
    End Sub

    ' -----------------
    ' Drawable collection management (IDrawableManager)
    ' -----------------

    Public Sub AddDrawableElement(element As FrameworkElement)
        Dim action As New AddDrawableAction(_document, element)
        If action.Execute() Then
            _undoRedoService.Push(action)
        End If
    End Sub

    Private Sub OnDocumentChanged(sender As Object, e As EventArgs)
        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(DrawableCollection))
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
        OnPropertyChanged(NameOf(HasDrawables))
        OnPropertyChanged(NameOf(FlatSidebarItems))
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

        Dim removed As New List(Of IDrawable)

        Dim batch = ModelBatch.Begin()
        _document.BeginRemovalDeferral()
        Try
            For Each drawable In drawables
                Dim action As New RemoveDrawableAction(_document, drawable)
                If action.Execute() Then
                    actions.Add(action)
                    removed.Add(drawable)
                End If
            Next
        Finally
            _document.EndRemovalDeferral()
            batch.Dispose()
        End Try

        If removed.Count > 0 Then
            _document.RemoveDrawablesFromCollection(removed)
            _document.NotifyChanged()
        End If

        For Each grp In parentGroups.Where(Function(g) Not _document.IsDrawingGroup(g) AndAlso Not g.GroupChildren.Any())
            Dim removeGroupAction As New RemoveGroupAction(_document, grp)
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

        If _document.IsDrawingGroup(group) Then
            Dim children = group.GroupChildren.ToList()
            If children.Count = 0 Then
                _snackbarService.GenerateError("Error".LT(), "Drawing Group is already empty".LT(), 3)
                Return
            End If
            PerformRemoveDrawables(children)
        Else
            Dim action As New RemoveGroupAction(_document, group)
            If action.Execute() Then
                _undoRedoService.Push(action)
            End If
        End If
    End Sub

    Friend Function GetParentGroup(drawable As IDrawable) As DrawableGroup
        Return TryCast(_document.ParentGroupOf(drawable), DrawableGroup)
    End Function

    Private Function ValidateShapesForBoolean(shapes As List(Of IDrawable)) As (isValid As Boolean, errorMessage As String)
        For Each drawable In shapes
            Dim element = drawable.DrawableElement

            If TypeOf element Is Line Then
                Return (False, "Cannot perform boolean operations on open paths (Lines). All shapes must be closed.".LT())
            End If

            If TypeOf element Is System.Windows.Shapes.Path Then
                Dim path = CType(element, System.Windows.Shapes.Path)
                Dim pathGeo = TryCast(path.Data, PathGeometry)
                If pathGeo IsNot Nothing Then
                    For Each figure In pathGeo.Figures
                        If Not figure.IsClosed Then
                            Return (False, "Cannot perform boolean operations on open paths. All shapes must be closed.".LT())
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

    Private _generatingGCode As Boolean = False

    <RelayCommand>
    Private Async Function GenerateGCode() As Task
        Try
            _generatingGCode = True
            GenerateGCodeCommand.NotifyCanExecuteChanged()

            Configuration.WorkAreaHeight = Printer.BedHeight
            Configuration.WorkAreaWidth = Printer.BedWidth
            Configuration.ToolOffsetX = Printer.ToolOffsetX
            Configuration.ToolOffsetY = Printer.ToolOffsetY
            Configuration.ToolDownGCode = Printer.ToolDownGCode
            Configuration.ToolUpGCode = Printer.ToolUpGCode

            Configuration.SoftwareVersion = SettingsHandler.Version

            Dim generator As IGenerator = New PolyCutGenerator(Configuration, Printer, DrawableCollection.
                                                               Where(Function(d) Not d.IsHidden).
                                                               ToList(), PolyCanvas.ActiveInstance)

            Dim retcode = Await generator.GenerateGcodeAsync

            If retcode.StatusCode = 1 Then
                _snackbarService.GenerateError("Error".LT(), retcode.Message, 5)
                Return
            End If

            GeneratedGCode = generator.GetGCode
            Dim compiledGCodeString = BuildStringFromGCodes(GeneratedGCode)

            GCode = compiledGCodeString
            GCodeGeometry = New GCodeGeometry(GeneratedGCode)
            _navigationService.Navigate(GetType(PreviewPage))
        Finally

            _generatingGCode = False
            GenerateGCodeCommand.NotifyCanExecuteChanged()
        End Try

    End Function

    Private Function CanGenerateGCode() As Boolean
        Return Not _generatingGCode
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

    <RelayCommand>
    Sub ExportSVG()
        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)
        }

        For Each drawableL In DrawableCollection
            If TypeOf (drawableL) Is DrawableGroup Then Continue For
            Debug.WriteLine(drawableL.Name)
            Dim finalElement = drawableL?.GetTransformedSVGElement
            outDoc.Children.Add(finalElement)
        Next

        Dim svgText = SVGImportService.SVGDocumentToString(outDoc)
        Dim saveDialog As New Microsoft.Win32.SaveFileDialog With {
            .Filter = "SVG File (*.svg)|*.svg",
            .DefaultExt = ".svg",
            .FileName = "PolyCut_Export.svg"
        }
        If saveDialog.ShowDialog() = True Then
            IO.File.WriteAllText(saveDialog.FileName, svgText)
            _snackbarService.GenerateSuccess("SVG Exported".LT(), Path.GetFileName(saveDialog.FileName))
        End If
    End Sub


    ' -----------------
    ' Boolean / Geometry operations
    ' -----------------
    Private Sub BooleanOperation(combineMode As GeometryCombineMode)
        Dim selectedItems = SelectedDrawables.ToList()
        If selectedItems.Count < 2 Then
            _snackbarService.GenerateError("Error".LT(), "Select at least 2 shapes to combine".LT(), 3)
            Return
        End If

        Dim validation = ValidateShapesForBoolean(selectedItems)
        If Not validation.isValid Then
            _snackbarService.GenerateError("Error".LT(), validation.errorMessage, 4)
            Return
        End If

        _suspendTransformMessageHandling = True
        Dim action As New BooleanOperationAction(_document, selectedItems, combineMode)
        If action.Execute() Then
            _undoRedoService.Push(action)
        Else
            _snackbarService.GenerateError("Error".LT(), GetBooleanErrorMessage(combineMode), 4)
        End If
        _suspendTransformMessageHandling = False
    End Sub

    Private Function GetBooleanErrorMessage(mode As GeometryCombineMode) As String
        Select Case mode
            Case GeometryCombineMode.Union
                Return "Union operation resulted in an empty geometry. This should not happen.".LT()
            Case GeometryCombineMode.Intersect
                Return "No intersection found. The selected shapes do not overlap.".LT()
            Case GeometryCombineMode.Exclude
                Return "Subtraction resulted in an empty geometry. The shapes may not overlap or the result is fully subtracted.".LT()
            Case GeometryCombineMode.Xor
                Return "XOR operation resulted in an empty geometry. The shapes may be identical.".LT()
            Case Else
                Return "Boolean operation failed.".LT()
        End Select
    End Function


    ' -----------------
    ' Grouping / Ungrouping
    ' -----------------

    <RelayCommand>
    Private Sub GroupSelected()
        Dim selectedItems = SelectedDrawables.ToList()
        If selectedItems.Count < 2 Then
            _snackbarService.GenerateError("Error".LT(), "Select at least 2 shapes to group".LT(), 3)
            Return
        End If

        ExecuteGroupingAction(New GroupAction(_document, selectedItems))
    End Sub

    <RelayCommand>
    Private Sub UngroupSelected()
        Dim selectedItems = SelectedDrawables.ToList()
        If Not selectedItems.OfType(Of NestedDrawableGroup)().Any(Function(g) DrawableCollection.Contains(g)) Then
            _snackbarService.GenerateError("Error".LT(), "No groups selected to ungroup".LT(), 3)
            Return
        End If

        ExecuteGroupingAction(New UngroupAction(_document, selectedItems))
    End Sub

    Private Sub ExecuteGroupingAction(action As IUndoableAction)
        _suspendTransformMessageHandling = True
        Try
            If action.Execute() Then _undoRedoService.Push(action)
        Finally
            _suspendTransformMessageHandling = False
        End Try
    End Sub

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
            If _projectService.SaveProject(CurrentProjectPath, DrawableCollection, ImportedGroups, Configuration) Then
                _snackbarService.GenerateSuccess("Project Saved".LT(), IO.Path.GetFileName(CurrentProjectPath))
            Else
                _snackbarService.GenerateError("Error".LT(), "Failed to save project".LT(), 3)
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
            If _projectService.SaveProject(CurrentProjectPath, DrawableCollection, ImportedGroups, Configuration) Then
                _snackbarService.GenerateSuccess("Project Saved".LT(), IO.Path.GetFileName(CurrentProjectPath))
            Else
                _snackbarService.GenerateError("Error".LT(), "Failed to save project".LT(), 3)
            End If
        End If
    End Sub

    <RelayCommand>
    Private Async Sub LoadProject()
        ' Warn if unsaved changes
        If DrawableCollection.Count > 1 Then

            Dim nx As New Wpf.Ui.Controls.MessageBox With {
                .Title = "Load Project".LT(),
                .Content = "Loading a project will clear the current workspace. Continue?".LT(),
                .IsPrimaryButtonEnabled = True,
                .PrimaryButtonText = "Yes".LT(),
                .CloseButtonText = "No".LT(),
                .Owner = Application.Current.MainWindow,
                .WindowStartupLocation = WindowStartupLocation.CenterOwner}


            Dim res = Await nx.ShowDialogAsync()

            If res <> Wpf.Ui.Controls.MessageBoxResult.Primary Then Return
        End If

        Dim openDialog As New Microsoft.Win32.OpenFileDialog With {
            .Filter = "PolyCut Project (*.polycut)|*.polycut|All Files (*.*)|*.*",
            .CheckFileExists = True
        }

        If openDialog.ShowDialog() = True Then
            Dim action As New LoadProjectAction(_document, _projectService, openDialog.FileName)
            If action.Execute() Then
                If action.LoadedConfiguration IsNot Nothing Then Configuration = action.LoadedConfiguration
                _undoRedoService.Clear()
                CurrentProjectPath = openDialog.FileName
                _snackbarService.GenerateSuccess("Project Loaded".LT(), IO.Path.GetFileName(CurrentProjectPath))
                _document.NotifyChanged()
            Else
                _snackbarService.GenerateError("Error".LT(), "Failed to load project".LT(), 3)
            End If
        End If
    End Sub

    <RelayCommand>
    Private Async Sub NewProject()
        If DrawableCollection.Count > 1 Then
            Dim nx As New Wpf.Ui.Controls.MessageBox With {
                .Title = "New Project".LT(),
                .Content = "Creating a new project will clear the current workspace. Continue?".LT(),
                .IsPrimaryButtonEnabled = True,
                .PrimaryButtonText = "Yes".LT(),
                .CloseButtonText = "No".LT(),
                .Owner = Application.Current.MainWindow,
                .WindowStartupLocation = WindowStartupLocation.CenterOwner}

            Dim res = Await nx.ShowDialogAsync()
            If res <> Wpf.Ui.Controls.MessageBoxResult.Primary Then Return

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
            _document.RemoveDrawableFromCollection(drawable)
        Next

        _suspendTransformMessageHandling = False
        CurrentProjectPath = Nothing

        ' Reinitialize drawing group
        DrawingGroup = New DrawableGroup(Document.DrawingGroupName)
        DrawableCollection.Add(DrawingGroup)
        ImportedGroups.Add(DrawingGroup)

        _document.NotifyChanged()
        _snackbarService.GenerateSuccess("New Project".LT(), "Workspace cleared".LT())
    End Sub


    Public Property WorkingAreaVisibility As Boolean
        Get
            Return UIConfiguration.ShowWorkArea
        End Get
        Set(value As Boolean)
            UIConfiguration.ShowWorkArea = value
            OnPropertyChanged(NameOf(WorkingAreaVisibility))
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
            OnPropertyChanged(NameOf(IsGridVisible))
        End Set
    End Property


End Class

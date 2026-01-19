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

        If msg Is Nothing OrElse msg.Items Is Nothing OrElse msg.Items.Count = 0 Then Return

        Dim items As New List(Of (IDrawable, TransformAction.Snapshot, TransformAction.Snapshot))()

        For Each it In msg.Items
            Dim drawable = it.Drawable
            If drawable Is Nothing Then Continue For

            Dim beforeSnap = TryCast(it.Before, TransformAction.Snapshot)
            Dim afterSnap = TryCast(it.After, TransformAction.Snapshot)

            If beforeSnap IsNot Nothing AndAlso afterSnap IsNot Nothing Then
                Dim unchanged As Boolean = Math.Abs(beforeSnap.Left - afterSnap.Left) < 0.01 AndAlso
                                      Math.Abs(beforeSnap.Top - afterSnap.Top) < 0.01 AndAlso
                                      Math.Abs(beforeSnap.Width - afterSnap.Width) < 0.01 AndAlso
                                      Math.Abs(beforeSnap.Height - afterSnap.Height) < 0.01 AndAlso
                                      ((beforeSnap.RenderTransform Is Nothing AndAlso afterSnap.RenderTransform Is Nothing) OrElse
                                       (beforeSnap.RenderTransform IsNot Nothing AndAlso afterSnap.RenderTransform IsNot Nothing AndAlso beforeSnap.RenderTransform.Value = afterSnap.RenderTransform.Value))

                If Not unchanged Then
                    items.Add((drawable, beforeSnap, afterSnap))
                End If
            End If
        Next

        If items.Count = 0 Then Return

        _undoRedoService.Push(New TransformAction(items))
    End Sub

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
        If drawableGroup Is Nothing Then Return

        If drawableGroup Is DrawingGroup OrElse String.Equals(drawableGroup.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then
            Return
        End If

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

        Dim actions As New List(Of IUndoableAction)()

        _suspendTransformMessageHandling = True
        For Each drawable In selectedItems
            If drawable IsNot Nothing Then
                Dim action As New RemoveDrawableAction(Me, drawable)
                If action.Execute() Then
                    actions.Add(action)
                End If
            End If
        Next
        _suspendTransformMessageHandling = False

        If actions.Count > 0 Then
            _undoRedoService.Push(New CompositeAction(actions))
        End If

        PolyCanvas.ClearSelection()
    End Sub

    <RelayCommand>
    Private Sub RemoveGroup(group As DrawableGroup)
        If group Is Nothing Then Return
        If group Is DrawingGroup OrElse String.Equals(group.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase) Then
            Dim children = group.GroupChildren.ToList()
            If children.Count = 0 Then
                _snackbarService.GenerateError("Error", "Drawing Group is already empty", 3)
                Return
            End If

            Dim actions As New List(Of IUndoableAction)()

            _suspendTransformMessageHandling = True
            For Each child In children
                Dim action As New RemoveDrawableAction(Me, child)
                If action.Execute() Then
                    actions.Add(action)
                End If
            Next
            _suspendTransformMessageHandling = False

            If actions.Count > 0 Then
                _undoRedoService.Push(New CompositeAction(actions))
            End If
        Else
            Dim action As New RemoveGroupAction(Me, group)
            If action.Execute() Then
                _undoRedoService.Push(action)
            End If
        End If
    End Sub

    Friend Function GetTopLevelGroup(g As DrawableGroup) As DrawableGroup
        Dim cur As DrawableGroup = g
        While cur IsNot Nothing AndAlso cur.ParentGroup IsNot Nothing
            cur = TryCast(cur.ParentGroup, DrawableGroup)
        End While
        Return cur
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

        For Each drawable In selectedItems
            Dim element = drawable.DrawableElement

            If TypeOf element Is Line Then
                _snackbarService.GenerateError("Error", "Cannot perform boolean operations on open paths (Lines). All shapes must be closed.", 4)
                Return
            End If

            If TypeOf element Is System.Windows.Shapes.Path Then
                Dim path = CType(element, System.Windows.Shapes.Path)
                Dim pathGeo = TryCast(path.Data, PathGeometry)
                If pathGeo IsNot Nothing Then
                    For Each figure In pathGeo.Figures
                        If Not figure.IsClosed Then
                            _snackbarService.GenerateError("Error", "Cannot perform boolean operations on open paths. All shapes must be closed.", 4)
                            Return
                        End If
                    Next
                End If
            End If
        Next

        Dim geometries As New List(Of Geometry)
        For Each drawable In selectedItems
            Dim geometry = GetTransformedGeometry(drawable)
            If geometry IsNot Nothing Then
                geometries.Add(geometry)
            End If
        Next

        If geometries.Count < 2 Then
            _snackbarService.GenerateError("Error", "Could not convert shapes to geometries", 3)
            Return
        End If

        Dim result As Geometry = geometries(0)
        For i = 1 To geometries.Count - 1
            result = New CombinedGeometry(combineMode, result, geometries(i))
        Next

        Dim pathGeometry = result.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute)
        If pathGeometry.Figures.Count = 0 OrElse pathGeometry.Bounds.IsEmpty Then

            Dim errorMessage = ""

            Select Case combineMode
                Case GeometryCombineMode.Union
                    errorMessage = "Union operation resulted in an empty geometry. This should not happen."
                Case GeometryCombineMode.Intersect
                    errorMessage = "No intersection found. The selected shapes do not overlap."
                Case GeometryCombineMode.Exclude
                    errorMessage = "Subtraction resulted in an empty geometry. The shapes may not overlap or the result is fully subtracted."
                Case GeometryCombineMode.Xor
                    errorMessage = "XOR operation resulted in an empty geometry. The shapes may be identical."
            End Select

            _snackbarService.GenerateError("Error", errorMessage, 4)
            Return
        End If

        Dim bounds = pathGeometry.Bounds

        Dim localGeometry As New PathGeometry()
        For Each figure In pathGeometry.Figures
            Dim newFigure As New PathFigure() With {
                .StartPoint = New Point(figure.StartPoint.X - bounds.Left, figure.StartPoint.Y - bounds.Top),
                .IsClosed = figure.IsClosed,
                .IsFilled = figure.IsFilled
            }

            For Each segment In figure.Segments
                If TypeOf segment Is LineSegment Then
                    Dim line = CType(segment, LineSegment)
                    newFigure.Segments.Add(New LineSegment(
                        New Point(line.Point.X - bounds.Left, line.Point.Y - bounds.Top), line.IsStroked))
                ElseIf TypeOf segment Is PolyLineSegment Then
                    Dim polyLine = CType(segment, PolyLineSegment)
                    Dim newPoints As New PointCollection()
                    For Each pt In polyLine.Points
                        newPoints.Add(New Point(pt.X - bounds.Left, pt.Y - bounds.Top))
                    Next
                    newFigure.Segments.Add(New PolyLineSegment(newPoints, polyLine.IsStroked))
                ElseIf TypeOf segment Is BezierSegment Then
                    Dim bezier = CType(segment, BezierSegment)
                    newFigure.Segments.Add(New BezierSegment(
                        New Point(bezier.Point1.X - bounds.Left, bezier.Point1.Y - bounds.Top),
                        New Point(bezier.Point2.X - bounds.Left, bezier.Point2.Y - bounds.Top),
                        New Point(bezier.Point3.X - bounds.Left, bezier.Point3.Y - bounds.Top),
                        bezier.IsStroked))
                Else
                    newFigure.Segments.Add(segment)
                End If
            Next

            localGeometry.Figures.Add(newFigure)
        Next

        Dim localBounds = localGeometry.Bounds

        Dim newPath As New System.Windows.Shapes.Path With {
            .Data = localGeometry,
            .Stroke = Brushes.Black,
            .StrokeThickness = 0.5,
            .Fill = Brushes.Transparent,
            .Stretch = Stretch.None,
            .Width = localBounds.Width,
            .Height = localBounds.Height
        }

        Canvas.SetLeft(newPath, bounds.Left)
        Canvas.SetTop(newPath, bounds.Top)

        Dim firstWrapper = TryCast(selectedItems(0).DrawableElement?.Parent, ContentControl)
        If firstWrapper IsNot Nothing Then
            Dim canvas = TryCast(VisualTreeHelper.GetParent(firstWrapper), Canvas)
            If canvas IsNot Nothing Then
                Dim actions As New List(Of IUndoableAction)()

                _suspendTransformMessageHandling = True

                For Each drawable In selectedItems
                    Dim removeAction As New RemoveDrawableAction(Me, drawable)
                    If removeAction.Execute() Then
                        actions.Add(removeAction)
                    End If
                Next

                Dim addAction As New AddDrawableAction(Me, newPath)
                If addAction.Execute() Then
                    actions.Add(addAction)
                End If

                _suspendTransformMessageHandling = False

                If actions.Count > 0 Then
                    _undoRedoService.Push(New CompositeAction(actions))
                End If
                Dim operationName = ""
                Select Case combineMode
                    Case GeometryCombineMode.Union
                        operationName = "Union"
                    Case GeometryCombineMode.Intersect
                        operationName = "Intersect"
                    Case GeometryCombineMode.Exclude
                        operationName = "Subtract"
                    Case GeometryCombineMode.Xor
                        operationName = "XOR"
                End Select

            End If
        End If
    End Sub

    Private Function GetTransformedGeometry(drawable As IDrawable) As Geometry
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim geometry As Geometry = Nothing

        If TypeOf element Is Rectangle Then
            Dim rect = CType(element, Rectangle)
            geometry = New RectangleGeometry(New Rect(0, 0, rect.ActualWidth, rect.ActualHeight))

        ElseIf TypeOf element Is Ellipse Then
            Dim ellipse = CType(element, Ellipse)
            Dim radiusX = ellipse.ActualWidth / 2
            Dim radiusY = ellipse.ActualHeight / 2
            geometry = New EllipseGeometry(New Point(radiusX, radiusY), radiusX, radiusY)

        ElseIf TypeOf element Is Line Then
            Dim line = CType(element, Line)
            Dim lineGeometry As New LineGeometry(New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            Dim thickness = If(line.StrokeThickness > 0, line.StrokeThickness, 1.0)
            geometry = lineGeometry.GetWidenedPathGeometry(New Pen(Brushes.Black, thickness))

        ElseIf TypeOf element Is System.Windows.Shapes.Path Then
            Dim path = CType(element, System.Windows.Shapes.Path)
            If path.Data IsNot Nothing Then
                geometry = path.Data.Clone()
            End If

        ElseIf TypeOf element Is TextBox Then
            Dim textBox = CType(element, TextBox)
            If Not String.IsNullOrEmpty(textBox.Text) Then
                Dim formattedText As New FormattedText(
                    textBox.Text,
                    Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    New Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                    textBox.FontSize,
                    Brushes.Black,
                    1.0)

                geometry = formattedText.BuildGeometry(New Point(0, 0))
            End If
        End If

        If geometry Is Nothing Then Return Nothing

        Dim elementTransformGroup = TryCast(element.RenderTransform, TransformGroup)
        If elementTransformGroup IsNot Nothing Then
            Dim elementScale = elementTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
            If elementScale IsNot Nothing Then
                Dim scaleTransform = New ScaleTransform(elementScale.ScaleX, elementScale.ScaleY,
                    geometry.Bounds.Width / 2, geometry.Bounds.Height / 2)
                geometry = Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, scaleTransform)
            End If
        End If

        Dim transformGroup As New TransformGroup()

        If Not TypeOf element Is TextBox Then
            If geometry.Bounds.Width > 0 AndAlso geometry.Bounds.Height > 0 Then
                Dim scaleX = wrapper.ActualWidth / geometry.Bounds.Width
                Dim scaleY = wrapper.ActualHeight / geometry.Bounds.Height
                transformGroup.Children.Add(New ScaleTransform(scaleX, scaleY))
            End If
        End If

        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            transformGroup.Children.Add(New RotateTransform(rotateTransform.Angle,
                wrapper.ActualWidth / 2, wrapper.ActualHeight / 2))
        End If

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Not Double.IsNaN(left) AndAlso Not Double.IsNaN(top) Then
            If TypeOf element Is TextBox Then
                transformGroup.Children.Add(New TranslateTransform(left + 3, top + 1))
            Else
                transformGroup.Children.Add(New TranslateTransform(left, top))
            End If
        End If

        Return Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, transformGroup)
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
            Return Application.GetService(Of SVGPageViewModel).CuttingMatIsVisible
        End Get
        Set(value As Boolean)
            Application.GetService(Of SVGPageViewModel).CuttingMatIsVisible = value
        End Set
    End Property

    Public Property WorkingAreaVisibility As Boolean
        Get
            Return Application.GetService(Of SVGPageViewModel).WorkingAreaIsVisible
        End Get
        Set(value As Boolean)
            Application.GetService(Of SVGPageViewModel).WorkingAreaIsVisible = value
        End Set
    End Property


    Public Property IsGridVisible As Boolean
        Get
            Return Application.GetService(Of SVGPageViewModel).GridLineBrush IsNot Brushes.Transparent
        End Get
        Set(value As Boolean)
            If value Then
                Application.GetService(Of SVGPageViewModel).GridLineBrush = New SolidColorBrush(Color.FromArgb(&H80, &HFF, &HFF, &HFF))
            Else
                Application.GetService(Of SVGPageViewModel).GridLineBrush = Brushes.Transparent
            End If
            Application.GetService(Of SVGPageViewModel).NotifyPropertyChangedForGrid()
        End Set
    End Property


End Class
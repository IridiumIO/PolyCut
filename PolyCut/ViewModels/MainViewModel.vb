Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Windows.Media

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports MeasurePerformance.IL.Weaver

Imports PolyCut.Core
Imports PolyCut.Shared
Imports PolyCut.RichCanvas

Imports Svg

Imports WPF.Ui

Public Class MainViewModel
    Inherits ObservableObject

    ' Services
    Private ReadOnly _snackbarService As SnackbarService
    Private ReadOnly _navigationService As INavigationService
    Private ReadOnly _argsService As CommandLineArgsService
    Private ReadOnly _svgImportService As ISvgImportService

    ' State / configuration
    Public Property UsingGCodePlot As Boolean
    Public Property Printers As ObservableCollection(Of Printer)
    Public Property Printer As Printer
    Public Property CuttingMats As ObservableCollection(Of CuttingMat)
    Public Property Configuration As ProcessorConfiguration

    Public Property GCode As String = Nothing
    Public Property GCodeGeometry As GCodeGeometry
    Public Property GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()
    Public Property GeneratedGCode As List(Of GCode)

    ' Drawable model collections
    Public Property DrawableCollection As ObservableCollection(Of IDrawable) = New ObservableCollection(Of IDrawable)
    Public Property ImportedGroups As New ObservableCollection(Of DrawableGroup)

    Private DrawingGroup As DrawableGroup

    ' Commands
    Public Property SavePrinterCommand As ICommand = New RelayCommand(Of String)(AddressOf SavePrinter)
    Public Property ConfigurePrinterCommand As ICommand = New RelayCommand(AddressOf ConfigurePrinter)
    Public Property AddPrinterCommand As ICommand = New RelayCommand(Of Printer)(AddressOf AddPrinter)
    Public Property DeletePrinterCommand As ICommand = New RelayCommand(AddressOf DeletePrinter)
    Public Property BrowseSVGCommand As ICommand = New RelayCommand(AddressOf BrowseSVG)
    Public Property OpenSnackbar_Save As ICommand = New RelayCommand(Of String)(Sub(x) _snackbarService.GenerateSuccess("Saved Preset", x))
    Public Property GenerateGCodeCommand As ICommand = New RelayCommand(AddressOf GenerateGcode)
    Public Property RemoveDrawableCommand As ICommand = New RelayCommand(Of DrawableGroup)(Sub(x) ModifyDrawableFiles(x, removeDrawable:=True))
    Public Property MainViewLoadedCommand As ICommand = New RelayCommand(Sub() If _argsService.Args.Length > 0 Then DragSVGs(_argsService.Args))
    Public Property MainViewClosingCommand As ICommand = New RelayCommand(Sub() SettingsHandler.WriteConfiguration(Configuration))
    Public Property CopyGCodeToClipboardCommand As ICommand = New RelayCommand(Sub() Clipboard.SetText(GCode))
    Public Property UnionShapesCommand As ICommand = New RelayCommand(Sub() BooleanOperation(GeometryCombineMode.Union))
    Public Property IntersectShapesCommand As ICommand = New RelayCommand(Sub() BooleanOperation(GeometryCombineMode.Intersect))
    Public Property SubtractShapesCommand As ICommand = New RelayCommand(Sub() BooleanOperation(GeometryCombineMode.Exclude))
    Public Property XorShapesCommand As ICommand = New RelayCommand(Sub() BooleanOperation(GeometryCombineMode.Xor))

    ' UI meta properties
    Private _PreviewRenderSpeed As Double = 0.48
    Public Property PreviewRenderSpeed As Double
        Get
            Return _PreviewRenderSpeed
        End Get
        Set(value As Double)
            _PreviewRenderSpeed = value
            LogarithmicPreviewSpeed = CInt(30 * (Math.Exp(6 * value) - 1) + 30)
            OnPropertyChanged(NameOf(LogarithmicPreviewSpeed))
            OnPropertyChanged(NameOf(PreviewRenderSpeed))
        End Set
    End Property

    Private _LogarithmicPreviewSpeed As Integer = 500
    Public Property LogarithmicPreviewSpeed As Integer
        Get
            Return _LogarithmicPreviewSpeed
        End Get
        Set(value As Integer)
            _LogarithmicPreviewSpeed = value
        End Set
    End Property

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

    ' Events
    Public Event PrinterConfigOpened()
    Public Event PrinterConfigClosed()

    Public Sub New(snackbarService As SnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService, svgImportService As ISvgImportService)
        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService
        _svgImportService = svgImportService

        AddHandler PolyCanvas.SelectionCountChanged, AddressOf OnCanvasSelectionChanged
        AddHandler PolyCanvas.CurrentSelectedChanged, AddressOf OnCurrentSelectedChanged

        Initialise()
    End Sub

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

        ' Move non-drawing groups out of the flat drawable collection into ImportedGroups
        For Each grp In DrawableCollection.OfType(Of DrawableGroup)().ToList()
            If grp.Name <> "Drawing Group" Then
                DrawableCollection.Remove(grp)
                If Not ImportedGroups.Contains(grp) Then ImportedGroups.Add(grp)
            End If
        Next
    End Sub

    ' ----- Printer management -----
    Public Sub AddPrinter(newPrinter As Printer)
        Printers.Add(newPrinter)
        Printer = newPrinter
        SettingsHandler.WritePrinter(Printer)
        _snackbarService.GenerateSuccess("Added Preset", Printer.Name)
    End Sub

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

    Public Sub ConfigurePrinter()
        Dim printerConfigWindow = Application.GetService(Of PrinterConfig)
        RaiseEvent PrinterConfigOpened()
        Dim result = printerConfigWindow.ShowDialog()
        RaiseEvent PrinterConfigClosed()

        For Each p In Printers
            SettingsHandler.WritePrinter(p)
        Next
    End Sub

    Public Sub DeletePrinter()
        If Printers.Count > 1 Then
            Dim toRemove = Printer
            Printer = Printers((Printers.IndexOf(toRemove) + 1) Mod (Printers.Count))
            Printers.Remove(toRemove)
            SettingsHandler.DeletePrinter(toRemove)
        End If
    End Sub

    ' ----- SVG import / file handling -----
    Private Sub BrowseSVG()
        Dim fs As New Microsoft.Win32.OpenFileDialog With {
            .Filter = "*.svg|*.svg",
            .Multiselect = True
        }
        If fs.ShowDialog Then
            For Each fl In fs.FileNames
                Dim imported = _svgImportService.ParseFromFile(fl)

                For Each d As IDrawable In imported
                    If TypeOf d Is DrawableGroup Then
                        Dim grp = CType(d, DrawableGroup)
                        For Each child In grp.GroupChildren.ToList()
                            EnsureDrawableInitialized(child)
                            If Not DrawableCollection.Contains(child) Then DrawableCollection.Add(child)
                        Next
                    Else
                        EnsureDrawableInitialized(d)
                        If Not DrawableCollection.Contains(d) Then DrawableCollection.Add(d)
                    End If
                Next

                Dim fileGroup As New DrawableGroup(Path.GetFileName(fl))
                For Each d As IDrawable In imported
                    fileGroup.AddChild(d)
                Next

                If Not ImportedGroups.Contains(fileGroup) Then ImportedGroups.Add(fileGroup)
            Next
        End If
    End Sub

    Public Sub DragSVGs(x As String())
        For Each file In x
            Dim finfo As New FileInfo(file)
            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                Dim imported = _svgImportService.ParseFromFile(file)
                Dim fileGroup As New DrawableGroup(finfo.Name)

                For Each d As IDrawable In imported
                    fileGroup.AddChild(d)
                    If Not DrawableCollection.Contains(d) Then DrawableCollection.Add(d)
                    Dim nested = TryCast(d, DrawableGroup)
                    If nested IsNot Nothing Then
                        For Each nd In nested.GroupChildren
                            If Not DrawableCollection.Contains(nd) Then DrawableCollection.Add(nd)
                        Next
                    End If
                Next

                If Not ImportedGroups.Contains(fileGroup) Then ImportedGroups.Add(fileGroup)
                If Not DrawableCollection.Contains(fileGroup) Then DrawableCollection.Add(fileGroup)
            End If
        Next
    End Sub

    Public Sub UpdateSVGFiles()
        For Each grp In ImportedGroups.ToList()
            For Each ch In grp.GroupChildren
                If Not DrawableCollection.Contains(ch) Then
                    EnsureDrawableInitialized(ch)
                    DrawableCollection.Add(ch)
                End If
            Next
        Next
        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(SelectedDrawable))
    End Sub

    Private Sub EnsureDrawableInitialized(drawable As IDrawable)
        If drawable Is Nothing Then Return
        If drawable.DrawableElement IsNot Nothing Then Return

        Try
            Dim setCanvasMeth = drawable.GetType().GetMethod("SetCanvas")
            If setCanvasMeth IsNot Nothing Then
                setCanvasMeth.Invoke(drawable, Nothing)
            End If
        Catch ex As Exception
            Debug.WriteLine($"EnsureDrawableInitialized failed for {drawable?.GetType()?.Name}: {ex.Message}")
        End Try
    End Sub

    ' ----- Drawing group lifecycle & canvas additions -----
    Private Function GenerateDrawableName(drawable As IDrawable) As String
        ' For text objects, use the actual text content as the name
        If TypeOf drawable Is DrawableText Then
            Dim textDrawable = CType(drawable, DrawableText)
            Dim textElement = TryCast(textDrawable.DrawableElement, System.Windows.Controls.TextBox)
            If textElement IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(textElement.Text) Then
                Dim textContent = textElement.Text.Trim()
                Return If(textContent.Length > 15, textContent.Substring(0, 15) & "...", textContent)
            End If
        End If

        ' For other types, find the lowest available number using pattern matching
        Dim baseName As String
        Dim pattern As String

        If TypeOf drawable Is DrawableLine Then
            baseName = "Line"
        ElseIf TypeOf drawable Is DrawableRectangle Then
            baseName = "Rect"
        ElseIf TypeOf drawable Is DrawableEllipse Then
            baseName = "Ellipse"
        ElseIf TypeOf drawable Is DrawableText Then
            baseName = "Text"
        ElseIf TypeOf drawable Is DrawablePath Then
            baseName = "Path"
        Else
            Return "Drawable1"
        End If

        Dim existingNumbers As New HashSet(Of Integer)
        For Each d In DrawableCollection
            If d IsNot Nothing AndAlso Not String.IsNullOrEmpty(d.Name) AndAlso d.Name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase) Then
                Dim numberPart = d.Name.Substring(baseName.Length)
                Dim number As Integer
                If Integer.TryParse(numberPart, number) Then
                    existingNumbers.Add(number)
                End If
            End If
        Next

        Dim nextNumber As Integer = 1
        While existingNumbers.Contains(nextNumber)
            nextNumber += 1
        End While

        Return $"{baseName}{nextNumber}"
    End Function

    Public Sub AddDrawableElement(element As FrameworkElement)
        Dim drawableL As IDrawable
        If TypeOf (element) Is Line Then
            drawableL = New DrawableLine(element)
        ElseIf TypeOf (element) Is Rectangle Then
            drawableL = New DrawableRectangle(element)
        ElseIf TypeOf (element) Is Ellipse Then
            drawableL = New DrawableEllipse(element)
        ElseIf TypeOf (element) Is System.Windows.Controls.TextBox Then
            drawableL = New DrawableText(element)
        ElseIf TypeOf (element) Is System.Windows.Shapes.Path Then
            drawableL = New DrawablePath(element)
        Else
            drawableL = Nothing
        End If

        If drawableL Is Nothing Then Return

        drawableL.Name = GenerateDrawableName(drawableL)

        If DrawingGroup Is Nothing OrElse Not DrawableCollection.Contains(DrawingGroup) Then
            DrawingGroup = New DrawableGroup("Drawing Group")
            DrawableCollection.Add(DrawingGroup)
            ImportedGroups.Add(DrawingGroup)
        End If

        DrawingGroup.AddChild(drawableL)
        If Not DrawableCollection.Contains(drawableL) Then DrawableCollection.Add(drawableL)
    End Sub


    Public Sub RemoveDrawableLeaf(drawable As IDrawable)
        If drawable Is Nothing Then Return

        ' Remove from multi-select tracking
        PolyCanvas.RemoveFromSelection(drawable)

        ' Remove from the flattened drawable collection if present
        If DrawableCollection.Contains(drawable) Then
            DrawableCollection.Remove(drawable)
        End If

        ' Remove the drawable from any imported/top-level groups (recursively)
        For Each grp In ImportedGroups.ToList()
            RemoveDrawableFromGroupRecursive(grp, drawable)
        Next

        ' If any top-level groups became empty, remove them from tracking and flat collection
        For Each grp In ImportedGroups.ToList()
            If Not grp.GroupChildren.Any() Then
                If ImportedGroups.Contains(grp) Then ImportedGroups.Remove(grp)
                If DrawableCollection.Contains(grp) Then DrawableCollection.Remove(grp)
            End If
        Next

        ' Ensure parent pointer cleared
        drawable.ParentGroup = Nothing

        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(DrawableCollection))
        OnPropertyChanged(NameOf(SelectedDrawable))
        OnPropertyChanged(NameOf(SelectedDrawables))
        OnPropertyChanged(NameOf(HasMultipleSelected))
    End Sub


    ' Removes all currently selected drawables

    Public Sub RemoveSelectedDrawables()
        Dim selectedItems = SelectedDrawables.ToList()
        For Each drawable In selectedItems
            RemoveDrawableLeaf(drawable)
        Next
        PolyCanvas.ClearSelection()
    End Sub

    Private Sub RemoveDrawableFromGroupRecursive(group As DrawableGroup, drawable As IDrawable)
        If group Is Nothing Then Return

        ' If group directly contains the drawable, remove it
        If group.GroupChildren.Contains(drawable) Then
            group.RemoveChild(drawable)
            Return
        End If

        ' Otherwise recurse into nested groups
        For Each nested In group.GroupChildren.OfType(Of DrawableGroup)().ToList()
            RemoveDrawableFromGroupRecursive(nested, drawable)
            ' If nested is now empty remove it from its parent group
            If Not nested.GroupChildren.Any() Then
                group.RemoveChild(nested)
            End If
        Next
    End Sub


    Public Sub ModifyDrawableFiles(root As DrawableGroup, Optional removeDrawable As Boolean = False)
        If root Is Nothing Then Return

        If removeDrawable Then
            Dim leaves = root.GetAllLeafChildren()
            For Each ch In leaves
                If DrawableCollection.Contains(ch) Then DrawableCollection.Remove(ch)
                ch.ParentGroup = Nothing
            Next

            For Each grp In DrawableCollection.OfType(Of DrawableGroup)().Where(Function(g) g IsNot DrawingGroup).ToList()
                If grp Is root OrElse leaves.Any(Function(leaf) grp.GroupChildren.Contains(leaf)) Then
                    If DrawableCollection.Contains(grp) Then DrawableCollection.Remove(grp)
                End If
            Next

            DetachGroupFromParent(root)

            ' Clear persistent drawing group reference when the drawing group instance is removed
            If DrawingGroup IsNot Nothing AndAlso (root Is DrawingGroup OrElse String.Equals(root.Name, "Drawing Group", StringComparison.OrdinalIgnoreCase)) Then
                DrawingGroup = Nothing
            End If
        Else
            For Each ch In root.GroupChildren
                EnsureDrawableInitialized(ch)
                If Not DrawableCollection.Contains(ch) Then DrawableCollection.Add(ch)

                If TypeOf ch Is DrawableGroup Then
                    Dim nested = CType(ch, DrawableGroup)
                    For Each leaf In nested.GetAllLeafChildren()
                        If Not DrawableCollection.Contains(leaf) Then DrawableCollection.Add(leaf)
                    Next
                End If
            Next

            If root.ParentGroup Is Nothing Then
                If Not ImportedGroups.Contains(root) Then ImportedGroups.Add(root)
            Else
                Dim top = GetTopLevelGroup(root)
                If top IsNot Nothing AndAlso Not ImportedGroups.Contains(top) Then ImportedGroups.Add(top)
            End If
        End If

        Dim topLevel = GetTopLevelGroup(root)
        If topLevel IsNot Nothing Then topLevel.RebuildDisplayChildren()
        root.RebuildDisplayChildren()

        OnPropertyChanged(NameOf(ImportedGroups))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(DrawableCollection))
        OnPropertyChanged(NameOf(SelectedDrawable))
    End Sub

    Private Sub DetachGroupFromParent(group As DrawableGroup)
        If group.ParentGroup IsNot Nothing Then
            Dim parent = TryCast(group.ParentGroup, DrawableGroup)
            If parent IsNot Nothing Then
                parent.RemoveChild(group)
                If Not parent.GroupChildren.Any() Then
                    If parent.ParentGroup Is Nothing Then
                        If ImportedGroups.Contains(parent) Then ImportedGroups.Remove(parent)
                    Else
                        DetachGroupFromParent(parent)
                    End If
                End If
            End If
        Else
            If ImportedGroups.Contains(group) Then ImportedGroups.Remove(group)
        End If
    End Sub

    Private Function GetTopLevelGroup(g As DrawableGroup) As DrawableGroup
        Dim cur As DrawableGroup = g
        While cur IsNot Nothing AndAlso cur.ParentGroup IsNot Nothing
            cur = TryCast(cur.ParentGroup, DrawableGroup)
        End While
        Return cur
    End Function

    ' ----- G-code generation -----
    Private Async Sub GenerateGcode()
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
    End Sub

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

    Public Sub NotifyPropertyChanged(propName As String)
        OnPropertyChanged(propName)
    End Sub

    ' ----- Boolean Operations -----
    Private Sub BooleanOperation(combineMode As GeometryCombineMode)
        Dim selectedItems = SelectedDrawables.ToList()
        If selectedItems.Count < 2 Then
            _snackbarService.GenerateError("Error", "Select at least 2 shapes to combine", 3)
            Return
        End If

        ' Check for open paths - boolean operations only work on closed shapes
        For Each drawable In selectedItems
            Dim element = drawable.DrawableElement

            ' Lines are always open
            If TypeOf element Is Line Then
                _snackbarService.GenerateError("Error", "Cannot perform boolean operations on open paths (Lines). All shapes must be closed.", 4)
                Return
            End If

            ' Check if Paths have any open figures
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

        ' Convert all selected shapes to transformed geometries (in world space)
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

        ' Combine geometries in world space
        Dim result As Geometry = geometries(0)
        For i = 1 To geometries.Count - 1
            result = New CombinedGeometry(combineMode, result, geometries(i))
        Next

        ' Flatten the combined geometry
        Dim pathGeometry = result.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute)

        ' Check if the result is empty (no figures or empty bounds)
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

        ' Get the bounds of the combined geometry (in world space)
        Dim bounds = pathGeometry.Bounds

        ' Create a new PathGeometry translated to local space
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
                    ' For other segment types, add them as-is (this is a simplified approach)
                    newFigure.Segments.Add(segment)
                End If
            Next

            localGeometry.Figures.Add(newFigure)
        Next

        ' Get local bounds
        Dim localBounds = localGeometry.Bounds

        ' Create a new Path element with the local-space geometry
        Dim newPath As New System.Windows.Shapes.Path With {
            .Data = localGeometry,
            .Stroke = Brushes.Black,
            .StrokeThickness = 0.5,
            .Fill = Brushes.Transparent,
            .Stretch = Stretch.None,
            .Width = localBounds.Width,
            .Height = localBounds.Height
        }

        ' Position the path itself on the canvas (like DrawingManager does)
        Canvas.SetLeft(newPath, bounds.Left)
        Canvas.SetTop(newPath, bounds.Top)

        ' Find a canvas to add to (use the first selected item's canvas)
        Dim firstWrapper = TryCast(selectedItems(0).DrawableElement?.Parent, ContentControl)
        If firstWrapper IsNot Nothing Then
            Dim canvas = TryCast(VisualTreeHelper.GetParent(firstWrapper), Canvas)
            If canvas IsNot Nothing Then
                ' Use the standard AddDrawableElement method which will wrap it and add to canvas
                AddDrawableElement(newPath)

                ' Remove original shapes
                For Each drawable In selectedItems
                    RemoveDrawableLeaf(drawable)
                Next

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

                _snackbarService.GenerateSuccess("Success", $"{operationName}: Combined {selectedItems.Count} shapes")
            End If
        End If
    End Sub

    Private Function GetTransformedGeometry(drawable As IDrawable) As Geometry
        If drawable?.DrawableElement Is Nothing Then Return Nothing

        Dim element = drawable.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return Nothing

        Dim geometry As Geometry = Nothing

        ' Convert element to geometry based on type
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
            ' Convert line to a stroked path with thickness
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
                ' Use DPI of 1.0 like DrawableText does to avoid distortion
                Dim formattedText As New FormattedText(
                    textBox.Text,
                    Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    New Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                    textBox.FontSize,
                    Brushes.Black,
                    1.0)


                ' Build geometry at origin - transforms will position it correctly
                geometry = formattedText.BuildGeometry(New Point(0, 0))
            End If
        End If

        If geometry Is Nothing Then Return Nothing

        ' Apply element-level transforms (mirror scale)
        Dim elementTransformGroup = TryCast(element.RenderTransform, TransformGroup)
        If elementTransformGroup IsNot Nothing Then
            Dim elementScale = elementTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
            If elementScale IsNot Nothing Then
                Dim scaleTransform = New ScaleTransform(elementScale.ScaleX, elementScale.ScaleY,
                    geometry.Bounds.Width / 2, geometry.Bounds.Height / 2)
                geometry = Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, scaleTransform)
            End If
        End If

        ' Apply wrapper transforms (position and rotation)
        Dim transformGroup As New TransformGroup()

        ' Scale to wrapper size (but NOT for text - text geometry is already correct size)
        If Not TypeOf element Is TextBox Then
            If geometry.Bounds.Width > 0 AndAlso geometry.Bounds.Height > 0 Then
                Dim scaleX = wrapper.ActualWidth / geometry.Bounds.Width
                Dim scaleY = wrapper.ActualHeight / geometry.Bounds.Height
                transformGroup.Children.Add(New ScaleTransform(scaleX, scaleY))
            End If
        End If

        ' Apply rotation if present
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            transformGroup.Children.Add(New RotateTransform(rotateTransform.Angle,
                wrapper.ActualWidth / 2, wrapper.ActualHeight / 2))
        End If

        ' Apply position
        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        If Not Double.IsNaN(left) AndAlso Not Double.IsNaN(top) Then
            ' For text, apply the same correction as DrawableText.BakeTransforms (LCorrection=-3, TCorrection=-1)
            ' This accounts for TextBox padding and text rendering offset
            If TypeOf element Is TextBox Then
                transformGroup.Children.Add(New TranslateTransform(left + 3, top + 1))
            Else
                transformGroup.Children.Add(New TranslateTransform(left, top))
            End If
        End If

        ' Apply all transforms to the geometry
        Return Geometry.Combine(geometry, geometry, GeometryCombineMode.Union, transformGroup)
    End Function

End Class
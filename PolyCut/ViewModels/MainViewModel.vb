Imports System.Collections.ObjectModel
Imports System.IO

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports MeasurePerformance.IL.Weaver

Imports PolyCut.Core
Imports PolyCut.Shared

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

    Private DrawableSVGFile As SVGFile
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
            Return DrawableCollection.FirstOrDefault(Function(f) f.IsSelected)
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

        ' Create persistent drawing group if needed
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

End Class
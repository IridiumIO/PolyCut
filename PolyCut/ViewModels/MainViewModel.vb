Imports System.Collections.ObjectModel
Imports System.IO

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports MeasurePerformance.IL.Weaver

Imports PolyCut.Core
Imports PolyCut.Shared

Imports Svg

Imports WPF.Ui

Public Class MainViewModel : Inherits ObservableObject

    Public Property UsingGCodePlot As Boolean

    Public Property Printers As ObservableCollection(Of Printer)
    Public Property Printer As Printer
    Public Property CuttingMats As ObservableCollection(Of CuttingMat)
    Public Property CuttingMat As CuttingMat
    Public Property Configuration As ProcessorConfiguration


    Public Property GCode As String = Nothing
    Public Property GCodeGeometry As GCodeGeometry
    Public Property GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()

    Public Property DrawableCollection As ObservableCollection(Of IDrawable) = New ObservableCollection(Of IDrawable)

    Public ReadOnly Property SelectedDrawable As IDrawable
        Get
            Return DrawableCollection.FirstOrDefault(Function(f) f.IsSelected)
        End Get
    End Property

    Public Property SVGFiles As New ObservableCollection(Of SVGFile)
    Public ReadOnly Property PolyCutDocumentName As String
        Get
            If SVGFiles.Count <> 0 AndAlso SVGFiles.First.ShortFileName <> "Drawing Group" Then
                Return SVGFiles.First.ShortFileName.Replace(".svg", ".gcode")
            Else
                Return "PolyCut1.gcode"
            End If
        End Get
    End Property

    Private ReadOnly _snackbarService As SnackbarService
    Private ReadOnly _navigationService As INavigationService
    Private ReadOnly _argsService As CommandLineArgsService

    Public Property SavePrinterCommand As ICommand = New RelayCommand(AddressOf SavePrinter)
    Public Property SaveCuttingMatCommand As ICommand = New RelayCommand(AddressOf SaveCuttingMat)
    Public Property BrowseSVGCommand As ICommand = New RelayCommand(AddressOf BrowseSVG)
    Public Property OpenSnackbar_Save As ICommand = New RelayCommand(Of String)(Sub(x) _snackbarService.GenerateSuccess("Saved Preset", x))
    Public Property GenerateGCodeCommand As ICommand = New RelayCommand(AddressOf GenerateGcode)
    Public Property RemoveSVGCommand As ICommand = New RelayCommand(Of SVGFile)(Sub(x) ModifySVGFiles(x, removeSVG:=True))

    Public Property MainViewLoadedCommand As ICommand = New RelayCommand(Sub() If _argsService.Args.Length > 0 Then DragSVGs(_argsService.Args))
    Public Property MainViewClosingCommand As ICommand = New RelayCommand(Sub() SettingsHandler.WriteConfiguration(Configuration))



    Public Sub New(snackbarService As SnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService)

        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService
        Initialise()

    End Sub


    Private Sub Initialise()

        Printers = SettingsHandler.GetPrinters
        Printer = Printers.First
        CuttingMats = SettingsHandler.GetCuttingMats
        CuttingMat = CuttingMats.First
        Configuration = (SettingsHandler.GetConfigurations).First
    End Sub


    Public Sub SavePrinter()
        SettingsHandler.WritePrinter(Printer)
        _snackbarService.GenerateSuccess("Saved Preset", Printer.Name)
    End Sub
    Public Sub SaveCuttingMat()
        SettingsHandler.WriteCuttingMat(CuttingMat)
        _snackbarService.GenerateSuccess("Saved Preset", CuttingMat.Name)
    End Sub


    Private Sub BrowseSVG()
        Dim fs As New Microsoft.Win32.OpenFileDialog With {
            .Filter = "*.svg|*.svg",
            .Multiselect = True
        }
        If fs.ShowDialog Then
            For Each fl In fs.FileNames
                ModifySVGFiles(New SVGFile(fl))
            Next
        End If

    End Sub

    Public Sub ModifySVGFiles(file As SVGFile, Optional removeSVG As Boolean = False)


        If removeSVG Then
            SVGFiles.Remove(file)
            For Each child As IDrawable In file.SVGVisualComponents
                DrawableCollection.Remove(child)
            Next
        Else

            If Not SVGFiles.Contains(file) Then SVGFiles.Add(file)

            For Each child As SVGComponent In file.SVGVisualComponents
                If Not DrawableCollection.Contains(child) Then
                    child.SetCanvas()
                    DrawableCollection.Add(child)
                End If

            Next
        End If

        OnPropertyChanged(NameOf(SVGFiles))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(DrawableCollection))
        OnPropertyChanged(NameOf(SelectedDrawable))

    End Sub

    Public Sub UpdateSVGFiles()

        For Each child As SVGComponent In SVGFiles.SelectMany(Function(f) f.SVGComponents).Where(Function(g) CType(g, SVGComponent).IsVisualElement)
            If Not DrawableCollection.Contains(child.SVGViewBox) Then
                child.SetCanvas()
                DrawableCollection.Add(child.SVGViewBox)
            End If
        Next

        OnPropertyChanged(NameOf(SVGFiles))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(SelectedDrawable))

    End Sub


    Private DrawableSVGFile As SVGFile

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

        If DrawableSVGFile Is Nothing OrElse Not SVGFiles.Contains(DrawableSVGFile) Then
            DrawableSVGFile = New SVGFile(drawableL, "Drawing Group")
            SVGFiles.Add(DrawableSVGFile)
        Else
            DrawableSVGFile.SVGComponents.Add(drawableL)
        End If

        DrawableCollection.Add(drawableL)

    End Sub


    Public Sub DragSVGs(x As String())

        For Each file In x
            Dim finfo As New FileInfo(file)

            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                ModifySVGFiles(New SVGFile(file))
            End If

        Next

    End Sub




    Public Property GeneratedGCode As List(Of GCode)
    Private Async Sub GenerateGcode()
        Configuration.WorkAreaHeight = Printer.BedHeight
        Configuration.WorkAreaWidth = Printer.BedWidth
        Configuration.SoftwareVersion = SettingsHandler.Version
        Dim generator As IGenerator = If(UsingGCodePlot,
            New GCodePlotGenerator((Configuration), Printer, GenerateSVGText),
            New PolyCutGenerator(Configuration, Printer, GenerateSVGText))


        Dim retcode = Await generator.GenerateGcodeAsync

        If retcode.StatusCode = 1 Then
            _snackbarService.GenerateError("Error", retcode.Message, 5)
            Return
        End If

        GeneratedGCode = generator.GetGCode

        'Maybe I should use Aggregate in other places as well?
        'Dim compiledGCodeString = GeneratedGCode.Select(Function(x) x.ToString).Aggregate(Function(a, b) a & Environment.NewLine & b)
        Dim compiledGCodeString = BuildStringFromGCodes(GeneratedGCode)

        GCode = compiledGCodeString
        GCodeGeometry = New GCodeGeometry(GeneratedGCode)
        OnPropertyChanged(NameOf(GCode))
        _navigationService.Navigate(GetType(PreviewPage))


    End Sub


    Private Shared Function BuildStringFromGCodes(GeneratedGCode As List(Of GCode)) As String

        Dim stringBuilder As New Text.StringBuilder()
        For Each gc In GeneratedGCode
            stringBuilder.AppendLine(gc.ToString())
        Next
        Return stringBuilder.ToString()
    End Function


    Function GenerateSVGText() As String

        Dim coll = SVGFiles.SelectMany(Function(fl) fl.SVGComponents).Where(Function(c)
                                                                                Dim svgComp = TryCast(c, SVGComponent)
                                                                                Return svgComp IsNot Nothing AndAlso svgComp.IsVisualElement _
                                                                                AndAlso svgComp.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) _
                                                                                AndAlso Not svgComp.IsHidden
                                                                            End Function).ToList

        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)}

        outDoc.Children.AddRange(coll.Select(Function(f) f.GetTransformedSVGElement))

        For Each drawableL In DrawableCollection

            Dim finalElement = drawableL?.GetTransformedSVGElement

            If finalElement?.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) Then
                outDoc.Children.Add(finalElement)
            ElseIf TypeOf (drawableL) Is DrawablePath Then
                If drawableL?.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) Then
                    outDoc.Children.Add(finalElement)
                End If
            End If

        Next

        Return SVGComponent.SVGDocumentToSVGString(outDoc)

    End Function

    Public Sub NotifyPropertyChanged(propName As String)
        OnPropertyChanged(propName)
    End Sub

End Class

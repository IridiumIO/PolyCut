Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.IO
Imports System.Reflection
Imports System.Xml

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.Core

Imports SharpVectors.Dom.Svg
Imports SharpVectors.Renderers.Utils
Imports SharpVectors.Renderers.Wpf

Imports Svg

Imports WPF.Ui
Imports WPF.Ui.Controls

Public Class MainViewModel : Inherits ObservableObject

    Public Property UsingGCodePlot As Boolean = False

    Public Property Printers As ObservableCollection(Of Printer)
    Public Property Printer As Printer
    Public Property CuttingMats As ObservableCollection(Of CuttingMat)
    Public Property CuttingMat As CuttingMat
    Public Property Configuration As New ProcessorConfiguration

    Public Property GCode As String = Nothing
    Public Property GCodeGeometry As GCodeGeometry
    Public Property GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()

    Public Property SVGFiles As New ObservableCollection(Of SVGFile)

    Private ReadOnly _snackbarService As ISnackbarService
    Private ReadOnly _navigationService As INavigationService
    Private ReadOnly _argsService As CommandLineArgsService

    Public Property SavePrinterCommand As ICommand = New RelayCommand(AddressOf SavePrinter)
    Public Property SaveCuttingMatCommand As ICommand = New RelayCommand(AddressOf SaveCuttingMat)
    Public Property BrowseSVGCommand As ICommand = New RelayCommand(AddressOf BrowseSVG)
    Public Property OpenSnackbar_Save As ICommand = New RelayCommand(Of String)(Sub(x) GenerateSnackbar("Saved Preset", x, ControlAppearance.Success))
    Public Property GenerateGCodeCommand As ICommand = New RelayCommand(AddressOf GenerateGCode)
    Public Property RemoveSVGCommand As ICommand = New RelayCommand(Of SVGFile)(Sub(x) ModifySVGFiles(x, removeSVG:=True))
    Public Property NetworkUploadCommand As ICommand = New RelayCommand(Sub()
                                                                            Dim MoonrakerExporter As New MoonrakerExporter(Configuration)

                                                                        End Sub)
    Public Property MainViewLoadedCommand As ICommand = New RelayCommand(Sub()
                                                                             If _argsService.Args.Length > 0 Then
                                                                                 DragSVGs(_argsService.Args)
                                                                             End If
                                                                         End Sub)


    Public ReadOnly Property SVGComponents As ObservableCollection(Of SVGComponent)
        Get
            Return New ObservableCollection(Of SVGComponent)(SVGFiles.SelectMany(Function(f) f.SVGComponents))
        End Get
    End Property

    Public Sub New(snackbarService As ISnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService)

        SettingsHandler.InitialiseSettings()
        Initialise()
        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService


    End Sub

    Private Async Sub Initialise()
        Printers = Await SettingsHandler.GetPrinters
        CuttingMats = Await SettingsHandler.GetCuttingMats
        Printer = Printers.First
        CuttingMat = CuttingMats.First
        Configuration = New ProcessorConfiguration
    End Sub
    Public Sub SavePrinter()
        SettingsHandler.WritePrinter(Printer)
        GenerateSnackbar("Saved Preset", Printer.Name, ControlAppearance.Success)
    End Sub
    Public Sub SaveCuttingMat()
        SettingsHandler.WriteCuttingMat(CuttingMat)
        GenerateSnackbar("Saved Preset", CuttingMat.Name, ControlAppearance.Success)
    End Sub


    Private Sub BrowseSVG()

        Dim fs As New Microsoft.Win32.OpenFileDialog

        fs.Filter = "*.svg|*.svg"

        If fs.ShowDialog Then

            Dim fl = fs.FileName

            ModifySVGFiles(New SVGFile(fl))


        End If

    End Sub

    Public Sub ModifySVGFiles(file As SVGFile, Optional removeSVG As Boolean = False)

        SVGComponents.ForEach(Sub(x) x.SaveState())


        If removeSVG Then
            SVGFiles.Remove(file)
        Else
            SVGFiles.Add(file)
        End If
        OnPropertyChanged(NameOf(SVGComponents))

        SVGComponents.ForEach(Sub(x) x.LoadState())


    End Sub


    Public Sub DragSVGs(x As String())

        For Each file In x
            Dim finfo As New FileInfo(file)

            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                ModifySVGFiles(New SVGFile(file))
            End If

        Next

    End Sub


    Private Sub GenerateSnackbar(Title As String,
                                 Subtitle As String,
                                 Optional ControlAppearance As ControlAppearance = ControlAppearance.Primary,
                                 Optional Icon As SymbolRegular = SymbolRegular.Info24,
                                 Optional Duration As Integer = 3)

        _snackbarService.Show(Title, Subtitle, ControlAppearance, New SymbolIcon(Icon), TimeSpan.FromSeconds(Duration))

    End Sub


    Private Async Sub GenerateGcode()
        Configuration.WorkAreaHeight = Printer.BedHeight
        Configuration.WorkAreaWidth = Printer.BedWidth

        Dim generator As IGenerator = If(UsingGCodePlot,
            New GCodePlotGenerator(Configuration, Printer, GenerateSVGText),
            New PolyCutGenerator(Configuration, Printer, GenerateSVGText))


        Dim retcode = Await generator.GenerateGcodeAsync

        If retcode.StatusCode = 1 Then
            GenerateSnackbar("Error", retcode.Message, ControlAppearance.Danger, SymbolRegular.ErrorCircle24, 5)
            Return
        End If

        Dim generatedGCodes = generator.GetGCode

        'Maybe I should use Aggregate in other places as well?
        Dim compiledGCodeString = generatedGCodes.Select(Function(x) x.ToString).Aggregate(Function(a, b) a & Environment.NewLine & b)
        GCode = compiledGCodeString
        GCodeGeometry = New GCodeGeometry(generatedGCodes)
        OnPropertyChanged(NameOf(GCode))
        _navigationService.Navigate(GetType(PreviewPage))


    End Sub


    Function GenerateSVGText() As String

        Dim coll = SVGComponents.Where(Function(c) c.IsVisualElement _
            AndAlso c.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) _
            AndAlso Not c.isHidden)

        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)}

        outDoc.Children.AddRange(coll.Select(Function(f) f.GetTransformedSVGElement))

        Return SVGComponent.SVGDocumentToSVGString(outDoc)

    End Function



End Class

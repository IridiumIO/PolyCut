Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Windows.Controls.Primitives

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input


Imports PolyCut.Core

Imports Svg

Imports WPF.Ui
Imports WPF.Ui.Controls

Public Class MainViewModel : Inherits ObservableObject

    Public Property UsingGCodePlot As Boolean

    Private Property CanvasColor As Brush = New SolidColorBrush(Color.FromRgb(50, 50, 50))
    Public Property CanvasThemeColor As String
        Get
            Return CanvasColor.ToString
        End Get
        Set(value As String)
            If value = "Light" Then
                CanvasColor = Brushes.White
            Else
                CanvasColor = New SolidColorBrush(Color.FromRgb(50, 50, 50))
            End If
        End Set
    End Property

    Public Property Printers As ObservableCollection(Of Printer)
    Public Property Printer As Printer
    Public Property CuttingMats As ObservableCollection(Of CuttingMat)
    Public Property CuttingMat As CuttingMat
    Public Property Configuration As ProcessorConfiguration

    Private _CanvasToolMode As CanvasMode
    Public Property CanvasToolMode As CanvasMode
        Get
            Return _CanvasToolMode
        End Get
        Set(value As CanvasMode)
            _CanvasToolMode = value
            If value <> CanvasMode.Selection Then
                For Each child In DrawableCollection
                    If TypeOf child.Parent Is ContentControl Then
                        Selector.SetIsSelected(child.Parent, False)
                    End If
                Next

            End If
        End Set
    End Property

    Public Property CanvasFontFamily As FontFamily = New FontFamily("Calibri")

    Public Property CanvasFontSize As String = 14

    Public Property GCode As String = Nothing
    Public Property GCodeGeometry As GCodeGeometry
    Public Property GCodePaths As ObservableCollection(Of Line) = New ObservableCollection(Of Line)()

    Public Property DrawableCollection As ObservableCollection(Of FrameworkElement) = New ObservableCollection(Of FrameworkElement)

    Public Property SVGFiles As New ObservableCollection(Of SVGFile)
    Public ReadOnly Property PolyCutDocumentName As String
        Get
            If SVGFiles.Count <> 0 Then
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

    Public Property DeleteDrawableElementCommand As ICommand = New RelayCommand(Sub()
                                                                                    Dim itemsToRemove As New List(Of FrameworkElement)
                                                                                    For Each child In DrawableCollection
                                                                                        If Selector.GetIsSelected(child.Parent) Then
                                                                                            itemsToRemove.Add(child)
                                                                                        End If
                                                                                    Next

                                                                                    For Each item In itemsToRemove
                                                                                        DrawableCollection.Remove(item)
                                                                                    Next

                                                                                End Sub)

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

        Dim fs As New Microsoft.Win32.OpenFileDialog

        fs.Filter = "*.svg|*.svg"
        fs.Multiselect = True

        If fs.ShowDialog Then

            For Each fl In fs.FileNames

                ModifySVGFiles(New SVGFile(fl))

            Next


        End If

    End Sub

    Public Sub ModifySVGFiles(file As SVGFile, Optional removeSVG As Boolean = False)

        'SVGComponents.ForEach(Sub(x) x.SaveState())


        If removeSVG Then
            SVGFiles.Remove(file)
            For Each child As SVGComponent In file.SVGVisualComponents
                DrawableCollection.Remove(child.SVGViewBox)
            Next
        Else

            If Not SVGFiles.Contains(file) Then SVGFiles.Add(file)

            For Each child As SVGComponent In file.SVGVisualComponents
                If Not DrawableCollection.Contains(child.SVGViewBox) Then
                    child.SetCanvas()
                    DrawableCollection.Add(child.SVGViewBox)
                End If

            Next
        End If

        'OnPropertyChanged(NameOf(SVGComponents))
        OnPropertyChanged(NameOf(SVGFiles))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
        OnPropertyChanged(NameOf(DrawableCollection))
        'SVGComponents.ForEach(Sub(x) x.LoadState())


    End Sub

    Public Sub UpdateSVGFiles()
        'OnPropertyChanged(NameOf(SVGComponents))

        For Each child As SVGComponent In SVGFiles.SelectMany(Function(f) f.SVGComponents).Where(Function(g) g.IsVisualElement)
            If Not DrawableCollection.Contains(child.SVGViewBox) Then
                child.SetCanvas()
                DrawableCollection.Add(child.SVGViewBox)
            End If

        Next

        OnPropertyChanged(NameOf(SVGFiles))
        OnPropertyChanged(NameOf(PolyCutDocumentName))
    End Sub


    Public Sub AddDrawableElement(element As FrameworkElement)
        DrawableCollection.Add(element)
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


    Private Function BuildStringFromGCodes(GeneratedGCode As List(Of GCode)) As String

        Dim stringBuilder As New Text.StringBuilder()
        For Each gc In GeneratedGCode
            stringBuilder.AppendLine(gc.ToString())
        Next
        Return stringBuilder.ToString()
    End Function


    Function GenerateSVGText() As String

        Dim coll = SVGFiles.SelectMany(Function(fl) fl.SVGComponents).Where(Function(c) c.IsVisualElement _
            AndAlso c.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) _
            AndAlso Not c.IsHidden)

        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)}

        outDoc.Children.AddRange(coll.Select(Function(f) f.GetTransformedSVGElement))

        For Each shp In DrawableCollection
            Dim drawableL As IDrawable
            If TypeOf (shp) Is Line Then
                drawableL = New DrawableLine(shp)
            ElseIf TypeOf (shp) Is Rectangle Then
                drawableL = New DrawableRectangle(shp)
            ElseIf TypeOf (shp) Is Ellipse Then
                drawableL = New DrawableEllipse(shp)
            ElseIf TypeOf (shp) Is TextBox Then
                drawableL = New DrawableText(shp)
            Else 'If TypeOf (shp) Is SharpVectors.Converters.SvgViewbox Then

                Dim asSVGViewbox = CType(shp, SharpVectors.Converters.SvgViewbox)

                'Dim existingSVG = SVGFiles.SelectMany(Of SVGComponent)(Function(f) f.SVGVisualComponents).FirstOrDefault(Function(g) g.SVGViewBox Is asSVGViewbox, Nothing)

                'If existingSVG IsNot Nothing Then
                drawableL = Nothing
                'Else

                'End If

            End If

            Dim finalElement = drawableL?.GetTransformedSVGElement

            If finalElement?.IsWithinBounds(Printer.BedWidth, Printer.BedHeight) Then
                outDoc.Children.Add(finalElement)
            End If

        Next

        Return SVGComponent.SVGDocumentToSVGString(outDoc)

    End Function

    Function CreateGCodeDocument(gcodes As List(Of GCode)) As FlowDocument

        Dim document As New FlowDocument
        document.FontFamily = New FontFamily("Consolas")
        document.FontSize = 14
        document.LineHeight = 1
        'Dim lines As String() = GCode.Split(Environment.NewLine)

        For Each line In gcodes
            Dim paragraph As New Paragraph

            If line.ToString.StartsWith(";") Then
                paragraph.Inlines.Add(New Run(line.ToString) With {.Foreground = New SolidColorBrush(Color.FromArgb(128, 255, 255, 255))})
                document.Blocks.Add(paragraph)
                Continue For
            End If

            Dim words As String() = line.ToString.Split(" "c)
            For Each word In words

                Dim run As New Run(word)
                If word.StartsWith("G0") Then
                    run.Foreground = Brushes.OrangeRed
                ElseIf word.StartsWith("G1") Then
                    run.Foreground = Brushes.CornflowerBlue
                End If

                If String.IsNullOrWhiteSpace(word) Then Continue For

                paragraph.Inlines.Add(run)
                paragraph.Inlines.Add(" ")


            Next

            document.Blocks.Add(paragraph)

        Next

        Return document

    End Function





End Class

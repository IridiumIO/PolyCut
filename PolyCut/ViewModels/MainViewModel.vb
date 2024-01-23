Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.IO
Imports System.Reflection
Imports System.Xml
Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports SharpVectors.Dom.Svg
Imports SharpVectors.Renderers.Utils
Imports SharpVectors.Renderers.Wpf
Imports Svg
Imports WPF.Ui
Imports WPF.Ui.Controls

Public Class MainViewModel : Inherits ObservableObject

    Public Property Printers As ObservableCollection(Of Printer)
    Public Property Printer As Printer
    Public Property CuttingMats As ObservableCollection(Of CuttingMat)
    Public Property CuttingMat As CuttingMat
    Public Property Configuration As New Configuration

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
                                                                            Configuration.NetworkPrinter.SendGcode(GCode)
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
        Configuration = New Configuration
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



    Private Sub GenerateGCode()


        Configuration.SetArea(Printer.WorkingOffsetX, Printer.WorkingOffsetY, Printer.WorkingOffsetX + Printer.WorkingWidth, Printer.WorkingOffsetY + Printer.WorkingHeight)

        Dim args = Configuration.BuildGCPArgs()

        Dim generatedSVGPath = GenerateSVG()

        args = args & " """ & generatedSVGPath & """"

        Dim ret As (String, String) = RunEmbeddedExecutable("gcodeplot.exe", args)
        Dim output = ret.Item1
        Dim eroutput = ret.Item2

        If output?.Length = 0 Then
            GenerateSnackbar("Error", eroutput, ControlAppearance.Caution, SymbolRegular.ErrorCircle24, 5)
            Return
        End If

        Dim lOut As String = ""

        Dim lines() As String = output.Split(Environment.NewLine)
        For Each line In lines
            Dim index = line.IndexOf(";"c)
            If index >= 0 Then
                lOut &= $"{line.Substring(0, index).Trim}{Environment.NewLine}"
            End If
        Next

        GCode = lOut & Environment.NewLine
        GCodeGeometry = New GCodeGeometry(GCode)
        OnPropertyChanged(NameOf(GCode))
        _navigationService.Navigate(GetType(PreviewPage))


    End Sub



    Function GenerateSVG()

        Dim coll = SVGComponents.Where(Function(c) c.IsVisualElement AndAlso c.IsWithinBounds(Printer.BedWidth, Printer.BedHeight))

        If Configuration.IgnoreHidden Then
            coll = coll.Where(Function(c) c.isHidden = False)
        End If

        Dim outDoc As New Svg.SvgDocument With {
            .Width = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedWidth),
            .Height = New SvgUnit(Svg.SvgUnitType.Millimeter, Printer.BedHeight),
            .ViewBox = New Svg.SvgViewBox(0, 0, Printer.BedWidth, Printer.BedHeight)}

        outDoc.Children.AddRange(coll.Select(Function(f) f.GetTransformedSVGElement))

        Dim rx = SVGComponent.SVGDocumentToSVGString(outDoc)
        Dim tempFilePath As String = Path.GetTempFileName()
        IO.File.WriteAllText(tempFilePath, rx)

        Return tempFilePath

    End Function


    Function RunEmbeddedExecutable(executableName As String, args As String) As (String, String)
        Dim executingAssembly As Assembly = Assembly.GetExecutingAssembly()

        Dim executablePath As String = Path.Combine(SettingsHandler.DataFolder.FullName, executableName)

        If Not File.Exists(executablePath) Then
            Using stream As Stream = executingAssembly.GetManifestResourceStream(executingAssembly.GetName().Name & "." & executableName)
                If stream IsNot Nothing Then
                    Dim exeBytes(CInt(stream.Length) - 1) As Byte
                    stream.Read(exeBytes, 0, exeBytes.Length)

                    Using tempFileStream As FileStream = File.Create(executablePath)
                        tempFileStream.Write(exeBytes, 0, exeBytes.Length)
                    End Using
                End If
            End Using
        End If


        ' Run the extracted executable
        Dim process As New Process()
        process.StartInfo.FileName = executablePath
        process.StartInfo.Arguments = args
        process.StartInfo.RedirectStandardOutput = True
        process.StartInfo.RedirectStandardError = True
        process.StartInfo.UseShellExecute = False
        process.StartInfo.CreateNoWindow = True
        process.Start()
        Dim output As String = process.StandardOutput.ReadToEnd()
        Dim outputER As String = process.StandardError.ReadToEnd()

        ' Optionally, wait for the process to exit
        process.WaitForExit()


        Return (output, outputER)


    End Function

End Class

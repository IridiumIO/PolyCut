﻿Imports System.Collections.ObjectModel
Imports System.IO
Imports System.Windows.Controls.Primitives

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input


Imports PolyCut.Core
Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Imports Svg

Imports WPF.Ui
Imports WPF.Ui.Controls

Public Class MainViewModel : Inherits ObservableObject

    Public Property UsingGCodePlot As Boolean

    Private Property CanvasColor As SolidColorBrush = New SolidColorBrush(Color.FromRgb(50, 50, 50))
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
            OnPropertyChanged(NameOf(CanvasToolMode))
            If value <> CanvasMode.Selection Then
                For Each child In DrawableCollection
                    child.IsSelected = False

                Next
                OnPropertyChanged(NameOf(SelectedDrawable))
            End If
        End Set
    End Property

    Private _CanvasFontFamily As New FontFamily("Calibri")
    Public Property CanvasFontFamily As FontFamily
        Get
            Return _CanvasFontFamily
        End Get
        Set(value As FontFamily)
            _CanvasFontFamily = value
            CanvasTextBox.FontFamily = value
            OnPropertyChanged(NameOf(CanvasTextBox))
        End Set
    End Property

    Private _CanvasFontSize As String = "14"
    Public Property CanvasFontSize As String
        Get
            Return _CanvasFontSize
        End Get
        Set(value As String)
            If String.IsNullOrEmpty(value) Then
                value = "14"
            End If
            _CanvasFontSize = value
            CanvasTextBox.FontSize = CInt(value)
            OnPropertyChanged(NameOf(CanvasTextBox))

        End Set
    End Property

    Public Property CanvasTextBox As TextBox = New TextBox With {.FontFamily = New FontFamily("Calibri"), .FontSize = 14}

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

    Public Property DeleteDrawableElementCommand As ICommand = New RelayCommand(Sub()

                                                                                    Dim drawableItemsToRemove As New List(Of IDrawable)

                                                                                    For Each child In DrawableCollection
                                                                                        If child.IsSelected Then
                                                                                            drawableItemsToRemove.Add(child)

                                                                                        End If
                                                                                    Next


                                                                                    ' Remove corresponding IDrawables from SVGFiles
                                                                                    For Each drawable In drawableItemsToRemove
                                                                                        DrawableCollection.Remove(drawable)
                                                                                        Dim svgFile = SVGFiles.FirstOrDefault(Function(file) file.SVGComponents.Contains(drawable))
                                                                                        If svgFile IsNot Nothing Then
                                                                                            svgFile.SVGComponents.Remove(drawable)
                                                                                            If svgFile.SVGVisualComponents.IsEmpty Then
                                                                                                ModifySVGFiles(svgFile, removeSVG:=True)
                                                                                            End If
                                                                                        End If
                                                                                    Next

                                                                                    OnPropertyChanged(NameOf(SelectedDrawable))

                                                                                End Sub)

    Public Property MirrorHorizontallyCommand As ICommand = New RelayCommand(Sub()
                                                                                 If SelectedDrawable IsNot Nothing Then
                                                                                     Dim parentContentControl As ContentControl = TryCast(SelectedDrawable.DrawableElement.Parent, ContentControl)

                                                                                     Dim mirrorTransform As New ScaleTransform With {.ScaleX = -1}
                                                                                     ' Get the current transform of the DrawableElement
                                                                                     Dim currentTransformGroup As TransformGroup = TryCast(SelectedDrawable.DrawableElement.RenderTransform, TransformGroup)

                                                                                     If currentTransformGroup Is Nothing Then
                                                                                         currentTransformGroup = New TransformGroup()
                                                                                         SelectedDrawable.DrawableElement.RenderTransform = currentTransformGroup
                                                                                     End If

                                                                                     Dim scaletransform = currentTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                                                                                     If scaletransform IsNot Nothing Then
                                                                                         scaletransform.ScaleX *= -1
                                                                                     Else
                                                                                         currentTransformGroup.Children.Add(mirrorTransform)
                                                                                     End If
                                                                                     SelectedDrawable.DrawableElement.RenderTransformOrigin = New Point(0.5, 0.5)

                                                                                 End If
                                                                             End Sub)


    Public Property MirrorVerticallyCommand As ICommand = New RelayCommand(Sub()
                                                                               If SelectedDrawable IsNot Nothing Then
                                                                                   Dim parentContentControl As ContentControl = TryCast(SelectedDrawable.DrawableElement.Parent, ContentControl)

                                                                                   Dim mirrorTransform As New ScaleTransform With {.ScaleY = -1}
                                                                                   ' Get the current transform of the DrawableElement
                                                                                   Dim currentTransformGroup As TransformGroup = TryCast(SelectedDrawable.DrawableElement.RenderTransform, TransformGroup)

                                                                                   If currentTransformGroup Is Nothing Then
                                                                                       currentTransformGroup = New TransformGroup()
                                                                                       SelectedDrawable.DrawableElement.RenderTransform = currentTransformGroup
                                                                                   End If

                                                                                   Dim scaletransform = currentTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                                                                                   If scaletransform IsNot Nothing Then
                                                                                       scaletransform.ScaleY *= -1
                                                                                   Else
                                                                                       currentTransformGroup.Children.Add(mirrorTransform)
                                                                                   End If
                                                                                   SelectedDrawable.DrawableElement.RenderTransformOrigin = New Point(0.5, 0.5)

                                                                               End If
                                                                           End Sub)



    Public Sub New(snackbarService As SnackbarService, navigationService As INavigationService, argsService As CommandLineArgsService)

        _snackbarService = snackbarService
        _navigationService = navigationService
        _argsService = argsService
        Initialise()

    End Sub

    Private Async Sub TickEverySecond()

    End Sub

    Private Sub Initialise()

        Printers = SettingsHandler.GetPrinters
        Printer = Printers.First
        CuttingMats = SettingsHandler.GetCuttingMats
        CuttingMat = CuttingMats.First
        Configuration = (SettingsHandler.GetConfigurations).First
        AddHandler DesignerItemDecorator.CurrentSelectedChanged, AddressOf OnDesignerItemDecoratorCurrentSelectedChanged
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


    Private Sub OnDesignerItemDecoratorCurrentSelectedChanged(sender As Object, e As EventArgs)
        ' Handle the change to the CurrentSelected property
        Dim currentSelected = DesignerItemDecorator.CurrentSelected
        For Each svgFile In SVGFiles
            For Each child In svgFile.SVGComponents
                If currentSelected IsNot Nothing AndAlso ((TryCast(child, SVGComponent) IsNot Nothing AndAlso TryCast(child, SVGComponent).SVGViewBox Is currentSelected.Content) OrElse child.DrawableElement Is currentSelected.Content) Then
                    child.IsSelected = True
                Else
                    child.IsSelected = False
                End If
            Next
        Next

        OnPropertyChanged(NameOf(SelectedDrawable))

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

    Shared Function CreateGCodeDocument(gcodes As List(Of GCode)) As FlowDocument
        Dim document As New FlowDocument With {
            .FontFamily = New FontFamily("Consolas"),
            .FontSize = 14,
            .LineHeight = 1
        }
        'Dim lines As String() = GCode.Split(Environment.NewLine)

        For Each line In gcodes
            Dim paragraph As New Paragraph

            If line.ToString.StartsWith(";"c) Then
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

    Public Sub NotifyPropertyChanged(propertyName As String)
        OnPropertyChanged(propertyName)
    End Sub



End Class

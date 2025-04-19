Imports System.IO

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class SVGPageViewModel : Inherits ObservableObject

    Public Property MainVM As MainViewModel

    Private Property CanvasColor As SolidColorBrush = New SolidColorBrush(Color.FromArgb(64, 100, 100, 100))
    Public Property CanvasThemeColor As String
        Get
            Return CanvasColor.ToString
        End Get
        Set(value As String)
            CanvasColor = If(value = "Light", Brushes.White, New SolidColorBrush(Color.FromArgb(64, 100, 100, 100)))
        End Set
    End Property


    Private _CanvasToolMode As CanvasMode
    Public Property CanvasToolMode As CanvasMode
        Get
            Return _CanvasToolMode
        End Get
        Set(value As CanvasMode)
            _CanvasToolMode = value
            OnPropertyChanged(NameOf(CanvasToolMode))
            OnPropertyChanged(NameOf(CanvasToolModeIsText))
            If value <> CanvasMode.Selection Then
                For Each child In MainVM.DrawableCollection
                    child.IsSelected = False

                Next
                MainVM.NotifyPropertyChanged(NameOf(MainVM.SelectedDrawable))
            End If
        End Set
    End Property

    Public ReadOnly Property CanvasToolModeIsText As Boolean
        Get
            Return CanvasToolMode = CanvasMode.Text
        End Get
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
            If String.IsNullOrEmpty(value) Then value = "14"

            _CanvasFontSize = value
            CanvasTextBox.FontSize = CInt(value)
            OnPropertyChanged(NameOf(CanvasTextBox))

        End Set
    End Property

    Public Property CanvasTextBox As TextBox = New TextBox With {.FontFamily = New FontFamily("Calibri"), .FontSize = 14}

    Public Property PreviewKeyDownCommand As ICommand = New RelayCommand(Of String)(Sub(key) ShortcutKeyHandler(key))
    Public Property MirrorHorizontallyCommand As ICommand = New RelayCommand(Sub() MirrorObject(MainVM.SelectedDrawable.DrawableElement, True, False))
    Public Property MirrorVerticallyCommand As ICommand = New RelayCommand(Sub() MirrorObject(MainVM.SelectedDrawable.DrawableElement, False, True))
    Public Property DeleteDrawableElementCommand As ICommand = New RelayCommand(Sub() DeleteSelectedDrawableElement())


    Private Sub DeleteSelectedDrawableElement()

        Dim drawableItemsToRemove = MainVM.DrawableCollection.Where(Function(d) d.IsSelected).ToList()

        ' Remove corresponding IDrawables from SVGFiles
        For Each drawable In drawableItemsToRemove
            MainVM.DrawableCollection.Remove(drawable)

            Dim svgFile = MainVM.SVGFiles.FirstOrDefault(Function(file) file.SVGComponents.Contains(drawable))

            If svgFile IsNot Nothing Then
                svgFile.SVGComponents.Remove(drawable)
                If svgFile.SVGVisualComponents.IsEmpty Then MainVM.ModifySVGFiles(svgFile, removeSVG:=True)
            End If
        Next

        MainVM.NotifyPropertyChanged(NameOf(MainVM.SelectedDrawable))

    End Sub


    Public Shared Sub MirrorObject(ByRef DrawableElement As FrameworkElement, MirrorX As Boolean, MirrorY As Boolean)

        Dim mirrorTransform As New ScaleTransform With {.ScaleX = If(MirrorX, -1, 1), .ScaleY = If(MirrorY, -1, 1)}

        Dim currentTransformGroup As TransformGroup = TryCast(DrawableElement.RenderTransform, TransformGroup)

        If currentTransformGroup Is Nothing Then
            currentTransformGroup = New TransformGroup()
            DrawableElement.RenderTransform = currentTransformGroup
        End If

        Dim existingScaleTransform = currentTransformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
        If existingScaleTransform IsNot Nothing Then
            existingScaleTransform.ScaleY *= If(MirrorY, -1, 1)
            existingScaleTransform.ScaleX *= If(MirrorX, -1, 1)
        Else
            currentTransformGroup.Children.Add(mirrorTransform)
        End If
        DrawableElement.RenderTransformOrigin = New Point(0.5, 0.5)


    End Sub

    Public Sub OnDesignerItemDecoratorCurrentSelectedChanged(currentSelected As ContentControl)

        For Each svgFile In MainVM.SVGFiles
            For Each child In svgFile.SVGComponents
                If currentSelected IsNot Nothing AndAlso ((TryCast(child, SVGComponent) IsNot Nothing AndAlso TryCast(child, SVGComponent).SVGViewBox Is currentSelected.Content) OrElse child.DrawableElement Is currentSelected.Content) Then
                    child.IsSelected = True
                Else
                    child.IsSelected = False
                End If
            Next
        Next
        MainVM.NotifyPropertyChanged(NameOf(MainVM.SelectedDrawable))

    End Sub












    Public Sub New(mainvm As MainViewModel)
        Me.MainVM = mainvm
    End Sub

    Private Shared Sub ShortcutKeyHandler(Key As String)

        If (Key = "]") AndAlso Keyboard.IsKeyDown(Windows.Input.Key.LeftCtrl) Then
            Dim currentSelected = DesignerItemDecorator.CurrentSelected
            If currentSelected Is Nothing Then Return
            Dim textbox As TextBox = TryCast(currentSelected.Content, TextBox)
            If textbox Is Nothing Then Return
            Dim currentFontSize As Double = textbox.FontSize
            textbox.FontSize = currentFontSize + 1

        ElseIf (Key = "[") AndAlso Keyboard.IsKeyDown(Windows.Input.Key.LeftCtrl) Then
            Dim currentSelected = DesignerItemDecorator.CurrentSelected
            If currentSelected Is Nothing Then Return
            Dim textbox As TextBox = TryCast(currentSelected.Content, TextBox)
            If textbox Is Nothing Then Return
            Dim currentFontSize As Double = textbox.FontSize
            textbox.FontSize = currentFontSize - 1

        End If
    End Sub


    Public Sub ProcessDroppedFiles(files() As String)

        For Each file In files
            Dim finfo As New FileInfo(file)

            If finfo.Exists AndAlso finfo.Extension = ".svg" Then
                MainVM.ModifySVGFiles(New SVGFile(file))
            End If

        Next

    End Sub



End Class

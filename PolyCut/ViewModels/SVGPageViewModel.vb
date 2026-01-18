Imports System.IO
Imports System.Linq

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class SVGPageViewModel : Inherits ObservableObject

    Public Property MainVM As MainViewModel
    Private ReadOnly _undoRedoService As UndoRedoService
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
                MainVM.NotifyCollectionsChanged()
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

    Public Property WorkingAreaIsVisible As Boolean = True
    Public Property CuttingMatIsVisible As Boolean = True

    Public Property PreviewKeyDownCommand As ICommand = New RelayCommand(Of String)(Sub(key) ShortcutKeyHandler(key))
    Public Property MirrorHorizontallyCommand As ICommand = New RelayCommand(Sub() MirrorSelection(True, False))
    Public Property MirrorVerticallyCommand As ICommand = New RelayCommand(Sub() MirrorSelection(False, True))
    Public Property DeleteDrawableElementCommand As ICommand = New RelayCommand(Sub() DeleteSelectedDrawableElement())


    Private Sub DeleteSelectedDrawableElement()
        MainVM.RemoveSelectedDrawables()
    End Sub


    Private Sub MirrorSelection(mirrorX As Boolean, mirrorY As Boolean)
        Dim selected = PolyCanvas.SelectedItems?.FirstOrDefault()
        If selected?.DrawableElement Is Nothing Then Return

        Dim element = selected.DrawableElement
        Dim wrapper = TryCast(element.Parent, ContentControl)
        If wrapper Is Nothing Then Return

        ' Set transform origin to center for consistent mirroring
        element.RenderTransformOrigin = New Point(0.5, 0.5)

        ' Get or create transform group on element
        Dim tg = TryCast(element.RenderTransform, TransformGroup)
        If tg Is Nothing Then
            Dim existing = element.RenderTransform
            tg = New TransformGroup()
            If existing IsNot Nothing AndAlso Not TypeOf existing Is TransformGroup Then
                tg.Children.Add(existing)
            End If
            element.RenderTransform = tg
        End If

        ' Find or create scale transform
        Dim scale = tg.Children.OfType(Of ScaleTransform)().FirstOrDefault()
        If scale Is Nothing Then
            scale = New ScaleTransform(1, 1)
            tg.Children.Add(scale)
        End If

        ' Toggle mirror
        If mirrorX Then scale.ScaleX *= -1
        If mirrorY Then scale.ScaleY *= -1

        ' Force visual update
        wrapper.InvalidateMeasure()
        wrapper.InvalidateArrange()
        wrapper.UpdateLayout()
    End Sub

    Public Sub New(mainvm As MainViewModel, undoRedoService As UndoRedoService)
        Me.MainVM = mainvm
        Me._undoRedoService = undoRedoService
    End Sub

    Private Shared Sub ShortcutKeyHandler(Key As String)
        If (Key = "]") AndAlso Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) Then
            Dim selectedItem = PolyCanvas.SelectedItems?.FirstOrDefault()
            If selectedItem Is Nothing Then Return

            Dim wrapper = TryCast(selectedItem.DrawableElement?.Parent, ContentControl)
            If wrapper Is Nothing Then Return

            Dim textbox As TextBox = TryCast(wrapper.Content, TextBox)
            If textbox Is Nothing Then Return

            textbox.FontSize += 1

        ElseIf (Key = "[") AndAlso Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) Then
            Dim selectedItem = PolyCanvas.SelectedItems?.FirstOrDefault()
            If selectedItem Is Nothing Then Return

            Dim wrapper = TryCast(selectedItem.DrawableElement?.Parent, ContentControl)
            If wrapper Is Nothing Then Return

            Dim textbox As TextBox = TryCast(wrapper.Content, TextBox)
            If textbox Is Nothing Then Return

            If textbox.FontSize > 1 Then
                textbox.FontSize -= 1
            End If
        End If
    End Sub


    Public Sub ProcessDroppedFiles(files() As String)

        MainVM.DragSVGs(files)

    End Sub


    ' -----------------
    ' Style / Formatting
    ' -----------------
    <RelayCommand>
    Private Sub ApplyFill(b As Brush)
        ApplyFill(b, Nothing)
    End Sub

    Public Sub ApplyFill(b As Brush, previousFill As Brush)
        If b Is Nothing Then Return
        ApplyStyle(b, Nothing, Nothing, previousFill, Nothing, Nothing)
    End Sub

    <RelayCommand>
    Private Sub ApplyStroke(b As Brush)
        ApplyStroke(b, Nothing)
    End Sub

    Public Sub ApplyStroke(b As Brush, previousStroke As Brush)
        If b Is Nothing Then Return
        ApplyStyle(Nothing, b, Nothing, Nothing, previousStroke, Nothing)
    End Sub

    <RelayCommand>
    Public Sub ApplyStrokeThickness(th As Double)
        ApplyStrokeThickness(th, Nothing)
    End Sub

    Public Sub ApplyStrokeThickness(th As Double, Optional previousThickness As Nullable(Of Double) = Nothing)
        If Double.IsNaN(th) Then Return
        ApplyStyle(Nothing, Nothing, th, Nothing, Nothing, previousThickness)
    End Sub

    Private Sub ApplyStyle(fill As Brush, stroke As Brush, thickness As Double?, previousFill As Brush, previousStroke As Brush, previousThickness As Double?)
        Dim items = MainVM.SelectedDrawables.ToList()
        If items.Count < 1 Then Return

        Dim action As New StyleAction(MainVM, items, fill, stroke, thickness, previousThickness, previousFill, previousStroke)
        If action.Execute() Then _undoRedoService.Push(action)

    End Sub


End Class

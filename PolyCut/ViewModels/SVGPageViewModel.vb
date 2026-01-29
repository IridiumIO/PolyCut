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

    Public Property PreviewKeyDownCommand As ICommand = New RelayCommand(Of String)(Sub(key) ShortcutKeyHandler(key))
    Public Property MirrorHorizontallyCommand As ICommand = New RelayCommand(Sub() MirrorSelection(True, False))
    Public Property MirrorVerticallyCommand As ICommand = New RelayCommand(Sub() MirrorSelection(False, True))
    Public Property DeleteDrawableElementCommand As ICommand = New RelayCommand(Sub() DeleteSelectedDrawableElement())


    Private Sub DeleteSelectedDrawableElement()
        MainVM.RemoveSelectedDrawables()
    End Sub


    Private Sub MirrorSelection(mirrorX As Boolean, mirrorY As Boolean)
        Dim selectedItems = PolyCanvas.SelectedItems.ToList()
        If selectedItems.Count = 0 Then Return

        Dim selectionCenter = CalculateSelectionCenter(selectedItems)
        Dim snapshots = New List(Of (Target As IDrawable, Before As Object, After As Object))

        For Each selected In selectedItems
            If selected?.DrawableElement Is Nothing Then Continue For
            Dim wrapper = TryCast(selected.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            Dim beforeSnapshot = TransformAction.MakeSnapshotFromWrapper(wrapper)
            TransformAction.ApplyMirror(wrapper, selectionCenter, mirrorX, mirrorY)
            Dim afterSnapshot = TransformAction.MakeSnapshotFromWrapper(wrapper)

            If beforeSnapshot IsNot Nothing AndAlso afterSnapshot IsNot Nothing Then
                snapshots.Add((selected, CType(beforeSnapshot, Object), CType(afterSnapshot, Object)))
            End If
        Next

        PublishTransformMessage(snapshots)
    End Sub

    Private Function CalculateSelectionCenter(selectedItems As List(Of IDrawable)) As Point
        Dim minX As Double = Double.MaxValue
        Dim minY As Double = Double.MaxValue
        Dim maxX As Double = Double.MinValue
        Dim maxY As Double = Double.MinValue

        For Each selected In selectedItems
            If selected?.DrawableElement Is Nothing Then Continue For
            Dim wrapper = TryCast(selected.DrawableElement.Parent, ContentControl)
            If wrapper Is Nothing Then Continue For

            Dim rotatedCorners = TransformAction.GetRotatedCorners(wrapper)
            For Each corner In rotatedCorners
                minX = Math.Min(minX, corner.X)
                minY = Math.Min(minY, corner.Y)
                maxX = Math.Max(maxX, corner.X)
                maxY = Math.Max(maxY, corner.Y)
            Next
        Next

        Return New Point((minX + maxX) / 2, (minY + maxY) / 2)
    End Function

    Private Sub PublishTransformMessage(snapshots As List(Of (Target As IDrawable, Before As Object, After As Object)))
        If snapshots.Count = 0 Then Return

        Try
            Dim msg As New TransformCompletedMessage With {.Items = snapshots}
            EventAggregator.Publish(Of TransformCompletedMessage)(msg)
        Catch ex As Exception
            Debug.WriteLine($"MirrorSelection publish failed: {ex.Message}")
        End Try
    End Sub

    Private _UIConfiguration As UIConfiguration

    Public Sub New(mainvm As MainViewModel, undoRedoService As UndoRedoService)
        Me.MainVM = mainvm
        Me._undoRedoService = undoRedoService
        _UIConfiguration = mainvm.UIConfiguration

        AddHandler _UIConfiguration.GridConfig.PropertyChanged, AddressOf NotifyPropertyChangedForGrid
        AddHandler _UIConfiguration.PropertyChanged, AddressOf NotifyPropertyChangedForGrid
        AddHandler mainvm.Printer.PropertyChanged, Sub(sender, e)
                                                       If e.PropertyName = NameOf(Printer.BedWidth) OrElse e.PropertyName = NameOf(Printer.BedHeight) Then
                                                           NotifyPropertyChangedForGrid()
                                                       End If
                                                   End Sub


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

    Public Sub ApplyFill(b As Brush, previousFillOverrides As System.Collections.Generic.IDictionary(Of IDrawable, Brush))
        If b Is Nothing Then Return
        ApplyStyle(b, Nothing, Nothing, previousFillOverrides, Nothing, Nothing)
    End Sub

    <RelayCommand>
    Private Sub ApplyStroke(b As Brush)
        ApplyStroke(b, Nothing)
    End Sub

    Public Sub ApplyStroke(b As Brush, previousStrokeOverrides As System.Collections.Generic.IDictionary(Of IDrawable, Brush))
        If b Is Nothing Then Return
        ApplyStyle(Nothing, b, Nothing, Nothing, previousStrokeOverrides, Nothing)
    End Sub

    <RelayCommand>
    Public Sub ApplyStrokeThickness(th As Double)
        ApplyStrokeThickness(th, Nothing)
    End Sub

    Public Sub ApplyStrokeThickness(th As Double, previousThicknessOverrides As System.Collections.Generic.IDictionary(Of IDrawable, Double))
        If Double.IsNaN(th) Then Return
        ApplyStyle(Nothing, Nothing, th, Nothing, Nothing, previousThicknessOverrides)
    End Sub

    Private Sub ApplyStyle(fill As Brush,
                       stroke As Brush,
                       thickness As Double?,
                       previousFill As IDictionary(Of IDrawable, Brush),
                       previousStroke As IDictionary(Of IDrawable, Brush),
                       previousThickness As IDictionary(Of IDrawable, Double))

        Dim raw = MainVM.SelectedDrawables.ToList()
        If raw.Count < 1 Then Return

        Dim items As New List(Of IDrawable)()
        For Each d In raw
            Dim ng = TryCast(d, NestedDrawableGroup)
            If ng IsNot Nothing Then
                items.AddRange(ng.GetAllLeafChildren())
            Else
                items.Add(d)
            End If
        Next
        items = items.Where(Function(x) x IsNot Nothing).Distinct().ToList()

        Dim action As New StyleAction(MainVM, items, fill, stroke, thickness, previousThickness, previousFill, previousStroke)
        If action.Execute() Then _undoRedoService.Push(action)
    End Sub




    <ObservableProperty>
    Private _GridLineThickness As Single = 0.3

    <ObservableProperty>
    Private _GridLineBrush As Brush = New SolidColorBrush(Color.FromArgb(&H80, &HFF, &HFF, &HFF))


    Public ReadOnly Property PrinterGridPreviewViewport As Rect
        Get
            Return New Rect(_UIConfiguration.GridConfig.InsetLeft, _UIConfiguration.GridConfig.InsetTop, _UIConfiguration.GridConfig.Spacing, _UIConfiguration.GridConfig.Spacing) : End Get
    End Property

    Public ReadOnly Property GridLineVerticalEnd As Point
        Get
            Return New Point(0, _UIConfiguration.GridConfig.Spacing + 1)
        End Get
    End Property

    Public ReadOnly Property GridLineHorizontalEnd As Point
        Get
            Return New Point(_UIConfiguration.GridConfig.Spacing + 1, 0)
        End Get
    End Property

    Public ReadOnly Property GridClipRect As Rect
        Get
            Dim left = Math.Max(_UIConfiguration.GridConfig.InsetLeft, 0)
            Dim top = Math.Max(_UIConfiguration.GridConfig.InsetTop, 0)
            Dim right = Math.Max(MainVM.Printer.BedWidth - _UIConfiguration.GridConfig.InsetRight, 0)
            Dim bottom = Math.Max(MainVM.Printer.BedHeight - _UIConfiguration.GridConfig.InsetBottom, 0)
            Return New Rect(left, top, right - left + GridLineThickness / 2, bottom - top + GridLineThickness / 2) ' extra 0.05 to avoid clipping last line 
        End Get
    End Property


    Public Sub NotifyPropertyChangedForGrid()
        OnPropertyChanged(NameOf(GridClipRect))
        OnPropertyChanged(NameOf(PrinterGridPreviewViewport))
        OnPropertyChanged(NameOf(GridLineVerticalEnd))
        OnPropertyChanged(NameOf(GridLineHorizontalEnd))
        OnPropertyChanged(NameOf(GridLineBrush))
    End Sub

End Class

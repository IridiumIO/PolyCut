
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel

Imports PolyCut.Shared


Public Class PolyCanvas : Inherits Controls.Canvas : Implements INotifyPropertyChanged

    ' New architecture: Decoupled selection management
    Private _selectionManager As SelectionManager
    Private _transformOverlay As TransformOverlay

    Public ReadOnly Property SelectionManager As SelectionManager
        Get
            Return _selectionManager
        End Get
    End Property

    Shared Sub New()
        DefaultStyleKeyProperty.OverrideMetadata(GetType(PolyCanvas), New FrameworkPropertyMetadata(GetType(PolyCanvas)))
    End Sub

    Sub New()
        ' Initialize selection manager
        _selectionManager = New SelectionManager()
        AddHandler _selectionManager.SelectionChanged, AddressOf OnSelectionChanged

        ' Handle mouse events
        AddHandler Me.MouseDown, AddressOf PolyCanvas_MouseDown
        AddHandler Me.Loaded, AddressOf PolyCanvas_Loaded

        ' Set as active instance (last created canvas wins)
        _activeInstance = Me
    End Sub

    Private Sub PolyCanvas_Loaded(sender As Object, e As RoutedEventArgs)
        ' Create and setup transform overlay
        If _transformOverlay Is Nothing Then
            InitializeOverlay()
        End If
    End Sub

    Private Sub InitializeOverlay()
        ' Host the overlay OUTSIDE the ZoomBorder's transformed layer so the gizmo renders at a constant 1:1 screen size regardless of zoom.
        Dim zoomHost As ZoomBorder = FindAncestorZoomBorder()
        Dim hostPanel As Panel = TryCast(zoomHost?.Parent, Panel)
        If hostPanel Is Nothing Then Return

        _transformOverlay = New TransformOverlay()
        _transformOverlay.Initialize(_selectionManager, Me)

        ' Size to the ZoomBorder viewport so the overlay covers exactly the canvas region.
        Dim widthBinding As New Binding("ActualWidth") With {.Source = zoomHost}
        Dim heightBinding As New Binding("ActualHeight") With {.Source = zoomHost}
        _transformOverlay.SetBinding(WidthProperty, widthBinding)
        _transformOverlay.SetBinding(HeightProperty, heightBinding)

        ' Match the ZoomBorder's Grid placement so the overlay covers the same area.
        CopyGridPlacement(zoomHost, _transformOverlay)

        hostPanel.Children.Add(_transformOverlay)
        Panel.SetZIndex(_transformOverlay, 0)
    End Sub

    Private Function FindAncestorZoomBorder() As ZoomBorder
        Dim current As DependencyObject = Me
        While current IsNot Nothing
            Dim zb = TryCast(current, ZoomBorder)
            If zb IsNot Nothing Then Return zb
            current = VisualTreeHelper.GetParent(current)
        End While
        Return Nothing
    End Function

    Private Shared Sub CopyGridPlacement(source As FrameworkElement, target As FrameworkElement)
        Dim row = Grid.GetRow(source)
        Dim column = Grid.GetColumn(source)
        Dim rowSpan = Grid.GetRowSpan(source)
        Dim columnSpan = Grid.GetColumnSpan(source)
        If row > 0 Then Grid.SetRow(target, row)
        If column > 0 Then Grid.SetColumn(target, column)
        If rowSpan > 1 Then Grid.SetRowSpan(target, rowSpan)
        If columnSpan > 1 Then Grid.SetColumnSpan(target, columnSpan)
    End Sub

    Private Sub OnSelectionChanged(sender As Object, e As EventArgs)
        RaiseEvent InstanceSelectionCountChanged(Me, EventArgs.Empty)
        RaiseEvent SelectionCountChanged(Me, EventArgs.Empty)
        OnPropertyChanged(NameOf(InstanceHasMultiSelection))

        If ChildrenCollection.Count > 0 AndAlso _selectionManager.Count = 0 Then

            Dim textDrawables = ChildrenCollection.Where(Function(d) TypeOf d.DrawableElement Is TextBox)

            'exit edit mode for all textboxes
            For Each drawable In textDrawables
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim textBox = TryCast(wrapper.Content, TextBox)
                    If textBox IsNot Nothing AndAlso (textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin) AndAlso Not TextEditHelper.GetIsEditing(textBox) Then
                        ' Unfocus the textbox (unless it is inside an active DrawingManager edit session)
                        Keyboard.ClearFocus()
                    End If
                End If
            Next

        End If


        ' Update legacy CurrentSelected for backwards compatibility
        If _selectionManager.Count = 1 Then
            Dim drawable = _selectionManager.SelectedItems.FirstOrDefault()
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                _currentSelected = wrapper
            Else
                _currentSelected = Nothing
            End If
        Else
            _currentSelected = Nothing
        End If

        RaiseEvent CurrentSelectedChanged(Me, EventArgs.Empty)
    End Sub

    ' Legacy compatibility for existing code that uses CurrentSelected
    Private Shared _currentSelected As ContentControl
    Public Shared Property CurrentSelected As ContentControl
        Get
            Return _currentSelected
        End Get
        Set(value As ContentControl)
            _currentSelected = value
            RaiseEvent CurrentSelectedChanged(Nothing, EventArgs.Empty)
        End Set
    End Property

    Public Shared Event CurrentSelectedChanged As EventHandler

    ' Static wrappers for backwards compatibility
    Private Shared _activeInstance As PolyCanvas
    Public Shared ReadOnly Property ActiveInstance As PolyCanvas
        Get
            Return _activeInstance
        End Get
    End Property

    Public Shared Event SelectionCountChanged As EventHandler

    ' Instance properties
    Public ReadOnly Property InstanceSelectedItems As IReadOnlyCollection(Of IDrawable)
        Get
            Return _selectionManager?.SelectedItems
        End Get
    End Property

    Public ReadOnly Property InstanceHasMultiSelection As Boolean
        Get
            Return _selectionManager?.HasMultipleSelection
        End Get
    End Property

    ' Static properties for backwards compatibility
    Public Shared ReadOnly Property SelectedItems As IReadOnlyCollection(Of IDrawable)
        Get
            Return If(_activeInstance?.InstanceSelectedItems, New List(Of IDrawable)().AsReadOnly())
        End Get
    End Property

    Public Shared ReadOnly Property HasMultiSelection As Boolean
        Get
            Return If(_activeInstance?.InstanceHasMultiSelection, False)
        End Get
    End Property


    Public Shared ReadOnly ChildrenCollectionProperty As DependencyProperty = DependencyProperty.Register(NameOf(ChildrenCollection), GetType(ObservableCollection(Of IDrawable)), GetType(PolyCanvas),
New PropertyMetadata(New ObservableCollection(Of IDrawable), AddressOf OnChildrenCollectionChanged))

    Public Property ChildrenCollection As ObservableCollection(Of IDrawable)
        Get
            Return CType(GetValue(ChildrenCollectionProperty), ObservableCollection(Of IDrawable))
        End Get
        Set(value As ObservableCollection(Of IDrawable))
            SetValue(ChildrenCollectionProperty, value)
        End Set
    End Property


    Private Shared Sub OnChildrenCollectionChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim canvas As PolyCanvas = CType(d, PolyCanvas)
        Dim oldCollection As ObservableCollection(Of IDrawable) = CType(e.OldValue, ObservableCollection(Of IDrawable))
        Dim newCollection As ObservableCollection(Of IDrawable) = CType(e.NewValue, ObservableCollection(Of IDrawable))

        If oldCollection IsNot Nothing Then
            RemoveHandler oldCollection.CollectionChanged, AddressOf canvas.OnCollectionChanged
        End If

        If newCollection IsNot Nothing Then
            AddHandler newCollection.CollectionChanged, AddressOf canvas.OnCollectionChanged
            ' Add existing items to the canvas at their respective indices
            For i As Integer = 0 To newCollection.Count - 1
                canvas.InsertDrawableVisual(newCollection(i), i)
            Next
        End If
    End Sub


    Private Sub OnCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        Select Case e.Action
            Case NotifyCollectionChangedAction.Add
                Dim startIndex As Integer = e.NewStartingIndex
                If startIndex < 0 Then startIndex = -1

                For i As Integer = 0 To e.NewItems.Count - 1
                    Dim newItem As IDrawable = CType(e.NewItems(i), IDrawable)
                    Dim insertIndex As Integer = If(startIndex >= 0, startIndex + i, -1)
                    InsertDrawableVisual(newItem, insertIndex)
                Next
            Case NotifyCollectionChangedAction.Remove
                For Each oldItem As IDrawable In e.OldItems
                    RemoveChild(oldItem)
                    ' If removed item was selected, remove from selection
                    If oldItem.IsSelected Then
                        _selectionManager.DeselectItem(oldItem)
                    End If
                Next
            Case NotifyCollectionChangedAction.Move
                Dim oldIndex As Integer = e.OldStartingIndex
                Dim newIndex As Integer = e.NewStartingIndex
                If oldIndex >= 0 AndAlso oldIndex < Me.Children.Count Then
                    Dim movedVisual As UIElement = Me.Children(oldIndex)
                    Me.Children.RemoveAt(oldIndex)
                    Me.Children.Insert(Math.Min(newIndex, Me.Children.Count), movedVisual)
                End If
            Case NotifyCollectionChangedAction.Reset
                Me.Children.Clear()
                _selectionManager.ClearSelection()
        End Select
    End Sub

    Private Sub AddChild(child As FrameworkElement, Optional parentIDrawable As IDrawable = Nothing, Optional insertIndex As Integer = -1)
        If TypeOf child Is ContentControl Then
            If insertIndex >= 0 AndAlso insertIndex <= Me.Children.Count Then
                Me.Children.Insert(insertIndex, child)
            Else
                Me.Children.Add(child)
            End If
        Else
            Dim style As Style = Nothing
            Try
                style = CType(Me.FindResource("DesignerItemStyle"), Style)
            Catch
                style = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)
            End Try

            'Move wrapper creation to factory but keep interactive wiring here
            Dim wrapper = DrawableWrapperFactory.CreateWrapper(child, parentIDrawable, style)
            If wrapper Is Nothing Then Return


            AddHandler wrapper.PreviewMouseLeftButtonDown, AddressOf OnWrapperPreviewMouseDown
            AddHandler wrapper.MouseLeftButtonDown, AddressOf OnWrapperMouseDown

            If TypeOf child Is TextBox Then
                Dim textBox = CType(child, TextBox)
                AddHandler textBox.GotFocus, AddressOf OnTextBoxFocusChanged
                AddHandler textBox.LostFocus, AddressOf OnTextBoxFocusChanged
                AddHandler textBox.KeyDown, AddressOf OnTextBoxKeyDown
            End If

            If insertIndex >= 0 AndAlso insertIndex <= Me.Children.Count Then
                Me.Children.Insert(insertIndex, wrapper)
            Else
                Me.Children.Add(wrapper)
            End If
            AddHandler wrapper.SizeChanged, AddressOf DesignerItem_SizeChanged
        End If
    End Sub

    Private Sub OnWrapperPreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim wrapper = TryCast(sender, ContentControl)
        If wrapper IsNot Nothing AndAlso TypeOf wrapper.Content Is TextBox Then
            Dim textBox = CType(wrapper.Content, TextBox)

            ' CRITICAL: If TextBox is in edit mode, DON'T call OnWrapperMouseDown
            ' Just return and let the event propagate naturally to the TextBox
            If textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin Then
                Return
            End If
        End If

        ' Only call OnWrapperMouseDown if we're not in TextBox edit mode
        OnWrapperMouseDown(sender, e)
    End Sub

    Private Sub OnWrapperMouseDown(sender As Object, e As MouseButtonEventArgs)
        Dim wrapper = TryCast(sender, ContentControl)
        If wrapper Is Nothing Then
            Return
        End If

        If TypeOf wrapper.Content Is TextBox Then
            Dim textBox = CType(wrapper.Content, TextBox)
            If textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin Then
                Return
            End If
        End If

        e.Handled = True

        ' Click on actual shapes not just the wrapper. 
        Dim point = e.GetPosition(Me)
        Dim drawable = GeometryHitTestHelper.HitTestTopmost(ChildrenCollection, point)

        ' Fall back to the clicked wrapper if there is no underlying drawable (e.g., clicking on a transparent area of a wrapper)
        If drawable Is Nothing Then drawable = FindDrawableForWrapper(wrapper)

        If drawable IsNot Nothing Then
            Dim isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

            If isShiftPressed Then
                _selectionManager.ToggleItem(drawable)
            Else
                _selectionManager.SelectItem(drawable, False)
            End If
        End If
    End Sub

    Private Sub OnTextBoxFocusChanged(sender As Object, e As RoutedEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox Is Nothing Then Return

        Dim wrapper = TryCast(textBox.Parent, ContentControl)
        If wrapper Is Nothing Then Return

        If textBox.IsFocused Then
            ' Entering edit mode - allow dynamic sizing
            wrapper.Width = Double.NaN
            wrapper.Height = Double.NaN
            textBox.Width = Double.NaN
            textBox.Height = Double.NaN
        Else
            ' Exiting edit mode - fix size based on actual rendered size
            Dim rotate = TryCast(wrapper.RenderTransform, RotateTransform)
            Dim preserveCenter As Boolean = rotate IsNot Nothing AndAlso Math.Abs(rotate.Angle) > 0.01
            Dim centerX As Double = 0
            Dim centerY As Double = 0
            If preserveCenter Then
                Dim left = Canvas.GetLeft(wrapper)
                Dim top = Canvas.GetTop(wrapper)
                If Double.IsNaN(left) Then left = 0
                If Double.IsNaN(top) Then top = 0
                centerX = left + wrapper.ActualWidth / 2
                centerY = top + wrapper.ActualHeight / 2
            End If

            wrapper.Width = textBox.ActualWidth
            wrapper.Height = textBox.ActualHeight

            If preserveCenter Then
                Canvas.SetLeft(wrapper, centerX - wrapper.Width / 2)
                Canvas.SetTop(wrapper, centerY - wrapper.Height / 2)
            End If

            ' Update stored original dimensions
            MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))
        End If

        If _transformOverlay IsNot Nothing Then
            _transformOverlay.UpdateGizmoImmediate()
        End If
    End Sub

    Private Sub OnTextBoxKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Escape Then
            Dim textBox = TryCast(sender, TextBox)
            If textBox IsNot Nothing Then
                ' Unfocus the textbox
                Keyboard.ClearFocus()

                ' Clear selection
                _selectionManager.ClearSelection()

                e.Handled = True
            End If
        End If
    End Sub

    Private Function FindDrawableForWrapper(wrapper As ContentControl) As IDrawable
        If ChildrenCollection Is Nothing Then Return Nothing

        For Each drawable In ChildrenCollection
            If drawable?.DrawableElement IsNot Nothing Then
                If drawable.DrawableElement Is wrapper.Content Then
                    Return drawable
                End If
            End If
        Next

        Return Nothing
    End Function

    Private Function FindVisualChild(Of T As Visual)(parent As DependencyObject) As T
        If parent Is Nothing Then Return Nothing

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(parent) - 1
            Dim child = VisualTreeHelper.GetChild(parent, i)
            Dim result = TryCast(child, T)
            If result IsNot Nothing Then
                Return result
            End If

            result = FindVisualChild(Of T)(child)
            If result IsNot Nothing Then
                Return result
            End If
        Next

        Return Nothing
    End Function

    Private Sub AddChild(drawable As IDrawable)
        Dim child As FrameworkElement = drawable.DrawableElement
        AddChild(child, drawable)
    End Sub

    Private Sub AddChild(drawable As IDrawable, Optional insertIndex As Integer = -1)
        Dim child As FrameworkElement = drawable.DrawableElement
        AddChild(child, drawable, insertIndex)
    End Sub

    Private Sub InsertDrawableVisual(drawable As IDrawable, Optional insertIndex As Integer = -1)
        Dim child As FrameworkElement = drawable.DrawableElement
        AddChild(child, drawable, insertIndex)
    End Sub

    Private Sub RemoveChild(child As FrameworkElement)
        If TypeOf child Is ContentControl Then
            Me.Children.Remove(child)
        Else
            Me.Children.Remove(child.Parent)
        End If

    End Sub
    Private Sub RemoveChild(drawable As IDrawable)
        Dim child As FrameworkElement = drawable.DrawableElement
        RemoveChild(child)
    End Sub


    Private Sub PolyCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs)
        ' Only handle if clicking directly on canvas (not on a child wrapper)
        If e.MiddleButton = MouseButtonState.Pressed Then
            ' Middle mouse button pressed - do nothing (reserved for panning)
            Return
        End If
        If e.OriginalSource Is Me OrElse e.OriginalSource Is Me.Background Then
            Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
            If Not isShiftPressed Then
                _selectionManager.ClearSelection()
            End If
            e.Handled = True
        End If
    End Sub

    Public Function IsClickOnCanvasChild(source As DependencyObject) As Boolean
        If source Is Nothing Then Return False
        Dim current = source
        While current IsNot Nothing
            If TypeOf current Is ContentControl Then
                If VisualTreeHelper.GetParent(current) Is Me Then Return True
            End If
            If current Is Me Then Return False
            current = VisualTreeHelper.GetParent(current)
        End While
        Return False
    End Function

    ' Instance methods (prefixed with Instance for clarity)
    Public Sub InstanceClearSelection()
        _selectionManager.ClearSelection()
    End Sub

    Public Sub InstanceAddToSelection(drawable As IDrawable)
        _selectionManager.SelectItem(drawable, True)
    End Sub

    Public Sub InstanceRemoveFromSelection(drawable As IDrawable)
        _selectionManager.DeselectItem(drawable)
    End Sub

    Public Sub InstanceToggleSelection(drawable As IDrawable)
        _selectionManager.ToggleItem(drawable)
    End Sub

    ' Static methods for backwards compatibility
    Public Shared Sub ClearSelection()
        _activeInstance?.InstanceClearSelection()
    End Sub

    Public Shared Sub AddToSelection(drawable As IDrawable)
        _activeInstance?.InstanceAddToSelection(drawable)
    End Sub

    Public Shared Sub RemoveFromSelection(drawable As IDrawable)
        _activeInstance?.InstanceRemoveFromSelection(drawable)
    End Sub

    Public Shared Sub ToggleSelection(drawable As IDrawable)
        _activeInstance?.InstanceToggleSelection(drawable)
    End Sub

    Public Event InstanceSelectionCountChanged As EventHandler

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Friend Shared GridDefinition As GridDefinition

    Public Shared Sub UpdateGridDefinition(gd As GridDefinition)
        GridDefinition = gd
    End Sub

    Public Sub RefreshTextWrapper(wrapper As ContentControl)
        If wrapper Is Nothing Then Return
        If _transformOverlay IsNot Nothing Then
            _transformOverlay.UpdateGizmoImmediate()
        End If
    End Sub




    Private Sub DesignerItem_SizeChanged(sender As Object, e As SizeChangedEventArgs)

        Dim wrapper As ContentControl = CType(sender, ContentControl)
        Dim content As FrameworkElement = CType(wrapper.Content, FrameworkElement)

        If TransformGizmo.HandleTextBoxSizeChanged(wrapper, e) Then
            Return
        End If

        Dim originalDimensions = MetadataHelper.GetOriginalDimensions(wrapper)
        Dim originalEndPoint = MetadataHelper.GetOriginalEndPoint(wrapper)
        If originalDimensions Is Nothing Then Return

        Dim scaleX As Double = e.NewSize.Width / originalDimensions.Value.Width
        Dim scaleY As Double = e.NewSize.Height / originalDimensions.Value.Height

        If TypeOf content Is Line Then
            Dim line As Line = CType(content, Line)
            DrawableWrapperFactory.FitLineToWrapper(line, wrapper, line.StrokeThickness)
            Return
        End If

        InvalidateMeasure()


    End Sub


End Class

Public Structure GridDefinition
    Public Property Spacing As Double
    Public Property InsetLeft As Double
    Public Property InsetTop As Double
    Public Property InsetRight As Double
    Public Property InsetBottom As Double
End Structure
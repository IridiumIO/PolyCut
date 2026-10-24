
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel

Imports PolyCut.Shared


Public Class PolyCanvas : Inherits Controls.Canvas

    Private _transformOverlay As TransformOverlay

    Public Shared ReadOnly SelectionManagerProperty As DependencyProperty = DependencyProperty.Register(
        NameOf(SelectionManager), GetType(SelectionManager), GetType(PolyCanvas),
        New PropertyMetadata(Nothing, AddressOf OnSelectionManagerChanged))

    Public Property SelectionManager As SelectionManager
        Get
            Return DirectCast(GetValue(SelectionManagerProperty), SelectionManager)
        End Get
        Set(value As SelectionManager)
            SetValue(SelectionManagerProperty, value)
        End Set
    End Property

    Private Shared Sub OnSelectionManagerChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim canvas = DirectCast(d, PolyCanvas)
        AddHandler DirectCast(e.NewValue, SelectionManager).SelectionChanged, AddressOf canvas.OnSelectionChanged
    End Sub

    Shared Sub New()
        DefaultStyleKeyProperty.OverrideMetadata(GetType(PolyCanvas), New FrameworkPropertyMetadata(GetType(PolyCanvas)))
    End Sub

    Sub New()
        ' Handle mouse events
        AddHandler Me.MouseDown, AddressOf PolyCanvas_MouseDown
        AddHandler Me.Loaded, AddressOf PolyCanvas_Loaded

        _rasteriser = New PerformantRasteriser(Me)

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
        If SelectionManager Is Nothing Then Return
        ' Host the overlay OUTSIDE the ZoomBorder's transformed layer so the gizmo renders at a constant 1:1 screen size regardless of zoom.
        Dim zoomHost As ZoomBorder = FindAncestorZoomBorder()
        Dim hostPanel As Panel = TryCast(zoomHost?.Parent, Panel)
        If hostPanel Is Nothing Then Return

        _transformOverlay = New TransformOverlay()
        _transformOverlay.Initialize(SelectionManager, Me)

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

    Public Sub CommitActiveText()
        FindAncestorZoomBorder()?.DrawingManager.TextEditor.CommitActiveText(Me)
    End Sub

    Friend Function GetSelectionMarquee() As SelectionMarquee
        If _transformOverlay Is Nothing Then InitializeOverlay()
        Return _transformOverlay?.Marquee
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
        If ChildrenCollection IsNot Nothing AndAlso ChildrenCollection.Count > 0 AndAlso SelectionManager.Count = 0 Then

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
    End Sub

    Private Shared _activeInstance As PolyCanvas
    Public Shared ReadOnly Property ActiveInstance As PolyCanvas
        Get
            Return _activeInstance
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


    Private ReadOnly _rasteriser As PerformantRasteriser

    Friend ReadOnly Property Rasteriser As PerformantRasteriser
        Get
            Return _rasteriser
        End Get
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

        ApplyCollectionChange(e)
        RequestInvalidate()
    End Sub

    Private Sub RequestInvalidate()
        If ModelBatch.Active Then
            ModelBatch.OnEndOnce("CanvasInvalidate", AddressOf Rasteriser.Invalidate)
            Return
        End If

        Rasteriser.Invalidate()
    End Sub

    Private Sub ApplyCollectionChange(e As NotifyCollectionChangedEventArgs)
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
                Dim removed As New List(Of IDrawable)
                For Each oldItem As IDrawable In e.OldItems
                    RemoveChild(oldItem)
                    If oldItem.IsSelected Then removed.Add(oldItem)
                Next

                If removed.Count > 0 Then SelectionManager?.ApplySelection(Nothing, removed)
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
                SelectionManager?.ClearSelection()

                ' AddRange/RemoveRange raise Reset, so the visuals have to be rebuilt from the collection:
                ' clearing alone leaves the model (and the sidebar) correct while the canvas draws nothing.
                Dim source = ChildrenCollection
                If source IsNot Nothing Then
                    For i As Integer = 0 To source.Count - 1
                        InsertDrawableVisual(source(i), i)
                    Next
                End If
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
            Dim wrapper = TryCast(child.Parent, ContentControl)
            If wrapper Is Nothing OrElse wrapper.Content IsNot child Then
                wrapper = DrawableWrapperFactory.CreateWrapper(child, parentIDrawable, style)
            Else
                wrapper.Style = style
                wrapper.IsHitTestVisible = True
            End If
            If wrapper Is Nothing Then Return

            RemoveHandler wrapper.PreviewMouseLeftButtonDown, AddressOf OnWrapperPreviewMouseDown
            RemoveHandler wrapper.MouseLeftButtonDown, AddressOf OnWrapperMouseDown
            AddHandler wrapper.PreviewMouseLeftButtonDown, AddressOf OnWrapperPreviewMouseDown
            AddHandler wrapper.MouseLeftButtonDown, AddressOf OnWrapperMouseDown

            If TypeOf child Is TextBox Then
                Dim textBox = CType(child, TextBox)
                RemoveHandler textBox.GotFocus, AddressOf OnTextBoxFocusChanged
                RemoveHandler textBox.LostFocus, AddressOf OnTextBoxFocusChanged
                RemoveHandler textBox.KeyDown, AddressOf OnTextBoxKeyDown
                AddHandler textBox.GotFocus, AddressOf OnTextBoxFocusChanged
                AddHandler textBox.LostFocus, AddressOf OnTextBoxFocusChanged
                AddHandler textBox.KeyDown, AddressOf OnTextBoxKeyDown
            End If

            If insertIndex >= 0 AndAlso insertIndex <= Me.Children.Count Then
                Me.Children.Insert(insertIndex, wrapper)
            Else
                Me.Children.Add(wrapper)
            End If
            RemoveHandler wrapper.SizeChanged, AddressOf DesignerItem_SizeChanged
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
                SelectionManager.ToggleItem(drawable)
            Else
                SelectionManager.SelectItem(drawable, False)
            End If
        End If
    End Sub

    Private Sub OnTextBoxFocusChanged(sender As Object, e As RoutedEventArgs)
        Dim textBox = TryCast(sender, TextBox)
        If textBox Is Nothing Then Return
        If TextEditHelper.GetIsRestoring(textBox) Then Return

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
            Dim preserveCenter As Boolean = wrapper.RenderTransform IsNot Nothing AndAlso Not wrapper.RenderTransform.Value.IsIdentity
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
                SelectionManager?.ClearSelection()

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


    Private Sub InsertDrawableVisual(drawable As IDrawable, Optional insertIndex As Integer = -1)
        Dim child As FrameworkElement = drawable.DrawableElement
        AddChild(child, drawable, insertIndex)
        Dim notifier = TryCast(drawable, ComponentModel.INotifyPropertyChanged)
        If notifier IsNot Nothing Then AddHandler notifier.PropertyChanged, AddressOf OnDrawablePropertyChanged
    End Sub

    'Hiding something has to take it out of the canvas selection as well: the model's selected set already filters
    'hidden drawables, so leaving it selected here makes the sidebar, the gizmo and the canvas disagree.
    Private Sub OnDrawablePropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If Not String.Equals(e.PropertyName, NameOf(IDrawable.IsHidden)) Then Return

        Dim drawable = TryCast(sender, IDrawable)
        If drawable IsNot Nothing AndAlso drawable.IsHidden Then
            SelectionManager?.ApplySelection(Nothing, {drawable})
        End If
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
        Dim notifier = TryCast(drawable, ComponentModel.INotifyPropertyChanged)
        If notifier IsNot Nothing Then RemoveHandler notifier.PropertyChanged, AddressOf OnDrawablePropertyChanged
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
                SelectionManager?.ClearSelection()
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
        If TextEditHelper.GetIsRestoring(content) Then Return

        If TransformAction.HandleTextBoxSizeChanged(wrapper, e) Then Return

        Dim originalDimensions = MetadataHelper.GetOriginalDimensions(wrapper)
        If originalDimensions Is Nothing Then Return

        Dim scaleX As Double = e.NewSize.Width / originalDimensions.Value.Width
        Dim scaleY As Double = e.NewSize.Height / originalDimensions.Value.Height

        'At creation the wrapper is already at its original size, so every one of them raises this during the first
        'layout pass with an identity scale. Invalidating measure there re-dirties the whole canvas mid-pass.
        If Math.Abs(scaleX - 1.0) < 1.0E-09 AndAlso Math.Abs(scaleY - 1.0) < 1.0E-09 Then Return



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

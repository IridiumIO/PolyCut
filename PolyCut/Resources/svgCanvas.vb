
Imports System.ComponentModel
Imports System.Security.Cryptography
Imports SharpVectors.Converters

Public Class resizableSVGCanvas : Inherits Grid : Implements INotifyPropertyChanged

    Public Shared ReadOnly ScaleProperty As DependencyProperty
    Public Shared ReadOnly SvgCanvasProperty As DependencyProperty
    Public Shared ReadOnly DynamicWidthProperty As DependencyProperty
    Public Shared ReadOnly DynamicHeightProperty As DependencyProperty
    Public Shared ReadOnly IsSelectedProperty As DependencyProperty

    Public Shared Property SelectedControl As resizableSVGCanvas
    Public Property IsSelected As Boolean
        Get
            Return GetValue(IsSelectedProperty)
        End Get
        Set(value As Boolean)
            SetValue(IsSelectedProperty, value)
        End Set
    End Property
    Public ReadOnly Property DynamicWidth As Double
        Get
            Return Me.DesiredSize.Width * Scale
        End Get
    End Property
    Public ReadOnly Property DynamicHeight As Double
        Get
            Return Me.DesiredSize.Height * Scale
        End Get
    End Property
    Public Property SvgCanvas As SvgCanvas
        Get
            Return CType(GetValue(SvgCanvasProperty), SvgCanvas)
        End Get
        Set(value As SvgCanvas)
            SetValue(SvgCanvasProperty, value)

            For Each sc In Me.Children
                If TypeOf (sc) Is SvgCanvas Then
                    Me.Children.Remove(sc)
                End If
            Next

            Me.Children.Add(value)
        End Set
    End Property
    Public Property Scale As Double
        Get
            Return CType(GetValue(ScaleProperty), Double)
        End Get
        Set(ByVal value As Double)
            SetValue(ScaleProperty, value)
            Me.LayoutTransform = New ScaleTransform(value, value)
        End Set
    End Property
    Private Property ZoomBorderScaling As Double = 2
    Private Property ZoomBorder As RichCanvas.ZoomBorder


    Public Shared Event SelectedControlChanged As EventHandler(Of EventArgs)
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private selectionRectangle As Rectangle
    Private thumb As Primitives.Thumb

    Shared Sub New()
        SvgCanvasProperty = DependencyProperty.Register(NameOf(SvgCanvas), GetType(SvgCanvas), GetType(resizableSVGCanvas), New PropertyMetadata(Nothing))
        ScaleProperty = DependencyProperty.Register(NameOf(Scale), GetType(Double), GetType(resizableSVGCanvas), New PropertyMetadata(1.0))
        DynamicWidthProperty = DependencyProperty.Register(NameOf(DynamicWidth), GetType(Double), GetType(resizableSVGCanvas), New PropertyMetadata(Double.NaN))
        DynamicHeightProperty = DependencyProperty.Register(NameOf(DynamicHeight), GetType(Double), GetType(resizableSVGCanvas), New PropertyMetadata(Double.NaN))
        IsSelectedProperty = DependencyProperty.Register(NameOf(IsSelected), GetType(Boolean), GetType(resizableSVGCanvas), New PropertyMetadata(False, AddressOf OnIsSelectedChanged))
    End Sub


    Private Shared Sub OnIsSelectedChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)

        Dim canvas = DirectCast(d, resizableSVGCanvas)
        If e.NewValue = True Then
            canvas.IsSelected = True
            SelectedControl = canvas
        Else
            canvas.IsSelected = False

        End If
        canvas.UpdateAppearance()
    End Sub

    Public Sub New(svgC As SvgCanvas)
        SvgCanvas = svgC
        AddHandler SelectedControlChanged, AddressOf UpdateAppearance
        AddHandler MouseDown, AddressOf Canvas_MouseDown
        InitialiseBorder()
        InitialiseThumb()
    End Sub


    Public Sub SubscribeToZoomBorderScaling(zB As RichCanvas.ZoomBorder)
        ZoomBorder = zB
        AddHandler ZoomBorder.ScaleChanged, AddressOf ZoomBorder_ScaleChanged
    End Sub


    Private Sub ZoomBorder_ScaleChanged(sender As RichCanvas.ZoomBorder, e As RoutedPropertyChangedEventArgs(Of Double))
        ZoomBorderScaling = sender.Scale
        UpdateAppearance()
        'selectionRectangle.StrokeThickness = 2 / Scale / ZoomBorderScaling
    End Sub

    Private Sub Canvas_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If Not e.LeftButton = MouseButtonState.Pressed Then Return
        SelectedControl = Me
        RaiseEvent SelectedControlChanged(Me, EventArgs.Empty)
    End Sub


    Private Sub InitialiseBorder()
        selectionRectangle = New Rectangle With {
            .StrokeDashArray = New DoubleCollection From {3, 3},
            .Stroke = Brushes.LightBlue,
            .StrokeThickness = 1 / Scale,
            .Width = SvgCanvas.ActualWidth,
            .Height = SvgCanvas.ActualHeight,
            .Margin = New Thickness(0)
        }
        Me.Children.Add(selectionRectangle)
    End Sub


    Private Sub InitialiseThumb()
        thumb = New Primitives.Thumb With {
            .Cursor = Cursors.SizeNWSE,
            .Width = 8,
            .Height = 8,
            .HorizontalAlignment = HorizontalAlignment.Right,
            .VerticalAlignment = VerticalAlignment.Bottom,
            .Visibility = Visibility.Collapsed}

        AddHandler thumb.DragDelta, AddressOf Resizing

        Me.Children.Add(thumb)
    End Sub


    Private Sub ResizeBegin()


    End Sub
    Dim _totalScale As Double = 1.0
    Private Sub Resizing(sender As Object, e As Primitives.DragDeltaEventArgs)
        Dim pos As Point = Mouse.GetPosition(Me.Parent)
        Scale = (pos.X - Canvas.GetLeft(Me)) / ActualWidth
        UpdateAppearance()
    End Sub

    Public Sub UpdateAppearance()

        IsSelected = SelectedControl Is Me

        If IsSelected Then
            selectionRectangle.Width = SvgCanvas.ActualWidth
            selectionRectangle.Height = SvgCanvas.ActualHeight
            selectionRectangle.StrokeThickness = 2 / Scale / ZoomBorderScaling
            selectionRectangle.Visibility = Visibility.Visible
            thumb.LayoutTransform = New ScaleTransform(1 / Scale / ZoomBorderScaling, 1 / Scale / ZoomBorderScaling)
            thumb.Visibility = Visibility.Visible
            Panel.SetZIndex(Me, 2)
        Else
            selectionRectangle.Visibility = Visibility.Collapsed
            thumb.Visibility = Visibility.Collapsed
            Panel.SetZIndex(Me, 0)
        End If
    End Sub

    Public Shared Sub DeSelectAll()
        SelectedControl = Nothing
        RaiseEvent SelectedControlChanged(Nothing, EventArgs.Empty)
    End Sub

    Private Shared Function FindAncestor(Of T As Class)(dependencyObject As DependencyObject) As T
        Dim parent = VisualTreeHelper.GetParent(dependencyObject)

        While parent IsNot Nothing
            Dim typedParent = TryCast(parent, T)
            If typedParent IsNot Nothing Then
                Return typedParent
            End If

            parent = VisualTreeHelper.GetParent(parent)
        End While

        Return Nothing
    End Function




End Class

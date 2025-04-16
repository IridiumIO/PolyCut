
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared


Public Class PolyCanvas : Inherits Controls.Canvas



    Shared Sub New()
        DefaultStyleKeyProperty.OverrideMetadata(GetType(PolyCanvas), New FrameworkPropertyMetadata(GetType(PolyCanvas)))
    End Sub

    Sub New()
        AddHandler Me.MouseDown, AddressOf PolyCanvas_MouseDown
    End Sub


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
            ' Add existing items to the canvas
            For Each item In newCollection
                canvas.AddChild(item)
            Next
        End If
    End Sub


    Private Sub OnCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        Select Case e.Action
            Case NotifyCollectionChangedAction.Add
                For Each newItem As IDrawable In e.NewItems
                    AddChild(newItem)
                Next
            Case NotifyCollectionChangedAction.Remove
                For Each oldItem As IDrawable In e.OldItems
                    RemoveChild(oldItem)
                Next

            Case NotifyCollectionChangedAction.Reset
                Me.Children.Clear()
        End Select
    End Sub

    Private Sub AddChild(child As FrameworkElement, Optional parentIDrawable As IDrawable = Nothing)
        If TypeOf child Is ContentControl Then
            Me.Children.Add(child)
        Else

            ' Wrap it in a ContentControl
            Dim wrapper As New ContentControl With {
                    .Content = child,
                    .Width = If(Not Double.IsNaN(child.Width), child.Width, child.ActualWidth),
                    .Height = If(Not Double.IsNaN(child.Height), child.Height, child.ActualHeight)
                }
            If TypeOf child Is Line Then
                Dim line As Line = CType(child, Line)
                wrapper.Width = Math.Abs(line.X2 - line.X1) + (line.StrokeThickness)
                wrapper.Height = Math.Abs(line.Y2 - line.Y1) + (line.StrokeThickness)
                MetadataHelper.SetOriginalEndPoint(wrapper, New Point(line.X2, line.Y2))
            ElseIf TypeOf child Is Path Then
                Dim path As Path = CType(child, Path)
                path.Stretch = Stretch.Fill
            End If

            wrapper.Tag = (wrapper.Width, wrapper.Height, If(TypeOf child Is Line, New Point(CType(child, Line).X2, CType(child, Line).Y2), Nothing))
            ' Use attached properties for metadata instead of Tag
            MetadataHelper.SetOriginalDimensions(wrapper, (wrapper.Width, wrapper.Height))


            wrapper.ClipToBounds = False
            ' Bind the child's Width and Height to the wrapper's Width and Height

            child.HorizontalAlignment = HorizontalAlignment.Stretch


            child.Width = Double.NaN
            child.Height = Double.NaN
            Canvas.SetLeft(wrapper, If(Double.IsNaN(Canvas.GetLeft(child)), 0, Canvas.GetLeft(child)))
            Canvas.SetTop(wrapper, If(Double.IsNaN(Canvas.GetTop(child)), 0, Canvas.GetTop(child)))
            wrapper.Style = CType(Me.FindResource("DesignerItemStyle"), Style)
            ' Add the wrapper to the canvas
            Me.Children.Add(wrapper)
            AddHandler wrapper.SizeChanged, AddressOf DesignerItem_SizeChanged
        End If
    End Sub

    Private Sub AddChild(drawable As IDrawable)
        Dim child As FrameworkElement = drawable.DrawableElement
        AddChild(child, drawable)
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
        DesignerItemDecorator.CurrentSelected = Nothing
    End Sub



    Private Sub DesignerItem_SizeChanged(sender As Object, e As SizeChangedEventArgs)

        Dim wrapper As ContentControl = CType(sender, ContentControl)
        Dim content As FrameworkElement = CType(wrapper.Content, FrameworkElement)

        Dim originalDimensions = MetadataHelper.GetOriginalDimensions(wrapper)
        Dim originalEndPoint = MetadataHelper.GetOriginalEndPoint(wrapper)
        If originalDimensions Is Nothing Then Return

        Dim scaleX As Double = e.NewSize.Width / originalDimensions.Value.Width
        Dim scaleY As Double = e.NewSize.Height / originalDimensions.Value.Height

        If TypeOf content Is Line AndAlso originalEndPoint.HasValue Then
            Dim line As Line = CType(content, Line)

            line.X2 = originalEndPoint.Value.X * scaleX
            line.Y2 = originalEndPoint.Value.Y * scaleY

            ' Adjust Y1 if necessary to maintain the correct orientation
            If line.Y1 > line.Y2 Then
                line.Y1 = wrapper.Height - line.StrokeThickness / 2
            End If

        End If

        InvalidateMeasure()


    End Sub

End Class

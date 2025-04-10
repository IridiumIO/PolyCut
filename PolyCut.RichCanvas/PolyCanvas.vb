﻿
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives


Public Class PolyCanvas : Inherits Controls.Canvas



    Shared Sub New()
        DefaultStyleKeyProperty.OverrideMetadata(GetType(PolyCanvas), New FrameworkPropertyMetadata(GetType(PolyCanvas)))
    End Sub

    Sub New()
        AddHandler Me.MouseDown, AddressOf PolyCanvas_MouseDown
    End Sub


    Public Shared ReadOnly ChildrenCollectionProperty As DependencyProperty = DependencyProperty.Register(NameOf(ChildrenCollection), GetType(ObservableCollection(Of FrameworkElement)), GetType(PolyCanvas),
New PropertyMetadata(New ObservableCollection(Of FrameworkElement), AddressOf OnChildrenCollectionChanged))

    Public Property ChildrenCollection As ObservableCollection(Of FrameworkElement)
        Get
            Return CType(GetValue(ChildrenCollectionProperty), ObservableCollection(Of FrameworkElement))
        End Get
        Set(value As ObservableCollection(Of FrameworkElement))
            SetValue(ChildrenCollectionProperty, value)
        End Set
    End Property


    Private Shared Sub OnChildrenCollectionChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim canvas As PolyCanvas = CType(d, PolyCanvas)
        Dim oldCollection As ObservableCollection(Of FrameworkElement) = CType(e.OldValue, ObservableCollection(Of FrameworkElement))
        Dim newCollection As ObservableCollection(Of FrameworkElement) = CType(e.NewValue, ObservableCollection(Of FrameworkElement))

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
                For Each newItem As FrameworkElement In e.NewItems
                    AddChild(newItem)
                Next
            Case NotifyCollectionChangedAction.Remove
                For Each oldItem As FrameworkElement In e.OldItems
                    RemoveChild(oldItem)
                Next
            Case NotifyCollectionChangedAction.Reset
                Me.Children.Clear()
        End Select
    End Sub

    Private Sub AddChild(child As FrameworkElement)
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
            End If

            wrapper.Tag = (wrapper.Width, wrapper.Height, If(TypeOf child Is Line, New Point(CType(child, Line).X2, CType(child, Line).Y2), Nothing))


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

    Private Sub RemoveChild(child As FrameworkElement)
        If TypeOf child Is ContentControl Then
            Me.Children.Remove(child)
        Else
            Me.Children.Remove(child.Parent)
        End If

    End Sub


    Private Sub PolyCanvas_MouseDown(sender As Object, e As MouseButtonEventArgs)
        For Each child In Me.Children
            Selector.SetIsSelected(child, False)
        Next
        DesignerItemDecorator.CurrentSelected = Nothing
    End Sub



    Private Sub DesignerItem_SizeChanged(sender As Object, e As SizeChangedEventArgs)

        Dim wrapper As ContentControl = CType(sender, ContentControl)

        Dim content As FrameworkElement = CType(wrapper.Content, FrameworkElement)

        Dim originalWidth As Double = CType(wrapper.Tag, (Double, Double, Point)).Item1
        Dim originalHeight As Double = CType(wrapper.Tag, (Double, Double, Point)).Item2
        Dim originalEndPoint As Point? = CType(wrapper.Tag, (Double, Double, Point)).Item3



        Dim scaleX As Double = e.NewSize.Width / originalWidth
        Dim scaleY As Double = e.NewSize.Height / originalHeight

        If TypeOf content Is Line AndAlso originalEndPoint.HasValue Then
            Dim line As Line = CType(content, Line)
            'line.X2 = (originalEndPoint.Value.X + line.StrokeThickness / 2) * scaleX
            'line.Y2 = (originalEndPoint.Value.Y + line.StrokeThickness / 2) * scaleY

            line.X2 = wrapper.Width - line.StrokeThickness / 2
            If line.Y1 < line.Y2 Then
                line.Y2 = wrapper.Height - line.StrokeThickness / 2
            Else
                line.Y1 = wrapper.Height - line.StrokeThickness / 2
            End If

        End If

        InvalidateMeasure()



        'Dim wrapper As ContentControl = CType(sender, ContentControl)

        'Dim originalWidth As Double = CType(wrapper.Tag, (Double, Double)).Item1
        'Dim originalHeight As Double = CType(wrapper.Tag, (Double, Double)).Item2

        '' Calculate the scale factors based on the original dimensions
        'Dim scaleX As Double = e.NewSize.Width / originalWidth
        'Dim scaleY As Double = e.NewSize.Height / originalHeight

        '' Reset the previous transform before applying a new one to avoid accumulative scaling
        'Dim content As FrameworkElement = CType(wrapper.Content, FrameworkElement)
        'If TypeOf content.RenderTransform Is ScaleTransform Then
        '    Dim currentTransform As ScaleTransform = CType(content.RenderTransform, ScaleTransform)
        '    scaleX = scaleX / currentTransform.ScaleX
        '    scaleY = scaleY / currentTransform.ScaleY
        'End If

        '' Apply the new scale transform
        'content.RenderTransform = New ScaleTransform(scaleX, scaleY)

        'InvalidateMeasure()
        'wrapper.Content.InvalidateMeasure()

    End Sub

End Class

Imports System.Windows.Controls.Primitives

Public Class ResizeThumb
    Inherits Thumb

    Private rotateTransform As RotateTransform
    Private angle As Double
    Private adorner As Adorner
    Private transformOrigin As Point
    Private designerItem As ContentControl
    Private canvas As Controls.Canvas
    Private startingAspectRatio As Double

    Public Sub New()
        AddHandler DragStarted, AddressOf Me.ResizeThumb_DragStarted
        AddHandler DragDelta, AddressOf Me.ResizeThumb_DragDelta
        AddHandler DragCompleted, AddressOf Me.ResizeThumb_DragCompleted
    End Sub

    Private Sub ResizeThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        Me.designerItem = TryCast(DataContext, ContentControl)

        If Me.designerItem IsNot Nothing Then
            Me.canvas = TryCast(VisualTreeHelper.GetParent(Me.designerItem), Controls.Canvas)

            If Me.canvas IsNot Nothing Then
                Me.transformOrigin = Me.designerItem.RenderTransformOrigin
                Me.startingAspectRatio = Me.designerItem.ActualWidth / Me.designerItem.ActualHeight
                Me.rotateTransform = TryCast(Me.designerItem.RenderTransform, RotateTransform)
                If Me.rotateTransform IsNot Nothing Then
                    Me.angle = Me.rotateTransform.Angle * Math.PI / 180.0
                Else
                    Me.angle = 0.0
                End If

                Dim adornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me.canvas)
                If adornerLayer IsNot Nothing Then
                    Me.adorner = New SizeAdorner(Me.designerItem)
                    adornerLayer.Add(Me.adorner)
                End If
            End If
        End If
    End Sub

    Private Sub ResizeThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
        If Me.designerItem Is Nothing Then Return

        Dim deltaVertical As Double, deltaHorizontal As Double

        Dim resizingFromCorner As Boolean = (VerticalAlignment = VerticalAlignment.Top Or VerticalAlignment = VerticalAlignment.Bottom) AndAlso
                                            (HorizontalAlignment = HorizontalAlignment.Left Or HorizontalAlignment = HorizontalAlignment.Right)


        If resizingFromCorner Then

            Me.designerItem.Width = Me.designerItem.Height * startingAspectRatio
            deltaVertical = Math.Min(If(VerticalAlignment = VerticalAlignment.Bottom, -e.VerticalChange, e.VerticalChange), Me.designerItem.ActualHeight - Me.designerItem.MinHeight)
            deltaHorizontal = Math.Min(deltaVertical * startingAspectRatio, Me.designerItem.ActualWidth - Me.designerItem.MinWidth)

            If ResizingWouldExceedMinimumSize(deltaVertical, deltaHorizontal) Then Return

            Dim newTopX As Double = Canvas.GetTop(Me.designerItem) + GetCanvasTopOffset(VerticalAlignment, deltaVertical) + GetCanvasTopOffset(HorizontalAlignment, deltaHorizontal)
            Dim newLeftX As Double = Canvas.GetLeft(Me.designerItem) + GetCanvasLeftOffset(VerticalAlignment, deltaVertical) + GetCanvasLeftOffset(HorizontalAlignment, deltaHorizontal)

            UpdateDesignerItem(deltaVertical, deltaHorizontal, newTopX, newLeftX)


            e.Handled = True
            Return

        End If


        deltaVertical = Math.Min(If(VerticalAlignment = VerticalAlignment.Bottom, -e.VerticalChange, e.VerticalChange), Me.designerItem.ActualHeight - Me.designerItem.MinHeight)
        deltaHorizontal = Math.Min(If(HorizontalAlignment = HorizontalAlignment.Right, -e.HorizontalChange, e.HorizontalChange), Me.designerItem.ActualWidth - Me.designerItem.MinWidth)

        Dim newTop As Double = Canvas.GetTop(Me.designerItem) + GetCanvasTopOffset(VerticalAlignment, deltaVertical) + GetCanvasTopOffset(HorizontalAlignment, deltaHorizontal)
        Dim newLeft As Double = Canvas.GetLeft(Me.designerItem) + GetCanvasLeftOffset(VerticalAlignment, deltaVertical) + GetCanvasLeftOffset(HorizontalAlignment, deltaHorizontal)

        If VerticalAlignment <> VerticalAlignment.Top AndAlso VerticalAlignment <> VerticalAlignment.Bottom Then deltaVertical = 0
        If HorizontalAlignment <> HorizontalAlignment.Left AndAlso HorizontalAlignment <> HorizontalAlignment.Right Then deltaHorizontal = 0

        UpdateDesignerItem(deltaVertical, deltaHorizontal, newTop, newLeft)

        e.Handled = True
    End Sub


    Private Function GetCanvasTopOffset(ThumbVerticalAlignment As VerticalAlignment, deltaVertical As Double) As Double
        Select Case ThumbVerticalAlignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Cos(-Me.angle) + (Me.transformOrigin.Y * deltaVertical * (1 - Math.Cos(-Me.angle)))
            Case VerticalAlignment.Bottom
                Return Me.transformOrigin.Y * deltaVertical * (1 - Math.Cos(-Me.angle))
            Case Else
                Return Nothing
        End Select
    End Function

    Private Function GetCanvasTopOffset(ThumbHorizontalAlignment As HorizontalAlignment, deltaHorizontal As Double) As Double
        Select Case ThumbHorizontalAlignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Sin(Me.angle) - Me.transformOrigin.X * deltaHorizontal * Math.Sin(Me.angle)
            Case HorizontalAlignment.Right
                Return -Me.transformOrigin.X * deltaHorizontal * Math.Sin(Me.angle)
            Case Else
                Return Nothing
        End Select
    End Function

    Private Function GetCanvasLeftOffset(ThumbVerticalAlignment As VerticalAlignment, deltaVertical As Double) As Double
        Select Case ThumbVerticalAlignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Sin(-Me.angle) - (Me.transformOrigin.Y * deltaVertical * Math.Sin(-Me.angle))
            Case VerticalAlignment.Bottom
                Return -deltaVertical * Me.transformOrigin.Y * Math.Sin(-Me.angle)
            Case Else
                Return Nothing
        End Select
    End Function

    Private Function GetCanvasLeftOffset(ThumbHorizontalAlignment As HorizontalAlignment, deltaHorizontal As Double) As Double
        Select Case ThumbHorizontalAlignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Cos(Me.angle) + (Me.transformOrigin.X * deltaHorizontal * (1 - Math.Cos(Me.angle)))
            Case HorizontalAlignment.Right
                Return deltaHorizontal * Me.transformOrigin.X * (1 - Math.Cos(Me.angle))
            Case Else
                Return Nothing
        End Select
    End Function



    Private Sub UpdateDesignerItem(deltaVertical, deltaHorizontal, newTop, newLeft)
        Me.designerItem.Height -= deltaVertical
        Me.designerItem.Width -= deltaHorizontal

        Canvas.SetTop(Me.designerItem, newTop)
        Canvas.SetLeft(Me.designerItem, newLeft)

        Dim dc = CType(DataContext, ContentControl)



    End Sub

    Private Function ResizingWouldExceedMinimumSize(deltaVertical As Double, deltaHorizontal As Double) As Boolean
        Return Me.designerItem.ActualHeight - deltaVertical <= Me.designerItem.MinHeight OrElse Me.designerItem.ActualWidth - deltaHorizontal <= Me.designerItem.MinWidth
    End Function

    Private Sub ResizeThumb_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        If Me.adorner IsNot Nothing Then
            Dim adornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me.canvas)
            If adornerLayer IsNot Nothing Then
                adornerLayer.Remove(Me.adorner)
            End If

            Me.adorner = Nothing
        End If
    End Sub
End Class
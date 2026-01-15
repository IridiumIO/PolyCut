Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared

Public Class ResizeThumb
    Inherits Thumb


    Private rotateTransform As RotateTransform
    Private angle As Double
    Private adorner As Adorner
    Private transformOrigin As Point
    Private designerItem As ContentControl
    Private canvas As Controls.Canvas
    Private startingAspectRatio As Double


    Private _multiSelectItems As IReadOnlyList(Of IDrawable)
    Private _multiSelectInitialBounds As Rect
    Private _multiSelectInitialBaseWidths As New Dictionary(Of IDrawable, Double)
    Private _multiSelectInitialBaseHeights As New Dictionary(Of IDrawable, Double)
    Private _multiSelectInitialPositions As New Dictionary(Of IDrawable, Point)
    Private _multiSelectInitialAngles As New Dictionary(Of IDrawable, Double)
    Private _multiSelectCumulativeChangeX As Double = 0
    Private _multiSelectCumulativeChangeY As Double = 0

    Public Sub New()

        AddHandler DragStarted, AddressOf Me.ResizeThumb_DragStarted
        AddHandler DragDelta, AddressOf Me.ResizeThumb_DragDelta
        AddHandler DragCompleted, AddressOf Me.ResizeThumb_DragCompleted
    End Sub

    Private Sub ResizeThumb_DragStarted(sender As Object, e As DragStartedEventArgs)
        ' Check for multi-select adorner
        Dim multiAdorner = TryCast(DataContext, MultiSelectAdorner)
        If multiAdorner IsNot Nothing Then
            HandleMultiSelectResizeStart(multiAdorner)
            Return
        End If

        ' Single item mode
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

    Private Sub HandleMultiSelectResizeStart(adorner As MultiSelectAdorner)
        _multiSelectItems = adorner.SelectedItems
        _multiSelectInitialBounds = adorner.GetSelectedItemsBounds().GetValueOrDefault()
        _multiSelectInitialBaseWidths.Clear()
        _multiSelectInitialBaseHeights.Clear()
        _multiSelectInitialPositions.Clear()
        _multiSelectInitialAngles.Clear()
        _multiSelectCumulativeChangeX = 0
        _multiSelectCumulativeChangeY = 0

        ' Store initial dimensions, positions, and rotations
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim baseWidth = wrapper.ActualWidth
                    Dim baseHeight = wrapper.ActualHeight

                    ' Extract current rotation angle
                    Dim currentAngle As Double = 0
                    Dim existingTransform = wrapper.RenderTransform

                    If TypeOf existingTransform Is RotateTransform Then
                        currentAngle = CType(existingTransform, RotateTransform).Angle
                    ElseIf TypeOf existingTransform Is TransformGroup Then
                        For Each t In CType(existingTransform, TransformGroup).Children
                            If TypeOf t Is RotateTransform Then
                                currentAngle = CType(t, RotateTransform).Angle
                                Exit For
                            End If
                        Next
                    End If

                    _multiSelectInitialBaseWidths(drawable) = baseWidth
                    _multiSelectInitialBaseHeights(drawable) = baseHeight
                    _multiSelectInitialPositions(drawable) = New Point(Canvas.GetLeft(wrapper), Canvas.GetTop(wrapper))
                    _multiSelectInitialAngles(drawable) = currentAngle
                End If
            End If
        Next
    End Sub

    Private Sub ResizeThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)

        ' Check for multi-select mode
        If _multiSelectItems IsNot Nothing AndAlso _multiSelectItems.Count > 0 Then
            HandleMultiSelectResize(e)
            Return
        End If

        ' Single item mode (existing logic)
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

    Private Sub HandleMultiSelectResize(e As DragDeltaEventArgs)
        _multiSelectCumulativeChangeX += e.HorizontalChange
        _multiSelectCumulativeChangeY += e.VerticalChange

        ' Calculate scale factors
        Dim scaleX As Double = 1.0
        Dim scaleY As Double = 1.0
        Dim anchorX As Double = _multiSelectInitialBounds.Left
        Dim anchorY As Double = _multiSelectInitialBounds.Top

        If HorizontalAlignment = HorizontalAlignment.Right Then
            scaleX = (_multiSelectInitialBounds.Width + _multiSelectCumulativeChangeX) / _multiSelectInitialBounds.Width
            anchorX = _multiSelectInitialBounds.Left
        ElseIf HorizontalAlignment = HorizontalAlignment.Left Then
            scaleX = (_multiSelectInitialBounds.Width - _multiSelectCumulativeChangeX) / _multiSelectInitialBounds.Width
            anchorX = _multiSelectInitialBounds.Right
        End If

        If VerticalAlignment = VerticalAlignment.Bottom Then
            scaleY = (_multiSelectInitialBounds.Height + _multiSelectCumulativeChangeY) / _multiSelectInitialBounds.Height
            anchorY = _multiSelectInitialBounds.Top
        ElseIf VerticalAlignment = VerticalAlignment.Top Then
            scaleY = (_multiSelectInitialBounds.Height - _multiSelectCumulativeChangeY) / _multiSelectInitialBounds.Height
            anchorY = _multiSelectInitialBounds.Bottom
        End If

        ' Uniform scaling from corners
        Dim isCorner = (HorizontalAlignment = HorizontalAlignment.Left OrElse HorizontalAlignment = HorizontalAlignment.Right) AndAlso
                       (VerticalAlignment = VerticalAlignment.Top OrElse VerticalAlignment = VerticalAlignment.Bottom)
        If isCorner Then
            Dim avgScale = (scaleX + scaleY) / 2
            scaleX = avgScale
            scaleY = avgScale
        End If

        If scaleX <= 0.01 Then scaleX = 0.01
        If scaleY <= 0.01 Then scaleY = 0.01

        ' Apply scaling to each object
        For Each drawable In _multiSelectItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing AndAlso _multiSelectInitialBaseWidths.ContainsKey(drawable) AndAlso _multiSelectInitialBaseHeights.ContainsKey(drawable) AndAlso _multiSelectInitialPositions.ContainsKey(drawable) AndAlso _multiSelectInitialAngles.ContainsKey(drawable) Then

                    Dim baseWidth = _multiSelectInitialBaseWidths(drawable)
                    Dim baseHeight = _multiSelectInitialBaseHeights(drawable)
                    Dim initialPos = _multiSelectInitialPositions(drawable)
                    Dim rotation = _multiSelectInitialAngles(drawable)

                    ' Scale Width/Height
                    wrapper.Width = baseWidth * scaleX
                    wrapper.Height = baseHeight * scaleY

                    ' Scale position
                    Dim offsetX = initialPos.X - anchorX
                    Dim offsetY = initialPos.Y - anchorY
                    Canvas.SetLeft(wrapper, anchorX + (offsetX * scaleX))
                    Canvas.SetTop(wrapper, anchorY + (offsetY * scaleY))

                    ' Maintain rotation
                    If Math.Abs(rotation) > 0.01 Then
                        wrapper.RenderTransform = New RotateTransform(rotation)
                    Else
                        wrapper.RenderTransform = Nothing
                    End If
                End If
            End If
        Next

        Dim adorner = TryCast(DataContext, MultiSelectAdorner)
        adorner?.InvalidateArrange()
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
        ' Clean up multi-select state
        _multiSelectItems = Nothing
        _multiSelectInitialBaseWidths.Clear()
        _multiSelectInitialBaseHeights.Clear()
        _multiSelectInitialPositions.Clear()
        _multiSelectInitialAngles.Clear()
        _multiSelectCumulativeChangeX = 0
        _multiSelectCumulativeChangeY = 0

        ' Clean up adorner
        If Me.adorner IsNot Nothing Then
            Dim adornerLayer As AdornerLayer = AdornerLayer.GetAdornerLayer(Me.canvas)
            If adornerLayer IsNot Nothing Then
                adornerLayer.Remove(Me.adorner)
            End If

            Me.adorner = Nothing
        End If
    End Sub

End Class

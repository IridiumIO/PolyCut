Imports PolyCut.Shared

''' <summary>
''' Virtual adorner that shows around multiple selected items WITHOUT moving them.
''' Items stay in their original positions on the canvas.
''' Transforms applied to this adorner are propagated to all selected items. In theory. I can't get the fucking rotates to work properly.
''' </summary>
Public Class MultiSelectAdorner : Inherits Adorner

    Private ReadOnly visuals As VisualCollection
    Public chrome As ResizeRotateChrome
    Private ReadOnly _selectedItems As IReadOnlyList(Of IDrawable)
    Private ReadOnly _canvas As Canvas

    Public Sub New(canvas As Canvas, selectedItems As IReadOnlyList(Of IDrawable))
        MyBase.New(canvas)
        _canvas = canvas
        _selectedItems = selectedItems


        Me.chrome = New ResizeRotateChrome With {
            .DataContext = Me
        }

        Me.visuals = New VisualCollection(Me) From {
            Me.chrome
        }

        AddHandler Me.MouseWheel, AddressOf MultiSelectAdorner_MouseWheel

        ' Subscribe to scale changes to keep adorner size consistent
        EventAggregator.Subscribe(AddressOf OnScaleChanged)

        ' Initialize with current scale immediately
        Dim currentScale = ScaleChangedMessage.LastScale
        chrome.OnScaleChanged(New ScaleChangedMessage(currentScale))
    End Sub


    Private Sub OnScaleChanged(message As Object)
        If TypeOf message IsNot ScaleChangedMessage Then Return
        chrome?.OnScaleChanged(message)
    End Sub

    Private Sub MultiSelectAdorner_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        e.Handled = False
    End Sub

    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return visuals.Count
        End Get
    End Property

    Protected Overrides Function GetVisualChild(index As Integer) As Visual
        Return visuals(index)
    End Function

    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size

        Dim bounds = GetSelectedItemsBounds()

        If bounds.HasValue Then
            chrome.Arrange(bounds.Value)
        End If

        Return finalSize
    End Function


    Public Function GetSelectedItemsBounds() As Rect?
        If _selectedItems Is Nothing OrElse _selectedItems.Count = 0 Then Return Nothing

        Dim minX As Double = Double.MaxValue
        Dim minY As Double = Double.MaxValue
        Dim maxX As Double = Double.MinValue
        Dim maxY As Double = Double.MinValue

        For Each drawable In _selectedItems
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    ' Get the item's bounds accounting for rotation
                    Dim itemBounds = GetTransformedBounds(wrapper)

                    minX = Math.Min(minX, itemBounds.Left)
                    minY = Math.Min(minY, itemBounds.Top)
                    maxX = Math.Max(maxX, itemBounds.Right)
                    maxY = Math.Max(maxY, itemBounds.Bottom)
                End If
            End If
        Next

        If minX = Double.MaxValue Then Return Nothing

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function


    Private Function GetTransformedBounds(wrapper As ContentControl) As Rect
        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        Dim width = wrapper.ActualWidth
        Dim height = wrapper.ActualHeight


        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform Is Nothing OrElse Math.Abs(rotateTransform.Angle) < 0.01 Then
            ' No rotation - simple bounds
            Return New Rect(left, top, width, height)
        End If

        ' Item is rotated- need to transform all four corners
        Dim angle = rotateTransform.Angle * Math.PI / 180.0
        Dim cosAngle = Math.Cos(angle)
        Dim sinAngle = Math.Sin(angle)


        Dim centerX = left + width * wrapper.RenderTransformOrigin.X
        Dim centerY = top + height * wrapper.RenderTransformOrigin.Y

        ' Four corners of the unrotated item (relative to top-left)
        Dim corners() As Point = {
            New Point(0, 0),                    ' Top-left
            New Point(width, 0),                ' Top-right
            New Point(0, height),               ' Bottom-left
            New Point(width, height)            ' Bottom-right
        }

        ' Transform each corner and find min/max
        Dim minX = Double.MaxValue
        Dim minY = Double.MaxValue
        Dim maxX = Double.MinValue
        Dim maxY = Double.MinValue

        For Each corner In corners
            ' Corner position in canvas space (before rotation)
            Dim cx = left + corner.X
            Dim cy = top + corner.Y


            Dim offsetX = cx - centerX
            Dim offsetY = cy - centerY

            ' Rotate around center
            Dim rotatedX = centerX + (offsetX * cosAngle - offsetY * sinAngle)
            Dim rotatedY = centerY + (offsetX * sinAngle + offsetY * cosAngle)

            ' Track min/max
            minX = Math.Min(minX, rotatedX)
            minY = Math.Min(minY, rotatedY)
            maxX = Math.Max(maxX, rotatedX)
            maxY = Math.Max(maxY, rotatedY)
        Next

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function


    Public ReadOnly Property SelectedItems As IReadOnlyList(Of IDrawable)
        Get
            Return _selectedItems
        End Get
    End Property

End Class






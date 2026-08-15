Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Class TransformAction
    Implements IUndoableAction

    Public Class Snapshot
        Public Property Left As Double
        Public Property Top As Double
        Public Property Width As Double
        Public Property Height As Double
        Public Property RenderTransform As Transform
    End Class

    Private ReadOnly _items As New List(Of (Target As IDrawable, Before As Snapshot, After As Snapshot))

    Public Sub New(items As IEnumerable(Of (IDrawable, Snapshot, Snapshot)))
        If items IsNot Nothing Then
            _items.AddRange(items)
        End If
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Transform ({_items.Count} items)"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        Return True
    End Function

    Private Sub Apply(snap As Snapshot, target As IDrawable)
        If snap Is Nothing OrElse target Is Nothing OrElse target.DrawableElement Is Nothing Then Return
        Dim wrapper = TryCast(target.DrawableElement.Parent, ContentControl)
        If wrapper Is Nothing Then Return

        Canvas.SetLeft(wrapper, snap.Left)
        Canvas.SetTop(wrapper, snap.Top)
        wrapper.Width = snap.Width
        wrapper.Height = snap.Height
        wrapper.RenderTransform = snap.RenderTransform
    End Sub

    Public Sub Undo() Implements IUndoableAction.Undo
        For Each t In _items
            Apply(t.Before, t.Target)
        Next
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        For Each t In _items
            Apply(t.After, t.Target)
        Next
    End Sub

    Public Shared Function MakeSnapshotFromWrapper(wrapper As ContentControl) As Snapshot
        If wrapper Is Nothing Then Return Nothing
        Dim state = TransformState.FromWrapper(wrapper)
        Return New Snapshot With {
            .Left = state.Translation.X,
            .Top = state.Translation.Y,
            .Width = state.Width,
            .Height = state.Height,
            .RenderTransform = If(wrapper.RenderTransform, Nothing)
        }
    End Function


    ' ====================
    ' Transform Operations
    ' ====================

    Public Shared Sub ApplyRotation(wrapper As ContentControl, centerPoint As Point, initialRotation As Double, angle As Double, initialPosition As Point)
        If wrapper Is Nothing Then Return

        wrapper.RenderTransform = New RotateTransform(initialRotation + angle)

        Dim initialItemCenter = New Point(
            initialPosition.X + wrapper.ActualWidth * wrapper.RenderTransformOrigin.X,
            initialPosition.Y + wrapper.ActualHeight * wrapper.RenderTransformOrigin.Y)

        Dim offsetFromCenter = Point.Subtract(initialItemCenter, centerPoint)
        Dim angleRad = angle * Math.PI / 180
        Dim cosA = Math.Cos(angleRad)
        Dim sinA = Math.Sin(angleRad)

        Dim rotatedOffset = New Point(
            offsetFromCenter.X * cosA - offsetFromCenter.Y * sinA,
            offsetFromCenter.X * sinA + offsetFromCenter.Y * cosA)

        Dim newItemCenter = Point.Add(centerPoint, CType(rotatedOffset, Vector))
        Canvas.SetLeft(wrapper, newItemCenter.X - wrapper.ActualWidth * wrapper.RenderTransformOrigin.X)
        Canvas.SetTop(wrapper, newItemCenter.Y - wrapper.ActualHeight * wrapper.RenderTransformOrigin.Y)
    End Sub


    Public Shared Sub ApplyMove(wrapper As ContentControl, deltaX As Double, deltaY As Double)
        If wrapper Is Nothing Then Return
        Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + deltaX)
        Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + deltaY)
    End Sub


    Public Shared Sub ApplyResizeSingle(wrapper As ContentControl, handleName As String, deltaX As Double, deltaY As Double)
        If wrapper Is Nothing Then Return

        Dim angle As Double = 0
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then angle = rotateTransform.Angle


        Dim angleRad = angle * Math.PI / 180.0
        Dim cosA = Math.Cos(-angleRad)
        Dim sinA = Math.Sin(-angleRad)
        Dim localDeltaX = deltaX * cosA - deltaY * sinA
        Dim localDeltaY = deltaX * sinA + deltaY * cosA

        Dim transformOrigin = wrapper.RenderTransformOrigin
        Dim deltaVertical As Double = 0
        Dim deltaHorizontal As Double = 0
        Dim verticalAlignment As VerticalAlignment = VerticalAlignment.Center
        Dim horizontalAlignment As HorizontalAlignment = HorizontalAlignment.Center

        Select Case handleName
            Case "Top"
                verticalAlignment = VerticalAlignment.Top
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
            Case "Bottom"
                verticalAlignment = VerticalAlignment.Bottom
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
            Case "Left"
                horizontalAlignment = HorizontalAlignment.Left
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "Right"
                horizontalAlignment = HorizontalAlignment.Right
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "TopLeft"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "TopRight"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "BottomLeft"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
            Case "BottomRight"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(-localDeltaY, wrapper.ActualHeight - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, wrapper.ActualWidth - wrapper.MinWidth)
        End Select

        ' For corners, maintain aspect ratio
        Dim isCorner = (verticalAlignment = VerticalAlignment.Top OrElse verticalAlignment = VerticalAlignment.Bottom) AndAlso
                       (horizontalAlignment = HorizontalAlignment.Left OrElse horizontalAlignment = HorizontalAlignment.Right)

        If isCorner Then
            Dim aspectRatio = wrapper.ActualWidth / wrapper.ActualHeight
            wrapper.Width = wrapper.Height * aspectRatio
            deltaVertical = Math.Min(deltaVertical, wrapper.ActualHeight - wrapper.MinHeight)
            deltaHorizontal = Math.Min(deltaVertical * aspectRatio, wrapper.ActualWidth - wrapper.MinWidth)
        End If

        Dim currentTop = Canvas.GetTop(wrapper)
        Dim currentLeft = Canvas.GetLeft(wrapper)

        Dim newWidth = wrapper.ActualWidth - deltaHorizontal
        Dim newHeight = wrapper.ActualHeight - deltaVertical


        Dim moveTop = (verticalAlignment = VerticalAlignment.Top)
        Dim moveLeft = (horizontalAlignment = HorizontalAlignment.Left)

        Dim anchor = New Point(If(moveLeft, 1.0, 0.0),
                               If(moveTop, 1.0, 0.0))

        Dim placement = TransformMath.ComputeResizePlacement(
            currentLeft, currentTop, wrapper.ActualWidth, wrapper.ActualHeight,
            angle, transformOrigin, newWidth, newHeight, anchor)

        wrapper.Height -= deltaVertical
        wrapper.Width -= deltaHorizontal

        Canvas.SetTop(wrapper, placement.Top)
        Canvas.SetLeft(wrapper, placement.Left)
    End Sub


    Public Shared Sub ApplyResizeMulti(wrapper As ContentControl, scaleX As Double, scaleY As Double, anchorX As Double, anchorY As Double, initialSize As (Width As Double, Height As Double), initialPosition As Point, initialRotation As Double)
        If wrapper Is Nothing Then Return

        wrapper.Width = initialSize.Width * scaleX
        wrapper.Height = initialSize.Height * scaleY

        Dim offsetX = initialPosition.X - anchorX
        Dim offsetY = initialPosition.Y - anchorY
        Canvas.SetLeft(wrapper, anchorX + (offsetX * scaleX))
        Canvas.SetTop(wrapper, anchorY + (offsetY * scaleY))

        If Math.Abs(initialRotation) > 0.01 Then
            wrapper.RenderTransform = New RotateTransform(initialRotation)
        Else
            wrapper.RenderTransform = Nothing
        End If
    End Sub


    Public Shared Sub SetSizeAndPosition(wrapper As ContentControl, width As Double, height As Double, Optional left As Double? = Nothing, Optional top As Double? = Nothing)
        If wrapper Is Nothing Then Return

        If width > 0 Then wrapper.Width = width
        If height > 0 Then wrapper.Height = height
        If left.HasValue Then Canvas.SetLeft(wrapper, left.Value)
        If top.HasValue Then Canvas.SetTop(wrapper, top.Value)
    End Sub


    Public Shared Function HandleTextBoxSizeChanged(wrapper As ContentControl, e As SizeChangedEventArgs) As Boolean
        Dim textBox = TryCast(wrapper.Content, TextBox)
        If textBox Is Nothing Then Return False

        If Not (textBox.IsFocused OrElse textBox.IsKeyboardFocusWithin) Then
            Return False
        End If

        If e.PreviousSize.Width <= 0 OrElse e.PreviousSize.Height <= 0 Then
            Return True
        End If

        If Math.Abs(e.NewSize.Width - e.PreviousSize.Width) < 0.01 AndAlso
           Math.Abs(e.NewSize.Height - e.PreviousSize.Height) < 0.01 Then
            Return True
        End If

        Dim angle = GetRotationAngle(wrapper)
        Dim transformOrigin = wrapper.RenderTransformOrigin

        Dim newTop = Canvas.GetTop(wrapper)
        If Double.IsNaN(newTop) Then newTop = 0
        Dim newLeft = Canvas.GetLeft(wrapper)
        If Double.IsNaN(newLeft) Then newLeft = 0

        Dim placement As (Left As Double, Top As Double)
        If Math.Abs(angle) > 0.01 Then
            'If we are rotated, then resize from teh centre instead of the corner

            placement = TransformMath.ComputeResizePlacement(
                newLeft, newTop, e.PreviousSize.Width, e.PreviousSize.Height,
                angle, transformOrigin, e.NewSize.Width, e.NewSize.Height, New Point(0.5, 0.5))
        Else
            ' Bottom-right resize semantics: the top-left corner stays anchored.

            placement = TransformMath.ComputeResizePlacement(
                newLeft, newTop, e.PreviousSize.Width, e.PreviousSize.Height,
                angle, transformOrigin, e.NewSize.Width, e.NewSize.Height, New Point(0, 0))
        End If

        Canvas.SetTop(wrapper, placement.Top)
        Canvas.SetLeft(wrapper, placement.Left)

        Return True
    End Function

    ' ====================
    ' Mirror Operations
    ' ====================


    Public Shared Sub ApplyMirror(wrapper As ContentControl, selectionCenter As Point, mirrorX As Boolean, mirrorY As Boolean)
        If wrapper Is Nothing Then Return

        Dim currentRotation = GetRotationAngle(wrapper)
        Dim transformOrigin = wrapper.RenderTransformOrigin

        ' Mirror position
        Dim currentLeft = Canvas.GetLeft(wrapper)
        Dim currentTop = Canvas.GetTop(wrapper)
        Dim objCenterX = currentLeft + wrapper.ActualWidth * transformOrigin.X
        Dim objCenterY = currentTop + wrapper.ActualHeight * transformOrigin.Y

        Dim offsetX = objCenterX - selectionCenter.X
        Dim offsetY = objCenterY - selectionCenter.Y

        If mirrorX Then offsetX = -offsetX
        If mirrorY Then offsetY = -offsetY

        Dim newCenterX = selectionCenter.X + offsetX
        Dim newCenterY = selectionCenter.Y + offsetY
        Canvas.SetLeft(wrapper, newCenterX - wrapper.ActualWidth * transformOrigin.X)
        Canvas.SetTop(wrapper, newCenterY - wrapper.ActualHeight * transformOrigin.Y)

        ' Mirror rotation
        Dim newRotation = CalculateMirroredRotation(currentRotation, mirrorX, mirrorY)
        wrapper.RenderTransform = New RotateTransform(newRotation)

        ' Mirror visual appearance (mirror scale on the child element)
        ApplyScaleTransform(wrapper.Content, mirrorX, mirrorY)

        wrapper.InvalidateMeasure()
        wrapper.InvalidateArrange()
        wrapper.UpdateLayout()
    End Sub





    Public Shared Function CalculateMirroredRotation(currentRotation As Double, mirrorX As Boolean, mirrorY As Boolean) As Double
        Dim newRotation As Double = currentRotation

        If mirrorX AndAlso mirrorY Then
            newRotation = currentRotation + 180
        ElseIf mirrorX OrElse mirrorY Then
            newRotation = -currentRotation
        End If

        ' Normalize to -180 to 180 range
        While newRotation > 180
            newRotation -= 360
        End While
        While newRotation < -180
            newRotation += 360
        End While

        Return newRotation
    End Function


    Public Shared Function GetRotationAngle(wrapper As ContentControl) As Double
        If wrapper Is Nothing Then Return 0
        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        Return If(rotateTransform IsNot Nothing, rotateTransform.Angle, 0)
    End Function


    Public Shared Sub ApplyScaleTransform(element As FrameworkElement, mirrorX As Boolean, mirrorY As Boolean)
        If element Is Nothing Then Return

        element.RenderTransformOrigin = New Point(0.5, 0.5)

        Dim tg = TryCast(element.RenderTransform, TransformGroup)
        If tg Is Nothing Then
            tg = New TransformGroup()
            If element.RenderTransform IsNot Nothing Then
                tg.Children.Add(element.RenderTransform)
            End If
            element.RenderTransform = tg
        End If

        Dim scale = tg.Children.OfType(Of ScaleTransform)().FirstOrDefault()
        If scale Is Nothing Then
            scale = New ScaleTransform(1, 1)
            tg.Children.Add(scale)
        End If

        If mirrorX Then scale.ScaleX *= -1
        If mirrorY Then scale.ScaleY *= -1
    End Sub


End Class
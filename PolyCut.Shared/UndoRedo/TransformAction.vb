Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Class TransformAction
    Implements IUndoableAction


    Private ReadOnly _items As New List(Of (Target As IDrawable, Before As TransformState, After As TransformState))

    Public Sub New(items As IEnumerable(Of (IDrawable, TransformState, TransformState)))
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

    Public Sub Undo() Implements IUndoableAction.Undo
        For Each t In _items
            ApplyState(t.Before, t.Target)
        Next
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        For Each t In _items
            ApplyState(t.After, t.Target)
        Next
    End Sub

    Public Shared Function CaptureState(wrapper As ContentControl) As TransformState
        If wrapper Is Nothing Then Return Nothing
        Return TransformState.FromWrapper(wrapper)
    End Function

    Public Shared Function CaptureState(drawable As IDrawable) As TransformState
        Return CaptureState(GetWrapper(drawable))
    End Function

    Public Shared Sub ApplyState(snapshot As TransformState, drawable As IDrawable)
        ApplyStateToWrapper(snapshot, GetWrapper(drawable))
    End Sub

    Public Shared Sub ApplyStateToWrapper(snapshot As TransformState, wrapper As ContentControl)
        If snapshot Is Nothing OrElse wrapper Is Nothing Then Return

        If snapshot IsNot Nothing Then snapshot.ApplyToWrapper(wrapper, applyScale:=True)
    End Sub

    Public Shared Function GetWrapper(drawable As IDrawable) As ContentControl
        If drawable?.DrawableElement Is Nothing Then Return Nothing
        Return TryCast(drawable.DrawableElement.Parent, ContentControl)
    End Function

    ' ====================
    ' Transform Operations
    ' ====================

    Public Shared Sub ApplyRotation(wrapper As ContentControl, centerPoint As Point, initialRotation As Double, angle As Double, initialPosition As Point)
        If wrapper Is Nothing Then Return

        TransformState.SetRotation(wrapper, initialRotation + angle)

        Dim size = TransformMath.GetWrapperSize(wrapper)
        Dim initialItemCenter = New Point(
            initialPosition.X + size.Width * wrapper.RenderTransformOrigin.X,
            initialPosition.Y + size.Height * wrapper.RenderTransformOrigin.Y)

        Dim offsetFromCenter = Point.Subtract(initialItemCenter, centerPoint)
        Dim angleRad = angle * Math.PI / 180
        Dim cosA = Math.Cos(angleRad)
        Dim sinA = Math.Sin(angleRad)

        Dim rotatedOffset = New Point(
            offsetFromCenter.X * cosA - offsetFromCenter.Y * sinA,
            offsetFromCenter.X * sinA + offsetFromCenter.Y * cosA)

        Dim newItemCenter = Point.Add(centerPoint, CType(rotatedOffset, Vector))
        Canvas.SetLeft(wrapper, newItemCenter.X - size.Width * wrapper.RenderTransformOrigin.X)
        Canvas.SetTop(wrapper, newItemCenter.Y - size.Height * wrapper.RenderTransformOrigin.Y)
    End Sub

    Public Shared Sub ApplyMove(wrapper As ContentControl, deltaX As Double, deltaY As Double)
        If wrapper Is Nothing Then Return
        Canvas.SetLeft(wrapper, Canvas.GetLeft(wrapper) + deltaX)
        Canvas.SetTop(wrapper, Canvas.GetTop(wrapper) + deltaY)
    End Sub

    Public Shared Sub ApplyResizeSingle(wrapper As ContentControl, handleName As String, deltaX As Double, deltaY As Double)
        If wrapper Is Nothing Then Return

        Dim angle As Double = GetRotationAngle(wrapper)

        Dim angleRad = angle * Math.PI / 180.0
        Dim cosA = Math.Cos(-angleRad)
        Dim sinA = Math.Sin(-angleRad)
        Dim localDeltaX = deltaX * cosA - deltaY * sinA
        Dim localDeltaY = deltaX * sinA + deltaY * cosA
        Dim state = TransformState.FromWrapper(wrapper)
        Dim affine = state.AffineTransform
        If Not affine.IsIdentity AndAlso affine.HasInverse Then
            Dim inverse = affine
            inverse.Invert()
            Dim localDelta = inverse.Transform(New Vector(localDeltaX, localDeltaY))
            localDeltaX = localDelta.X
            localDeltaY = localDelta.Y
        End If

        Dim transformOrigin = wrapper.RenderTransformOrigin
        Dim startSize = TransformMath.GetWrapperSize(wrapper)
        Dim deltaVertical As Double = 0
        Dim deltaHorizontal As Double = 0
        Dim verticalAlignment As VerticalAlignment = VerticalAlignment.Center
        Dim horizontalAlignment As HorizontalAlignment = HorizontalAlignment.Center

        Select Case handleName
            Case "Top"
                verticalAlignment = VerticalAlignment.Top
                deltaVertical = Math.Min(localDeltaY, startSize.Height - wrapper.MinHeight)
            Case "Bottom"
                verticalAlignment = VerticalAlignment.Bottom
                deltaVertical = Math.Min(-localDeltaY, startSize.Height - wrapper.MinHeight)
            Case "Left"
                horizontalAlignment = HorizontalAlignment.Left
                deltaHorizontal = Math.Min(localDeltaX, startSize.Width - wrapper.MinWidth)
            Case "Right"
                horizontalAlignment = HorizontalAlignment.Right
                deltaHorizontal = Math.Min(-localDeltaX, startSize.Width - wrapper.MinWidth)
            Case "TopLeft"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(localDeltaY, startSize.Height - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, startSize.Width - wrapper.MinWidth)
            Case "TopRight"
                verticalAlignment = VerticalAlignment.Top
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(localDeltaY, startSize.Height - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, startSize.Width - wrapper.MinWidth)
            Case "BottomLeft"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Left
                deltaVertical = Math.Min(-localDeltaY, startSize.Height - wrapper.MinHeight)
                deltaHorizontal = Math.Min(localDeltaX, startSize.Width - wrapper.MinWidth)
            Case "BottomRight"
                verticalAlignment = VerticalAlignment.Bottom
                horizontalAlignment = HorizontalAlignment.Right
                deltaVertical = Math.Min(-localDeltaY, startSize.Height - wrapper.MinHeight)
                deltaHorizontal = Math.Min(-localDeltaX, startSize.Width - wrapper.MinWidth)
        End Select

        ' For corners, maintain aspect ratio
        Dim isCorner = (verticalAlignment = VerticalAlignment.Top OrElse verticalAlignment = VerticalAlignment.Bottom) AndAlso
                       (horizontalAlignment = HorizontalAlignment.Left OrElse horizontalAlignment = HorizontalAlignment.Right)

        If isCorner Then
            Dim aspectRatio = startSize.Width / startSize.Height
            deltaVertical = Math.Min(deltaVertical, startSize.Height - wrapper.MinHeight)
            deltaHorizontal = Math.Min(deltaVertical * aspectRatio, startSize.Width - wrapper.MinWidth)
        End If

        Dim currentTop = Canvas.GetTop(wrapper)
        Dim currentLeft = Canvas.GetLeft(wrapper)

        Dim newWidth = startSize.Width - deltaHorizontal
        Dim newHeight = startSize.Height - deltaVertical

        Dim moveTop = (verticalAlignment = VerticalAlignment.Top)
        Dim moveLeft = (horizontalAlignment = HorizontalAlignment.Left)

        Dim anchor = New Point(If(moveLeft, 1.0, 0.0),
                               If(moveTop, 1.0, 0.0))

        If TypeOf wrapper.Content Is TextBox Then
            If startSize.Width <= 0 OrElse startSize.Height <= 0 Then Return
            ' Text does not stretch with its layout box. Scale its native cordinates instead,before the existing affine/rotation, keeping the opposite gizmo anchor fixed.
            Dim scale = Matrix.Identity
            scale.Scale(Math.Max(newWidth, 0.001) / startSize.Width, Math.Max(newHeight, 0.001) / startSize.Height)
            state.AffineTransform = Matrix.Multiply(scale, affine)
            Dim before = affine
            before.Rotate(angle)
            Dim after = state.AffineTransform
            after.Rotate(angle)
            Dim fixedOffset As New Vector((anchor.X - transformOrigin.X) * startSize.Width,
                                          (anchor.Y - transformOrigin.Y) * startSize.Height)
            Dim shift = before.Transform(fixedOffset) - after.Transform(fixedOffset)
            state.Translation = Point.Add(state.Translation, shift)
            state.ApplyToWrapper(wrapper, applyScale:=False)
            Return
        End If

        Dim placement = TransformMath.ComputeResizePlacement(
            currentLeft, currentTop, startSize.Width, startSize.Height,
            angle, transformOrigin, newWidth, newHeight, anchor, affine)

        wrapper.Height -= deltaVertical
        wrapper.Width -= deltaHorizontal

        Canvas.SetTop(wrapper, placement.Top)
        Canvas.SetLeft(wrapper, placement.Left)
    End Sub

    Public Shared Sub ApplyResizeMulti(wrapper As ContentControl, scaleX As Double, scaleY As Double, anchorX As Double, anchorY As Double, initialSize As (Width As Double, Height As Double), initialPosition As Point, initialRotation As Double, Optional selectionRotation As Double = 0, Optional initialAffine As Matrix? = Nothing)
        If wrapper Is Nothing Then Return

        If TypeOf wrapper.Content Is TextBox Then
            If initialSize.Width <= 0 OrElse initialSize.Height <= 0 OrElse scaleX <= 0 OrElse scaleY <= 0 Then Return
            Dim state = TransformState.FromWrapper(wrapper)

            Dim affine = If(initialAffine, state.AffineTransform)
            affine.Rotate(initialRotation - selectionRotation)
            affine.Scale(scaleX, scaleY)
            affine.Rotate(selectionRotation - initialRotation)
            state.AffineTransform = affine
            state.Rotation = initialRotation
            state.Width = initialSize.Width
            state.Height = initialSize.Height

            Dim pivot As New Point(initialSize.Width * state.TransformOrigin.X, initialSize.Height * state.TransformOrigin.Y)
            Dim textAnchor As New Point(anchorX, anchorY)
            Dim textCenter As New Point(initialPosition.X + pivot.X, initialPosition.Y + pivot.Y)
            Dim textLocal = TransformMath.RotateAbout(textCenter, textAnchor, -selectionRotation)
            textLocal = New Point(textAnchor.X + (textLocal.X - textAnchor.X) * scaleX, textAnchor.Y + (textLocal.Y - textAnchor.Y) * scaleY)
            Dim scaledTextCenter = TransformMath.RotateAbout(textLocal, textAnchor, selectionRotation)
            state.Translation = New Point(scaledTextCenter.X - pivot.X, scaledTextCenter.Y - pivot.Y)
            state.ApplyToWrapper(wrapper, applyScale:=False)
            Return
        End If

        wrapper.Width = initialSize.Width * scaleX
        wrapper.Height = initialSize.Height * scaleY


        Dim anchor = New Point(anchorX, anchorY)
        Dim center = New Point(initialPosition.X + initialSize.Width / 2, initialPosition.Y + initialSize.Height / 2)
        Dim local = TransformMath.RotateAbout(center, anchor, -selectionRotation)
        local = New Point(anchor.X + (local.X - anchor.X) * scaleX, anchor.Y + (local.Y - anchor.Y) * scaleY)
        Dim scaledCenter = TransformMath.RotateAbout(local, anchor, selectionRotation)

        Canvas.SetLeft(wrapper, scaledCenter.X - wrapper.Width / 2)
        Canvas.SetTop(wrapper, scaledCenter.Y - wrapper.Height / 2)

        TransformState.SetRotation(wrapper, initialRotation)
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
                angle, transformOrigin, e.NewSize.Width, e.NewSize.Height, New Point(0.5, 0.5), TransformState.FromWrapper(wrapper).AffineTransform)
        Else
            ' Bottom-right resize semantics: the top-left corner stays anchored.

            placement = TransformMath.ComputeResizePlacement(
                newLeft, newTop, e.PreviousSize.Width, e.PreviousSize.Height,
                angle, transformOrigin, e.NewSize.Width, e.NewSize.Height, New Point(0, 0), TransformState.FromWrapper(wrapper).AffineTransform)
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
        Dim size = TransformMath.GetWrapperSize(wrapper)
        Dim objCenterX = currentLeft + size.Width * transformOrigin.X
        Dim objCenterY = currentTop + size.Height * transformOrigin.Y

        Dim offsetX = objCenterX - selectionCenter.X
        Dim offsetY = objCenterY - selectionCenter.Y

        If mirrorX Then offsetX = -offsetX
        If mirrorY Then offsetY = -offsetY

        Dim newCenterX = selectionCenter.X + offsetX
        Dim newCenterY = selectionCenter.Y + offsetY
        Canvas.SetLeft(wrapper, newCenterX - size.Width * transformOrigin.X)
        Canvas.SetTop(wrapper, newCenterY - size.Height * transformOrigin.Y)

        ' Mirror rotation
        Dim newRotation = CalculateMirroredRotation(currentRotation, mirrorX, mirrorY)
        Dim state = TransformState.FromWrapper(wrapper)
        Dim reflection = Matrix.Identity
        reflection.Scale(If(mirrorX, -1.0, 1.0), If(mirrorY, -1.0, 1.0))
        state.AffineTransform = Matrix.Multiply(Matrix.Multiply(reflection, state.AffineTransform), reflection)
        state.Rotation = newRotation
        state.ApplyToWrapper(wrapper, applyScale:=False)

        ' Mirror visual appearance (mirror scale on the child element)
        ' A reflection in both axes is already represented by the 180-degree rotation.
        If mirrorX Xor mirrorY Then ApplyScaleTransform(wrapper.Content, mirrorX, mirrorY)

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
        Return TransformState.GetRotation(wrapper)
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

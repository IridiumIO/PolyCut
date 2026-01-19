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
        Dim left = Canvas.GetLeft(wrapper)
        If Double.IsNaN(left) Then left = 0
        Dim top = Canvas.GetTop(wrapper)
        If Double.IsNaN(top) Then top = 0
        Return New Snapshot With {
            .Left = left,
            .Top = top,
            .Width = wrapper.ActualWidth,
            .Height = wrapper.ActualHeight,
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
        If rotateTransform IsNot Nothing Then
            angle = rotateTransform.Angle * Math.PI / 180.0
        End If

        Dim cosA = Math.Cos(-angle)
        Dim sinA = Math.Sin(-angle)
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
        Dim newTop = currentTop
        Dim newLeft = currentLeft

        If verticalAlignment <> VerticalAlignment.Center Then
            newTop += GetCanvasTopOffsetForVertical(verticalAlignment, deltaVertical, angle, transformOrigin)
            newLeft += GetCanvasLeftOffsetForVertical(verticalAlignment, deltaVertical, angle, transformOrigin)
        End If

        If horizontalAlignment <> HorizontalAlignment.Center Then
            newTop += GetCanvasTopOffsetForHorizontal(horizontalAlignment, deltaHorizontal, angle, transformOrigin)
            newLeft += GetCanvasLeftOffsetForHorizontal(horizontalAlignment, deltaHorizontal, angle, transformOrigin)
        End If

        wrapper.Height -= deltaVertical
        wrapper.Width -= deltaHorizontal

        Canvas.SetTop(wrapper, newTop)
        Canvas.SetLeft(wrapper, newLeft)
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

        Dim deltaWidth = e.NewSize.Width - e.PreviousSize.Width
        Dim deltaHeight = e.NewSize.Height - e.PreviousSize.Height

        If Math.Abs(deltaWidth) < 0.01 AndAlso Math.Abs(deltaHeight) < 0.01 Then
            Return True
        End If

        Dim angle As Double = 0
        Dim rt = TryCast(wrapper.RenderTransform, RotateTransform)
        If rt IsNot Nothing Then
            angle = rt.Angle * Math.PI / 180.0
        End If

        Dim transformOrigin = wrapper.RenderTransformOrigin

        Dim deltaHorizontal = -deltaWidth
        Dim deltaVertical = -deltaHeight

        Dim newTop = Canvas.GetTop(wrapper)
        If Double.IsNaN(newTop) Then newTop = 0
        Dim newLeft = Canvas.GetLeft(wrapper)
        If Double.IsNaN(newLeft) Then newLeft = 0

        newTop += GetCanvasTopOffsetForVertical(VerticalAlignment.Bottom, deltaVertical, angle, transformOrigin)
        newLeft += GetCanvasLeftOffsetForVertical(VerticalAlignment.Bottom, deltaVertical, angle, transformOrigin)

        newTop += GetCanvasTopOffsetForHorizontal(HorizontalAlignment.Right, deltaHorizontal, angle, transformOrigin)
        newLeft += GetCanvasLeftOffsetForHorizontal(HorizontalAlignment.Right, deltaHorizontal, angle, transformOrigin)

        Canvas.SetTop(wrapper, newTop)
        Canvas.SetLeft(wrapper, newLeft)

        Return True
    End Function

    ' ==========================
    ' Transform Helper Functions
    ' ==========================

    Private Shared Function GetCanvasTopOffsetForVertical(alignment As VerticalAlignment, deltaVertical As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Cos(-angle) + (transformOrigin.Y * deltaVertical * (1 - Math.Cos(-angle)))
            Case VerticalAlignment.Bottom
                Return transformOrigin.Y * deltaVertical * (1 - Math.Cos(-angle))
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasTopOffsetForHorizontal(alignment As HorizontalAlignment, deltaHorizontal As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Sin(angle) - transformOrigin.X * deltaHorizontal * Math.Sin(angle)
            Case HorizontalAlignment.Right
                Return -transformOrigin.X * deltaHorizontal * Math.Sin(angle)
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasLeftOffsetForVertical(alignment As VerticalAlignment, deltaVertical As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case VerticalAlignment.Top
                Return deltaVertical * Math.Sin(-angle) - (transformOrigin.Y * deltaVertical * Math.Sin(-angle))
            Case VerticalAlignment.Bottom
                Return -deltaVertical * transformOrigin.Y * Math.Sin(-angle)
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function GetCanvasLeftOffsetForHorizontal(alignment As HorizontalAlignment, deltaHorizontal As Double, angle As Double, transformOrigin As Point) As Double
        Select Case alignment
            Case HorizontalAlignment.Left
                Return deltaHorizontal * Math.Cos(angle) + (transformOrigin.X * deltaHorizontal * (1 - Math.Cos(angle)))
            Case HorizontalAlignment.Right
                Return deltaHorizontal * transformOrigin.X * (1 - Math.Cos(angle))
            Case Else
                Return 0
        End Select
    End Function


End Class
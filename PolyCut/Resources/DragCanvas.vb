Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Reflection
Imports System.Text
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Media3D





''' <summary>
''' A Canvas which manages dragging of the UIElements it contains.
''' THIS HAS BEEN CONVERTED FROM JOSH SMITH'S DRAG CANVAS HERE: https://github.com/denxorz/WPF.JoshSmith.Controls.DragCanvas
''' </summary>
Public Class DragCanvas
    Inherits Canvas

    ' Stores a reference to the UIElement currently being dragged by the user.
    Private _elementBeingDragged As UIElement

    ' Keeps track of where the mouse cursor was when a drag operation began.
    Private origCursorLocation As Point

    ' The offsets from the DragCanvas' edges when the drag operation began.
    Private origHorizOffset, origVertOffset As Double

    ' Keeps track of which horizontal and vertical offset should be modified for the drag element.
    Private modifyLeftOffset, modifyTopOffset As Boolean

    ''' <summary>
    ''' True if a drag operation is underway, else false.
    ''' </summary>
    Public Property IsDragInProgress As Boolean

    ' Dependency Properties
    Public Shared ReadOnly AllowDraggingProperty As DependencyProperty
    Public Shared ReadOnly AllowDragOutOfViewProperty As DependencyProperty

    ' Attached Property
    Public Shared ReadOnly CanBeDraggedProperty As DependencyProperty

    ' Static Constructor
    Shared Sub New()
        AllowDraggingProperty = DependencyProperty.Register("AllowDragging", GetType(Boolean), GetType(DragCanvas), New PropertyMetadata(True))
        AllowDragOutOfViewProperty = DependencyProperty.Register("AllowDragOutOfView", GetType(Boolean), GetType(DragCanvas), New UIPropertyMetadata(False))
        CanBeDraggedProperty = DependencyProperty.RegisterAttached("CanBeDragged", GetType(Boolean), GetType(DragCanvas), New UIPropertyMetadata(True))
    End Sub

    ' Constructor
    Public Sub New()
    End Sub

    ' AllowDragging Property
    Public Property AllowDragging As Boolean
        Get
            Return CBool(GetValue(AllowDraggingProperty))
        End Get
        Set(value As Boolean)
            SetValue(AllowDraggingProperty, value)
        End Set
    End Property

    ' AllowDragOutOfView Property
    Public Property AllowDragOutOfView As Boolean
        Get
            Return CBool(GetValue(AllowDragOutOfViewProperty))
        End Get
        Set(value As Boolean)
            SetValue(AllowDragOutOfViewProperty, value)
        End Set
    End Property

    ' ElementBeingDragged Property
    Public Property ElementBeingDragged As UIElement
        Get
            If Not AllowDragging Then
                Return Nothing
            Else
                Return _elementBeingDragged
            End If
        End Get
        Protected Set(value As UIElement)
            If ElementBeingDragged IsNot Nothing Then
                ElementBeingDragged.ReleaseMouseCapture()
            End If

            If Not AllowDragging Then
                _elementBeingDragged = Nothing
            Else
                If DragCanvas.GetCanBeDragged(value) Then
                    _elementBeingDragged = value
                    _elementBeingDragged.CaptureMouse()
                Else
                    _elementBeingDragged = Nothing
                End If
            End If
        End Set
    End Property

    ' GetCanBeDragged Attached Property
    Public Shared Function GetCanBeDragged(uiElement As UIElement) As Boolean
        If uiElement Is Nothing Then
            Return False
        End If

        Return CBool(uiElement.GetValue(CanBeDraggedProperty))
    End Function

    ' SetCanBeDragged Attached Property
    Public Shared Sub SetCanBeDragged(uiElement As UIElement, value As Boolean)
        If uiElement IsNot Nothing Then
            uiElement.SetValue(CanBeDraggedProperty, value)
        End If
    End Sub

    ' OnMouseLeftButtonDown Event
    Protected Overrides Sub OnMouseLeftButtonDown(e As MouseButtonEventArgs)
        MyBase.OnMouseLeftButtonDown(e)

        IsDragInProgress = False

        ' Cache the mouse cursor location.
        origCursorLocation = e.GetPosition(Me)



        ' Walk up the visual tree from the element that was clicked,
        ' looking for an element that is a direct child of the Canvas.
        ElementBeingDragged = FindCanvasChild(TryCast(e.Source, DependencyObject))
        If ElementBeingDragged Is Nothing Then
            Return
        End If

        ' Get the element's offsets from the four sides of the Canvas.
        Dim left As Double = Canvas.GetLeft(ElementBeingDragged)
        Dim right As Double = Canvas.GetRight(ElementBeingDragged)
        Dim top As Double = Canvas.GetTop(ElementBeingDragged)
        Dim bottom As Double = Canvas.GetBottom(ElementBeingDragged)


        ' Calculate the offset deltas and determine for which sides
        ' of the Canvas to adjust the offsets.
        origHorizOffset = ResolveOffset(left, right, modifyLeftOffset)
        origVertOffset = ResolveOffset(top, bottom, modifyTopOffset)

        ' Set the Handled flag so that a control being dragged 
        ' does not react to the mouse input.
        e.Handled = True

        IsDragInProgress = True
    End Sub

    ' OnPreviewMouseMove Event
    Protected Overrides Sub OnPreviewMouseMove(e As MouseEventArgs)
        MyBase.OnPreviewMouseMove(e)

        ' If no element is being dragged, there is nothing to do.
        If ElementBeingDragged Is Nothing OrElse Not IsDragInProgress Then
            Return
        End If

        ' Get the position of the mouse cursor, relative to the Canvas.
        Dim cursorLocation As Point = e.GetPosition(Me)

        ' These values will store the new offsets of the drag element.
        Dim newHorizontalOffset, newVerticalOffset As Double

        ' Calculate Offsets
        ' Determine the horizontal offset.
        If modifyLeftOffset Then
            newHorizontalOffset = origHorizOffset + (cursorLocation.X - origCursorLocation.X)
        Else
            newHorizontalOffset = origHorizOffset - (cursorLocation.X - origCursorLocation.X)
        End If

        ' Determine the vertical offset.
        If modifyTopOffset Then
            newVerticalOffset = origVertOffset + (cursorLocation.Y - origCursorLocation.Y)
        Else
            newVerticalOffset = origVertOffset - (cursorLocation.Y - origCursorLocation.Y)
        End If

        If Not AllowDragOutOfView Then
            ' Verify Drag Element Location
            ' Get the bounding rect of the drag element.
            Dim elemRect As Rect = CalculateDragElementRect(newHorizontalOffset, newVerticalOffset)

            ' If the element is being dragged out of the viewable area, 
            ' determine the ideal rect location, so that the element is 
            ' within the edge(s) of the canvas.
            Dim leftAlign As Boolean = elemRect.Left < 0
            Dim rightAlign As Boolean = elemRect.Right > ActualWidth

            If leftAlign Then
                newHorizontalOffset = If(modifyLeftOffset, 0, ActualWidth - elemRect.Width)
            ElseIf rightAlign Then
                newHorizontalOffset = If(modifyLeftOffset, ActualWidth - elemRect.Width, 0)
            End If

            Dim topAlign As Boolean = elemRect.Top < 0
            Dim bottomAlign As Boolean = elemRect.Bottom > ActualHeight

            If topAlign Then
                newVerticalOffset = If(modifyTopOffset, 0, ActualHeight - elemRect.Height)
            ElseIf bottomAlign Then
                newVerticalOffset = If(modifyTopOffset, ActualHeight - elemRect.Height, 0)
            End If
        End If

        ' Move Drag Element
        If modifyLeftOffset Then
            Canvas.SetLeft(ElementBeingDragged, newHorizontalOffset)
        Else
            Canvas.SetRight(ElementBeingDragged, newHorizontalOffset)
        End If

        If modifyTopOffset Then
            Canvas.SetTop(ElementBeingDragged, newVerticalOffset)
        Else
            Canvas.SetBottom(ElementBeingDragged, newVerticalOffset)
        End If
    End Sub

    ' OnPreviewMouseUp Event
    Protected Overrides Sub OnPreviewMouseUp(e As MouseButtonEventArgs)
        MyBase.OnPreviewMouseUp(e)



        ' Reset the field whether the left or right mouse button was 
        ' released, in case a context menu was opened on the drag element.
        ElementBeingDragged = Nothing
    End Sub

    ' FindCanvasChild Method
    Private Function FindCanvasChild(depObj As DependencyObject) As UIElement
        Do While depObj IsNot Nothing
            ' If the current object is a UIElement which is a child of the
            ' Canvas, exit the loop and return it.
            Dim elem As UIElement = TryCast(depObj, UIElement)
            If elem IsNot Nothing AndAlso MyBase.Children.Contains(elem) Then
                Return elem
            End If

            ' VisualTreeHelper works with objects of type Visual or Visual3D.
            ' If the current object is not derived from Visual or Visual3D,
            ' then use the LogicalTreeHelper to find the parent element.
            If TypeOf depObj Is Visual OrElse TypeOf depObj Is Visual3D Then
                depObj = VisualTreeHelper.GetParent(depObj)
            Else
                depObj = LogicalTreeHelper.GetParent(depObj)
            End If
        Loop
        Return TryCast(depObj, UIElement)
    End Function

    ' CalculateDragElementRect Method
    Private Function CalculateDragElementRect(newHorizOffset As Double, newVertOffset As Double) As Rect
        If ElementBeingDragged Is Nothing Then
            Throw New InvalidOperationException("ElementBeingDragged is null.")
        End If

        Dim elemSize As Size = ElementBeingDragged.RenderSize
        Dim x, y As Double

        If modifyLeftOffset Then
            x = newHorizOffset
        Else
            x = ActualWidth - newHorizOffset - elemSize.Width
        End If

        If modifyTopOffset Then
            y = newVertOffset
        Else
            y = ActualHeight - newVertOffset - elemSize.Height
        End If

        Dim elemLoc As New Point(x, y)
        Return New Rect(elemLoc, elemSize)
    End Function

    ' ResolveOffset Method
    Private Shared Function ResolveOffset(side1 As Double, side2 As Double, ByRef useSide1 As Boolean) As Double
        ' If the Canvas.Left and Canvas.Right attached properties 
        ' are specified for an element, the 'Left' value is honored.
        ' The 'Top' value is
        useSide1 = True
        Dim result As Double

        If Double.IsNaN(side1) Then

            If Double.IsNaN(side2) Then
                result = 0
            Else
                result = side2
                useSide1 = False
            End If
        Else
            result = side1
        End If

        Return result
    End Function

    Private Sub UpdateZOrder(ByVal element As UIElement, ByVal bringToFront As Boolean)
        If element Is Nothing Then Throw New ArgumentNullException("element")
        If Not MyBase.Children.Contains(element) Then Throw New ArgumentException("Must be a child element of the Canvas.", "element")
        Dim elementNewZIndex As Integer = -1

        If bringToFront Then

            For Each elem As UIElement In MyBase.Children
                If elem.Visibility <> Visibility.Collapsed Then elementNewZIndex += 1
            Next
        Else
            elementNewZIndex = 0
        End If

        Dim offset As Integer = If((elementNewZIndex = 0), +1, -1)
        Dim elementCurrentZIndex As Integer = Canvas.GetZIndex(element)

        For Each childElement As UIElement In MyBase.Children

            If childElement Is element Then
                Canvas.SetZIndex(element, elementNewZIndex)
            Else
                Dim zIndex As Integer = Canvas.GetZIndex(childElement)

                If bringToFront AndAlso elementCurrentZIndex < zIndex OrElse Not bringToFront AndAlso zIndex < elementCurrentZIndex Then
                    Canvas.SetZIndex(childElement, zIndex + offset)
                End If
            End If
        Next
    End Sub
End Class


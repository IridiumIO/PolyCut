' Lives beside the transform gizmo, outside the document's zoom/affine transforms.
Public NotInheritable Class TextBoxBorderOverlay
    Inherits FrameworkElement

    Private ReadOnly _canvas As Canvas
    Private ReadOnly _editor As TextEditingController
    Private _borders As New List(Of BorderOutline)
    Private _renderHooked As Boolean
    Private _attached As Boolean

    Public Sub New(canvas As Canvas, editor As TextEditingController)
        _canvas = canvas
        _editor = editor
        IsHitTestVisible = False
        AddHandler Loaded, AddressOf OnLoaded
        AddHandler Unloaded, AddressOf OnUnloaded
    End Sub

    Public Shared Sub PrepareTextBox(textBox As TextBox)
        textBox.SetResourceReference(Control.TemplateProperty, "CanvasTextBoxTemplate")
        textBox.FocusVisualStyle = Nothing
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        If _attached Then Return
        _attached = True
        AddHandler InputManager.Current.PostProcessInput, AddressOf OnInput
        AddHandler _canvas.LayoutUpdated, AddressOf OnRendering
        RefreshBorders()
    End Sub

    Private Sub OnUnloaded(sender As Object, e As RoutedEventArgs)
        If Not _attached Then Return
        _attached = False
        RemoveHandler InputManager.Current.PostProcessInput, AddressOf OnInput
        RemoveHandler _canvas.LayoutUpdated, AddressOf OnRendering
        UpdateRenderingSubscription(False)
        _borders.Clear()
        InvalidateVisual()
    End Sub

    Private Sub OnRendering(sender As Object, e As EventArgs)
        RefreshBorders()
    End Sub

    Private Sub OnInput(sender As Object, e As ProcessInputEventArgs)
        RefreshBorders()
    End Sub

    Private Sub UpdateRenderingSubscription(needed As Boolean)
        If needed = _renderHooked Then Return
        If needed Then
            AddHandler CompositionTarget.Rendering, AddressOf OnRendering
        Else
            RemoveHandler CompositionTarget.Rendering, AddressOf OnRendering
        End If
        _renderHooked = needed
    End Sub

    Private Function FindCanvasTextBox(source As DependencyObject) As TextBox
        Dim textBox As TextBox = Nothing
        Dim current = source
        While current IsNot Nothing
            If current Is _canvas Then Return textBox
            If TypeOf current Is TextBox Then textBox = DirectCast(current, TextBox)
            current = If(TypeOf current Is Visual, VisualTreeHelper.GetParent(current), LogicalTreeHelper.GetParent(current))
        End While
        Return Nothing
    End Function

    Private Sub RefreshBorders()
        Dim borders As New List(Of BorderOutline)
        Dim editing = FindCanvasTextBox(_editor?.ActiveTextBox)
        If editing Is Nothing Then editing = FindCanvasTextBox(TryCast(Keyboard.FocusedElement, DependencyObject))
        Dim hovered = FindCanvasTextBox(TryCast(Mouse.DirectlyOver, DependencyObject))
        If hovered IsNot Nothing AndAlso hovered IsNot editing Then AddBorder(borders, hovered, False)
        If editing IsNot Nothing Then AddBorder(borders, editing, True)

        ' Follow pan/zoom/transform animation only while an outline is visible.
        UpdateRenderingSubscription(_attached AndAlso borders.Count > 0)
        If borders.Count = _borders.Count AndAlso borders.Zip(_borders, Function(a, b) a.SameAs(b)).All(Function(same) same) Then Return
        _borders = borders
        InvalidateVisual()
    End Sub

    Private Sub AddBorder(borders As List(Of BorderOutline), textBox As TextBox, editing As Boolean)
        If Not textBox.IsVisible OrElse textBox.ActualWidth <= 0 OrElse textBox.ActualHeight <= 0 Then Return
        Try
            Dim transform = textBox.TransformToVisual(Me)
            Dim corners = {
                transform.Transform(New Point(0, 0)),
                transform.Transform(New Point(textBox.ActualWidth, 0)),
                transform.Transform(New Point(textBox.ActualWidth, textBox.ActualHeight)),
                transform.Transform(New Point(0, textBox.ActualHeight))
            }
            If corners.Any(Function(p) Not Double.IsFinite(p.X) OrElse Not Double.IsFinite(p.Y)) Then Return
            borders.Add(New BorderOutline With {.Corners = corners, .Editing = editing, .Dpi = VisualTreeHelper.GetDpi(Me)})
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)
        For Each border In _borders
            Dim dpi = border.Dpi
            ' Construct the path in device pixels, then map it back to WPF units.
            Dim toPixels = New ScaleTransform(dpi.DpiScaleX, dpi.DpiScaleY)
            Dim geometry As New StreamGeometry()
            Using context = geometry.Open()
                context.BeginFigure(toPixels.Transform(border.Corners(0)), False, True)
                For i = 1 To 3
                    context.LineTo(toPixels.Transform(border.Corners(i)), True, False)
                Next
            End Using
            geometry.Freeze()
            Dim brush = If(border.Editing, SystemColors.HighlightBrush, SystemColors.ControlDarkBrush)
            Dim pen As New Pen(brush, 2) With {.LineJoin = PenLineJoin.Miter}
            dc.PushTransform(New ScaleTransform(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY))
            dc.DrawGeometry(Nothing, pen, geometry)
            dc.Pop()
        Next
    End Sub

    Private NotInheritable Class BorderOutline
        Public Property Corners As Point()
        Public Property Editing As Boolean
        Public Property Dpi As DpiScale

        Public Function SameAs(other As BorderOutline) As Boolean
            Return Editing = other.Editing AndAlso Dpi.Equals(other.Dpi) AndAlso Corners.SequenceEqual(other.Corners)
        End Function
    End Class
End Class

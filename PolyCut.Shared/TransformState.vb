Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Public Class TransformState

    Public Property Translation As Point
    Public Property Rotation As Double ' In degrees
    Public Property Scale As Point
    Public Property TransformOrigin As Point
    Public Property Width As Double = Double.NaN
    Public Property Height As Double = Double.NaN
    Public Property ZIndex As Integer

    Public Sub New()
        Translation = New Point(0, 0)
        Rotation = 0
        Scale = New Point(1, 1)
        TransformOrigin = New Point(0.5, 0.5)
    End Sub

    Public Sub New(translation As Point, rotation As Double, scale As Point, transformOrigin As Point)
        Me.Translation = translation
        Me.Rotation = rotation
        Me.Scale = scale
        Me.TransformOrigin = transformOrigin
    End Sub

    Public Function Clone() As TransformState
        Dim c As New TransformState(Translation, Rotation, Scale, TransformOrigin) With {
            .Width = Width,
            .Height = Height,
            .ZIndex = ZIndex
        }
        Return c
    End Function


    'Reads the  wrapper transform state: Canvas Left/Top + Width/Heighton the wrapper, rotation from the wrapper's RenderTransform (RotateTransform), scale (mirror) from the child element's TransformGroup ScaleTransform, and the wrapper's RenderTransformOrigin. Mirrors DrawableCodec serialization.
    Public Shared Function FromWrapper(wrapper As ContentControl) As TransformState
        Dim state As New TransformState()
        If wrapper Is Nothing Then Return state

        Dim left = Canvas.GetLeft(wrapper)
        Dim top = Canvas.GetTop(wrapper)
        state.Translation = New Point(If(Double.IsNaN(left), 0, left), If(Double.IsNaN(top), 0, top))
        Dim size = TransformMath.GetWrapperSize(wrapper)
        state.Width = size.Width
        state.Height = size.Height
        state.TransformOrigin = wrapper.RenderTransformOrigin
        state.ZIndex = Panel.GetZIndex(wrapper)

        Dim rotateTransform = TryCast(wrapper.RenderTransform, RotateTransform)
        If rotateTransform IsNot Nothing Then
            state.Rotation = rotateTransform.Angle
        End If

        ' Mirror scale lives on the child element's render transform.
        Dim contentFe = TryCast(wrapper.Content, FrameworkElement)
        If contentFe IsNot Nothing Then
            Dim transformGroup = TryCast(contentFe.RenderTransform, TransformGroup)
            If transformGroup IsNot Nothing Then
                Dim scale = transformGroup.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                If scale IsNot Nothing Then
                    state.Scale = New Point(scale.ScaleX, scale.ScaleY)
                End If
            Else
                Dim scale = TryCast(contentFe.RenderTransform, ScaleTransform)
                If scale IsNot Nothing Then
                    state.Scale = New Point(scale.ScaleX, scale.ScaleY)
                End If
            End If
        End If

        Return state
    End Function


    'Writes the state back to the exact same storage layout FromWrapper reads. Canvas Left/Top + Width/Height on the wrapper, RotateTransform on the  wrapper, mirror scale in the child element's TransformGroup ScaleTransform.
    Public Sub ApplyToWrapper(wrapper As ContentControl, Optional applyScale As Boolean = True)
        If wrapper Is Nothing Then Return

        Canvas.SetLeft(wrapper, Translation.X)
        Canvas.SetTop(wrapper, Translation.Y)
        If Not Double.IsNaN(Width) AndAlso Width > 0 Then wrapper.Width = Width
        If Not Double.IsNaN(Height) AndAlso Height > 0 Then wrapper.Height = Height

        wrapper.RenderTransformOrigin = TransformOrigin
        wrapper.RenderTransform = If(Math.Abs(Rotation) > 0.01, New RotateTransform(Rotation), Nothing)
        Panel.SetZIndex(wrapper, ZIndex)

        If Not applyScale Then Return

        Dim contentFe = TryCast(wrapper.Content, FrameworkElement)
        If contentFe IsNot Nothing Then
            If Math.Abs(Scale.X - 1.0) > 0.0001 OrElse Math.Abs(Scale.Y - 1.0) > 0.0001 Then
                Dim tg = TryCast(contentFe.RenderTransform, TransformGroup)
                If tg Is Nothing Then
                    tg = New TransformGroup()
                    If contentFe.RenderTransform IsNot Nothing Then
                        tg.Children.Add(contentFe.RenderTransform)
                    End If
                    contentFe.RenderTransform = tg
                End If

                Dim scales = tg.Children.OfType(Of ScaleTransform)().ToList()
                Dim st = scales.FirstOrDefault()
                If st Is Nothing Then
                    st = New ScaleTransform(1, 1)
                    tg.Children.Add(st)
                Else
                    For Each extra In scales.Skip(1).ToList()
                        tg.Children.Remove(extra)
                    Next
                End If

                st.ScaleX = Scale.X
                st.ScaleY = Scale.Y
                contentFe.RenderTransformOrigin = New Point(0.5, 0.5)
            Else
                RemoveContentScale(contentFe)
            End If
        End If
    End Sub

    Private Shared Sub RemoveContentScale(contentFe As FrameworkElement)
        If contentFe Is Nothing Then Return

        Dim group = TryCast(contentFe.RenderTransform, TransformGroup)
        If group IsNot Nothing Then
            For Each s In group.Children.OfType(Of ScaleTransform)().ToList()
                group.Children.Remove(s)
            Next

            Return
        End If

        If TypeOf contentFe.RenderTransform Is ScaleTransform Then
            contentFe.RenderTransform = Nothing
        End If
    End Sub
    Public Function Matches(other As TransformState, Optional tolerance As Double = 0.01) As Boolean
        If other Is Nothing Then Return False

        If Math.Abs(Translation.X - other.Translation.X) >= tolerance Then Return False
        If Math.Abs(Translation.Y - other.Translation.Y) >= tolerance Then Return False
        If Math.Abs(Rotation - other.Rotation) >= tolerance Then Return False
        If Math.Abs(Scale.X - other.Scale.X) >= 0.0001 Then Return False
        If Math.Abs(Scale.Y - other.Scale.Y) >= 0.0001 Then Return False
        If Math.Abs(TransformOrigin.X - other.TransformOrigin.X) >= tolerance Then Return False
        If Math.Abs(TransformOrigin.Y - other.TransformOrigin.Y) >= tolerance Then Return False
        If Not SizeMatches(Width, other.Width, tolerance) Then Return False
        If Not SizeMatches(Height, other.Height, tolerance) Then Return False
        If ZIndex <> other.ZIndex Then Return False

        Return True
    End Function

    Private Shared Function SizeMatches(a As Double, b As Double, tolerance As Double) As Boolean
        If Double.IsNaN(a) AndAlso Double.IsNaN(b) Then Return True
        If Double.IsNaN(a) OrElse Double.IsNaN(b) Then Return False
        Return Math.Abs(a - b) < tolerance
    End Function
End Class

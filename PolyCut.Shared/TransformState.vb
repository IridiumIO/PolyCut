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
            .Height = Height
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
        state.Width = If(wrapper.ActualWidth > 0, wrapper.ActualWidth,
                  If(Double.IsNaN(wrapper.Width) OrElse wrapper.Width < 0, 0, wrapper.Width))
        state.Height = If(wrapper.ActualHeight > 0, wrapper.ActualHeight,
                  If(Double.IsNaN(wrapper.Height) OrElse wrapper.Height < 0, 0, wrapper.Height))
        state.TransformOrigin = wrapper.RenderTransformOrigin

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
        If Not Double.IsNaN(Width) Then wrapper.Width = Width
        If Not Double.IsNaN(Height) Then wrapper.Height = Height

        wrapper.RenderTransformOrigin = TransformOrigin
        wrapper.RenderTransform = If(Math.Abs(Rotation) > 0.01, New RotateTransform(Rotation), Nothing)

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

                Dim st = tg.Children.OfType(Of ScaleTransform)().FirstOrDefault()
                If st Is Nothing Then
                    st = New ScaleTransform(1, 1)
                    tg.Children.Add(st)
                End If

                st.ScaleX = Scale.X
                st.ScaleY = Scale.Y
                contentFe.RenderTransformOrigin = New Point(0.5, 0.5)
            End If
        End If
    End Sub

End Class

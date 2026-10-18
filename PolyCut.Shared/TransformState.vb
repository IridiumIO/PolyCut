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
    ' Scale/shear inherited when a visual group is removed; rotation stays independently editable.
    Public Property AffineTransform As Matrix = Matrix.Identity

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
            .AffineTransform = AffineTransform
        }
        Return c
    End Function


    ' The wrapper carries position, size, origin, and affine-then-rotation transforms.
    ' Mirror scale remains on the child element, matching project serialization.
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

        state.Rotation = GetRotation(wrapper)
        If wrapper.RenderTransform IsNot Nothing AndAlso Not TypeOf wrapper.RenderTransform Is RotateTransform Then
            state.AffineTransform = wrapper.RenderTransform.Value
            Dim unrotate = Matrix.Identity
            unrotate.Rotate(-state.Rotation)
            state.AffineTransform = Matrix.Multiply(state.AffineTransform, unrotate)
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


    ' Restore the same storage layout FromWrapper reads, including inherited scale/shear.
    Public Sub ApplyToWrapper(wrapper As ContentControl, Optional applyScale As Boolean = True)
        If wrapper Is Nothing Then Return

        Canvas.SetLeft(wrapper, Translation.X)
        Canvas.SetTop(wrapper, Translation.Y)
        If Not Double.IsNaN(Width) AndAlso Width > 0 Then wrapper.Width = Width
        If Not Double.IsNaN(Height) AndAlso Height > 0 Then wrapper.Height = Height

        wrapper.RenderTransformOrigin = TransformOrigin
        wrapper.RenderTransform = CreateRenderTransform(AffineTransform, Rotation)

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

    Public Shared Function GetRotation(wrapper As ContentControl) As Double
        Dim rotation = TryCast(wrapper?.RenderTransform, RotateTransform)
        If rotation IsNot Nothing Then Return rotation.Angle
        Dim group = TryCast(wrapper?.RenderTransform, TransformGroup)
        Return If(group?.Children.OfType(Of RotateTransform)().LastOrDefault()?.Angle, 0)
    End Function

    Public Shared Sub SetRotation(wrapper As ContentControl, angle As Double)
        If wrapper Is Nothing Then Return
        Dim state = FromWrapper(wrapper)
        wrapper.RenderTransform = CreateRenderTransform(state.AffineTransform, angle)
    End Sub

    Public Shared Sub SetAffineTransform(wrapper As ContentControl, affine As Matrix)
        If wrapper Is Nothing Then Return
        wrapper.RenderTransform = CreateRenderTransform(affine, GetRotation(wrapper))
    End Sub

    Private Shared Function CreateRenderTransform(affine As Matrix, angle As Double) As Transform
        If affine.IsIdentity Then Return New RotateTransform(angle)
        Dim group As New TransformGroup()
        group.Children.Add(New MatrixTransform(affine))
        group.Children.Add(New RotateTransform(angle))
        Return group
    End Function

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
        If Not MatrixMatches(AffineTransform, other.AffineTransform) Then Return False

        Return True
    End Function

    Private Shared Function MatrixMatches(a As Matrix, b As Matrix) As Boolean
        Const epsilon As Double = 0.000000001
        Return Math.Abs(a.M11 - b.M11) < epsilon AndAlso Math.Abs(a.M12 - b.M12) < epsilon AndAlso
               Math.Abs(a.M21 - b.M21) < epsilon AndAlso Math.Abs(a.M22 - b.M22) < epsilon AndAlso
               Math.Abs(a.OffsetX - b.OffsetX) < epsilon AndAlso Math.Abs(a.OffsetY - b.OffsetY) < epsilon
    End Function

    Private Shared Function SizeMatches(a As Double, b As Double, tolerance As Double) As Boolean
        If Double.IsNaN(a) AndAlso Double.IsNaN(b) Then Return True
        If Double.IsNaN(a) OrElse Double.IsNaN(b) Then Return False
        Return Math.Abs(a - b) < tolerance
    End Function
End Class

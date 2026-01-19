Imports System.Windows
Imports System.Windows.Media

Public Class TransformState

    Public Property Translation As Point
    Public Property Rotation As Double ' In degrees
    Public Property Scale As Point
    Public Property TransformOrigin As Point

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


    Public Function ToMatrix() As Matrix
        Dim matrix As New Matrix()

        matrix.ScaleAt(Scale.X, Scale.Y, TransformOrigin.X, TransformOrigin.Y)
        matrix.RotateAt(Rotation, TransformOrigin.X, TransformOrigin.Y)
        matrix.Translate(Translation.X, Translation.Y)

        Return matrix
    End Function

    Public Sub ApplyTo(element As FrameworkElement)
        If element Is Nothing Then Return

        element.RenderTransformOrigin = TransformOrigin

        Dim transformGroup As New TransformGroup()
        transformGroup.Children.Add(New ScaleTransform(Scale.X, Scale.Y))
        transformGroup.Children.Add(New RotateTransform(Rotation))
        transformGroup.Children.Add(New TranslateTransform(Translation.X, Translation.Y))

        element.RenderTransform = transformGroup
    End Sub


    Public Shared Function FromElement(element As FrameworkElement) As TransformState
        Dim state As New TransformState()

        If element Is Nothing Then Return state

        state.TransformOrigin = element.RenderTransformOrigin

        Dim transform = element.RenderTransform

        If TypeOf transform Is TransformGroup Then
            Dim group = CType(transform, TransformGroup)
            For Each t In group.Children
                If TypeOf t Is ScaleTransform Then
                    Dim scale = CType(t, ScaleTransform)
                    state.Scale = New Point(scale.ScaleX, scale.ScaleY)
                ElseIf TypeOf t Is RotateTransform Then
                    state.Rotation = CType(t, RotateTransform).Angle
                ElseIf TypeOf t Is TranslateTransform Then
                    Dim translate = CType(t, TranslateTransform)
                    state.Translation = New Point(translate.X, translate.Y)
                End If
            Next
        ElseIf TypeOf transform Is RotateTransform Then
            state.Rotation = CType(transform, RotateTransform).Angle
        ElseIf TypeOf transform Is ScaleTransform Then
            Dim scale = CType(transform, ScaleTransform)
            state.Scale = New Point(scale.ScaleX, scale.ScaleY)
        End If

        Return state
    End Function

    Public Function Clone() As TransformState
        Return New TransformState(Translation, Rotation, Scale, TransformOrigin)
    End Function

End Class

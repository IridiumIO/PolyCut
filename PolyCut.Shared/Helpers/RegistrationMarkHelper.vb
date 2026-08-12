Imports System.Windows

Public Class RegistrationMarkHelper
    Public Shared ReadOnly IsRegistrationMarkProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsRegistrationMark",
            GetType(Boolean),
            GetType(RegistrationMarkHelper),
            New FrameworkPropertyMetadata(False))

    Public Shared Function GetIsRegistrationMark(obj As DependencyObject) As Boolean
        If obj Is Nothing Then Return False
        Return CBool(obj.GetValue(IsRegistrationMarkProperty))
    End Function

    Public Shared Sub SetIsRegistrationMark(obj As DependencyObject, value As Boolean)
        If obj Is Nothing Then Return
        obj.SetValue(IsRegistrationMarkProperty, value)
    End Sub

    Public Shared Function IsRegistrationMark(elementOrDrawable As Object) As Boolean
        If elementOrDrawable Is Nothing Then Return False

        Dim element = TryCast(elementOrDrawable, DependencyObject)
        If element Is Nothing Then
            Dim drawable = TryCast(elementOrDrawable, IDrawable)
            If drawable IsNot Nothing Then element = TryCast(drawable.DrawableElement, DependencyObject)
        End If

        Return element IsNot Nothing AndAlso GetIsRegistrationMark(element)
    End Function
End Class

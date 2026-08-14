Imports System.Windows

Public Class TextEditHelper
    Public Shared ReadOnly IsEditingProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsEditing",
            GetType(Boolean),
            GetType(TextEditHelper),
            New FrameworkPropertyMetadata(False))

    Public Shared Function GetIsEditing(obj As DependencyObject) As Boolean
        If obj Is Nothing Then Return False
        Return CBool(obj.GetValue(IsEditingProperty))
    End Function

    Public Shared Sub SetIsEditing(obj As DependencyObject, value As Boolean)
        If obj Is Nothing Then Return
        obj.SetValue(IsEditingProperty, value)
    End Sub

    Public Shared ReadOnly IsTextStyleHostProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsTextStyleHost",
            GetType(Boolean),
            GetType(TextEditHelper),
            New FrameworkPropertyMetadata(False))

    Public Shared Function GetIsTextStyleHost(obj As DependencyObject) As Boolean
        If obj Is Nothing Then Return False
        Return CBool(obj.GetValue(IsTextStyleHostProperty))
    End Function

    Public Shared Sub SetIsTextStyleHost(obj As DependencyObject, value As Boolean)
        If obj Is Nothing Then Return
        obj.SetValue(IsTextStyleHostProperty, value)
    End Sub
End Class

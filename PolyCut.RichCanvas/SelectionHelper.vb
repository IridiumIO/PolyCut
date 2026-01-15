Imports System.Windows.Controls.Primitives

Imports PolyCut.Shared

Public Class SelectionHelper

    ' Attached property to track selection state on ContentControl wrappers
    ' This replaces Selector.IsSelected to avoid WPF's automatic single-selection behavior
    Public Shared ReadOnly IsItemSelectedProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "IsItemSelected", GetType(Boolean), GetType(SelectionHelper),
        New FrameworkPropertyMetadata(False, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Shared Function GetIsItemSelected(obj As DependencyObject) As Boolean
        Return CBool(obj.GetValue(IsItemSelectedProperty))
    End Function

    Public Shared Sub SetIsItemSelected(obj As DependencyObject, value As Boolean)
        obj.SetValue(IsItemSelectedProperty, value)
    End Sub


    'Synchronizes the IsItemSelected attached property for all drawables o match their IDrawable.IsSelected property.

    Public Shared Sub SyncSelectionStates(drawables As IEnumerable(Of IDrawable))
        If drawables Is Nothing Then Return

        For Each drawable In drawables
            If drawable?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(drawable.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim shouldBeSelected = drawable.IsSelected
                    Dim currentlySelected = GetIsItemSelected(wrapper)

                    If shouldBeSelected <> currentlySelected Then
                        SetIsItemSelected(wrapper, shouldBeSelected)
                    End If
                End If
            End If
        Next
    End Sub

End Class


Imports PolyCut.Shared

Public Class MetadataHelper

    ' DrawableReference property to link wrapper to IDrawable
    Public Shared ReadOnly DrawableReferenceProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "DrawableReference", GetType(IDrawable), GetType(MetadataHelper))

    Public Shared Sub SetDrawableReference(element As DependencyObject, value As IDrawable)
        element.SetValue(DrawableReferenceProperty, value)
    End Sub

    Public Shared Function GetDrawableReference(element As DependencyObject) As IDrawable
        Return CType(element.GetValue(DrawableReferenceProperty), IDrawable)
    End Function
End Class

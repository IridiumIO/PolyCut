Imports PolyCut.Shared

Public Class MetadataHelper

    Public Shared ReadOnly OriginalDimensionsProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "OriginalDimensions", GetType((Width As Double, Height As Double)?), GetType(MetadataHelper))

    Public Shared Sub SetOriginalDimensions(element As DependencyObject, value As (Width As Double, Height As Double)?)
        element.SetValue(OriginalDimensionsProperty, value)
    End Sub

    Public Shared Function GetOriginalDimensions(element As DependencyObject) As (Width As Double, Height As Double)?
        Return CType(element.GetValue(OriginalDimensionsProperty), (Width As Double, Height As Double)?)
    End Function

    Public Shared ReadOnly OriginalEndPointProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "OriginalEndPoint", GetType(Point?), GetType(MetadataHelper))

    Public Shared Sub SetOriginalEndPoint(element As DependencyObject, value As Point?)
        element.SetValue(OriginalEndPointProperty, value)
    End Sub

    Public Shared Function GetOriginalEndPoint(element As DependencyObject) As Point?
        Return CType(element.GetValue(OriginalEndPointProperty), Point?)
    End Function

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
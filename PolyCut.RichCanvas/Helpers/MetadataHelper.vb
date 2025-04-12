Public Class MetadataHelper
    ' Existing OriginalDimensionsProperty
    Public Shared ReadOnly OriginalDimensionsProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "OriginalDimensions", GetType((Width As Double, Height As Double)?), GetType(MetadataHelper))

    Public Shared Sub SetOriginalDimensions(element As DependencyObject, value As (Width As Double, Height As Double)?)
        element.SetValue(OriginalDimensionsProperty, value)
    End Sub

    Public Shared Function GetOriginalDimensions(element As DependencyObject) As (Width As Double, Height As Double)?
        Return CType(element.GetValue(OriginalDimensionsProperty), (Width As Double, Height As Double)?)
    End Function

    ' New OriginalEndPointProperty
    Public Shared ReadOnly OriginalEndPointProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "OriginalEndPoint", GetType(Point?), GetType(MetadataHelper))

    Public Shared Sub SetOriginalEndPoint(element As DependencyObject, value As Point?)
        element.SetValue(OriginalEndPointProperty, value)
    End Sub

    Public Shared Function GetOriginalEndPoint(element As DependencyObject) As Point?
        Return CType(element.GetValue(OriginalEndPointProperty), Point?)
    End Function
End Class
Imports System.Windows

Public Class CanvasHelper
    Public Shared ReadOnly CanvasLeftProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "CanvasLeft",
        GetType(Double),
        GetType(CanvasHelper),
        New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange))

    Public Shared Function GetCanvasLeft(obj As DependencyObject) As Double
        Return CDbl(obj.GetValue(CanvasLeftProperty))
    End Function

    Public Shared Sub SetCanvasLeft(obj As DependencyObject, value As Double)
        obj.SetValue(CanvasLeftProperty, value)
    End Sub

    Public Shared ReadOnly CanvasTopProperty As DependencyProperty = DependencyProperty.RegisterAttached(
        "CanvasTop",
        GetType(Double),
        GetType(CanvasHelper),
        New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange))

    Public Shared Function GetCanvasTop(obj As DependencyObject) As Double
        Return CDbl(obj.GetValue(CanvasTopProperty))
    End Function

    Public Shared Sub SetCanvasTop(obj As DependencyObject, value As Double)
        obj.SetValue(CanvasTopProperty, value)
    End Sub
End Class
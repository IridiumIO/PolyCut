Imports System.Windows

Imports Svg

Public Interface IDrawable

    ReadOnly Property Id As Guid

    Property Name As String

    Property DrawableElement As FrameworkElement

    Property IsHidden As Boolean
    Property IsSelected As Boolean
    ReadOnly Property VisualName As String

    Property ParentGroup As IDrawable
    Property Stroke As System.Windows.Media.Brush
    Property Fill As System.Windows.Media.Brush
    Property StrokeThickness As Double
    Function GetTransformedSVGElement() As SvgVisualElement
    Function DrawingToSVG() As SvgVisualElement

    Event SelectionChanged(sender As Object, e As EventArgs)

End Interface

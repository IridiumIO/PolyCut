Imports System.Windows

Imports Svg

Public Interface IDrawable

    Property Name As String

    Property DrawableElement As FrameworkElement

    Property Children As IEnumerable(Of IDrawable)

    Property IsHidden As Boolean
    Property IsSelected As Boolean
    ReadOnly Property VisualName As String

    Function GetTransformedSVGElement() As SvgVisualElement
    Function DrawingToSVG() As SvgVisualElement

    Event SelectionChanged(sender As Object, e As EventArgs)

End Interface

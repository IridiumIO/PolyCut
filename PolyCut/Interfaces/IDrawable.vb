Imports Svg

Public Interface IDrawable

    Property Name As String

    Property DrawableElement As FrameworkElement

    Property Children As IEnumerable(Of IDrawable)

    Property IsHidden As Boolean
    Property IsSelected As Boolean

    Function GetTransformedSVGElement() As SvgVisualElement


End Interface

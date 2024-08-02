Public Interface IDrawable

    Property Name As String

    Property Children As IObservable(Of IDrawable)

    Property IsHidden
    Property IsSelected


    Function GetShapes()

    Function IsWithinBounds(x As Double, y As Double)



End Interface

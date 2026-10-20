Public Class PathBasedElement
    Public ReadOnly Property FlattenedLines As List(Of GeoLine)
        Get
            Return Figures.SelectMany(Of GeoLine)(Function(x) x).ToList
        End Get
    End Property
    Property Figures As List(Of List(Of GeoLine))
    Property IsFilled As Boolean
    Property FillColor As String
End Class

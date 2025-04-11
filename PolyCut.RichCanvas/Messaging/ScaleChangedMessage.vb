Public Class ScaleChangedMessage

    Public Property NewScale As Double

    Public Sub New(newScale As Double)
        Me.NewScale = newScale
        ScaleChangedMessage.LastScale = newScale
    End Sub

    Public Shared Property LastScale As Double = 1


End Class

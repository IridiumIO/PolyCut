Public Class TransformCompletedMessage
    Public Property Items As List(Of (Drawable As IDrawable, Before As Object, After As Object))
    Public Sub New()
        Items = New List(Of (IDrawable, Object, Object))()
    End Sub
End Class

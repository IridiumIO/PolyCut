Public Class TranslationChangedMessage

    Public Property NewTranslation As Point

    Public Sub New(newTranslation As Point)
        Me.NewTranslation = newTranslation
    End Sub
End Class

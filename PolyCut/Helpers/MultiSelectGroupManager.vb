Imports PolyCut.Shared

Public Class MultiSelectGroupManager

    Private Shared _temporaryGroup As DrawableGroup = Nothing

    Public Shared Function CreateTemporaryGroup(selectedItems As IEnumerable(Of IDrawable), parentCanvas As Canvas) As DrawableGroup
        DisbandTemporaryGroup(parentCanvas)

        _temporaryGroup = DrawableGroup.CreateTemporaryGroup(selectedItems, parentCanvas)
        Return _temporaryGroup
    End Function


    Public Shared Sub DisbandTemporaryGroup(parentCanvas As Canvas)
        If _temporaryGroup IsNot Nothing AndAlso parentCanvas IsNot Nothing Then
            _temporaryGroup.DisbandTemporaryGroup(parentCanvas)
            _temporaryGroup = Nothing
        End If
    End Sub

    Public Shared ReadOnly Property CurrentTemporaryGroup As DrawableGroup
        Get
            Return _temporaryGroup
        End Get
    End Property

End Class

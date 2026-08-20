Imports System.ComponentModel


Friend NotInheritable Class LocalisationState
    Implements INotifyPropertyChanged

    Public Shared ReadOnly Instance As New LocalisationState()

    Private _version As Integer

    Public ReadOnly Property Version As Integer
        Get
            Return _version
        End Get
    End Property

    Friend Sub Refresh()
        _version += 1
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Version)))
    End Sub

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
End Class

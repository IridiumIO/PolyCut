Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports Wpf.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Class SettingsPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel

    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel


    End Sub

End Class
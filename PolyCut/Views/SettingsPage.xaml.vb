Imports System.ComponentModel
Imports System.Data.SqlTypes
Imports System.Xml
Imports WPF.Ui.Controls
Imports SharpVectors
Imports System.Windows.Media.Animation
Imports System.IO
Imports WPF.Ui.Abstractions.Controls
Class SettingsPage : Implements INavigableView(Of MainViewModel)

    Public ReadOnly Property ViewModel As MainViewModel Implements INavigableView(Of MainViewModel).ViewModel


    Sub New(viewmodel As MainViewModel)

        Me.ViewModel = viewmodel
        DataContext = viewmodel
        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub
End Class
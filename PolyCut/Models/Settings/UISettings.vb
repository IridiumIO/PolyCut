Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Public Class UISettings : Inherits SettingsBase : Implements ISettingsService

End Class

Partial Public Class UIConfiguration : Inherits ObservableObject : Implements ISaveable

    Public Property Version As Single = 0.1 Implements ISaveable.Version
    Public Property Name As String = "UIConfiguration" Implements ISaveable.Name
    Public Property ShowGrid As Boolean = True
    Public Property ShowCuttingMat As Boolean = False
    Public Property ShowWorkArea As Boolean = False
End Class


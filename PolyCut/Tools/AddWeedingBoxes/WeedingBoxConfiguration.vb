Imports PolyCut.Core

Public Class WeedingBoxConfiguration : Inherits SettingsBase : Implements ISaveable

    Public Property Version As Single = 0.1 Implements ISaveable.Version
    Public Property Name As String = NameOf(WeedingBoxConfiguration) Implements ISaveable.Name
    Public Property DesiredExteriorMargin As Double = 5
    Public Property MinimumInterObjectMargin As Double = 2
    Public Property Colour As String = "#FF32CD32"
End Class

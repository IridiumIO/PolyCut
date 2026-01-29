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


    <ObservableProperty> Private _GridConfig As GridConfiguration = New GridConfiguration()

End Class

Partial Public Class GridConfiguration : Inherits ObservableObject
    <ObservableProperty> Private _Spacing As Double = 10.0 'Grid spacing in mm
    <ObservableProperty> Private _InsetLeft As Double = 0.0
    <ObservableProperty> Private _InsetTop As Double = 0.0
    <ObservableProperty> Private _InsetRight As Double = 0.0
    <ObservableProperty> Private _InsetBottom As Double = 0.0
    <ObservableProperty> Private _GridBrush As String = "#80FFFFFF"
    <ObservableProperty> Private _SnapToGrid As Boolean = False


End Class

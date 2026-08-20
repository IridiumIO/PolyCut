Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Public Class UISettings : Inherits SettingsBase : Implements ISettingsService

End Class

Partial Public Class UIConfiguration : Inherits ObservableObject : Implements ISaveable

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Version))>
    <ObservableProperty> Private _Version As Single = 0.1

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Name))>
    <ObservableProperty> Private _Name As String = "UIConfiguration"

    <ObservableProperty> Private _Language As String = "en-AU"

    <ObservableProperty> Private _ShowGrid As Boolean = True
    <ObservableProperty> Private _ShowWorkArea As Boolean = False


    <ObservableProperty> Private _GridConfig As GridConfiguration = New GridConfiguration()

    <ObservableProperty> Private _PreviewDrawingBrush As String = "#FF00FF80"
    <ObservableProperty> Private _PreviewDrawingStrokeThickness As Double = 0.2

    <ObservableProperty> Private _PreviewTravelBrush As String = "#FFFF8000"
    <ObservableProperty> Private _PreviewTravelStrokeThickness As Double = 0.1

    <ObservableProperty> Private _PreviewCursorBrush As String = "#80FF0000"

    <ObservableProperty> Private _AddToStartMenu As Boolean = False

    <ObservableProperty> Private _CanvasThemeColour As String = "#16181D"

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

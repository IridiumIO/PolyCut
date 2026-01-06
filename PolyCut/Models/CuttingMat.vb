Imports System.Collections.ObjectModel

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core
Public Class CuttingMat : Inherits ObservableObject : Implements ISaveable
    Public Property Id As Guid = Guid.NewGuid()
    Public Property Version As Single = 0.1 Implements ISaveable.Version
    Public Property Name As String = "Standard 12 Cutting Mat" Implements ISaveable.Name
    Public Property DisplayName As String = "Standard 12"" Cutting Mat"
    Public Property Width As Decimal = 330.2
    Public Property Height As Decimal = 355.6
    Public Property SVGSource As String = "CuttingMat.Dark.svg"

    Public Shared ReadOnly Property VerticalAlignment As ObservableCollection(Of String) = New ObservableCollection(Of String)({"Top", "Bottom"})
    Public Shared ReadOnly Property HorizontalAlignment As ObservableCollection(Of String) = New ObservableCollection(Of String)({"Left", "Right"})
    Public Shared ReadOnly Property Rotation As ObservableCollection(Of Double) = New ObservableCollection(Of Double)({0, 90, 180, 270})
    Public ReadOnly Property QualifiedSVGSource As String
        Get
            Return IO.Path.Combine(SettingsHandler.CuttingMatSettings.SettingsFolder.FullName, SVGSource).ToString
        End Get
    End Property

End Class

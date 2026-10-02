Imports System.Globalization
Imports System.Text.RegularExpressions
Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Partial Public Class Printer : Inherits ObservableObject : Implements ISaveable

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Version))>
    <ObservableProperty> Private _Version As Single = 0.1

    <ImplementsProperty(GetType(ISaveable), NameOf(ISaveable.Name))>
    <ObservableProperty> Private _Name As String = "Ender 3 S1"

    <NotifyPropertyChangedFor(NameOf(BedRect))>
    <ObservableProperty> Private _BedWidth As Decimal = 235

    <NotifyPropertyChangedFor(NameOf(BedRect))>
    <ObservableProperty> Private _BedHeight As Decimal = 235

    Private _WorkingOffsetX As Decimal = 0
    Private _WorkingOffsetY As Decimal = 0
    Private _WorkingWidth As Decimal = 235
    Private _WorkingHeight As Decimal = 235

    Public Property WorkingOffsetX As Decimal
        Get
            Return _WorkingOffsetX
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetX, value, NameOf(WorkingOffsetX))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Property WorkingOffsetY As Decimal
        Get
            Return _WorkingOffsetY
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetY, value, NameOf(WorkingOffsetY))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Property WorkingWidth As Decimal
        Get
            Return _WorkingWidth
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingWidth, value, NameOf(WorkingWidth))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Property WorkingHeight As Decimal
        Get
            Return _WorkingHeight
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingHeight, value, NameOf(WorkingHeight))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Sub ClampWorkingArea()
        WorkingOffsetX = Math.Min(Math.Max(WorkingOffsetX, 0), BedWidth)
        WorkingOffsetY = Math.Min(Math.Max(WorkingOffsetY, 0), BedHeight)
        WorkingWidth = Math.Min(Math.Max(WorkingWidth, 0), BedWidth - WorkingOffsetX)
        WorkingHeight = Math.Min(Math.Max(WorkingHeight, 0), BedHeight - WorkingOffsetY)
    End Sub

    Public ReadOnly Property BedRect As Rect
        Get
            Return New Rect(0, 0, BedWidth, BedHeight)
        End Get
    End Property

    Public ReadOnly Property WorkingRect As Rect
        Get
            Return New Rect(WorkingOffsetX, WorkingOffsetY, WorkingWidth, WorkingHeight)
        End Get
    End Property

    <ObservableProperty> Private _StartGCode As String = $"G0 E0{Environment.NewLine}G21{Environment.NewLine}G28"
    <ObservableProperty> Private _EndGCode As String = $""
    <ObservableProperty> Private _PreviewStartGCode As String = $"G0 E0{Environment.NewLine}G21{Environment.NewLine}G28"
    <ObservableProperty> Private _PreviewEndGCode As String = $""

    <ObservableProperty> Private _ToolOffsetX As Decimal = 0
    <ObservableProperty> Private _ToolOffsetY As Decimal = 0

    Public Function Clone() As Printer
        Dim p As New Printer With {
            .Version = Me.Version,
            .Name = Me.Name,
            .BedWidth = Me.BedWidth,
            .BedHeight = Me.BedHeight,
            .WorkingOffsetX = Me.WorkingOffsetX,
            .WorkingOffsetY = Me.WorkingOffsetY,
            .WorkingWidth = Me.WorkingWidth,
            .WorkingHeight = Me.WorkingHeight,
            .StartGCode = Me.StartGCode,
            .EndGCode = Me.EndGCode
        }
        Return p
    End Function

    Public Sub CopyFrom(other As Printer)
        If other Is Nothing Then Return

        Me.Version = other.Version
        Me.Name = other.Name
        Me.BedWidth = other.BedWidth
        Me.BedHeight = other.BedHeight
        Me.WorkingOffsetX = other.WorkingOffsetX
        Me.WorkingOffsetY = other.WorkingOffsetY
        Me.WorkingWidth = other.WorkingWidth
        Me.WorkingHeight = other.WorkingHeight
        Me.StartGCode = other.StartGCode
        Me.EndGCode = other.EndGCode
    End Sub

End Class


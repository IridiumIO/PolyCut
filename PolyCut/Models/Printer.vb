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

    Private Sub OnBedWidthChanged(oldValue As Decimal, newValue As Decimal)
        WorkingOffsetX = If(WorkingOffsetX < newValue, WorkingOffsetX, 0)
        WorkingWidth = If(WorkingWidth < newValue - WorkingOffsetX, WorkingWidth, newValue - WorkingOffsetX)
    End Sub

    Private Sub OnBedHeightChanged(oldValue As Decimal, newValue As Decimal)
        WorkingOffsetY = If(WorkingOffsetY < newValue, WorkingOffsetY, 0)
        WorkingHeight = If(WorkingHeight < newValue - WorkingOffsetY, WorkingHeight, newValue - WorkingOffsetY)
    End Sub

    Private _WorkingOffsetX As Decimal = 0
    Private _WorkingOffsetY As Decimal = 0
    Private _WorkingWidth As Decimal = 235
    Private _WorkingHeight As Decimal = 235

    Public Property WorkingOffsetX As Decimal
        Get
            Return _WorkingOffsetX
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetX, If(value <= BedWidth, value, _WorkingOffsetX), NameOf(WorkingOffsetX))
            OnPropertyChanged(NameOf(WorkingRect))
            WorkingWidth = If(WorkingWidth <= BedWidth - WorkingOffsetX, WorkingWidth, BedWidth - WorkingOffsetX)
        End Set
    End Property

    Public Property WorkingOffsetY As Decimal
        Get
            Return _WorkingOffsetY
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingOffsetY, If(value <= BedHeight, value, _WorkingOffsetY), NameOf(WorkingOffsetY))
            OnPropertyChanged(NameOf(WorkingRect))
            WorkingHeight = If(WorkingHeight <= BedHeight - WorkingOffsetY, WorkingHeight, BedWidth - WorkingOffsetY)
        End Set
    End Property

    Public Property WorkingWidth As Decimal
        Get
            Return _WorkingWidth
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingWidth, If(value <= BedWidth - WorkingOffsetX, value, BedWidth - WorkingOffsetX), NameOf(WorkingWidth))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

    Public Property WorkingHeight As Decimal
        Get
            Return _WorkingHeight
        End Get
        Set(value As Decimal)
            SetProperty(_WorkingHeight, If(value <= BedHeight - WorkingOffsetY, value, BedHeight - WorkingOffsetY), NameOf(WorkingHeight))
            OnPropertyChanged(NameOf(WorkingRect))
        End Set
    End Property

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


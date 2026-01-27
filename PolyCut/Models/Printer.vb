Imports System.Globalization
Imports System.Text.RegularExpressions
Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.Core

Public Class Printer : Inherits ObservableObject : Implements ISaveable
    Public Property Version As Single = 0.1 Implements ISaveable.Version
    Public Property Name As String = "Ender 3 S1" Implements ISaveable.Name

    Public Property BedWidth As Decimal
        Get
            Return _BedWidth
        End Get
        Set(value As Decimal)
            _BedWidth = value
            WorkingOffsetX = If(WorkingOffsetX < value, WorkingOffsetX, 0)
            WorkingWidth = If(WorkingWidth < value - WorkingOffsetX, WorkingWidth, value - WorkingOffsetX)
        End Set
    End Property
    Public Property BedHeight As Decimal
        Get
            Return _BedHeight
        End Get
        Set(value As Decimal)
            _BedHeight = value
            WorkingOffsetY = If(WorkingOffsetY < value, WorkingOffsetY, 0)
            WorkingHeight = If(WorkingHeight < value - WorkingOffsetY, WorkingHeight, value - WorkingOffsetY)
        End Set
    End Property
    Public Property WorkingOffsetX As Decimal
        Get
            Return _WorkingOffsetX
        End Get
        Set(value As Decimal)


            _WorkingOffsetX = If(value <= BedWidth, value, _WorkingOffsetX)
            WorkingWidth = If(WorkingWidth <= BedWidth - WorkingOffsetX, WorkingWidth, BedWidth - WorkingOffsetX)

        End Set
    End Property
    Public Property WorkingOffsetY As Decimal
        Get
            Return _WorkingOffsetY
        End Get
        Set(value As Decimal)


            _WorkingOffsetY = If(value <= BedHeight, value, _WorkingOffsetY)
            WorkingHeight = If(WorkingHeight <= BedHeight - WorkingOffsetY, WorkingHeight, BedWidth - WorkingOffsetY)
        End Set
    End Property
    Public Property WorkingWidth As Decimal
        Get
            Return _WorkingWidth
        End Get
        Set(value As Decimal)


            _WorkingWidth = If(value <= BedWidth - WorkingOffsetX, value, BedWidth - WorkingOffsetX)
        End Set
    End Property
    Public Property WorkingHeight As Decimal
        Get
            Return _WorkingHeight
        End Get
        Set(value As Decimal)

            _WorkingHeight = If(value <= BedHeight - WorkingOffsetY, value, BedHeight - WorkingOffsetY)
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

    Public Property CuttingMat As CuttingMat
    Public Property CuttingMatVerticalAlignment As String = "Top"
    Public Property CuttingMatHorizontalAlignment As String = "Left"
    Public Property CuttingMatRotation As Double = 0

    Public Property StartGCode As String
        Get
            Return _StartGCode
        End Get
        Set(value As String)
            _StartGCode = value
        End Set
    End Property
    Public Property EndGCode As String
        Get
            Return _EndGCode
        End Get
        Set(value As String)
            _EndGCode = value
        End Set
    End Property

    Public Property PreviewStartGCode As String
        Get
            Return _PreviewStartGCode
        End Get
        Set(value As String)
            _PreviewStartGCode = value
        End Set
    End Property

    Public Property PreviewEndGCode As String
        Get
            Return _PreviewEndGCode
        End Get
        Set(value As String)
            _PreviewEndGCode = value
        End Set
    End Property


    Private _BedWidth As Decimal = 235
    Private _BedHeight As Decimal = 235
    Private _WorkingWidth As Decimal = 235
    Private _WorkingHeight As Decimal = 235
    Private _WorkingOffsetX As Decimal = 0
    Private _WorkingOffsetY As Decimal = 0

    Private _StartGCode As String = $"G0 E0{Environment.NewLine}G21{Environment.NewLine}G28"
    Private _EndGCode As String = $""
    Private _PreviewStartGCode As String = $"G0 E0{Environment.NewLine}G21{Environment.NewLine}G28"
    Private _PreviewEndGCode As String = $""

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
            .CuttingMat = Me.CuttingMat,
            .CuttingMatVerticalAlignment = Me.CuttingMatVerticalAlignment,
            .CuttingMatHorizontalAlignment = Me.CuttingMatHorizontalAlignment,
            .CuttingMatRotation = Me.CuttingMatRotation,
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
        Me.CuttingMat = other.CuttingMat
        Me.CuttingMatVerticalAlignment = other.CuttingMatVerticalAlignment
        Me.CuttingMatHorizontalAlignment = other.CuttingMatHorizontalAlignment
        Me.CuttingMatRotation = other.CuttingMatRotation
        Me.StartGCode = other.StartGCode
        Me.EndGCode = other.EndGCode
    End Sub

End Class


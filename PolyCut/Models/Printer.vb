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

    Private _BedWidth As Decimal = 235
    Private _BedHeight As Decimal = 235
    Private _WorkingWidth As Decimal = 235
    Private _WorkingHeight As Decimal = 235
    Private _WorkingOffsetX As Decimal = 0
    Private _WorkingOffsetY As Decimal = 0



End Class


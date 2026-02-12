Imports PolyCut.Shared

Public Class SidebarItemVM
    Public Sub New(parent As DrawableGroup, item As IDrawable)
        Me.ParentGroup = parent
        Me.Item = item
    End Sub

    Public ReadOnly Property ParentGroup As DrawableGroup
    Public ReadOnly Property Item As IDrawable

    Public ReadOnly Property ParentName As String
        Get
            Return If(ParentGroup?.Name = "Drawing Group", "Basic Drawing", ParentGroup.Name)
        End Get
    End Property

    Public ReadOnly Property Name As String
        Get
            Return Item?.Name
        End Get
    End Property

    Public ReadOnly Property VisualName As String
        Get
            Return Item?.VisualName
        End Get
    End Property
End Class

Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input

Imports PolyCut.Shared

Partial Public Class SidebarItemVM : Inherits ObservableObject

    <ObservableProperty> Private _isEditingName As Boolean
    Private _nameBeforeEdit As String

    Public Sub New(parent As DrawableGroup, item As IDrawable)
        Me.ParentGroup = parent
        Me.Item = item
    End Sub

    Public ReadOnly Property ParentGroup As DrawableGroup
    Public ReadOnly Property Item As IDrawable



    <RelayCommand>
    Public Sub BeginRename()
        _nameBeforeEdit = Item?.Name
        IsEditingName = True
    End Sub


    Public Sub CommitRename()
        Dim newName = Item?.Name?.Trim()
        If String.IsNullOrEmpty(newName) Then
            If Item IsNot Nothing Then Item.Name = _nameBeforeEdit
        Else
            If Item IsNot Nothing Then Item.Name = newName
        End If
        IsEditingName = False
    End Sub

    Public Sub CancelRename()
        If Item IsNot Nothing Then Item.Name = _nameBeforeEdit
        IsEditingName = False
    End Sub

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

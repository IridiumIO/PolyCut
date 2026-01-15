Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.[Shared]

Public Class DrawableGroup : Inherits BaseDrawable

    Public Property GroupChildren As New ObservableCollection(Of IDrawable)

    ' Flattened view for UI (all leaf drawables under this group, including nested groups)
    Public ReadOnly Property DisplayChildren As New ObservableCollection(Of IDrawable)

    Public Sub New(Optional groupName As String = "Group")
        Name = groupName
        ' Invisible placeholder element (group is logical, placeholder not shown - TODO, this is dumb)
        Dim placeholder As New ContentControl With {
            .Visibility = Visibility.Collapsed,
            .Width = 0,
            .Height = 0
        }
        DrawableElement = placeholder

        ' Keep flattened list in sync
        AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
    End Sub

    Private Sub OnGroupChildrenChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        RebuildDisplayChildren()
    End Sub

    Public Sub RebuildDisplayChildren()
        DisplayChildren.Clear()
        For Each child In GroupChildren
            If TypeOf child Is DrawableGroup Then
                Dim nested = CType(child, DrawableGroup)
                nested.RebuildDisplayChildren()
                For Each nd In nested.DisplayChildren
                    DisplayChildren.Add(nd)
                Next
            Else
                DisplayChildren.Add(child)
            End If
        Next
    End Sub

    Public Shadows Property Children As IEnumerable(Of IDrawable)
        Get
            Return GroupChildren
        End Get
        Set(value As IEnumerable(Of IDrawable))
            RemoveHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            GroupChildren.Clear()
            If value IsNot Nothing Then
                For Each c In value
                    GroupChildren.Add(c)
                Next
            End If
            AddHandler GroupChildren.CollectionChanged, AddressOf OnGroupChildrenChanged
            RebuildDisplayChildren()
        End Set
    End Property

    Public Sub AddChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then Return

        If child.ParentGroup IsNot Nothing Then
            Dim prior = TryCast(child.ParentGroup, DrawableGroup)
            If prior IsNot Nothing Then prior.GroupChildren.Remove(child)
        End If
        child.ParentGroup = Me
        GroupChildren.Add(child)
    End Sub

    Public Sub RemoveChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then
            GroupChildren.Remove(child)
            child.ParentGroup = Nothing
        End If
    End Sub


    Public Function GetAllLeafChildren() As List(Of IDrawable)
        Dim results As New List(Of IDrawable)
        For Each ch In GroupChildren
            Dim nested = TryCast(ch, DrawableGroup)
            If nested IsNot Nothing Then
                results.AddRange(nested.GetAllLeafChildren())
            Else
                results.Add(ch)
            End If
        Next
        Return results
    End Function

End Class
Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.[Shared]

Public Class DrawableGroup : Inherits BaseDrawable

    Public Property GroupChildren As New ObservableCollection(Of IDrawable)

    Private Shared ReadOnly _bulkTouchedGroups As New HashSet(Of DrawableGroup)

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

        If ModelBatch.Active Then
            If _bulkTouchedGroups.Add(Me) Then ModelBatch.OnEndOnce("GroupDisplayChildren", AddressOf RebuildTouchedGroups)
            Return
        End If
        RebuildDisplayChildren()
    End Sub

    Private Shared Sub RebuildTouchedGroups()
        For Each g In _bulkTouchedGroups
            g.RebuildDisplayChildren()
        Next
        _bulkTouchedGroups.Clear()
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


    Public Sub AddChild(child As IDrawable, Optional index As Integer = -1)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then Return

        If child.ParentGroup IsNot Nothing Then
            Dim prior = TryCast(child.ParentGroup, DrawableGroup)
            If prior IsNot Nothing Then prior.GroupChildren.Remove(child)
        End If
        child.ParentGroup = Me
        If index >= 0 AndAlso index < GroupChildren.Count Then
            GroupChildren.Insert(index, child)
        Else
            GroupChildren.Add(child)
        End If
    End Sub

    Public Sub RemoveChild(child As IDrawable)
        If child Is Nothing Then Return
        If GroupChildren.Contains(child) Then
            GroupChildren.Remove(child)
            child.ParentGroup = Nothing
        End If
    End Sub



End Class



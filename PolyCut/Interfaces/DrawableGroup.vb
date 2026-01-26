Imports System.Collections.ObjectModel
Imports System.Collections.Specialized

Imports PolyCut.[Shared]

Public Class DrawableGroup : Inherits BaseDrawable

    Public Property GroupChildren As New ObservableCollection(Of IDrawable)

    ' Flattened view for UI (all leaf drawables under this group, including nested groups)
    Public ReadOnly Property DisplayChildren As New ObservableCollection(Of IDrawable)

    ' Flag to indicate this is a temporary multi-select group
    Public Property IsTemporaryGroup As Boolean = False

    ' Container for group visuals when used as a temporary multi-select group
    Private _groupContainer As Canvas = Nothing

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


    Public Shared Function CreateTemporaryGroup(selectedItems As IEnumerable(Of IDrawable), parentCanvas As Canvas) As DrawableGroup
        Dim tempGroup As New DrawableGroup("_TempMultiSelectGroup") With {
            .IsTemporaryGroup = True
        }

        Dim itemsSnapshot = selectedItems.ToList()

        tempGroup._groupContainer = New Canvas With {
            .ClipToBounds = False
        }

        Dim wrapper As New ContentControl With {
            .Content = tempGroup._groupContainer,
            .ClipToBounds = False
        }

        tempGroup.DrawableElement = tempGroup._groupContainer

        Dim bounds = CalculateBounds(itemsSnapshot)

        For Each item In itemsSnapshot
            If item?.DrawableElement IsNot Nothing Then
                Dim itemWrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If itemWrapper IsNot Nothing Then

                    Dim itemLeft = Canvas.GetLeft(itemWrapper)
                    Dim itemTop = Canvas.GetTop(itemWrapper)

                    parentCanvas.Children.Remove(itemWrapper)

                    Canvas.SetLeft(itemWrapper, itemLeft - bounds.Left)
                    Canvas.SetTop(itemWrapper, itemTop - bounds.Top)

                    tempGroup._groupContainer.Children.Add(itemWrapper)
                    tempGroup.AddChild(item)
                End If
            End If
        Next


        tempGroup._groupContainer.Width = bounds.Width
        tempGroup._groupContainer.Height = bounds.Height

        Dim groupWrapper As New ContentControl With {
            .Content = tempGroup._groupContainer,
            .Width = bounds.Width,
            .Height = bounds.Height,
            .RenderTransform = New RotateTransform(0),
            .ClipToBounds = False
        }

        Canvas.SetLeft(groupWrapper, bounds.Left)
        Canvas.SetTop(groupWrapper, bounds.Top)
        groupWrapper.Style = TryCast(Application.Current?.TryFindResource("DesignerItemStyle"), Style)

        parentCanvas.Children.Add(groupWrapper)
        tempGroup.DrawableElement = tempGroup._groupContainer

        Return tempGroup
    End Function


    Public Sub DisbandTemporaryGroup(parentCanvas As Canvas)
        If Not IsTemporaryGroup OrElse _groupContainer Is Nothing Then Return

        Dim groupWrapper = TryCast(_groupContainer.Parent, ContentControl)
        If groupWrapper Is Nothing Then Return

        Dim groupLeft = Canvas.GetLeft(groupWrapper)
        Dim groupTop = Canvas.GetTop(groupWrapper)
        Dim groupTransform = TryCast(groupWrapper.RenderTransform, RotateTransform)

        Dim childrenSnapshot = GroupChildren.ToList()

        For Each item In childrenSnapshot
            If item?.DrawableElement IsNot Nothing Then
                Dim itemWrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If itemWrapper IsNot Nothing Then

                    Dim itemLeft = Canvas.GetLeft(itemWrapper)
                    Dim itemTop = Canvas.GetTop(itemWrapper)

                    _groupContainer.Children.Remove(itemWrapper)

                    Canvas.SetLeft(itemWrapper, groupLeft + itemLeft)
                    Canvas.SetTop(itemWrapper, groupTop + itemTop)


                    parentCanvas.Children.Add(itemWrapper)
                End If
            End If
        Next


        parentCanvas.Children.Remove(groupWrapper)

        GroupChildren.Clear()
    End Sub


    Private Shared Function CalculateBounds(items As IEnumerable(Of IDrawable)) As Rect
        Dim minX = Double.MaxValue
        Dim minY = Double.MaxValue
        Dim maxX = Double.MinValue
        Dim maxY = Double.MinValue

        For Each item In items
            If item?.DrawableElement IsNot Nothing Then
                Dim wrapper = TryCast(item.DrawableElement.Parent, ContentControl)
                If wrapper IsNot Nothing Then
                    Dim left = Canvas.GetLeft(wrapper)
                    Dim top = Canvas.GetTop(wrapper)
                    Dim right = left + wrapper.ActualWidth
                    Dim bottom = top + wrapper.ActualHeight

                    minX = Math.Min(minX, left)
                    minY = Math.Min(minY, top)
                    maxX = Math.Max(maxX, right)
                    maxY = Math.Max(maxY, bottom)
                End If
            End If
        Next

        Return New Rect(minX, minY, maxX - minX, maxY - minY)
    End Function

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



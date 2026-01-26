Imports System.Runtime.CompilerServices

Imports PolyCut.[Shared]

Friend Module ProjectGraph

    <Extension>
    Friend Function IsAnyGroup(d As IDrawable) As Boolean
        Return TypeOf d Is DrawableGroup OrElse TypeOf d Is NestedDrawableGroup
    End Function

    <Extension>
    Friend Function IsVisualGroup(d As IDrawable) As Boolean
        Return TypeOf d Is NestedDrawableGroup
    End Function

    <Extension>
    Friend Function GetGroupChildren(g As IDrawable) As IEnumerable(Of IDrawable)
        If TypeOf g Is DrawableGroup Then
            Return DirectCast(g, DrawableGroup).GroupChildren
        ElseIf TypeOf g Is NestedDrawableGroup Then
            Return DirectCast(g, NestedDrawableGroup).GroupChildren
        End If
        Return Enumerable.Empty(Of IDrawable)()
    End Function

    <Extension>
    Friend Function HasNestedAncestor(ByVal item As IDrawable) As Boolean
        Dim p = item?.ParentGroup
        While p IsNot Nothing
            If TypeOf p Is NestedDrawableGroup Then Return True
            p = p.ParentGroup
        End While
        Return False
    End Function

    Friend Sub WalkGraph(roots As IEnumerable(Of IDrawable),
                     ByRef groups As HashSet(Of IDrawable),
                     ByRef leaves As HashSet(Of IDrawable))

        Dim g = If(groups, New HashSet(Of IDrawable)())
        Dim l = If(leaves, New HashSet(Of IDrawable)())

        If roots IsNot Nothing Then
            For Each r In roots
                VisitNode(r, g, l)
            Next
        End If

        groups = g
        leaves = l
    End Sub

    Private Sub VisitNode(node As IDrawable,
                      groups As HashSet(Of IDrawable),
                      leaves As HashSet(Of IDrawable))

        If node Is Nothing Then Return

        If node.IsAnyGroup() Then
            If groups.Add(node) Then
                For Each ch In node.GetGroupChildren()
                    VisitNode(ch, groups, leaves)
                Next
            End If
        Else
            leaves.Add(node)
        End If
    End Sub

End Module

Friend Module CanvasUtil
    Friend Function GetLeftSafe(fe As FrameworkElement) As Double
        Dim v = Canvas.GetLeft(fe)
        If Double.IsNaN(v) Then Return 0
        Return v
    End Function

    Friend Function GetTopSafe(fe As FrameworkElement) As Double
        Dim v = Canvas.GetTop(fe)
        If Double.IsNaN(v) Then Return 0
        Return v
    End Function

    Friend Function GetWidthSafe(fe As FrameworkElement) As Double
        If fe Is Nothing Then Return 0
        If Not Double.IsNaN(fe.Width) AndAlso fe.Width > 0 Then Return fe.Width
        Return fe.ActualWidth
    End Function

    Friend Function GetHeightSafe(fe As FrameworkElement) As Double
        If fe Is Nothing Then Return 0
        If Not Double.IsNaN(fe.Height) AndAlso fe.Height > 0 Then Return fe.Height
        Return fe.ActualHeight
    End Function
End Module
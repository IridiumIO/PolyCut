Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class Tab_Elements

    'manually sync Listiew selection from canvas (avoids infinite loop from binding)
    Public Sub SyncListViewSelection(selectedDrawables As IEnumerable(Of IDrawable))
        Dim listViews = FindVisualChildren(Of ListView)(Me).ToList()

        ' Disconnect handlers to prevent cascading events
        For Each listView In listViews
            RemoveHandler listView.SelectionChanged, AddressOf ListView_SelectionChanged
        Next

        Try
            For Each listView In listViews
                If listView.ItemsSource IsNot Nothing Then
                    listView.SelectedItems.Clear()
                    For Each drawable In selectedDrawables
                        If TypeOf drawable Is DrawableGroup Then Continue For
                        If listView.Items.Contains(drawable) Then
                            listView.SelectedItems.Add(drawable)
                        End If
                    Next
                End If
            Next
        Finally
            ' Reconnect handlers
            For Each listView In listViews
                AddHandler listView.SelectionChanged, AddressOf ListView_SelectionChanged
            Next
        End Try
    End Sub

    Private Iterator Function FindVisualChildren(Of T As DependencyObject)(parent As DependencyObject) As IEnumerable(Of T)
        If parent Is Nothing Then Return
        Dim childCount = VisualTreeHelper.GetChildrenCount(parent)
        For i As Integer = 0 To childCount - 1
            Dim child = VisualTreeHelper.GetChild(parent, i)
            If TypeOf child Is T Then Yield CType(child, T)
            For Each descendant In FindVisualChildren(Of T)(child)
                Yield descendant
            Next
        Next
    End Function

    Private Sub ListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
        Dim isCtrlPressed As Boolean = Keyboard.IsKeyDown(Key.LeftCtrl) OrElse Keyboard.IsKeyDown(Key.RightCtrl)

        If Not isShiftPressed AndAlso Not isCtrlPressed Then
            PolyCanvas.ClearSelection()
        End If

        For Each item In e.AddedItems
            Dim drawable = TryCast(item, IDrawable)
            If drawable IsNot Nothing Then PolyCanvas.AddToSelection(drawable)
        Next

        For Each item In e.RemovedItems
            Dim drawable = TryCast(item, IDrawable)
            If drawable IsNot Nothing Then PolyCanvas.RemoveFromSelection(drawable)
        Next

        Dim mainViewModel = TryCast(DataContext, SVGPageViewModel)?.MainVM
        If mainViewModel IsNot Nothing Then
            SelectionHelper.SyncSelectionStates(mainViewModel.DrawableCollection)
        End If
    End Sub

End Class







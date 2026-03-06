Imports PolyCut.RichCanvas
Imports PolyCut.Shared

Public Class Tab_Elements

    'manually sync Listiew selection from canvas (avoids infinite loop from binding)
    Public Sub SyncListViewSelection(selectedDrawables As IEnumerable(Of IDrawable))
        Dim lv = FilesList ' your flat ListView
        If lv Is Nothing OrElse lv.ItemsSource Is Nothing Then Return

        RemoveHandler lv.SelectionChanged, AddressOf ListView_SelectionChanged
        Try
            lv.SelectedItems.Clear()

            Dim setSel = New HashSet(Of IDrawable)(
            selectedDrawables.Where(Function(d) d IsNot Nothing AndAlso Not TypeOf d Is DrawableGroup)
        )

            For Each obj In lv.Items
                Dim vm = TryCast(obj, SidebarItemVM)
                If vm?.Item IsNot Nothing AndAlso setSel.Contains(vm.Item) Then
                    lv.SelectedItems.Add(vm)
                End If
            Next
        Finally
            AddHandler lv.SelectionChanged, AddressOf ListView_SelectionChanged
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
            Dim vm = TryCast(item, SidebarItemVM)
            Dim drawable = vm?.Item
            If drawable IsNot Nothing AndAlso Not TypeOf drawable Is DrawableGroup Then
                PolyCanvas.AddToSelection(drawable)
            End If
        Next

        For Each item In e.RemovedItems
            Dim vm = TryCast(item, SidebarItemVM)
            Dim drawable = vm?.Item
            If drawable IsNot Nothing AndAlso Not TypeOf drawable Is DrawableGroup Then
                PolyCanvas.RemoveFromSelection(drawable)
            End If
        Next

        Dim mainViewModel = TryCast(DataContext, SVGPageViewModel)?.MainVM
        If mainViewModel IsNot Nothing Then
            SelectionHelper.SyncSelectionStates(mainViewModel.DrawableCollection)
        End If
    End Sub

End Class







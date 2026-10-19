Imports PolyCut.RichCanvas
Imports PolyCut.Shared
Imports GongSolutions.Wpf.DragDrop

Public Class Tab_Elements

    Sub New()

        ' This call is required by the designer.
        InitializeComponent()
        GongSolutions.Wpf.DragDrop.DragDrop.SetDropHandler(FilesList, New ElementsDropHandler(Me))

        ' Add any initialization after the InitializeComponent() call.

    End Sub

    Private Class ElementsDropHandler : Inherits DefaultDropHandler
        Private ReadOnly _owner As Tab_Elements

        Public Sub New(owner As Tab_Elements)
            _owner = owner
        End Sub

        Public Overrides Sub Drop(info As IDropInfo)
            Dim vm = TryCast(_owner.DataContext, SVGPageViewModel)?.MainVM
            If vm Is Nothing Then Return
            _owner._syncingFromCanvas = True
            Try
                MyBase.Drop(info)
                _owner.SyncListViewSelection(PolyCanvas.SelectedItems)
            Finally
                _owner._syncingFromCanvas = False
            End Try
            vm.ReorderSelectedItems(vm.FlatSidebarItems.Select(Function(row) row.Item).ToList())
        End Sub
    End Class

    'manually sync Listiew selection from canvas (avoids infinite loop from binding)
    Public Sub SyncListViewSelection(selectedDrawables As IEnumerable(Of IDrawable))
        Dim lv = FilesList ' your flat ListView
        If lv Is Nothing OrElse lv.ItemsSource Is Nothing Then Return

        RemoveHandler lv.SelectionChanged, AddressOf ListView_SelectionChanged
        Try
            Dim setSel = New HashSet(Of IDrawable)(selectedDrawables.Where(Function(d) d IsNot Nothing AndAlso Not TypeOf d Is DrawableGroup))

            Dim matches = lv.Items.OfType(Of SidebarItemVM)().Where(Function(vm) setSel.Contains(vm.Item))
            If lv.SelectionMode = SelectionMode.Single Then
                lv.SelectedItem = matches.FirstOrDefault()
            Else
                lv.SelectedItems.Clear()
                For Each vm In matches
                    lv.SelectedItems.Add(vm)
                Next
            End If
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

    Private _syncingFromCanvas As Boolean

    Private Sub ListView_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

        Dim mainViewModel = TryCast(DataContext, SVGPageViewModel)?.MainVM
        If mainViewModel IsNot Nothing AndAlso e.RemovedItems.Count > 0 Then
            Dim isProgrammaticRebuild = False
            For Each item In e.RemovedItems
                Dim vm = TryCast(item, SidebarItemVM)
                If vm IsNot Nothing AndAlso Not mainViewModel.FlatSidebarItems.Contains(vm) Then
                    isProgrammaticRebuild = True
                    Exit For
                End If
            Next

            If isProgrammaticRebuild Then
                ' Once the rebuild settles, restore the sidebar highlight from the canvas.
                Me.Dispatcher.BeginInvoke(Sub()
                                               _syncingFromCanvas = True
                                               Try
                                                   SyncListViewSelection(PolyCanvas.SelectedItems)
                                               Finally
                                                   _syncingFromCanvas = False
                                               End Try
                                           End Sub)
                Return
            End If
        End If

        If _syncingFromCanvas OrElse PolyCanvas.IsBatchingSelectionChanges Then Return

        Dim isShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)
        Dim isCtrlPressed As Boolean = Keyboard.IsKeyDown(Key.LeftCtrl) OrElse Keyboard.IsKeyDown(Key.RightCtrl)

        Dim added As New List(Of IDrawable)
        For Each item In e.AddedItems
            Dim d = TryCast(item, SidebarItemVM)?.Item
            If d IsNot Nothing AndAlso Not TypeOf d Is DrawableGroup Then added.Add(d)
        Next

        Dim removed As New List(Of IDrawable)
        For Each item In e.RemovedItems
            Dim d = TryCast(item, SidebarItemVM)?.Item
            If d IsNot Nothing AndAlso Not TypeOf d Is DrawableGroup Then removed.Add(d)
        Next

        PolyCanvas.ApplySelection(added, removed, Not isShiftPressed AndAlso Not isCtrlPressed)

        If mainViewModel IsNot Nothing Then
            SelectionHelper.SyncSelectionStates(mainViewModel.DrawableCollection)
        End If
    End Sub

    Private Sub NameTextBox_KeyDown(sender As Object, e As KeyEventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Return
        Dim vm = TryCast(tb.DataContext, SidebarItemVM)

        If e.Key = Key.Enter Then
            vm?.CommitRename()
            e.Handled = True
        ElseIf e.Key = Key.Escape Then
            vm?.CancelRename()
            e.Handled = True
        End If
    End Sub

    Private Sub NameTextBox_LostFocus(sender As Object, e As RoutedEventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb Is Nothing Then Return
        Dim vm = TryCast(tb.DataContext, SidebarItemVM)
        If vm IsNot Nothing AndAlso vm.IsEditingName Then
            vm.CommitRename()
        End If
    End Sub

    Private Sub NameTextBox_IsVisibleChanged(sender As Object, e As DependencyPropertyChangedEventArgs)
        Dim tb = TryCast(sender, TextBox)
        If tb IsNot Nothing AndAlso tb.IsVisible Then
            Dispatcher.BeginInvoke(Sub()
                                       tb.Focus()
                                       tb.SelectAll()
                                   End Sub)
        End If
    End Sub

End Class


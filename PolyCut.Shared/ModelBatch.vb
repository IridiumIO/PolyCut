

Public NotInheritable Class ModelBatch
    Implements IDisposable

    Private Shared _depth As Integer
    Private Shared ReadOnly _pendingKeys As New HashSet(Of String)
    Private Shared ReadOnly _pendingWork As New List(Of Action)
    Private Shared ReadOnly _pendingRestores As New List(Of PendingRestore)

    Public Shared ReadOnly Property Active As Boolean
        Get
            Return _depth > 0
        End Get
    End Property

    Public Shared Sub OnEndOnce(key As String, work As Action)
        If work Is Nothing OrElse key Is Nothing Then Return
        If Not _pendingKeys.Add(key) Then Return
        _pendingWork.Add(work)
    End Sub

    Public Shared Sub RestoreToCollection(manager As IDrawableManager, drawable As IDrawable, index As Integer, Optional afterInsert As Action = Nothing)
        If manager Is Nothing OrElse drawable Is Nothing Then Return

        If Not Active Then
            manager.AddDrawableToCollection(drawable, index)
            If afterInsert IsNot Nothing Then afterInsert()
            Return
        End If

        _pendingRestores.Add(New PendingRestore With {
            .Manager = manager,
            .Drawable = drawable,
            .Index = index,
            .AfterInsert = afterInsert
        })
    End Sub

    Private Class PendingRestore
        Public Manager As IDrawableManager
        Public Drawable As IDrawable
        Public Index As Integer
        Public AfterInsert As Action
    End Class

    Private Shared Sub FlushRestores()
        If _pendingRestores.Count = 0 Then Return

        Dim pending = _pendingRestores.OrderBy(Function(r) If(r.Index < 0, Integer.MaxValue, r.Index)).ToList()
        _pendingRestores.Clear()

        For Each r In pending
            Try
                r.Manager.AddDrawableToCollection(r.Drawable, r.Index)
                If r.AfterInsert IsNot Nothing Then r.AfterInsert()
            Catch
            End Try
        Next
    End Sub

    Public Shared Function Begin() As IDisposable
        _depth += 1
        Return New ModelBatch()
    End Function

    Private Sub Dispose() Implements IDisposable.Dispose
        If _depth = 0 Then Return
        _depth -= 1

        If _depth > 0 Then Return

        FlushRestores()

        For i As Integer = _pendingWork.Count - 1 To 0 Step -1
            Try
                _pendingWork(i)()
            Catch
            End Try
        Next

        _pendingWork.Clear()
        _pendingKeys.Clear()
    End Sub
End Class

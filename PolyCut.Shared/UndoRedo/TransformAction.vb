Imports System.Collections.Generic
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media

Imports PolyCut.Shared

Public Class TransformAction
    Implements IUndoableAction

    Public Class Snapshot
        Public Property Left As Double
        Public Property Top As Double
        Public Property Width As Double
        Public Property Height As Double
        Public Property RenderTransform As Transform
    End Class

    Private ReadOnly _items As New List(Of (Target As IDrawable, Before As Snapshot, After As Snapshot))

    Public Sub New(items As IEnumerable(Of (IDrawable, Snapshot, Snapshot)))
        If items IsNot Nothing Then
            _items.AddRange(items)
        End If
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Transform ({_items.Count} items)"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        Return True
    End Function

    Private Sub Apply(snap As Snapshot, target As IDrawable)
        If snap Is Nothing OrElse target Is Nothing OrElse target.DrawableElement Is Nothing Then Return
        Dim wrapper = TryCast(target.DrawableElement.Parent, ContentControl)
        If wrapper Is Nothing Then Return

        Canvas.SetLeft(wrapper, snap.Left)
        Canvas.SetTop(wrapper, snap.Top)
        wrapper.Width = snap.Width
        wrapper.Height = snap.Height
        wrapper.RenderTransform = snap.RenderTransform
    End Sub

    Public Sub Undo() Implements IUndoableAction.Undo
        For Each t In _items
            Apply(t.Before, t.Target)
        Next
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        For Each t In _items
            Apply(t.After, t.Target)
        Next
    End Sub

    Public Shared Function MakeSnapshotFromWrapper(wrapper As ContentControl) As Snapshot
        If wrapper Is Nothing Then Return Nothing
        Dim left = Canvas.GetLeft(wrapper)
        If Double.IsNaN(left) Then left = 0
        Dim top = Canvas.GetTop(wrapper)
        If Double.IsNaN(top) Then top = 0
        Return New Snapshot With {
            .Left = left,
            .Top = top,
            .Width = wrapper.ActualWidth,
            .Height = wrapper.ActualHeight,
            .RenderTransform = If(wrapper.RenderTransform, Nothing)
        }
    End Function
End Class
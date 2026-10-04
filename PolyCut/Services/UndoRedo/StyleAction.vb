Imports PolyCut.Shared

Public Class StyleAction
    Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _targets As List(Of IDrawable)
    Private ReadOnly _newFill As System.Windows.Media.Brush
    Private ReadOnly _newStroke As System.Windows.Media.Brush
    Private ReadOnly _newStrokeThickness As Nullable(Of Double)
    Private ReadOnly _previousThicknessOverrides As System.Collections.Generic.IDictionary(Of IDrawable, Double)
    Private ReadOnly _previousFillOverrides As System.Collections.Generic.IDictionary(Of IDrawable, System.Windows.Media.Brush)
    Private ReadOnly _previousStrokeOverrides As System.Collections.Generic.IDictionary(Of IDrawable, System.Windows.Media.Brush)

    Private _previous As New List(Of StyleRecord)()

    Public Sub New(manager As IDrawableManager, targets As IEnumerable(Of IDrawable), newFill As Media.Brush, newStroke As System.Windows.Media.Brush, Optional newStrokeThickness As Nullable(Of Double) = Nothing, Optional previousThicknessOverrides As IDictionary(Of IDrawable, Double) = Nothing, Optional previousFillOverrides As IDictionary(Of IDrawable, Media.Brush) = Nothing, Optional previousStrokeOverrides As IDictionary(Of IDrawable, Media.Brush) = Nothing)
        _manager = manager
        _targets = ExpandTargets(targets)
        _newFill = newFill
        _newStroke = newStroke
        _newStrokeThickness = newStrokeThickness
        _previousThicknessOverrides = previousThicknessOverrides
        _previousFillOverrides = previousFillOverrides
        _previousStrokeOverrides = previousStrokeOverrides
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Style ({_targets.Count} items)"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _targets Is Nothing OrElse _targets.Count = 0 Then Return False

        Dim fillOverrides = ExpandOverrideMap(_previousFillOverrides)
        Dim strokeOverrides = ExpandOverrideMap(_previousStrokeOverrides)
        Dim thicknessOverrides = ExpandOverrideMap(_previousThicknessOverrides)

        Dim captured As New List(Of StyleRecord)()
        For Each d In _targets
            Dim prevFill As System.Windows.Media.Brush = Nothing
            Dim prevStroke As System.Windows.Media.Brush = Nothing

            If fillOverrides IsNot Nothing AndAlso fillOverrides.ContainsKey(d) Then
                prevFill = fillOverrides(d)
            Else
                prevFill = d.Fill
            End If

            If strokeOverrides IsNot Nothing AndAlso strokeOverrides.ContainsKey(d) Then
                prevStroke = strokeOverrides(d)
            Else
                prevStroke = d.Stroke
            End If
            Dim prevThickness As Double
            If thicknessOverrides IsNot Nothing AndAlso thicknessOverrides.ContainsKey(d) Then
                prevThickness = thicknessOverrides(d)
            Else
                prevThickness = d.StrokeThickness
            End If

            captured.Add(CapturePrevious(d, prevFill, prevStroke, prevThickness))
        Next

        _previous.Clear()
        _previous.AddRange(captured)

        For Each d In _targets
            Dim group = TryCast(d, NestedDrawableGroup)
            If group IsNot Nothing Then
                If _newFill IsNot Nothing Then group.Fill = _newFill
                If _newStroke IsNot Nothing Then group.Stroke = _newStroke
                If _newStrokeThickness.HasValue Then group.StrokeThickness = _newStrokeThickness.Value
            Else
                If _newFill IsNot Nothing Then d.Fill = _newFill
                If _newStroke IsNot Nothing Then d.Stroke = _newStroke
                If _newStrokeThickness.HasValue Then d.StrokeThickness = _newStrokeThickness.Value
            End If
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing Then mainVM.NotifyCollectionsChanged()

        Return True
    End Function


    Private Shared Function ExpandTargets(targets As IEnumerable(Of IDrawable)) As List(Of IDrawable)
        Dim result As New List(Of IDrawable)()
        If targets Is Nothing Then Return result

        For Each d In targets
            If d Is Nothing Then Continue For

            Dim ng = TryCast(d, NestedDrawableGroup)
            If ng IsNot Nothing Then
                ' Apply style to the group AND its current leaves so Undo/Redo is deterministic
                result.Add(ng)
                For Each leaf In ng.GetAllLeafChildren()
                    If leaf IsNot Nothing Then result.Add(leaf)
                Next
            Else
                result.Add(d)
            End If
        Next

        ' Avoid duplicates if multiple selections overlap
        Return result.Distinct().ToList()
    End Function


    Private Shared Function ExpandOverrideMap(Of T)(input As IDictionary(Of IDrawable, T)) As IDictionary(Of IDrawable, T)
        If input Is Nothing Then Return Nothing

        Dim expanded As New Dictionary(Of IDrawable, T)()

        For Each kv In input
            expanded(kv.Key) = kv.Value

            Dim ng = TryCast(kv.Key, NestedDrawableGroup)
            If ng IsNot Nothing Then
                For Each leaf In ng.GetAllLeafChildren()
                    If leaf IsNot Nothing AndAlso Not expanded.ContainsKey(leaf) Then
                        expanded(leaf) = kv.Value
                    End If
                Next
            End If
        Next

        Return expanded
    End Function


    Private Function CapturePrevious(d As IDrawable, prevFill As System.Windows.Media.Brush, prevStroke As System.Windows.Media.Brush, prevThickness As Double) As StyleRecord
        Dim record As New StyleRecord With {
            .Drawable = d,
            .Fill = prevFill,
            .Stroke = prevStroke,
            .Thickness = prevThickness
        }

        Dim group = TryCast(d, NestedDrawableGroup)
        If group IsNot Nothing Then
            record.IsGroup = True
            record.GroupStates = New List(Of (Group As NestedDrawableGroup, State As NestedDrawableGroup.StyleState))()
            CaptureGroupStates(group, record.GroupStates)
        End If

        Return record
    End Function


    Private Shared Sub CaptureGroupStates(group As NestedDrawableGroup, states As List(Of (Group As NestedDrawableGroup, State As NestedDrawableGroup.StyleState)))
        states.Add((group, group.CaptureOwnStyle()))

        For Each child In group.GroupChildren
            Dim nested = TryCast(child, NestedDrawableGroup)
            If nested IsNot Nothing Then CaptureGroupStates(nested, states)
        Next
    End Sub


    Public Sub Undo() Implements IUndoableAction.Undo

        For Each record In _previous
            RestorePrevious(record)
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing Then mainVM.NotifyCollectionsChanged()
    End Sub


    Private Sub RestorePrevious(record As StyleRecord)
        If record Is Nothing OrElse record.Drawable Is Nothing Then Return

        If record.IsGroup AndAlso record.GroupStates IsNot Nothing Then
            For Each entry In record.GroupStates
                entry.Group.RestoreOwnStyle(entry.State)
            Next
            Return
        End If

        record.Drawable.Fill = record.Fill
        record.Drawable.Stroke = record.Stroke
        record.Drawable.StrokeThickness = record.Thickness
    End Sub

    Private Class StyleRecord
        Public Drawable As IDrawable
        Public IsGroup As Boolean
        Public Fill As System.Windows.Media.Brush
        Public Stroke As System.Windows.Media.Brush
        Public Thickness As Double
        Public GroupStates As List(Of (Group As NestedDrawableGroup, State As NestedDrawableGroup.StyleState))
    End Class

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class

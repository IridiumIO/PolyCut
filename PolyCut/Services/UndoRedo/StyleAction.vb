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

    Private _previous As New List(Of (IDrawable, System.Windows.Media.Brush, System.Windows.Media.Brush, Double))

    Public Sub New(manager As IDrawableManager, targets As IEnumerable(Of IDrawable), newFill As System.Windows.Media.Brush, newStroke As System.Windows.Media.Brush, Optional newStrokeThickness As Nullable(Of Double) = Nothing, Optional previousThicknessOverrides As System.Collections.Generic.IDictionary(Of IDrawable, Double) = Nothing, Optional previousFillOverrides As System.Collections.Generic.IDictionary(Of IDrawable, System.Windows.Media.Brush) = Nothing, Optional previousStrokeOverrides As System.Collections.Generic.IDictionary(Of IDrawable, System.Windows.Media.Brush) = Nothing)
        _manager = manager
        _targets = If(targets?.ToList(), New List(Of IDrawable)())
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

        For Each d In _targets
            Dim prevFill As System.Windows.Media.Brush = Nothing
            Dim prevStroke As System.Windows.Media.Brush = Nothing

            If _previousFillOverrides IsNot Nothing AndAlso _previousFillOverrides.ContainsKey(d) Then
                prevFill = _previousFillOverrides(d)
            Else
                prevFill = d.Fill
            End If

            If _previousStrokeOverrides IsNot Nothing AndAlso _previousStrokeOverrides.ContainsKey(d) Then
                prevStroke = _previousStrokeOverrides(d)
            Else
                prevStroke = d.Stroke
            End If
            Dim prevThickness As Double
            If _previousThicknessOverrides IsNot Nothing AndAlso _previousThicknessOverrides.ContainsKey(d) Then
                prevThickness = _previousThicknessOverrides(d)
            Else
                prevThickness = d.StrokeThickness
            End If
            _previous.Add((d, prevFill, prevStroke, prevThickness))

            If _newFill IsNot Nothing Then d.Fill = _newFill
            If _newStroke IsNot Nothing Then d.Stroke = _newStroke
            If _newStrokeThickness.HasValue Then d.StrokeThickness = _newStrokeThickness.Value
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing Then mainVM.NotifyCollectionsChanged()

        Return True
    End Function

    Public Sub Undo() Implements IUndoableAction.Undo
        For Each t In _previous
            Dim d = t.Item1
            d.Fill = t.Item2
            d.Stroke = t.Item3
            d.StrokeThickness = t.Item4
        Next

        Dim mainVM = TryCast(_manager, MainViewModel)
        If mainVM IsNot Nothing Then mainVM.NotifyCollectionsChanged()
    End Sub

    Public Sub Redo() Implements IUndoableAction.Redo
        Execute()
    End Sub

End Class

Imports PolyCut.Shared

Public Class StyleAction
    Implements IUndoableAction

    Private ReadOnly _manager As IDrawableManager
    Private ReadOnly _targets As List(Of IDrawable)
    Private ReadOnly _newFill As System.Windows.Media.Brush
    Private ReadOnly _newStroke As System.Windows.Media.Brush
    Private ReadOnly _newStrokeThickness As Nullable(Of Double)
    Private ReadOnly _previousThicknessOverride As Nullable(Of Double)
    Private ReadOnly _previousFillOverride As System.Windows.Media.Brush
    Private ReadOnly _previousStrokeOverride As System.Windows.Media.Brush

    Private _previous As New List(Of (IDrawable, System.Windows.Media.Brush, System.Windows.Media.Brush, Double))

    Public Sub New(manager As IDrawableManager, targets As IEnumerable(Of IDrawable), newFill As System.Windows.Media.Brush, newStroke As System.Windows.Media.Brush, Optional newStrokeThickness As Nullable(Of Double) = Nothing, Optional previousThicknessOverride As Nullable(Of Double) = Nothing, Optional previousFillOverride As System.Windows.Media.Brush = Nothing, Optional previousStrokeOverride As System.Windows.Media.Brush = Nothing)
        _manager = manager
        _targets = If(targets?.ToList(), New List(Of IDrawable)())
        _newFill = newFill
        _newStroke = newStroke
        _newStrokeThickness = newStrokeThickness
        _previousThicknessOverride = previousThicknessOverride
        _previousFillOverride = previousFillOverride
        _previousStrokeOverride = previousStrokeOverride
    End Sub

    Public ReadOnly Property Description As String Implements IUndoableAction.Description
        Get
            Return $"Style ({_targets.Count} items)"
        End Get
    End Property

    Public Function Execute() As Boolean Implements IUndoableAction.Execute
        If _targets Is Nothing OrElse _targets.Count = 0 Then Return False

        For Each d In _targets
            Dim prevFill = If(_previousFillOverride IsNot Nothing, _previousFillOverride, d.Fill)
            Dim prevStroke = If(_previousStrokeOverride IsNot Nothing, _previousStrokeOverride, d.Stroke)
            Dim prevThickness = If(_previousThicknessOverride.HasValue, _previousThicknessOverride.Value, d.StrokeThickness)
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

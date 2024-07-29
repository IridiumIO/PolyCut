Public Class ResizeRotateAdorner : Inherits Adorner

    Private visuals As VisualCollection
    Public chrome As ResizeRotateChrome


    Public Sub New(designerItem As ContentControl)
        MyBase.New(designerItem)
        Me.chrome = New ResizeRotateChrome
        Me.chrome.DataContext = designerItem
        Me.visuals = New VisualCollection(Me)
        Me.visuals.Add(Me.chrome)

        AddHandler Me.MouseWheel, AddressOf Me.ResizeRotateAdorner_MouseWheel
        AddHandler Me.PreviewMouseWheel, AddressOf Me.ResizeRotateAdorner_MouseWheel
    End Sub

    Private Sub ResizeRotateAdorner_MouseWheel(sender As Object, e As MouseWheelEventArgs)
        e.Handled = False
    End Sub

    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return Me.visuals.Count
        End Get
    End Property

    Protected Overrides Function GetVisualChild(index As Integer) As Visual
        Return Me.visuals(index)
    End Function

    'Protected Overrides Function MeasureOverride(constraint As Size) As Size
    '    Me.chrome.Measure(constraint)
    '    Return Me.chrome.DesiredSize
    'End Function

    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size
        Me.chrome.Arrange(New Rect(finalSize))
        Return finalSize
    End Function



End Class

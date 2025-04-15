Public Class SizeAdorner
    Inherits Adorner

    Private ReadOnly chrome As SizeChrome
    Private ReadOnly visuals As VisualCollection
    Private ReadOnly designerItem As ContentControl

    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return Me.visuals.Count
        End Get
    End Property

    Public Sub New(designerItem As ContentControl)
        MyBase.New(designerItem)
        Me.SnapsToDevicePixels = True
        Me.designerItem = designerItem
        Me.chrome = New SizeChrome With {
            .DataContext = designerItem
        }
        Me.visuals = New VisualCollection(Me) From {
            Me.chrome
        }
    End Sub

    Protected Overrides Function GetVisualChild(index As Integer) As Visual
        Return Me.visuals(index)
    End Function

    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size
        Me.chrome.Arrange(New Rect(New Point(0.0, 0.0), finalSize))
        Return finalSize
    End Function
End Class
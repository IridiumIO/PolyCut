Imports System.ComponentModel


Partial Public Class GridPreviewControl
    Inherits UserControl

    Public Sub New()
        InitializeComponent()
        AddHandler Me.SizeChanged, AddressOf OnSizeChanged
    End Sub

    Public Shared ReadOnly GridConfigProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridConfig), GetType(GridConfiguration), GetType(GridPreviewControl), New PropertyMetadata(Nothing, AddressOf OnGridConfigChanged))
    Public Shared ReadOnly GridClipRectProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridClipRect), GetType(Rect), GetType(GridPreviewControl), New PropertyMetadata(Rect.Empty))
    Public Shared ReadOnly PrinterGridPreviewViewportProperty As DependencyProperty = DependencyProperty.Register(NameOf(PrinterGridPreviewViewport), GetType(Rect), GetType(GridPreviewControl), New PropertyMetadata(Rect.Empty))
    Public Shared ReadOnly GridLineHorizontalEndProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridLineHorizontalEnd), GetType(Point), GetType(GridPreviewControl), New PropertyMetadata(New Point(0, 0)))
    Public Shared ReadOnly GridLineVerticalEndProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridLineVerticalEnd), GetType(Point), GetType(GridPreviewControl), New PropertyMetadata(New Point(0, 0)))
    Public Shared ReadOnly GridLineThicknessProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridLineThickness), GetType(Double), GetType(GridPreviewControl), New PropertyMetadata(0.3))
    Public Shared ReadOnly GridLineBrushProperty As DependencyProperty = DependencyProperty.Register(NameOf(GridLineBrush), GetType(Brush), GetType(GridPreviewControl), New PropertyMetadata(New SolidColorBrush(Color.FromArgb(&H80, &HFF, &HFF, &HFF))))

    Public Property GridConfig As GridConfiguration
        Get
            Return GetValue(Of GridConfiguration)(GridConfigProperty)
        End Get
        Set(value As GridConfiguration)
            SetValue(GridConfigProperty, value)
        End Set
    End Property

    Private Shared Sub OnGridConfigChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim ctl = TryCast(d, GridPreviewControl)
        If ctl Is Nothing Then Return
        ctl.OnGridConfigChanged(TryCast(e.OldValue, GridConfiguration), TryCast(e.NewValue, GridConfiguration))
    End Sub

    Private Sub OnGridConfigChanged(oldCfg As GridConfiguration, newCfg As GridConfiguration)
        If oldCfg IsNot Nothing Then
            RemoveHandler oldCfg.PropertyChanged, AddressOf GridConfig_PropertyChanged
        End If
        If newCfg IsNot Nothing Then
            AddHandler newCfg.PropertyChanged, AddressOf GridConfig_PropertyChanged
        End If
        UpdateDerivedProperties()
    End Sub

    Private Sub GridConfig_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        UpdateDerivedProperties()
    End Sub

    Private Sub OnSizeChanged(sender As Object, e As SizeChangedEventArgs)
        UpdateDerivedProperties()
    End Sub


    Public Property GridClipRect As Rect
        Get
            Return GetValue(Of Rect)(GridClipRectProperty)
        End Get
        Set(value As Rect)
            SetValue(GridClipRectProperty, value)
        End Set
    End Property

    Public Property PrinterGridPreviewViewport As Rect
        Get
            Return GetValue(Of Rect)(PrinterGridPreviewViewportProperty)
        End Get
        Set(value As Rect)
            SetValue(PrinterGridPreviewViewportProperty, value)
        End Set
    End Property

    Public Property GridLineHorizontalEnd As Point
        Get
            Return GetValue(Of Point)(GridLineHorizontalEndProperty)
        End Get
        Set(value As Point)
            SetValue(GridLineHorizontalEndProperty, value)
        End Set
    End Property

    Public Property GridLineVerticalEnd As Point
        Get
            Return GetValue(Of Point)(GridLineVerticalEndProperty)
        End Get
        Set(value As Point)
            SetValue(GridLineVerticalEndProperty, value)
        End Set
    End Property

    Public Property GridLineThickness As Double
        Get
            Return GetValue(Of Double)(GridLineThicknessProperty)
        End Get
        Set(value As Double)
            SetValue(GridLineThicknessProperty, value)
            UpdateDerivedProperties()
        End Set
    End Property

    Public Property GridLineBrush As Brush
        Get
            Return GetValue(Of Brush)(GridLineBrushProperty)
        End Get
        Set(value As Brush)
            SetValue(GridLineBrushProperty, value)
        End Set
    End Property

    Private Sub UpdateDerivedProperties()
        Try
            Dim spacing As Double = 10.0
            Dim insetLeft As Double = 0.0
            Dim insetTop As Double = 0.0
            Dim insetRight As Double = 0.0
            Dim insetBottom As Double = 0.0
            Dim gridBrushString As String = Nothing

            If GridConfig IsNot Nothing Then
                spacing = GridConfig.Spacing
                insetLeft = GridConfig.InsetLeft
                insetTop = GridConfig.InsetTop
                insetRight = GridConfig.InsetRight
                insetBottom = GridConfig.InsetBottom
                gridBrushString = GridConfig.GridBrush
            End If

            Dim bedWidth = Me.Width
            Dim bedHeight = Me.Height

            If Double.IsNaN(bedWidth) Then bedWidth = 0
            If Double.IsNaN(bedHeight) Then bedHeight = 0

            Dim left = Math.Max(insetLeft, 0)
            Dim top = Math.Max(insetTop, 0)
            Dim right = Math.Max(bedWidth - insetRight, 0)
            Dim bottom = Math.Max(bedHeight - insetBottom, 0)

            Dim w = Math.Max(0, right - left + (GridLineThickness / 2.0))
            Dim h = Math.Max(0, bottom - top + (GridLineThickness / 2.0))

            ' Round to device-friendly values to avoid sub-pixel differences
            left = Math.Round(left, 2)
            top = Math.Round(top, 2)
            w = Math.Round(w, 2)
            h = Math.Round(h, 2)

            GridClipRect = New Rect(left, top, w, h)

            PrinterGridPreviewViewport = New Rect(insetLeft, insetTop, spacing, spacing)
            GridLineVerticalEnd = New Point(0, spacing + 1)
            GridLineHorizontalEnd = New Point(spacing + 1, 0)

            If Not String.IsNullOrEmpty(gridBrushString) Then
                Dim conv As New BrushConverter()
                Try
                    Dim b = TryCast(conv.ConvertFromString(gridBrushString), Brush)
                    If b IsNot Nothing Then GridLineBrush = b
                Catch ex As Exception

                End Try
            End If

        Catch ex As Exception
        End Try
    End Sub

End Class

Public Module DependencyPropertyHelpers

    <Runtime.CompilerServices.Extension>
    Public Function GetValue(Of T)(obj As DependencyObject, dp As DependencyProperty) As T
        Return CType(obj.GetValue(dp), T)
    End Function

    <Runtime.CompilerServices.Extension>
    Public Sub SetValue(Of T)(obj As DependencyObject, dp As DependencyProperty, value As T)
        obj.SetValue(dp, value)
    End Sub

End Module
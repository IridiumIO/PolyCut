Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

' Geometry-backed view for existing drawables from old projects
Public Class GeometricShape
    Inherits Shape

    Public Shared ReadOnly DataProperty As DependencyProperty = DependencyProperty.Register(NameOf(Data), GetType(Geometry), GetType(GeometricShape), New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.AffectsMeasure Or FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property Data As Geometry
        Get
            Return DirectCast(GetValue(DataProperty), Geometry)
        End Get
        Set(value As Geometry)
            SetValue(DataProperty, value)
        End Set
    End Property

    Public Property Kind As String = "Path"

    Public ReadOnly Property BoundsTransform As Matrix
        Get
            Return TransformMath.GeometryBoundsTransform(If(Data?.Bounds, Rect.Empty), RenderSize)
        End Get
    End Property

    Protected Overrides ReadOnly Property DefiningGeometry As Geometry
        Get
            If Data Is Nothing Then Return Geometry.Empty
            If Kind = "Line" Then
                Dim line = DirectCast(Data, LineGeometry)
                Return New LineGeometry(New Point(line.StartPoint.X * RenderSize.Width, line.StartPoint.Y * RenderSize.Height),
                                        New Point(line.EndPoint.X * RenderSize.Width, line.EndPoint.Y * RenderSize.Height))
            End If
            Dim result = Data.Clone()
            Dim fit = If(result.Transform?.Value, Matrix.Identity)
            fit.Append(BoundsTransform)
            result.Transform = New MatrixTransform(fit)
            Return result
        End Get
    End Property

    Public Function LocalGeometry() As Geometry
        Return DefiningGeometry
    End Function

    Protected Overrides Function MeasureOverride(availableSize As Size) As Size
        If Data Is Nothing OrElse Data.Bounds.IsEmpty Then Return New Size()
        Return New Size(Math.Min(Data.Bounds.Width, availableSize.Width), Math.Min(Data.Bounds.Height, availableSize.Height))
    End Function

    Protected Overrides Function ArrangeOverride(finalSize As Size) As Size
        Return finalSize
    End Function

    Protected Overrides Function GetLayoutClip(layoutSlotSize As Size) As Geometry
        Return Nothing
    End Function

    Protected Overrides Sub OnRender(context As DrawingContext)
        Dim pen As Pen = Nothing
        If Stroke IsNot Nothing AndAlso StrokeThickness > 0 Then
            pen = New Pen(Stroke, StrokeThickness) With {
                .StartLineCap = StrokeStartLineCap, .EndLineCap = StrokeEndLineCap,
                .DashCap = StrokeDashCap, .LineJoin = StrokeLineJoin, .MiterLimit = StrokeMiterLimit,
                .DashStyle = If(StrokeDashArray IsNot Nothing AndAlso StrokeDashArray.Count > 0,
                                New DashStyle(StrokeDashArray, StrokeDashOffset), DashStyles.Solid)
            }
        End If
        context.DrawGeometry(Fill, pen, DefiningGeometry)
    End Sub

    Public Shared Function FromShape(source As Shape) As GeometricShape
        If TypeOf source Is GeometricShape Then Return DirectCast(source, GeometricShape)
        Dim geometry As Geometry
        Dim kind As String
        If TypeOf source Is Path Then
            geometry = DirectCast(source, Path).Data?.Clone()
            kind = "Path"
        ElseIf TypeOf source Is Rectangle Then
            Dim rect = DirectCast(source, Rectangle)
            geometry = New RectangleGeometry(New Rect(0, 0, 1, 1), rect.RadiusX, rect.RadiusY)
            kind = "Rectangle"
        ElseIf TypeOf source Is Ellipse Then
            geometry = New EllipseGeometry(New Point(0.5, 0.5), 0.5, 0.5)
            kind = "Ellipse"
        ElseIf TypeOf source Is Line Then
            Dim line = DirectCast(source, Line)
            geometry = New LineGeometry(New Point(line.X1, line.Y1), New Point(line.X2, line.Y2))
            kind = "Line"
        Else
            Throw New NotSupportedException(source.GetType().Name)
        End If
        Dim naturalBounds = If(geometry?.Bounds, Rect.Empty)
        If kind = "Line" Then
            Dim line = DirectCast(source, Line)
            geometry = New LineGeometry(New Point(If(line.X1 <= line.X2, 0, 1), If(line.Y1 <= line.Y2, 0, 1)),
                                        New Point(If(line.X1 <= line.X2, 1, 0), If(line.Y1 <= line.Y2, 1, 0)))
        End If
        Dim result As New GeometricShape With {
            .Data = geometry, .Kind = kind, .Width = source.Width, .Height = source.Height,
            .Stroke = source.Stroke, .StrokeThickness = source.StrokeThickness, .Fill = source.Fill,
            .StrokeStartLineCap = source.StrokeStartLineCap, .StrokeEndLineCap = source.StrokeEndLineCap,
            .StrokeDashCap = source.StrokeDashCap, .StrokeLineJoin = source.StrokeLineJoin,
            .StrokeMiterLimit = source.StrokeMiterLimit, .StrokeDashArray = source.StrokeDashArray?.Clone(),
            .StrokeDashOffset = source.StrokeDashOffset, .RenderTransform = source.RenderTransform,
            .RenderTransformOrigin = source.RenderTransformOrigin, .Visibility = source.Visibility,
            .Opacity = source.Opacity, .Clip = source.Clip, .IsHitTestVisible = source.IsHitTestVisible,
            .HorizontalAlignment = HorizontalAlignment.Left, .VerticalAlignment = VerticalAlignment.Top
        }
        Canvas.SetLeft(result, Canvas.GetLeft(source))
        Canvas.SetTop(result, Canvas.GetTop(source))
        RegistrationMarkHelper.SetIsRegistrationMark(result, RegistrationMarkHelper.IsRegistrationMark(source))
        If kind = "Line" Then
            If Double.IsNaN(result.Width) Then result.Width = naturalBounds.Width
            If Double.IsNaN(result.Height) Then result.Height = naturalBounds.Height
            If Double.IsNaN(Canvas.GetLeft(result)) Then Canvas.SetLeft(result, naturalBounds.X)
            If Double.IsNaN(Canvas.GetTop(result)) Then Canvas.SetTop(result, naturalBounds.Y)
        End If
        Return result
    End Function
End Class

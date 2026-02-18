Imports System.Runtime.CompilerServices
Imports System.Windows.Media.Media3D

Imports PolyCut.Shared

Imports Svg
Imports Svg.Transforms

Public Module Extensions

    <Extension()>
    Public Sub AddRange(Of T)(collection As ICollection(Of T), items As IEnumerable(Of T))
        If collection Is Nothing Then
            Throw New ArgumentNullException(NameOf(collection))
        End If

        If items IsNot Nothing Then
            For Each item In items
                collection.Add(item)
            Next
        End If
    End Sub


    <Extension()>
    Public Sub ForEach(Of T)(source As IEnumerable(Of T), action As Action(Of T))
        For Each item In source
            action(item)
        Next
    End Sub


    <Extension()>
    Public Function BakeTransforms(SVGelement As SvgVisualElement, drawableElement As FrameworkElement, Optional LCorrection As Double = 0, Optional TCorrection As Double = 0, Optional IgnoreDrawableScale As Boolean = False) As SvgVisualElement

        Dim component As SvgVisualElement = SVGelement.DeepCopy
        Dim originalBounds = component.Bounds
        Dim transformableContentControl As ContentControl = drawableElement.Parent
        If component.Transforms Is Nothing Then component.Transforms = New Transforms.SvgTransformCollection

        ' Check for scale transforms on the element's RenderTransform (mirror)
        Dim tfg As TransformGroup = TryCast(drawableElement.RenderTransform, TransformGroup)
        If tfg IsNot Nothing Then
            For Each transform As Transform In tfg.Children
                If TypeOf transform Is ScaleTransform Then
                    Dim st = CType(transform, ScaleTransform)
                    component.Transforms.Insert(0, New Transforms.SvgScale(st.ScaleX, st.ScaleY))
                End If
            Next
        End If

        Dim scaleTF As New Transforms.SvgScale(transformableContentControl.ActualWidth / component.Bounds.Width, transformableContentControl.ActualHeight / component.Bounds.Height)
        component.Transforms.Insert(0, scaleTF)

        'Need to recheck the bounds because the scaling affects the children and translates the parent. 
        Dim newBounds = component.Bounds

        'For some ghastly reason, all translations are ALSO scaled by the scale value so this needs to be undone
        Dim scaledXTranslate = (-LCorrection + Canvas.GetLeft(transformableContentControl) - (newBounds.X - originalBounds.X))
        Dim scaledYTranslate = (-TCorrection + Canvas.GetTop(transformableContentControl) - (newBounds.Y - originalBounds.Y))

        Dim translateTF As New Transforms.SvgTranslate(scaledXTranslate, scaledYTranslate)
        component.Transforms.Insert(0, translateTF)

        ' Handle wrapper rotation (simple RotateTransform only)
        Dim transformableCCTransforms = transformableContentControl.RenderTransform

        If TypeOf transformableCCTransforms Is RotateTransform Then
            Dim rt = CType(transformableCCTransforms, RotateTransform)
            Dim rotateTF As New Transforms.SvgRotate(rt.Angle, component.Bounds.X + component.Bounds.Width / 2, component.Bounds.Y + component.Bounds.Height / 2)
            component.Transforms.Insert(0, rotateTF)
        End If

        Return component

    End Function


    <Extension()>
    Public Function IsWithinBounds(svgElement As SvgVisualElement, x As Double, y As Double) As Boolean
        If svgElement Is Nothing Then Return False

        Dim b = svgElement.Bounds ' compute once

        Return b.X >= 0 AndAlso b.Y >= 0 AndAlso (b.Right <= x) AndAlso (b.Bottom <= y)

        'If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso svgElement.Bounds.Width + cxLeft < x AndAlso svgElement.Bounds.Height + cxTop < y Then
        'Return True
        'End If

        Return False


    End Function

    <Extension()>
    Public Function IsWithinBounds(drawable As IDrawable, canvasWidth As Double, canvasHeight As Double, mainCanvas As Visual) As Boolean
        If drawable Is Nothing OrElse mainCanvas Is Nothing Then Return False

        Dim fe = TryCast(drawable.DrawableElement, FrameworkElement)
        If fe Is Nothing Then Return False

        Dim testElement As FrameworkElement = fe

        If testElement.ActualWidth <= 0 OrElse testElement.ActualHeight <= 0 Then Return False

        Dim gt As GeneralTransform
        Try
            gt = testElement.TransformToVisual(mainCanvas)
        Catch
            ' not in visual tree
            Return False
        End Try

        ' Local bounds of the element (axis-aligned in its own space)
        Dim localRect As New Rect(0, 0, testElement.ActualWidth, testElement.ActualHeight)

        ' Transform into canvas space; result is axis-aligned bounds of the transformed quad
        Dim worldRect As Rect = gt.TransformBounds(localRect)

        Dim canvasRect As New Rect(0, 0, canvasWidth, canvasHeight)


        Return canvasRect.Contains(worldRect)
    End Function

End Module

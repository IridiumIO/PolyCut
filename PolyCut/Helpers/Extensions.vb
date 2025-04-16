﻿Imports System.Runtime.CompilerServices

Imports PolyCut.Shared

Imports Svg

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

        Dim scaleTF As New Transforms.SvgScale(transformableContentControl.ActualWidth / component.Bounds.Width, transformableContentControl.ActualHeight / component.Bounds.Height)
        component.Transforms.Insert(0, scaleTF)

        'Need to recheck the bounds because the scaling affects the children and translates the parent. 
        Dim newBounds = component.Bounds

        'For some ghastly reason, all translations are ALSO scaled by the scale value so this needs to be undone
        Dim scaledXTranslate = (-LCorrection + Canvas.GetLeft(transformableContentControl) - (newBounds.X - originalBounds.X))
        Dim scaledYTranslate = (-TCorrection + Canvas.GetTop(transformableContentControl) - (newBounds.Y - originalBounds.Y))

        Dim translateTF As New Transforms.SvgTranslate(scaledXTranslate, scaledYTranslate)
        component.Transforms.Insert(0, translateTF)


        Dim transformableCCTransforms = transformableContentControl.RenderTransform

        If TypeOf (transformableCCTransforms) Is RotateTransform Then

            Dim rt = CType(transformableCCTransforms, RotateTransform)

            Dim rotateTF As New Transforms.SvgRotate(rt.Angle, component.Bounds.X + component.Bounds.Width / 2, component.Bounds.Y + component.Bounds.Height / 2)

            component.Transforms.Insert(0, rotateTF)

        End If

        Return component

    End Function


    <Extension()>
    Public Function IsWithinBounds(SVGelement As SvgVisualElement, x As Double, y As Double) As Boolean

        Dim cxLeft = SVGelement.Bounds.X
        Dim cxTop = SVGelement.Bounds.Y

        If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso SVGelement.Bounds.Width + cxLeft < x AndAlso SVGelement.Bounds.Height + cxTop < y Then
            Return True
        End If

        Return False

    End Function

    <Extension()>
    Public Function IsWithinBounds(drawableElement As IDrawable, x As Double, y As Double) As Boolean

        Dim renderable = drawableElement.DrawableElement
        Dim cxLeft = Canvas.GetLeft(renderable.Parent)
        Dim cxTop = Canvas.GetTop(renderable.Parent)
        Dim cxWidth = renderable.ActualWidth
        Dim cxHeight = renderable.ActualHeight
        If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso cxWidth + cxLeft < x AndAlso cxHeight + cxTop < y Then
            Return True
        End If
        Return False
    End Function

End Module

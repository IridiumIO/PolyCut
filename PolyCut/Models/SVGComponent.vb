﻿Imports System.IO
Imports System.Windows.Controls.Primitives
Imports System.Xml

Imports PolyCut.Shared

Imports SharpVectors.Converters

Imports Svg

Public Class SVGComponent : Inherits BaseDrawable : Implements IDrawable


    Public Property SVGString As String

    Public ReadOnly Property IsVisualElement As Boolean
        Get

            If SVGElement.GetType Is GetType(SvgGroup) Then
                If Not SVGElement.HasChildren Then Return False
            End If

            Return TryCast(SVGElement, SvgVisualElement) IsNot Nothing
        End Get
    End Property

    Public Overloads ReadOnly Property VisualName As String Implements IDrawable.VisualName
        Get
            Dim id = TryCast(SVGElement, SvgPathBasedElement)?.ID
            If id IsNot Nothing Then Return id

            Dim g = TryCast(SVGElement, SvgGroup)?.ID
            If g IsNot Nothing Then Return g

            Dim rss = SVGElement.GetType()

            Return rss.Name.Replace("Svg", "")
        End Get
    End Property

    Public ReadOnly Property Renderable As SvgDocument
        Get


            Dim ele = TryCast(SVGElement, SvgVisualElement)

            If ele Is Nothing Then Return Nothing

            Dim doc As New SvgDocument With {
                .Transforms = New Transforms.SvgTransformCollection
            }
            doc.Transforms.Insert(0, New Transforms.SvgTranslate(-ele.Bounds.X, -ele.Bounds.Y))

            doc.Children.Add(SVGElement)

            Return doc

        End Get
    End Property


    Public Property SVGElement As SvgElement
    Public Property SVGLeft As Double
    Public Property SVGTop As Double

    Public Overloads Property Children As IEnumerable(Of IDrawable) Implements IDrawable.Children

    Private _IsHidden As Boolean = False
    Public Overloads Property IsHidden As Boolean Implements IDrawable.IsHidden
        Get
            Return _IsHidden
        End Get
        Set(value As Boolean)
            _IsHidden = value

            If value Then
                SVGViewBox.Visibility = Visibility.Collapsed
                IsSelected = False
            Else
                SVGViewBox.Visibility = Visibility.Visible
            End If

            OnPropertyChanged(NameOf(IsHidden))
        End Set
    End Property



    Public Overloads Property IsSelected As Boolean Implements IDrawable.IsSelected
        Get

            If SVGViewBox?.Parent Is Nothing Then Return False
            '  Return Selector.GetIsSelected(SVGViewBox.Parent)
            Return _CurrentlySelectedComponent Is Me
        End Get
        Set(value As Boolean)
            If value Then
                ' Deselect the currently selected component
                If _CurrentlySelectedComponent IsNot Nothing AndAlso _CurrentlySelectedComponent IsNot Me Then
                    _CurrentlySelectedComponent.IsSelected = False
                End If

                ' Update the currently selected component
                _CurrentlySelectedComponent = Me
            Else
                ' Clear the currently selected component if this component is being deselected
                If _CurrentlySelectedComponent Is Me Then
                    _CurrentlySelectedComponent = Nothing
                End If
            End If

            If Selector.GetIsSelected(SVGViewBox?.Parent) <> value Then
                Selector.SetIsSelected(SVGViewBox?.Parent, value)
            End If
            OnPropertyChanged(NameOf(IsSelected))
        End Set
    End Property


    Public ReadOnly Property Parent As SVGFile


    Private Sub Initialise()

        If SVGElement.Display = "none" Then
            IsHidden = True
            SVGElement.Display = "inline"
        End If

        UnhideChildren(SVGElement)

        If Renderable IsNot Nothing Then
            SVGString = SVGDocumentToSVGString(Renderable)
        End If

        Dim ele = TryCast(SVGElement, SvgVisualElement)

        If ele Is Nothing Then
            SVGLeft = 0
            SVGTop = 0
            Return
        End If

        SVGLeft = ele.Bounds.X
        SVGTop = ele.Bounds.Y
        SetCanvas()
    End Sub


    Private Shared Sub UnhideChildren(element As SvgElement)
        For Each child In element.Children
            If child.Display = "none" Then child.Display = "inline"
            UnhideChildren(child)
        Next
    End Sub


    Public Sub New(svgele As SvgElement, ByRef parentFile As SVGFile)
        SVGElement = svgele
        Parent = parentFile
        Initialise()
    End Sub

    Public Sub New(svgviewbox As SharpVectors.Converters.SvgViewbox, ByRef parentFile As SVGFile)
        DrawableElement = svgviewbox
        Parent = parentFile


    End Sub


    Public Property SVGViewBox As SharpVectors.Converters.SvgViewbox
        Get
            Return CType(DrawableElement, SharpVectors.Converters.SvgViewbox)
        End Get
        Set(value As SharpVectors.Converters.SvgViewbox)
            DrawableElement = value
        End Set
    End Property
    Public Overloads Property Name As String Implements IDrawable.Name
    Public Overloads Property DrawableElement As FrameworkElement Implements IDrawable.DrawableElement


    Public Sub SetCanvas()

        SVGViewBox = New SharpVectors.Converters.SvgViewbox With {.SvgSource = SVGString, .Height = Double.NaN, .Width = Double.NaN, .Stretch = Stretch.Fill}

        Dim bounds = TryCast(SVGElement, SvgVisualElement)?.Bounds

        If bounds IsNot Nothing Then

            'Dim strokeWidth As Single = If(svgVisEle.StrokeWidth.Value <> 0, svgVisEle.StrokeWidth.Value, 0)
            Dim strokeWidth As Single = 0
            SVGViewBox.Width = bounds.Value.Width - strokeWidth
            SVGViewBox.Height = bounds.Value.Height - strokeWidth
            Canvas.SetLeft(SVGViewBox, bounds.Value.X + strokeWidth / 2)
            Canvas.SetTop(SVGViewBox, bounds.Value.Y + strokeWidth / 2)

            If bounds.Value.Width < 5 Then
                Canvas.SetLeft(SVGViewBox, Canvas.GetLeft(SVGViewBox) - 2.5)
            End If

            If bounds.Value.Height < 5 Then
                Canvas.SetTop(SVGViewBox, Canvas.GetTop(SVGViewBox) - 2.5)
            End If
        Else

            Canvas.SetLeft(SVGViewBox, SVGLeft)
            Canvas.SetTop(SVGViewBox, SVGTop)
        End If

        If IsHidden Then SVGViewBox.Visibility = Visibility.Collapsed


    End Sub

    Public Shared Function SVGDocumentToSVGString(svgdocument As SvgDocument)
        Using sw As New StringWriter()
            Using writer As XmlWriter = XmlWriter.Create(sw, New XmlWriterSettings With {.Encoding = Text.Encoding.UTF8})
                svgdocument.Write(writer)
            End Using
            Return sw.ToString()
        End Using
    End Function


    Public Function IsWithinBounds(x As Double, y As Double)

        Dim ele = TryCast(SVGElement, SvgVisualElement)

        If ele Is Nothing Then
            Return False
        End If

        Dim cxLeft = Canvas.GetLeft(SVGViewBox.Parent)
        Dim cxTop = Canvas.GetTop(SVGViewBox.Parent)

        If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso SVGViewBox.ActualWidth + cxLeft < x AndAlso SVGViewBox.ActualHeight + cxTop < y Then
            Return True
        End If

        Return False

    End Function


    Public Overloads Function GetTransformedSVGElement() As SvgVisualElement Implements IDrawable.GetTransformedSVGElement

        Dim component As SvgVisualElement = SVGElement.DeepCopy

        Return component.BakeTransforms(SVGViewBox, SVGLeft, SVGTop)

    End Function

    Public Overloads Function DrawingToSVG() As SvgVisualElement Implements IDrawable.DrawingToSVG
        Throw New NotImplementedException()
    End Function



End Class

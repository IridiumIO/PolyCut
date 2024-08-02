Imports System.IO
Imports System.Windows.Controls.Primitives
Imports System.Xml

Imports CommunityToolkit.Mvvm.ComponentModel

Imports SharpVectors.Converters

Imports Svg

Public Class SVGComponent : Inherits ObservableObject


    Public Property SVGString As String

    Public ReadOnly Property IsVisualElement As Boolean
        Get

            If SVGElement.GetType Is GetType(SvgGroup) Then
                If Not SVGElement.HasChildren Then Return False
            End If

            Return TryCast(SVGElement, SvgVisualElement) IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property VisualName As String
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

            Dim doc As New SvgDocument

            doc.Transforms = New Transforms.SvgTransformCollection
            doc.Transforms.Insert(0, New Transforms.SvgTranslate(-ele.Bounds.X, -ele.Bounds.Y))

            doc.Children.Add(SVGElement)

            Return doc

        End Get
    End Property


    Public Property SVGElement As SvgElement
    Public Property SVGLeft As Double
    Public Property SVGTop As Double

    Public Property Children As IEnumerable(Of SVGComponent)

    Private _IsHidden As Boolean = False
    Public Property IsHidden As Boolean
        Get
            Return _IsHidden
        End Get
        Set(value As Boolean)
            _IsHidden = value

            If value Then
                SVGViewBox.Visibility = Visibility.Collapsed
                Selector.SetIsSelected(SVGViewBox.Parent, False)
            Else
                SVGViewBox.Visibility = Visibility.Visible
            End If

            OnPropertyChanged(NameOf(IsHidden))
        End Set
    End Property


    Public Property IsSelected As Boolean
        Get
            If SVGViewBox?.Parent Is Nothing Then Return False
            Return Selector.GetIsSelected(SVGViewBox.Parent)
        End Get
        Set(value As Boolean)
            Selector.SetIsSelected(SVGViewBox?.Parent, value)
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


    Private Sub UnhideChildren(element As SvgElement)
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

    Public Property SVGViewBox As SharpVectors.Converters.SvgViewbox

    Public Sub SetCanvas()

        SVGViewBox = New SharpVectors.Converters.SvgViewbox With {.SvgSource = SVGString, .Height = Double.NaN, .Width = Double.NaN}

        Dim svgVisEle = TryCast(SVGElement, SvgVisualElement)

        Dim bounds = TryCast(SVGElement, SvgVisualElement)?.Bounds

        If bounds IsNot Nothing Then

            Dim strokeWidth As Single = If(svgVisEle.StrokeWidth.Value <> 0, svgVisEle.StrokeWidth.Value, 0)


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

        Dim cxLeft = Canvas.GetLeft(SVGViewBox)
        Dim cxTop = Canvas.GetTop(SVGViewBox)

        'If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso ele.Bounds.Width * ECanvas.Scale + cxLeft < x AndAlso ele.Bounds.Height * ECanvas.Scale + cxTop < y Then
        '    Return True
        'End If

        Return False

    End Function


    'Public Function GetTransformedSVGElement() As SvgVisualElement

    '    Dim component As SvgVisualElement = SVGElement.DeepCopy
    '    Dim originalBounds = component.Bounds

    '    If component.Transforms Is Nothing Then component.Transforms = New Transforms.SvgTransformCollection

    '    Dim scaleTF As New Transforms.SvgScale(ECanvas.Scale)
    '    component.Transforms.Insert(0, scaleTF)

    '    'Need to recheck the bounds because the scaling affects the children and translates the parent. 
    '    Dim newBounds = component.Bounds

    '    'For some ghastly reason, all translations are ALSO scaled by the scale value so this needs to be undone
    '    Dim scaledXTranslate = (-SVGLeft + Canvas.GetLeft(ECanvas) - (newBounds.X - originalBounds.X))
    '    Dim scaledYTranslate = (-SVGTop + Canvas.GetTop(ECanvas) - (newBounds.Y - originalBounds.Y))

    '    Dim translateTF As New Transforms.SvgTranslate(scaledXTranslate, scaledYTranslate)
    '    component.Transforms.Insert(0, translateTF)

    '    Return component

    'End Function


    'Private State As Nullable(Of (Double, Double, Double))
    'Public Sub SaveState()
    '    If Not IsVisualElement Then Return

    '    State = (Canvas.GetLeft(ECanvas), Canvas.GetTop(ECanvas), ECanvas.Scale)

    'End Sub

    'Public Sub LoadState()
    '    If State Is Nothing Then Return

    '    Canvas.SetLeft(ECanvas, State.Value.Item1)
    '    Canvas.SetTop(ECanvas, State.Value.Item2)
    '    ECanvas.Scale = State.Value.Item3
    'End Sub

End Class

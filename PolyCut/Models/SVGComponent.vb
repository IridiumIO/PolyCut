Imports System.IO
Imports System.Xml
Imports CommunityToolkit.Mvvm.ComponentModel
Imports Svg

Public Class SVGComponent : Inherits ObservableObject


    Public Property SVGString As String

    Public ReadOnly Property IsVisualElement As Boolean
        Get

            If SVGElement.GetType Is GetType(SvgGroup) Then
                If Not SVGElement.HasChildren Then Return False
            End If

            Return If(TryCast(SVGElement, SvgVisualElement) Is Nothing, False, True)
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

    Public Property ECanvas As resizableSVGCanvas


    Public Property SVGElement As SvgElement
    Public Property SVGLeft As Double
    Public Property SVGTop As Double

    Public Property Children As IEnumerable(Of SVGComponent)

    Private _isHidden As Boolean = False
    Public Property isHidden As Boolean
        Get
            Return _isHidden
        End Get
        Set(value As Boolean)
            _isHidden = value
            If ECanvas Is Nothing OrElse Not IsVisualElement Then Return
            ECanvas.Visibility = If(_isHidden, Visibility.Collapsed, Visibility.Visible)
        End Set
    End Property
    Public ReadOnly Property Parent As SVGFile

    Private Sub Initialise()

        If SVGElement.Display = "none" Then
            isHidden = True
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


    Public Sub SetCanvas()

        Dim svgC As New SharpVectors.Converters.SvgCanvas With {.SvgSource = SVGString, .Height = Double.NaN, .Width = Double.NaN}

        ECanvas = New resizableSVGCanvas(svgC)

        If isHidden Then ECanvas.Visibility = Visibility.Collapsed

        Canvas.SetLeft(_Ecanvas, SVGLeft)
        Canvas.SetTop(_Ecanvas, SVGTop)
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

        Dim cxLeft = Canvas.GetLeft(ECanvas)
        Dim cxTop = Canvas.GetTop(ECanvas)

        If cxLeft >= 0 AndAlso cxTop >= 0 AndAlso ele.Bounds.Width * ECanvas.Scale + cxLeft < x AndAlso ele.Bounds.Height * ECanvas.Scale + cxTop < y Then
            Return True
        End If

        Return False

    End Function


    Public Function GetTransformedSVGElement() As SvgVisualElement

        Dim component As SvgVisualElement = SVGElement.DeepCopy
        Dim originalBounds = component.Bounds

        If component.Transforms Is Nothing Then component.Transforms = New Transforms.SvgTransformCollection

        Dim scaleTF As New Transforms.SvgScale(ECanvas.Scale)
        component.Transforms.Insert(0, scaleTF)

        'Need to recheck the bounds because the scaling affects the children and translates the parent. 
        Dim newBounds = component.Bounds

        'For some ghastly reason, all translations are ALSO scaled by the scale value so this needs to be undone
        Dim scaledXTranslate = (-SVGLeft + Canvas.GetLeft(ECanvas) - (newBounds.X - originalBounds.X))
        Dim scaledYTranslate = (-SVGTop + Canvas.GetTop(ECanvas) - (newBounds.Y - originalBounds.Y))

        Dim translateTF As New Transforms.SvgTranslate(scaledXTranslate, scaledYTranslate)
        component.Transforms.Insert(0, translateTF)

        Return component

    End Function


    Private State As Nullable(Of (Double, Double, Double))
    Public Sub SaveState()
        If Not IsVisualElement Then Return

        State = (Canvas.GetLeft(ECanvas), Canvas.GetTop(ECanvas), ECanvas.Scale)

    End Sub

    Public Sub LoadState()
        If State Is Nothing Then Return

        Canvas.SetLeft(ECanvas, State.Value.Item1)
        Canvas.SetTop(ECanvas, State.Value.Item2)
        ECanvas.Scale = State.Value.Item3
    End Sub

End Class

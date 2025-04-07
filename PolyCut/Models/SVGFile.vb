Imports System.Collections.ObjectModel
Imports System.ComponentModel

Imports CommunityToolkit.Mvvm.ComponentModel

Imports PolyCut.RichCanvas

Imports Svg.Transforms

Public Class SVGFile : Inherits ObservableObject

    Public Property FilePath As String

    Public ReadOnly Property ShortFileName As String
        Get
            Return FilePath.Replace("/", "\").Substring(FilePath.LastIndexOf("\") + 1)

        End Get
    End Property

    Public Property SVGComponents As New ObservableCollection(Of SVGComponent)

    Public Property SVGVisualComponents As ICollectionView


    Public Function ConvertSVGScaleToMM(unitType As Svg.SvgUnitType) As Double
        Select Case unitType
            Case Svg.SvgUnitType.Centimeter
                Return 10
            Case Svg.SvgUnitType.Inch
                Return 25.4
            Case Svg.SvgUnitType.Millimeter
                Return 1
            Case Svg.SvgUnitType.Pixel
                Return 0.264583333333333
            Case Svg.SvgUnitType.Percentage
                Return 0.264583333333333
            Case Svg.SvgUnitType.Point
                Return 0.352777777777778
            Case Svg.SvgUnitType.Pica
                Return 4.23333333333333
            Case Else
                Application.GetService(Of SnackbarService).GenerateCaution("Unknown SVG Unit Type: " & unitType.ToString, "Scaling may not be correct")
                Return 1
        End Select
    End Function

    Public Sub New(path As String)
        FilePath = path

        Dim inDoc As Svg.SvgDocument = Svg.SvgDocument.Open(FilePath)

        ParseSVG(inDoc)

    End Sub

    Public Sub New(svgDoc As Svg.SvgDocument)
        ParseSVG(svgDoc)
    End Sub

    Private Sub ParseSVG(inDoc As Svg.SvgDocument)
        If inDoc.Height.Type <> Svg.SvgUnitType.Millimeter OrElse inDoc.Width.Type <> Svg.SvgUnitType.Millimeter Then
            Dim heighScale As Double = ConvertSVGScaleToMM(inDoc.Height.Type)
            Dim widthScale As Double = ConvertSVGScaleToMM(inDoc.Width.Type)

            For Each child In inDoc.Children
                If child.Transforms?.Count > 0 Then
                    child.Transforms.Insert(0, New SvgScale(widthScale, heighScale))
                Else
                    child.Transforms = New SvgTransformCollection
                    child.Transforms.Add(New SvgScale(widthScale, heighScale))
                End If
            Next
        End If

        Dim vbH = inDoc.ViewBox.Height
        Dim vbW = inDoc.ViewBox.Width

        If inDoc.Height.Type <> Svg.SvgUnitType.Percentage AndAlso inDoc.Width.Type <> Svg.SvgUnitType.Percentage Then
            For Each child In inDoc.Children
                If child.Transforms?.Count > 0 Then
                    child.Transforms.Insert(0, New SvgScale(inDoc.Width.Value / vbW, inDoc.Height.Value / vbH))
                Else
                    child.Transforms = New SvgTransformCollection
                    child.Transforms.Add(New SvgScale(inDoc.Width.Value / vbW, inDoc.Height.Value / vbH))
                End If
            Next
        End If



        For Each child In inDoc.Children
            SVGComponents.Add(New SVGComponent(child, Me))
        Next

        SVGVisualComponents = CollectionViewSource.GetDefaultView(SVGComponents)
        SVGVisualComponents.Filter = Function(item As Object)
                                         Return (TypeOf item Is SVGComponent) AndAlso DirectCast(item, SVGComponent).IsVisualElement = True
                                     End Function
        OnPropertyChanged(NameOf(SVGVisualComponents))


    End Sub

    Public Sub New(comp As Svg.SvgVisualElement, flName As String)
        FilePath = flName
        AddComponent(New SVGComponent(comp, Me))
    End Sub

    Public Sub AddComponent(svgcomp As SVGComponent)
        SVGComponents.Add(svgcomp)
        SVGVisualComponents = CollectionViewSource.GetDefaultView(SVGComponents)
        SVGVisualComponents.Filter = Function(item As Object)
                                         Return (TypeOf item Is SVGComponent) AndAlso DirectCast(item, SVGComponent).IsVisualElement = True
                                     End Function
        OnPropertyChanged(NameOf(SVGVisualComponents))
        OnPropertyChanged(NameOf(SVGComponents))
    End Sub


End Class

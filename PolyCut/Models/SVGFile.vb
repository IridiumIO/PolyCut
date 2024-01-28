Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports CommunityToolkit.Mvvm.ComponentModel
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
            Case Svg.SvgUnitType.Point
                Return 0.352777777777778
            Case Svg.SvgUnitType.Pica
                Return 4.23333333333333
            Case Else
                Application.GetService(Of WPF.Ui.ISnackbarService).Show("Unknown SVG Unit Type: " & unitType.ToString, "Scaling may not be correct", WPF.Ui.Controls.ControlAppearance.Caution, Nothing, TimeSpan.FromSeconds(5))
                Return 1
        End Select
    End Function

    Public Sub New(path As String)
        FilePath = path

        Dim inDoc As Svg.SvgDocument = Svg.SvgDocument.Open(FilePath)

        If inDoc.Height.Type <> Svg.SvgUnitType.Millimeter OrElse inDoc.Width.Type <> Svg.SvgUnitType.Millimeter Then
            Dim heighScale As Double = ConvertSVGScaleToMM(inDoc.Height.Type)
            Dim widthScale As Double = ConvertSVGScaleToMM(inDoc.Width.Type)

            For Each child In inDoc.Children
                If child.Transforms?.Count > 0 Then
                    child.Transforms.Insert(0, New SvgScale(ConvertSVGScaleToMM(widthScale), heighScale))
                Else
                    child.Transforms = New SvgTransformCollection
                    child.Transforms.Add(New SvgScale(widthScale, heighScale))
                End If
            Next
        End If

        Dim vbH = inDoc.ViewBox.Height
        Dim vbW = inDoc.ViewBox.Width
        For Each child In inDoc.Children
            If child.Transforms?.Count > 0 Then
                child.Transforms.Insert(0, New SvgScale(inDoc.Width.Value / vbW, inDoc.Height.Value / vbH))
            Else
                child.Transforms = New SvgTransformCollection
                child.Transforms.Add(New SvgScale(inDoc.Width.Value / vbW, inDoc.Height.Value / vbH))
            End If
        Next

        For Each child In inDoc.Children
            SVGComponents.Add(New SVGComponent(child, Me))
        Next

        SVGVisualComponents = CollectionViewSource.GetDefaultView(SVGComponents)
        SVGVisualComponents.Filter = Function(item As Object)

                                         If TypeOf item Is SVGComponent Then
                                             Return DirectCast(item, SVGComponent).IsVisualElement = True
                                         Else
                                             Return False
                                         End If

                                     End Function
        OnPropertyChanged(NameOf(SVGVisualComponents))


    End Sub



End Class

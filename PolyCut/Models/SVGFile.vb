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


    Public Sub New(path As String)
        FilePath = path

        Dim inDoc As Svg.SvgDocument = Svg.SvgDocument.Open(FilePath)

        If inDoc.Height.Type = Svg.SvgUnitType.Inch Then
            For Each child In inDoc.Children
                If child.Transforms?.Count > 0 Then
                    child.Transforms.Insert(0, New SvgScale(25.4, 25.4))
                Else
                    child.Transforms = New SvgTransformCollection
                    child.Transforms.Add(New SvgScale(25.4, 25.4))
                End If
            Next
        End If

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

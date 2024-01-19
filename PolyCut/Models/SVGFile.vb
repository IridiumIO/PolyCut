Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports CommunityToolkit.Mvvm.ComponentModel

Public Class SVGFile : Inherits ObservableObject

    Public Property FilePath As String

    Public ReadOnly Property ShortFileName As String

    Public Property SVGComponents As New ObservableCollection(Of SVGComponent)

    Public Property SVGVisualComponents As ICollectionView


    Public Sub New(path As String)
        FilePath = path

        Dim inDoc As Svg.SvgDocument = Svg.SvgDocument.Open(FilePath)

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

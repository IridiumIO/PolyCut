Imports PolyCut.[Shared]

Imports System.IO

Public Class ImportService

    Private Const _filterImg As String = "Image Files (*.svg;*.png;*.bmp;*.jpg;*.jpeg)|*.svg;*.png;*.bmp;*.jpg;*.jpeg"
    Private Const _filterVector As String = "SVG Files (*.svg) |*.svg"
    Private Const _filterDXF As String = "DXF Files (*.dxf)|*.dxf"
    Private Const _filterAllSupported As String = "Image and Vector Files (*.svg;*.png;*.bmp;*.jpg;*.jpeg;*.dxf)|*.svg;*.png;*.bmp;*.jpg;*.jpeg;*.dxf"

    Public Shared ReadOnly AllSupportedExtensions As String() = {".svg", ".png", ".bmp", ".jpg", ".jpeg", ".dxf"}

    Private Shared ReadOnly _filter As String = String.Join("|", _filterAllSupported, _filterImg, _filterVector, _filterDXF, "All Files (*.*)|*.*")


    Public Shared Function OpenFileImportDialog() As String()
        Dim fs As New Microsoft.Win32.OpenFileDialog With {.Filter = _filter, .Multiselect = True}
        If Not fs.ShowDialog Then Return Nothing

        Return fs.FileNames

    End Function


    Public Shared Async Function Import(svgImportService As IVectorImportService, document As Document, undoRedoService As UndoRedoService, filepaths As IEnumerable(Of String)) As Task


        Dim actions As New List(Of IUndoableAction)()
        Dim vtracer = Application.GetService(Of VTracerService)

        For Each fl In filepaths

            'DXF
            If Path.GetExtension(fl).Equals(".dxf", StringComparison.CurrentCultureIgnoreCase) Then
                Dim svgdoc = Await DXFImportService.ConvertDXFToSVG(fl)
                If svgdoc Is Nothing Then Continue For

                Dim act As New ImportSVGAction(document, svgImportService, svgdoc, fl)
                If act.Execute() Then actions.Add(act)
                Continue For

            End If

            'Bitmap Images
            If Not Path.GetExtension(fl).Equals(".svg", StringComparison.CurrentCultureIgnoreCase) Then
                Dim svgPath = vtracer.ConvertRasterToSVG(fl)
                If svgPath Is Nothing Then Continue For
                fl = svgPath
            End If

            'SVG
            Dim action As New ImportSVGAction(document, svgImportService, fl)
            If action.Execute() Then actions.Add(action)

        Next

        If actions.Count > 0 Then undoRedoService.Push(New CompositeAction(actions))

    End Function


End Class

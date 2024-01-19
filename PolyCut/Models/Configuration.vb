Imports CommunityToolkit.Mvvm.ComponentModel

Public Class Configuration : Inherits ObservableObject

    Public Property ToolMode As Integer = 1
    Public Property TravelSpeed As Double = 45
    Public Property DrawSpeed As Double = 35
    Public Property ZSpeed As Double = 10

    Public Property WorkingZHeight As Double = 0
    Public Property TravelZHeight As Double = 4
    Public Property ParkingZHeight As Double = 20

    Public Property Precision As Double = 0.01
    Public Property ExtractOneColour As Boolean = False
    Public Property ExtractionColor As String = ""

    Public Property ToolOffset As Double = 0.45
    Public Property Overcut As Double = 1.0

    Public Property InsideOutCuttingOrder As Boolean = False
    Public Property DrawingDirection As Integer? = Nothing
    Public Property OptimisationTime As Integer = 60
    Public Property EnableCrosshatching As Boolean = True
    Public Property ShadingThreshold As Double = 1.0
    Public Property LightestShading As Double = 3.0
    Public Property DarkestShading As Double = 0.5
    Public Property ShadingAngle As Double = 0.0

    Public Property WorkingHeight As Double = 0.0
    Public Property WorkingWidth As Double = 0.0

    Public Property Area As String
    Public Property IgnoreLocked As Boolean = False
    Public Property IgnoreHidden As Boolean = True

    Private ReadOnly Mappings As New Dictionary(Of String, String)() From {
    {NameOf(ToolMode), "tool-mode"},
    {NameOf(TravelSpeed), "pen-up-speed"},
    {NameOf(DrawSpeed), "pen-down-speed"},
    {NameOf(ZSpeed), "z-speed"},
    {NameOf(WorkingZHeight), "work-z"},
    {NameOf(TravelZHeight), "lift-delta-z"},
    {NameOf(ParkingZHeight), "safe-delta-z"},
    {NameOf(Precision), "tolerance"},
    {NameOf(ExtractOneColour), "boolean-extract-color"},
    {NameOf(ExtractionColor), "extract-color"},
    {NameOf(ToolOffset), "tool-offset"},
    {NameOf(Overcut), "overcut"},
    {NameOf(InsideOutCuttingOrder), "boolean-sort"},
    {NameOf(DrawingDirection), "direction"},
    {NameOf(OptimisationTime), "optimization-time"},
    {NameOf(EnableCrosshatching), "boolean-shading-crosshatch"},
    {NameOf(ShadingThreshold), "shading-threshold"},
    {NameOf(LightestShading), "shading-lightest"},
    {NameOf(DarkestShading), "shading-darkest"},
    {NameOf(ShadingAngle), "shading-angle"},
    {NameOf(Area), "area"},
    {NameOf(IgnoreLocked), "ignore-locked"},
    {NameOf(IgnoreHidden), "ignore-hidden"}
    }


    Public Sub SetArea(offsetX, offsetY, W, H)

        Area = $"""{offsetX},{offsetY},{W},{H}"""
        WorkingHeight = H
        WorkingWidth = W

    End Sub


    Public Function BuildGCPArgs()

        Dim properties = Me.GetType().GetProperties(Reflection.BindingFlags.Public Or Reflection.BindingFlags.Instance)

        Dim args As New List(Of String)

        For Each prop In properties
            If prop.GetValue(Me) Is Nothing Then
                Continue For
            End If

            If prop.Name = NameOf(ToolMode) Then
                If prop.GetValue(Me) = 0 Then
                    args.Add($"--{Mappings(prop.Name)}=cut")
                Else
                    args.Add($"--{Mappings(prop.Name)}=draw")
                End If
                Continue For

            End If

            If Not Mappings.ContainsKey(prop.Name) Then
                Continue For
            End If

            args.Add($"--{Mappings(prop.Name)}={prop.GetValue(Me)?.ToString.ToLower}")
        Next
        args.Add("--align-x=none --align-y=none")

        Debug.WriteLine(String.Join(" ", args))

        Return String.Join(" ", args)

    End Function


End Class

Public Interface INetworkPrinter

    Property Name As String
    Property UploadURL As String
    Property AutoPrint As Boolean
    Function SendGcode(gcode As String) As Integer



End Interface

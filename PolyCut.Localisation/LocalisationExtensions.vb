Imports System.Runtime.CompilerServices


Public Module LocalisationExtensions

    <Extension>
    Public Function LT(source As String, Optional context As String = Nothing) As String
        Return L.T(source, context)
    End Function

    <Extension>
    Public Function LTF(source As String, ParamArray args As Object()) As String
        Return String.Format(L.CurrentCulture, L.T(source), args)
    End Function

    <Extension>
    Public Function LTFC(source As String, context As String, ParamArray args As Object()) As String
        Return String.Format(L.CurrentCulture, L.T(source, context), args)
    End Function

End Module

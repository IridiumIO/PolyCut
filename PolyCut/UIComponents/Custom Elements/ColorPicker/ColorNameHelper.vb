

Imports System.Reflection

''' <summary>
''' Adapted from https://github.com/microsoft/PowerToys/blob/main/src/common/ManagedCommon/ColorNameHelper.cs
''' ﻿// Copyright (c) Microsoft Corporation
''' // The Microsoft Corporation licenses this file to you under the MIT license.
''' // See the LICENSE file in the project root for more information.
''' </summary>

Public Module ColorNameHelper

    Private ReadOnly hueLimitsForSatLevel1 As UShort() = {8, 0, 0, 44, 0, 0, 0, 63, 0, 0, 122, 0, 134, 0, 0, 0, 0, 166, 176, 241, 0, 256, 0}
    Private ReadOnly hueLimitsForSatLevel2 As UShort() = {0, 10, 0, 32, 46, 0, 0, 0, 61, 0, 106, 0, 136, 144, 0, 0, 0, 158, 166, 241, 0, 0, 256}
    Private ReadOnly hueLimitsForSatLevel3 As UShort() = {0, 8, 0, 0, 39, 46, 0, 0, 0, 71, 120, 0, 131, 144, 0, 0, 163, 0, 177, 211, 249, 0, 256}
    Private ReadOnly hueLimitsForSatLevel4 As UShort() = {0, 11, 26, 0, 0, 38, 45, 0, 0, 56, 100, 121, 129, 0, 140, 0, 180, 0, 0, 224, 241, 0, 256}
    Private ReadOnly hueLimitsForSatLevel5 As UShort() = {0, 13, 27, 0, 0, 36, 45, 0, 0, 59, 118, 0, 127, 136, 142, 0, 185, 0, 0, 216, 239, 0, 256}

    Private ReadOnly lumLow As Byte() = {130, 100, 115, 100, 100, 100, 110, 75, 100, 90, 100, 100, 100, 100, 80, 100, 100, 100, 100, 100, 100, 100, 100}
    Private ReadOnly lumHigh As Byte() = {170, 170, 170, 155, 170, 170, 170, 170, 170, 115, 170, 170, 170, 170, 170, 170, 170, 170, 150, 150, 170, 140, 165}

    Private ReadOnly colorNamesLight As String() = {
        "Light Coral",
        "Misty Rose",
        "Peach",
        "Sand",
        "Beige",
        "Light Gold",
        "Lemon",
        "Khaki",
        "Pistachio",
        "Light Lime",
        "Mint",
        "Spring Green",
        "Light Teal",
        "Light Aqua",
        "Light Turquoise",
        "Powder Blue",
        "Sky Blue",
        "Ice Blue",
        "Periwinkle",
        "Lavender",
        "Light Pink",
        "Warm Taupe",
        "Blush"
    }

    Private ReadOnly colorNamesMid As String() = {
        "Coral",
        "Red",
        "Orange",
        "Brown",
        "Tan",
        "Gold",
        "Yellow",
        "Olive",
        "Yellow-Green",
        "Lime",
        "Green",
        "Chartreuse",
        "Teal",
        "Aqua",
        "Turquoise",
        "Pale Blue",
        "Blue",
        "Slate",
        "Indigo",
        "Purple",
        "Hot Pink",
        "Umber",
        "Crimson"
    }

    Private ReadOnly colorNamesDark As String() = {
        "Mahogany",            
        "Burgundy",            
        "Burnt Orange",        
        "Espresso",            
        "Taupe",               
        "Bronze",              
        "Mustard",             
        "Olive Drab",          
        "Forest Green",        
        "Dark Lime",           
        "Dark Green",          
        "Emerald",             
        "Dark Teal",           
        "Deep Teal",           
        "Deep Turquoise",      
        "Navy",                
        "Dark Blue",           
        "Blue-Gray",           
        "Deep Indigo",         
        "Dark Purple",         
        "Plum",                
        "Dark Umber",          
        "Dark Crimson"         
    }

    Public Function GetColorNameIdentifier(color As Color) As String
        Dim hDeg As Double, s01 As Double, l01 As Double
        ConvertToHsl(color, hDeg, s01, l01)

        ' Scale to 0..255 once (ints for fast comparisons)
        Dim hue255 As Integer = If(hDeg = 0, 0, CInt(hDeg * (255.0 / 360.0)))
        Dim sat255 As Integer = CInt(s01 * 255.0)
        Dim lum255 As Integer = CInt(l01 * 255.0)

        ' Achromatic by luminosity extremes
        If lum255 > 240 Then Return "White"
        If lum255 < 20 Then Return "Black"

        ' Achromatic by low saturation
        If sat255 <= 20 Then
            If lum255 > 170 Then Return "Light Gray"
            If lum255 > 100 Then Return "Gray"
            Return "Dark Gray"
        End If

        Dim limits As UShort() =
                If(sat255 <= 75, hueLimitsForSatLevel1,
                If(sat255 <= 115, hueLimitsForSatLevel2,
                If(sat255 <= 150, hueLimitsForSatLevel3,
                If(sat255 <= 240, hueLimitsForSatLevel4, hueLimitsForSatLevel5))))

        ' Determine bucket
        Dim idx As Integer = -1
        For i As Integer = 0 To colorNamesMid.Length - 1
            If hue255 < limits(i) Then
                idx = i
                Exit For
            End If
        Next
        If idx < 0 Then Return String.Empty

        Dim hi As Integer = lumHigh(idx)
        Dim lo As Integer = lumLow(idx)

        If lum255 > hi Then Return colorNamesLight(idx)
        If lum255 < lo Then Return colorNamesDark(idx)
        Return colorNamesMid(idx)
    End Function

    ' h: degrees [0..360), s/l: [0..1]
    Private Sub ConvertToHsl(c As Color, ByRef h As Double, ByRef s As Double, ByRef l As Double)
        Dim r As Double = c.R / 255.0
        Dim g As Double = c.G / 255.0
        Dim b As Double = c.B / 255.0

        Dim maxv = Math.Max(r, Math.Max(g, b))
        Dim minv = Math.Min(r, Math.Min(g, b))
        Dim delta = maxv - minv

        l = (maxv + minv) * 0.5

        If delta = 0 Then
            h = 0
            s = 0
            Return
        End If

        Dim denom = 1.0 - Math.Abs(2.0 * l - 1.0)
        s = If(denom = 0, 0, delta / denom)

        Dim hp As Double
        If maxv = r Then
            hp = (g - b) / delta
            If hp < 0 Then hp += 6.0
        ElseIf maxv = g Then
            hp = ((b - r) / delta) + 2.0
        Else
            hp = ((r - g) / delta) + 4.0
        End If

        h = 60.0 * hp
        If h >= 360.0 Then h -= 360.0
    End Sub

End Module







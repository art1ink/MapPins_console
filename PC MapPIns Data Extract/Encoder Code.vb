Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

Public Class Form1
    Private TargetTables As String = "|Bosses|SkyShards|Lorebooks|TreasureMaps|ChestData|Achievements|FishingNodes|UnknownPOI"
    Private MapIdMap As New Dictionary(Of String, String)()

    Private Async Sub btnConvert_Click(sender As Object, e As EventArgs) Handles btnConvert.Click
        btnConvert.Enabled = False
        lblStatus.Text = "Status: Loading MapIdMap.lua..."
        TargetTables = "|Bosses|SkyShards|Lorebooks|TreasureMaps|ChestData|Achievements|FishingNodes|UnknownPOI"
        If File.Exists("MapIdMap.lua") Then
            MapIdMap.Clear()
            For Each line As String In File.ReadLines("MapIdMap.lua")
                Dim match = Regex.Match(line, "\[""(.*?)""\]\s*=\s*([\d.]+)")
                If match.Success Then
                    MapIdMap(match.Groups(1).Value) = match.Groups(2).Value
                End If

            Next
        Else
            MessageBox.Show("MapIdMap.lua not found!")
            btnConvert.Enabled = True
            Exit Sub
        End If

        lblStatus.Text = "Status: Converting MapPins.lua..."

        Try
            Await Task.Run(Sub() ProcessMapPins())
            lblStatus.Text = "Status: Done!" & vbCrLf & "CombinedMapData.lua created."
        Catch ex As Exception
            MessageBox.Show("Error: " & ex.Message)
        Finally
            btnConvert.Enabled = True
        End Try
    End Sub
    Dim errorString As String = ""

    Private Sub ProcessMapPins()
        Dim activeTable As String = ""
        Dim currentPos As Long = 0
        Dim currentMapId As String = ""
        Dim justStartedMap As Boolean = False
        Dim inPOI As Boolean = False
        Dim tableKeys As New Dictionary(Of String, Tuple(Of Long, Long))()

        Dim TableLine As Boolean = False
        Dim MapStart As Boolean = False
        Dim RemoverCommer As Boolean = False
        Dim bracketIndex As Integer = 0
        Dim inString As Boolean = False
        Dim ExtraIndexed As Integer = 0
        Dim rawKey As String = ""
        Dim currentKey As String = ""
        Dim outputText As String = ""

        Using reader As New StreamReader("MapPins.lua", Encoding.UTF8)
            Using writer As New StreamWriter("CombinedMapData.lua", False, Encoding.UTF8)

                While Not reader.EndOfStream
                    If TargetTables = "" And activeTable = "" Then Exit While
                    ' Get next line
                    Dim rawLine As String = reader.ReadLine()
                    ' Removes comments
                    If rawLine.Contains("--") Then rawLine = rawLine.Split(New String() {"--"}, StringSplitOptions.None)(0)
                    ' Removes the spaceing in line, used for finding start point
                    Dim cleanLine As String = rawLine.Replace(" ", "").Replace("	", "").Trim()
                    ' if line is empty go to next line
                    If String.IsNullOrWhiteSpace(cleanLine) Then Continue While
                    ' 1. Detect Table Start
                    If cleanLine.StartsWith("local") AndAlso cleanLine.Contains("=") Then
                        'grab table name
                        Dim tableName As String = rawLine.Substring(rawLine.IndexOf("local ") + 6, rawLine.IndexOf("=") - (rawLine.IndexOf("local ") + 6)).Trim()
                        'check if its a table we want to extract
                        If TargetTables.Contains("|" & tableName) Then
                            'set the table
                            activeTable = tableName
                            TableLine = True
                            currentPos = 0
                            outputText = ""
                            currentMapId = ""
                            tableKeys.Clear()
                            writer.Write("local " & activeTable & "=" & Chr(34))
                            TargetTables = TargetTables.Replace("|" & tableName, "")

                            If activeTable = "UnknownPOI" Or activeTable = "ChestData" Or activeTable = "Achievements" Then
                                ExtraIndexed = 1
                            Else
                                ExtraIndexed = 0
                            End If

                        Else
                            activeTable = ""
                            Continue While
                        End If
                    End If
                    ' Start Reading the line
                    If activeTable <> "" Then
                        'strips the start part
                        If TableLine Then
                            rawLine = rawLine.Replace("local " & activeTable & "={", "")
                            bracketIndex = 1
                            TableLine = False
                        End If
                        Dim formatLine As String = ""
                        For Each c As Char In rawLine
                            If c = " " Or c = vbTab Then
                                If inString Then
                                    formatLine += " "
                                End If
                            ElseIf c = "{" Then
                                If MapStart Then
                                    MapStart = False
                                ElseIf bracketIndex = 2 And ExtraIndexed = 0 Or bracketIndex = 3 And ExtraIndexed = 2 Then
                                    formatLine += "|"
                                Else
                                    formatLine += "{"
                                End If
                                bracketIndex += 1
                            ElseIf c = "}" Then
                                bracketIndex -= 1
                                If bracketIndex <> 1 And bracketIndex <> 2 And ExtraIndexed = 0 Or bracketIndex <> 1 And bracketIndex <> 2 And bracketIndex <> 3 And ExtraIndexed = 2 Then
                                    formatLine += "}"
                                End If
                                RemoverCommer = True
                                If bracketIndex = 1 Then
                                    If activeTable = "UnknownPOI" Or activeTable = "ChestData" Or activeTable = "Achievements" Then ExtraIndexed = 1
                                    outputText += formatLine
                                    tableKeys(rawKey) = New Tuple(Of Long, Long)(currentPos + 1, outputText.Length)
                                    currentPos = outputText.Length
                                    writer.Write(formatLine)
                                    formatLine = ""
                                End If
                            ElseIf c = """" Then
                                inString = Not inString
                                formatLine += "@"
                            ElseIf c = "," Then
                                If RemoverCommer Then
                                    RemoverCommer = False
                                ElseIf inString Then
                                    formatLine += "^"
                                Else
                                    formatLine += ","
                                End If
                            ElseIf c = "=" Then
                                MapStart = True
                                If ExtraIndexed = 2 Then
                                    formatLine = "[" & formatLine
                                ElseIf ExtraIndexed = 1 Then
                                    ExtraIndexed = 2
                                    rawKey = formatLine.Replace("[", "").Replace("]", "").Replace("@", "")
                                    If activeTable <> "UnknownPOI" Then
                                        If MapIdMap.ContainsKey(rawKey) Then
                                            rawKey = MapIdMap(rawKey)
                                        Else
                                            errorString += activeTable & " Missing: " & rawKey & vbCrLf
                                            rawKey = "0"
                                        End If
                                    End If
                                    If rawKey <> "" Then tableKeys(rawKey) = New Tuple(Of Long, Long)(currentPos + 1, 0)
                                    formatLine = ""
                                Else
                                    rawKey = formatLine.Replace("[", "").Replace("]", "").Replace("@", "")

                                    If MapIdMap.ContainsKey(rawKey) Then
                                        rawKey = MapIdMap(rawKey)
                                    End If

                                    formatLine = "#"
                                End If


                            ElseIf c = "[" Or c = "]" Then
                            Else
                                formatLine += c
                            End If
                        Next
                        RemoverCommer = False
                        outputText += formatLine
                        writer.Write(formatLine)
                        If bracketIndex = 0 Then
                            writer.WriteLine(Chr(34))
                            writer.Write("local " & activeTable & "_key={")
                            For Each kvp In tableKeys
                                writer.Write("[" & kvp.Key & "]={" & kvp.Value.Item1 & "," & kvp.Value.Item2 & "},")
                            Next
                            writer.WriteLine("}")
                            writer.WriteLine()
                            activeTable = ""
                        End If
                    End If
                End While
            End Using
        End Using
        Dim strFile As String = "errorOutput.txt"
        Dim fileExists As Boolean = File.Exists(strFile)
        Using sw As New StreamWriter(File.Open(strFile, FileMode.OpenOrCreate))
            sw.WriteLine(errorString)
        End Using
    End Sub
End Class
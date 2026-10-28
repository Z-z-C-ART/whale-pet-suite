Option Explicit
' Silent, single-instance launcher for the standalone whale pet.
' Double-click to start the pet when DSH is not running.
' If an instance is already alive (tracked in standalone-runtime.json), exits quietly.
Dim sh, fso, cmd, runtime, text, re, port, http, alive
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

cmd = sh.ExpandEnvironmentStrings("%APPDATA%") & "\npm\dsh-pet-standalone.cmd"
runtime = sh.ExpandEnvironmentStrings("%USERPROFILE%") & "\.dsh\dsh-pet\standalone-runtime.json"

If Not fso.FileExists(cmd) Then
  MsgBox "dsh-pet is not installed." & vbCrLf & vbCrLf & _
         "Open a terminal and run:" & vbCrLf & "npm install -g dsh-pet", _
         vbExclamation, "Whale Pet"
  WScript.Quit 1
End If

' Already running? Probe the port recorded by the previous instance.
alive = False
If fso.FileExists(runtime) Then
  On Error Resume Next
  text = fso.OpenTextFile(runtime, 1, False).ReadAll
  Set re = New RegExp
  re.Pattern = """port"":\s*(\d+)"
  If re.Test(text) Then
    port = re.Execute(text)(0).SubMatches(0)
    Set http = CreateObject("MSXML2.XMLHTTP")
    http.Open "GET", "http://127.0.0.1:" & port & "/health", False
    http.Send
    If Err.Number = 0 Then
      If http.Status = 200 Then alive = True
    End If
    Err.Clear
  End If
  On Error GoTo 0
End If

If alive Then
  WScript.Quit 0
End If

' cmd.exe /c is required: WScript.Shell cannot launch a .cmd directly with a hidden window.
sh.Run "cmd.exe /c """ & cmd & """", 0, False

$ErrorActionPreference='Stop'
$source=Get-Content "$PSScriptRoot\..\NextConfig\clOprosCommands.vb" -Raw
$methods=@('SetTime','ResetController') | ForEach-Object {[regex]::Match($source,'(?s)    Public Function '+$_+'\(.*?    End Function').Value}
$prefix=@"
Imports System
Imports System.Threading
Public Class AuditOpros
 Public Class Wire
  Public Reply As Byte()
  Public Function SendRecCmd(ByRef rec As Byte(), command As Byte(), timeout As Integer) As Boolean
   rec=Reply
   Return True
  End Function
 End Class
 Public Comport As New Wire
 Private _isPause As Boolean
 Private _isSending As Boolean
 Private Const cmdIdx As Integer=8
 Private Const payloadIdx As Integer=9
 Private Enum eCmd
  CMD_RESET=2
  CMD_SETTIME=23
  REC_ANSWER=11
 End Enum
 Private Function SetCmd(cmd As Byte, payload As Byte()) As Byte()
  Return New Byte() {cmd}
 End Function
"@
Add-Type -IgnoreWarnings -WarningAction SilentlyContinue -Language VisualBasic -TypeDefinition ($prefix+[Environment]::NewLine+($methods -join [Environment]::NewLine)+[Environment]::NewLine+'End Class')
$probe=New-Object AuditOpros
$reply=New-Object byte[] 13
$reply[8]=11; $reply[9]=23; $reply[10]=0
$probe.Comport.Reply=$reply
if (-not $probe.SetTime(1)) {throw 'SetTime success ACK rejected'}
$reply[9]=3
if ($probe.ResetController()) {throw 'foreign reset ACK accepted'}
$reply[9]=2; if (-not $probe.ResetController()) {throw 'reset ACK rejected'}
$bootSource=Get-Content "$PSScriptRoot\..\NextConfig\frmBootDevice.vb" -Raw
$selection=[regex]::Match($bootSource,'(?m)^        Dim oldModem = target = 140\r?\n        Dim firmwareHw = If\(target = 0 OrElse target = 140, hardware, target\)').Value
$bootCode="Public Class BootSelectionProbe`r`n Private target As Byte=140`r`n Private hardware As Byte=139`r`n Public Function Attempt() As Boolean`r`n"+$selection+"`r`n Return oldModem`r`n End Function`r`nEnd Class"
Add-Type -Language VisualBasic -TypeDefinition $bootCode
$bootProbe=New-Object BootSelectionProbe
if (-not $bootProbe.Attempt() -or -not $bootProbe.Attempt()) {throw 'old modem profile lost on retry'}
Write-Output 'PASS: 4 isolated protocol/profile checks; no transport.'

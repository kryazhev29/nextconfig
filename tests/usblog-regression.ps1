param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe", [string]$SnapshotDirectory='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
if($SnapshotDirectory) {
    Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class UsbLogDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }'
    [UsbLogDpi]::SetProcessDPIAware() | Out-Null
}
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:checks=0
function Check($ok,$name) { if(-not $ok){throw "FAIL: $name"}; $script:checks++ }
function Frame([byte]$kind,[byte[]]$body,[uint16]$lost=0) {
    $p=New-Object byte[] (7+$body.Length)
    $p[0]=$kind
    [BitConverter]::GetBytes([uint32]15903).CopyTo($p,1)
    [BitConverter]::GetBytes($lost).CopyTo($p,5)
    $body.CopyTo($p,7)
    return ,([NextConfig.SldnStream]::Frame(109,$p,0))
}
# Этот сценарий использует только старый публичный API и падает на 3dead6f.
$idle=New-Object NextConfig.ClComPort((New-Object IO.Ports.SerialPort('OFFLINE')),$false)
$script:idleLines=0
$script:idleAnswers=0
$idle.add_ReceiveComand({param($line) $script:idleLines++})
$idle.add_ReceiveBytes({param($frame) $script:idleAnswers++})
$idleWire=[byte[]]((Frame 1 ([Text.Encoding]::ASCII.GetBytes('AT+CSQ')))+[NextConfig.SldnStream]::Frame(108,[byte[]]@(),12))
for($i=0;$i -lt $idleWire.Length;$i+=3) {
    $piece=New-Object byte[] ([Math]::Min(3,$idleWire.Length-$i))
    [Array]::Copy($idleWire,$i,$piece,0,$piece.Length)
    $idle.CheckReceivedBytes($piece)
}
Check ($script:idleLines -eq 1 -and $script:idleAnswers -eq 1) 'fragmented idle log and reply'
$idle.Dispose()

$format=New-Object NextConfig.UsbLog
$resetBody=New-Object byte[] 32
$resetBody[9]=[byte][char]'4'
$resetText=$format.Format((Frame 32 $resetBody))
Check (($resetText -join ' ').Contains('Заводской сброс: кнопка')) 'factory reset button source'
$resetBody[8]=41
$resetText=$format.Format((Frame 32 $resetBody))
Check (($resetText -join ' ').Contains('Мост не ответил на выключение')) 'bridge shutdown timeout event'
$samples=@(
    @(1,[Text.Encoding]::ASCII.GetBytes('AT+CEREG?'),'→ AT+CEREG?'),
    @(2,[Text.Encoding]::ASCII.GetBytes('+CEREG: 0,1'),'← +CEREG: 0,1'),
    @(3,[Text.Encoding]::ASCII.GetBytes('RING'),'уведомление: RING'),
    @(4,[byte[]](0,0,0),'← OK'),
    @(4,[byte[]](2,10,0),'CME 10'),
    @(4,[byte[]](4,0,0),'модем не ответил'),
    @(5,[byte[]](1,128,0),'128 байт'),
    @(16,[byte[]](2,0,156,24,0,0,1),'SIM'),
    @(16,[byte[]](6,5,156,24,0,0,2),'SIM требует PIN'),
    @(17,[byte[]](0,0,0,0,0),'регистрация принята'),
    @(18,[byte[]](1,96,234,0,0),'60,0 с')
)
foreach($s in $samples) {
    $lines=$format.Format((Frame $s[0] $s[1]))
    Check ($lines.Count -eq 1 -and $lines[0].Contains('15,903') -and $lines[0].Contains($s[2])) "format $($s[0]) $($s[2])"
}
function Net([byte[]]$head,[string]$plmn,[byte[]]$lte) { return ,([byte[]]($head+[Text.Encoding]::ASCII.GetBytes($plmn.PadRight(7,[char]0))+$lte)) }
$line=$format.Format((Frame 19 (Net ([byte[]](22,1,1,4)) '25002' ([byte[]](95,9,30,7,3)))))[0]
Check ($line.Contains('Сеть: 4G, 25002') -and $line.Contains('сигнал средний (−95 дБм, −9 дБ)') -and $line.EndsWith('регистрация: голос и данные')) 'network snapshot LTE'
$line=$format.Format((Frame 19 (Net ([byte[]](22,1,1,4)) '25002' ([byte[]](85,16,30,7,3)))))[0]
Check ($line.Contains('сигнал хороший (−85 дБм, −16 дБ)')) 'network snapshot LTE: quality does not lower level'
$line=$format.Format((Frame 19 (Net ([byte[]](12,1,0,1)) '25001' ([byte[]](255,255,255,255,255)))))[0]
Check ($line.Contains('Сеть: 2G, 25001') -and $line.Contains('сигнал средний (12 из 31)') -and $line.EndsWith('регистрация: только голос')) 'network snapshot CSQ'
$line=$format.Format((Frame 19 (Net ([byte[]](99,0,0,0)) '' ([byte[]](255,255,255,255,255)))))[0]
Check ($line.EndsWith('Сеть: не найдена, регистрация: нет')) 'network snapshot none'
$event=New-Object byte[] 32
$event[0]=7; $event[8]=38; $event[9]=49; $event[10]=50
Check ($format.Format((Frame 32 $event))[0].Contains('Низкий уровень сигнала')) 'event name'
Check ($format.Format((Frame 32 $event))[0].Contains('Низкий уровень сигнала, ID 12')) 'event fields'
Check (-not $format.Format((Frame 32 $event))[0].Contains('значение')) 'empty event value hidden'
Check ($format.Format((Frame 4 ([byte[]](1,0,0))))[0].Contains('← ERROR')) 'plain ERROR'
$lost=$format.Format((Frame 1 ([byte[]](65)) 37))
Check ($lost.Count -eq 2 -and $lost[0] -eq '— пропущено 37 записей журнала: USB был занят —') 'loss visible'
$glue=New-Object NextConfig.UsbLog
Check ($glue.Format((Frame 130 ([Text.Encoding]::ASCII.GetBytes('+QENG: "servingcell"')))).Count -eq 0) 'continuation waits'
$line=$glue.Format((Frame 2 ([Text.Encoding]::ASCII.GetBytes(',"CONNECT","LTE"'))))
Check ($line.Count -eq 1 -and $line[0].EndsWith('← +QENG: "servingcell","CONNECT","LTE"')) 'continuation glued'
Check ($glue.Format((Frame 130 ([byte[]](65)))).Count -eq 0) 'next long line waits'
$cut=$glue.Format((Frame 1 ([byte[]](66)) 3))
Check ($cut.Count -eq 3 -and $cut[0].EndsWith('← A … [конец строки потерян]') -and $cut[1].Contains('пропущено 3') -and $cut[2].EndsWith('→ B')) 'lost tail visible'
Check (-not $format.Format((Frame 2 ([byte[]](65,13,10,0,66))))[0].Contains("`n")) 'control bytes sanitized'
Check ($format.Format((Frame 16 ([byte[]](99,0,0,0,0,0,0)))).Count -eq 0) 'invalid phase rejected'

Check ((New-Object NextConfig.UsbLog).Format((Frame 16 ([byte[]](6,1,156,24,0,0,2)) 17))[1].Contains('на этапе «SIM»')) 'phase known after loss or enabling midway'

$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$port=New-Object NextConfig.ClComPort((New-Object IO.Ports.SerialPort('OFFLINE')), $false)
$script:lines=New-Object 'Collections.Generic.List[string]'
$port.add_ReceiveComand({param($line) $script:lines.Add($line)})
$request=[NextConfig.SldnStream]::Command(108,[byte[]](7),12)
$answer=[NextConfig.SldnStream]::Frame(108,[byte[]]@(),12)
$log=Frame 1 ([Text.Encoding]::ASCII.GetBytes('AT+CSQ'))
$wire=[byte[]]($log+$answer+$log)
$feed=$port.GetType().GetMethod('ProcessReceived',$flags)
$random=New-Object Random(109)
for($sample=0;$sample -lt 100;$sample++) {
    $before=$script:lines.Count
    $received=$null
    for($i=0;$i -lt $wire.Length;) {
        $count=[Math]::Min($random.Next(1,18),$wire.Length-$i)
        $piece=New-Object byte[] $count
        [Array]::Copy($wire,$i,$piece,0,$count)
        $args=New-Object object[] 2
        $args[0]=[byte[]]$piece
        $args[1]=[byte[]]$request
        $reply=$feed.Invoke($port,$args)
        if($reply){$received=$reply}
        $i+=$count
    }
    Check ($script:lines.Count-$before -eq 2) "fragmented logs $sample"
    Check ([BitConverter]::ToString($received) -eq [BitConverter]::ToString($answer)) "interleaved reply $sample"
}
$port.Dispose()

# Настоящий SendRecCmdCore с моделью SerialPort: никакого COM.
$source=Get-Content "$PSScriptRoot\..\NextConfig\clComPort.vb" -Raw
$methods=@('SendRecCmdCore','ProcessReceived','ReadReceived') | ForEach-Object {
    [regex]::Match($source,'(?ms)^    (?:Private|Friend) Function '+$_+'\(.*?^    End Function').Value
}
Check (@($methods | Where-Object {$_ -ne ''}).Count -eq 3) 'actual receive methods extracted'
$prefix=@'
Imports System
Imports System.Linq
Imports System.Threading
Imports System.Collections.Generic
Imports Microsoft.VisualBasic
Imports NextConfig
Public Class UsbLogTransportProbe
 Public Event ReceiveComand(value As String)
 Public Event ReceiveBytes(value As Byte())
 Public Event PinRequired()
 Public Event ErrorPort()
 Private rxGate As New Object
 Private receiver As New SldnStream
 Private journal As New UsbLog
 Private EnableEventReceived As Boolean
 Public _port As New Wire
 Public Class Wire
  Public IsOpen As Boolean=True
  Public WriteTimeout As Integer=1000
  Public Before As Byte()={}
  Public After As Byte()={}
  Public Commands As New List(Of Byte())
  Private pending As New Queue(Of Byte)
  Public ReadOnly Property PortName As String
   Get
    Return "OFFLINE"
   End Get
  End Property
  Public ReadOnly Property BytesToWrite As Integer
   Get
    Return 0
   End Get
  End Property
  Public ReadOnly Property BytesToRead As Integer
   Get
    If pending.Count=0 AndAlso Before.Length>0 Then
     For Each b In Before
      pending.Enqueue(b)
     Next
     Before={}
    End If
    Return pending.Count
   End Get
  End Property
  Public Sub Write(bytes As Byte(),start As Integer,count As Integer)
   Commands.Add(bytes.Skip(start).Take(count).ToArray())
   For Each b In After
    pending.Enqueue(b)
   Next
  End Sub
  Public Function Read(bytes As Byte(),start As Integer,count As Integer) As Integer
   For i=0 To count-1
    bytes(start+i)=pending.Dequeue()
   Next
   Return count
  End Function
 End Class
 Private Shared Function Crc16(b As Byte(),offset As Integer) As Integer
  Return ClComPort.Crc16(b,offset,b.Length-offset)
 End Function
 Public Function Exchange(ByRef response As Byte(),command As Byte()) As Boolean
  Return SendRecCmdCore(response,command,500,False,False)
 End Function
'@
$enableSource=Get-Content "$PSScriptRoot\..\NextConfig\clOprosCommands.vb" -Raw
$enable=[regex]::Match($enableSource,'(?ms)^    Public Function EnableLog\(.*?^    End Function').Value.Replace('Comport.SendRecCmd','Me.SendRecCmd')
$pollerSource=Get-Content "$PSScriptRoot\..\NextConfig\clOpros.vb" -Raw
$logProperty=[regex]::Match($pollerSource,'(?m)^    Public Property LogBits As Byte[^\r\n]*').Value.Trim()
$extra=@'
 Public Property LogBits As Byte
 Private _isPause As Boolean
 Private _isSending As Boolean
 Private Const cmdIdx As Integer=8
 Private Enum eCmd
  CMD_ENABLELOG=108
 End Enum
 Private Function SetCmd(cmd As Byte,payload As Byte()) As Byte()
  Return SldnStream.Command(cmd,payload,12)
 End Function
 Public Function SendRecCmd(ByRef rec As Byte(),command As Byte(),timeout As Integer) As Boolean
  Return SendRecCmdCore(rec,command,timeout,False,False)
 End Function
'@
$formSource=Get-Content "$PSScriptRoot\..\NextConfig\Form2.vb" -Raw
$restoreMethods=@('RestoreLogSelection','ShowLogSelection') | ForEach-Object {[regex]::Match($formSource,'(?ms)^    Private Sub '+$_+'\(.*?^    End Sub').Value}
$reconnect=@'
Public Class ReconnectProbe
 Public opros As New UsbLogTransportProbe
 Private updatingLog As Boolean
 Public warnings As New List(Of String)
 Private Sub AppendLogLine(value As String)
  warnings.Add(value)
 End Sub
 Public Class Box
  Public Property Checked As Boolean
 End Class
 Public CheckBox1 As New Box
 Public CheckBox2 As New Box
 Public CheckBox3 As New Box
 Public Sub Connected()
  RestoreLogSelection()
 End Sub
'@
$code=$prefix+"`r`n"+($methods -join "`r`n")+"`r`n"+$extra+"`r`n"+$enable+"`r`nEnd Class`r`n"+$reconnect+"`r`n"+($restoreMethods -join "`r`n")+"`r`nEnd Class`r`n"
$code=$code.Replace('Public Property LogBits As Byte',$logProperty)
# Add-Type с внешней сборкой не работает в этой среде; стенд собирает тот же MSBuild.
$harness=Join-Path $env:TEMP 'domovoi-usblog-next-harness'
New-Item -ItemType Directory -Force -Path $harness | Out-Null
$utf8=New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText((Join-Path $harness 'Harness.vb'),$code,$utf8)
$reference=[Security.SecurityElement]::Escape((Resolve-Path $AssemblyPath).Path)
$project=@"
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
 <PropertyGroup><OptionInfer>On</OptionInfer><OutputType>Library</OutputType><AssemblyName>UsbLogHarness</AssemblyName><TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion><OutputPath>bin\</OutputPath></PropertyGroup>
 <ItemGroup><Reference Include="System"/><Reference Include="System.Core"/><Reference Include="NextConfig"><HintPath>$reference</HintPath><Private>False</Private></Reference><Compile Include="Harness.vb"/></ItemGroup>
 <Import Project="`$(MSBuildToolsPath)\Microsoft.VisualBasic.targets"/>
</Project>
"@
[IO.File]::WriteAllText((Join-Path $harness 'Harness.vbproj'),$project,$utf8)
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' (Join-Path $harness 'Harness.vbproj') /v:quiet /nologo > (Join-Path $harness 'build.log')
if($LASTEXITCODE){throw (Get-Content (Join-Path $harness 'build.log') -Raw)}
[Reflection.Assembly]::LoadFrom((Join-Path $harness 'bin\UsbLogHarness.dll')) | Out-Null
$probe=New-Object UsbLogTransportProbe
$script:exchangeLines=New-Object 'Collections.Generic.List[string]'
$probe.add_ReceiveComand({param($line) $script:exchangeLines.Add($line)})
$probe._port.Before=$log
$probe._port.After=[byte[]]($log+$answer+$log)
$rec=$null
Check ($probe.Exchange([ref]$rec,$request)) 'SendRecCmd accepts reply'
Check ($script:exchangeLines.Count -eq 3) 'logs before write, during exchange and after reply preserved'
Check ([BitConverter]::ToString($rec) -eq [BitConverter]::ToString($answer)) 'correct exchange answer'

$reconnect=New-Object ReconnectProbe
Check ($reconnect.opros.LogBits -eq 6) 'new session defaults to GSM and general events'
$reconnect.opros._port.After=$answer
$reconnect.Connected()
Check ($reconnect.opros._port.Commands[0][8] -eq 108 -and $reconnect.opros._port.Commands[0][9] -eq 6) 'default mask 108 sent immediately'
Check (-not $reconnect.CheckBox1.Checked -and $reconnect.CheckBox2.Checked -and $reconnect.CheckBox3.Checked) 'AT opt-in, GSM and general selected'
$reconnect.opros._port.Commands.Clear()
$reconnect.opros.LogBits=7
$reconnect.opros._port.After=$answer
1..2 | ForEach-Object {$reconnect.Connected()}
Check ($reconnect.opros._port.Commands.Count -eq 2) '108 resent after both connections'
foreach($command in $reconnect.opros._port.Commands) {Check ($command[8] -eq 108 -and $command[9] -eq 7) '108 current mask'}
$reconnect.opros.LogBits=0
$reconnect.Connected()
Check ($reconnect.opros.LogBits -eq 0 -and -not $reconnect.CheckBox2.Checked -and -not $reconnect.CheckBox3.Checked) 'explicit all-off survives reconnect'
$reconnect.opros.LogBits=6
$reconnect.opros._port.After=[byte[]]@()
$reconnect.Connected()
Check ($reconnect.opros.LogBits -eq 6 -and $reconnect.CheckBox2.Checked -and $reconnect.CheckBox3.Checked) 'lost 108 reply preserves selection'
Check ($reconnect.warnings.Count -eq 1) 'lost 108 reply explained in journal'
$reconnect.opros._port.After=$answer
$reconnect.Connected()
Check ($reconnect.opros._port.Commands[$reconnect.opros._port.Commands.Count-1][9] -eq 6) 'reconnect retries saved selection after lost reply'

# Два подключения проходят тот же метод восстановления галочек.
$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
[NextConfig.Demo].GetField('Mode').SetValue($null,[NextConfig.eDemoMode]::Found)
$poller.LogBits=7
$form=New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',4321)
try {
    $restore=$form.GetType().GetMethod('RestoreLogSelection',$flags)
    1..2 | ForEach-Object {
        $restore.Invoke($form,@()) | Out-Null
        foreach($name in 'CheckBox1','CheckBox2','CheckBox3') {
            Check ($form.GetType().GetProperty($name,$flags).GetValue($form,$null).Checked) "reconnect $_ $name"
        }
        Check ($poller.LogBits -eq 7) "reconnect $_ mask"
    }
    if($SnapshotDirectory) {
        New-Item -ItemType Directory -Force -Path $SnapshotDirectory | Out-Null
        $load=[Delegate]::CreateDelegate([EventHandler],$form,$form.GetType().GetMethod('Form2_Load',$flags))
        $form.remove_Load($load)
        $form.GetType().GetField('linkLoading',$flags).SetValue($form,$true)
        $form.ShowInTaskbar=$false
        $form.Show()
        $tabs=$form.GetType().GetProperty('tabs',$flags).GetValue($form,$null)
        $tabs.SelectedTab=$form.GetType().GetProperty('tabLog',$flags).GetValue($form,$null)
        $append=$form.GetType().GetMethod('AppendLogLine',$flags)
        foreach($s in $samples) {foreach($line in $format.Format((Frame $s[0] $s[1]))) {$append.Invoke($form,@($line)) | Out-Null}}
        foreach($line in $lost) {$append.Invoke($form,@($line)) | Out-Null}
        foreach($line in $format.Format((Frame 32 $event))) {$append.Invoke($form,@($line)) | Out-Null}
        Check ($form.GetType().GetProperty('RichTextBox1',$flags).GetValue($form,$null).Text.Contains('15,903')) 'journal text in UI'
        $form.TopMost=$true
        $form.Activate()
        $form.PerformLayout()
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 200
        $bitmap=New-Object Drawing.Bitmap($form.ClientSize.Width,$form.ClientSize.Height)
        try {
            $graphics=[Drawing.Graphics]::FromImage($bitmap)
            try {$graphics.CopyFromScreen($form.PointToScreen([Drawing.Point]::Empty),[Drawing.Point]::Empty,$form.ClientSize)} finally {$graphics.Dispose()}
            $bitmap.Save((Join-Path $SnapshotDirectory 'journal-demo.png'))
        } finally {$bitmap.Dispose()}
    }
} finally {$form.Dispose()}
Write-Output "PASS: $script:checks USB log checks; offline, no COM."

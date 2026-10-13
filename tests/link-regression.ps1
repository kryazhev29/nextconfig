param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:checks = 0
function Check($ok, $name) {
    if (-not $ok) { throw "FAIL: $name" }
    $script:checks++
}
function Reject([byte[]]$frame, $name) {
    $rejected = $false
    try { [NextConfig.LinkDiag]::Parse($frame) | Out-Null } catch { $rejected = $true }
    Check $rejected $name
}
function Bytes($hex) {
    return ,([byte[]]($hex.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [Convert]::ToByte($_, 16) }))
}
function Seal([byte[]]$frame) {
    $crc = 65535
    foreach ($b in $frame[4..($frame.Length - 3)]) {
        $crc = $crc -bxor $b
        for ($i = 0; $i -lt 8; $i++) {
            if ($crc -band 1) { $crc = ($crc -shr 1) -bxor 40961 } else { $crc = $crc -shr 1 }
        }
    }
    $frame[$frame.Length - 2] = $crc -band 255
    $frame[$frame.Length - 1] = $crc -shr 8
    return ,$frame
}

function V4([byte[]]$frame, [byte]$rsrp = 105, [byte]$rsrq = 10, [byte]$sinr = 175) {
    $result = New-Object byte[] 96
    [Array]::Copy($frame, $result, 91)
    $result[4] = 88; $result[9] = 4
    $result[91] = $rsrp; $result[92] = $rsrq; $result[93] = $sinr
    return ,(Seal $result)
}

# Независимые побайтовые образцы протокола v3, CRC зафиксированы в кадрах.
$online = Bytes '53 4C 44 4E 55 00 00 2A 70 03 07 00 1A 04 01 32 35 30 30 31 00 00 00 00 00 00 B0 04 00 00 8C 0A 00 00 58 02 00 00 68 10 00 00 DC 05 00 00 20 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 5A 0D'
$sim = Bytes '53 4C 44 4E 55 00 00 2A 70 03 01 00 63 00 00 00 00 00 00 00 00 00 00 00 00 00 B0 04 00 00 8C 0A 00 00 64 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 01 22 04 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 19 2E'
$waiting = Bytes '53 4C 44 4E 55 00 00 2A 70 03 06 00 1A 04 02 32 35 30 30 31 00 00 7C 15 00 00 B0 04 00 00 8C 0A 00 00 58 02 00 00 68 10 00 00 DC 05 00 00 20 03 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 4E D8'
# После вытеснения старых записей прошивка передаёт 10 оставшихся от старой к новой.
# Секунды 110 -> 4 показывают перезапуск: сортировка по секундам испортила бы порядок.
$ring = Bytes '53 4C 44 4E 55 00 00 2A 70 03 02 00 1A 04 01 32 35 30 30 31 00 00 00 00 00 00 B0 04 00 00 8C 0A 00 00 58 02 00 00 68 10 00 00 DC 05 00 00 20 03 00 00 0A 08 62 00 00 11 6E 00 00 1A 04 00 00 23 0C 00 00 2C 14 00 00 35 1E 00 00 38 28 00 00 41 32 00 00 4A 3C 00 00 53 46 00 00 CC A5'
foreach ($frame in @($online, $sim, $waiting, $ring)) {
    Check ($frame.Length -eq 93) 'fixture length'
    $decoder = New-Object NextConfig.SldnStream
    $received = New-Object 'Collections.Generic.List[byte[]]'
    foreach ($b in $frame) {
        foreach ($reply in $decoder.Feed([byte[]]@($b))) { $received.Add($reply) }
    }
    Check ($received.Count -eq 1) 'one frame from byte-by-byte USB reads'
    Check ([Convert]::ToBase64String($received[0]) -eq [Convert]::ToBase64String($frame)) 'all 93 bytes preserved'
    Check ([NextConfig.LinkDiag]::Parse($received[0]).State -eq $frame[10]) 'fragmented response parsed'
}
$v4 = V4 $online
$s4 = [NextConfig.LinkDiag]::Parse($v4)
Check ($s4.Version -eq 4 -and $s4.Rsrp -eq 105 -and $s4.Rsrq -eq 10 -and $s4.Sinr -eq 175) 'v4 appended metrics'
Check ([NextConfig.LinkDiag]::SignalText($s4) -eq "RSRP −105 дБм (плохо),`r`nRSRQ −10 дБ (средне)") 'LTE units and quality without unconfirmed SINR'
foreach ($case in @(@(89,9,'хорошо','хорошо'), @(90,10,'средне','средне'), @(100,15,'средне','средне'), @(101,16,'плохо','плохо'), @(93,9,'средне','хорошо'), @(100,10,'средне','средне'))) {
    $text = [NextConfig.LinkDiag]::SignalText([NextConfig.LinkDiag]::Parse((V4 $online $case[0] $case[1] 54)))
    Check ($text -match ('RSRP .*\(' + $case[2] + '\)') -and $text -match ('RSRQ .*\(' + $case[3] + '\)')) "LTE thresholds $($case[0])/$($case[1])"
}
Check ([NextConfig.LinkDiag]::SignalText([NextConfig.LinkDiag]::Parse((V4 $online 255 255 255))) -eq "RSRP нет данных,`r`nRSRQ нет данных") 'LTE unknown does not fall back to CSQ'
Check ([NextConfig.LinkDiag]::SignalText([NextConfig.LinkDiag]::Parse((V4 $online 90 255 101))) -eq "RSRP −90 дБм (средне),`r`nRSRQ нет данных") 'independent unknown RSRQ'
Check ([NextConfig.LinkDiag]::SignalText([NextConfig.LinkDiag]::Parse((V4 $online 255 9 61))) -eq "RSRP нет данных,`r`nRSRQ −9 дБ (хорошо)") 'independent unknown RSRP'
foreach ($raw in 0,54,61,250,255) {
    $snapshot = [NextConfig.LinkDiag]::Parse((V4 $online 93 9 $raw))
    Check ($snapshot.Sinr -eq $raw -and [NextConfig.LinkDiag]::SignalText($snapshot) -notmatch 'SINR') "raw SINR $raw preserved but hidden"
}
foreach ($offset in 91..95) { $b=$v4.Clone(); $b[$offset]=$b[$offset] -bxor 1; Reject $b "v4 corrupt tail $offset" }
foreach ($version in 2,3,5) { $b=$v4.Clone(); $b[9]=$version; Reject (Seal $b) "v4 length with version $version" }
$b=$online.Clone(); $b[9]=4; Reject (Seal $b) 'v3 length with v4 version'
Reject ([byte[]]$v4[0..94]) 'truncated v4'
Reject (V4 $online 105 10 251) 'invalid raw SINR'
$s = [NextConfig.LinkDiag]::Parse($online)
Check ($s.State -eq 7 -and $s.Channel -eq 0 -and $s.Csq -eq 26 -and $s.Network -eq 4 -and $s.Probe -eq 1) 'online fields'
Check ($s.Plmn -eq '25001' -and $s.Failures.Count -eq 0 -and $s.RetryMilliseconds -eq 0) 'online operator and empty history'
Check (($s.StageMilliseconds -join ',') -eq '1200,2700,600,4200,1500,800') 'all six little-endian durations'
$s = [NextConfig.LinkDiag]::Parse($sim)
Check ($s.State -eq 1 -and $s.Csq -eq 99 -and $s.Network -eq 0 -and $s.Probe -eq 0 -and $s.Plmn -eq '') 'SIM unavailable fields'
Check ($s.Failures[0].Stage -eq 2 -and $s.Failures[0].Reason -eq 4 -and $s.Failures[0].Seconds -eq 4) 'SIM failure bits'
$s = [NextConfig.LinkDiag]::Parse($waiting)
Check ($s.State -eq 6 -and $s.RetryMilliseconds -eq 5500 -and $s.Probe -eq 2) 'wait countdown and successful DNS'
$s = [NextConfig.LinkDiag]::Parse($ring)
Check ($s.Failures.Count -eq 10) 'full ring'
Check (($s.Failures.Seconds -join ',') -eq '70,60,50,40,30,20,12,4,110,98') 'newest first across restart'
for ($i = 0; $i -lt 10; $i++) {
    Check ($s.Failures[$i].Stage -eq ((9 - $i) % 6) -and $s.Failures[$i].Reason -eq 10 - $i) "ring entry $i bits"
}
foreach ($plmn in @('00101', '001001', '25077')) {
    $b = $online.Clone()
    [Array]::Clear($b, 15, 7)
    [Text.Encoding]::ASCII.GetBytes($plmn).CopyTo($b, 15)
    $s = [NextConfig.LinkDiag]::Parse((Seal $b))
    Check ($s.Plmn -ceq $plmn -and [NextConfig.LinkDiag]::OperatorText($s.Plmn) -ceq $plmn) "PLMN preserved: $plmn"
}
foreach ($offset in @(0, 4, 6, 8, 9, 10, 22, 50, 90, 91, 92)) {
    $b = $online.Clone()
    $b[$offset] = $b[$offset] -bxor 1
    Reject $b "corrupt byte $offset"
}
Reject $null 'null response'
Reject ([byte[]]$online[0..91]) 'short response'
Reject ([byte[]]($online + 0)) 'long response'
foreach ($field in @(@(4,84), @(6,255), @(8,111), @(9,2), @(10,8), @(12,32), @(13,2), @(14,4), @(50,11))) {
    $b = $online.Clone()
    $b[$field[0]] = $field[1]
    Reject (Seal $b) "invalid field $($field[0]) with valid CRC"
}
$b = $online.Clone()
[Text.Encoding]::ASCII.GetBytes('0010010').CopyTo($b, 15)
Reject (Seal $b) 'PLMN without NUL'
$b = $online.Clone()
$b[16] = 65
Reject (Seal $b) 'non-digit PLMN'
$b = $online.Clone()
[BitConverter]::GetBytes([uint32]::MaxValue).CopyTo($b, 26)
Check ([NextConfig.LinkDiag]::Parse((Seal $b)).StageMilliseconds[0] -eq [uint32]::MaxValue) 'unsigned stage time'
Check ([NextConfig.LinkDiag]::DurationText(1200) -eq '1,2 с') 'one decimal in seconds'
Check ([NextConfig.LinkDiag]::DurationText(0) -eq '—') 'zero duration'
$request = [NextConfig.SldnStream]::Command(112, $null, 42)
Check ([NextConfig.SldnStream]::MatchesReply($request, $online)) 'reply UID matches'
$b = $online.Clone()
$b[7] = 41
Check (-not [NextConfig.SldnStream]::MatchesReply($request, (Seal $b))) 'late UID rejected'

# Конструктор clOpros сканирует реальные порты, поэтому его не вызываем.
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$poller = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$demoConfig = $poller.GetType().GetMethod('DemoConfig', $flags).Invoke($poller, @())
Check ($demoConfig.rawConfig.Length -eq 512 -and $demoConfig.simNumber -eq '+79000000000') 'demo settings fit the wire format'
$cancel = New-Object Threading.CancellationTokenSource
$cancel.Cancel()
$rejected = $false
try { $poller.GetLinkDiag($cancel.Token) | Out-Null } catch { $rejected = $true }
Check $rejected 'cancelled query cannot touch missing transport'
$cancel.Dispose()
$form = New-Object NextConfig.Form2($poller, 'offline', '000000000000000', 0)
function Field($name) { return $form.GetType().GetField($name, $flags).GetValue($form) }
function Display($frame) { $form.GetType().GetMethod('DisplayLink', $flags).Invoke($form, @([NextConfig.LinkDiag]::Parse($frame))) | Out-Null }
try {
    Check ((Field 'linkTimer').Interval -eq 2000 -and -not (Field 'linkTimer').Enabled) 'hidden screen timer stopped'
    Display $online
    Check ((Field 'linkState').Text.StartsWith('Связь установлена') -and (Field 'linkDot').ForeColor -eq [NextConfig.Theme]::Ok) 'online phrase and green dot'
    Check ((Field 'linkSignal').Value -eq 26) 'CSQ meter'
    Display $v4
    Check ((Field 'linkValues')[1].Text -match 'RSRP −105 дБм \(плохо\)' -and (Field 'linkValues')[1].Text -match 'RSRQ −10 дБ \(средне\)' -and (Field 'linkValues')[1].Text -notmatch 'SINR' -and -not (Field 'linkSignal').Visible -and (Field 'linkSignal').Value -eq 0) 'LTE screen hides SINR and CSQ meter'
    $gsm = V4 $online 255 255 255; $gsm[13]=1; $gsm=Seal $gsm
    Display $gsm
    Check ((Field 'linkValues')[1].Text -eq 'CSQ 26' -and (Field 'linkSignal').Value -eq 26) '2G retains CSQ'
    Display $online
    Check ((Field 'linkValues')[1].Text -eq 'CSQ 26') 'v3 LTE retains CSQ'
    Display $sim
    Check ((Field 'linkValues')[1].Text -eq 'нет данных' -and (Field 'linkSignal').Value -eq 0) 'unknown signal never fills meter'
    Check ((Field 'linkDot').ForeColor -eq [NextConfig.Theme]::Danger) 'error red dot'
    Display $waiting
    Check ((Field 'linkCountdown').Text -match '6 с$' -and (Field 'linkDot').ForeColor -eq [NextConfig.Theme]::Warning) 'wait yellow dot and rounded countdown'
    Start-Sleep -Milliseconds 1100
    $form.GetType().GetMethod('UpdateLinkCountdown', $flags).Invoke($form, @()) | Out-Null
    Check ((Field 'linkCountdown').Text -match '5 с$') 'countdown advances between polls'
    Display $ring
    Check ((Field 'linkFailures').Rows.Count -eq 10 -and (Field 'linkFailures').Rows[0].Cells[2].Value -eq '70 с') 'all failures displayed newest first'
    $row = (Field 'linkFailures').Rows[0]
    Display $ring
    Check ([Object]::ReferenceEquals($row, (Field 'linkFailures').Rows[0])) 'unchanged history is not recreated on every poll'
    $form.GetType().GetMethod('DisplayLink', $flags).Invoke($form, @($null)) | Out-Null
    Check ((Field 'linkState').Text -eq 'нет ответа от платы' -and (Field 'linkCountdown').Text -eq '') 'no response clears countdown'
    Check ((Field 'linkFailures').Rows.Count -eq 0 -and (Field 'linkValues')[3].Text -eq '—') 'no stale operator or failures'
    $form.GetType().GetField('bootOffered', $flags).SetValue($form, $true)
    $handle = $form.Handle
    $status = New-Object 'NextConfig.clOpros+s_status'
    $status.signal = 99
    $status.gsm = 1
    $status.gprs = 1
    $status.connectToServer = 1
    $post = $form.GetType().GetMethod('opros_onStatus', $flags)
    1..100 | ForEach-Object { $post.Invoke($form, @($status)) | Out-Null }
    Check ((Field 'statusPosted') -eq 1) 'status burst has one pending UI update'
    [Windows.Forms.Application]::DoEvents()
    Check ((Field 'statusPosted') -eq 0) 'status queue released after UI update'
} finally { $form.Dispose() }
$legacyReader = New-Object NextConfig.OldFirmware($null, [NextConfig.Demo]::OldFirmwareReply(0, $null), $true)
$legacy = New-Object NextConfig.frmOldFirmware($legacyReader, 'OFFLINE')
try {
    $pages = $legacy.GetType().GetField('tabs', $flags).GetValue($legacy).TabPages
    Check (@($pages | Where-Object Text -eq 'Связь').Count -eq 0) 'legacy board has no link section'
} finally { $legacy.Dispose() }
Write-Output "PASS: $script:checks checks (offline, no COM)"

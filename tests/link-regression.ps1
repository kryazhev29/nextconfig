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
    foreach ($b in $frame[4..90]) {
        $crc = $crc -bxor $b
        for ($i = 0; $i -lt 8; $i++) {
            if ($crc -band 1) { $crc = ($crc -shr 1) -bxor 40961 } else { $crc = $crc -shr 1 }
        }
    }
    $frame[91] = $crc -band 255
    $frame[92] = $crc -shr 8
    return ,$frame
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

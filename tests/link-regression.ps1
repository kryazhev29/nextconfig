param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe", [string]$SnapshotDirectory = '')
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
function Signal($frame, [byte]$csq = 20) {
    $snapshot = if ($null -ne $frame) { [NextConfig.LinkDiag]::Parse($frame) } else { $null }
    return [NextConfig.LinkDiag]::SignalView($snapshot, $csq)
}
$view = Signal $v4
Check ($view.LevelText -eq 'Плохо · −105 дБм' -and $view.QualityText -eq 'Средне · −10 дБ') 'LTE units and quality'
foreach ($case in @(@(89,9,'Хорошо','Хорошо'), @(90,10,'Средне','Средне'), @(100,15,'Средне','Средне'), @(101,16,'Плохо','Плохо'), @(93,9,'Средне','Хорошо'), @(100,10,'Средне','Средне'))) {
    $view = Signal (V4 $online $case[0] $case[1] 54)
    Check ($view.LevelText -eq "$($case[2]) · −$($case[0]) дБм" -and $view.QualityText -eq "$($case[3]) · −$($case[1]) дБ") "LTE thresholds $($case[0])/$($case[1])"
}
$gsm = V4 $online 100 9; $gsm[13] = 1; $gsm = Seal $gsm
foreach ($frame in @($online, $null)) {
    $view = Signal $frame
    Check ($view.LevelText -eq '20 из 31' -and $view.QualityText -eq '' -and $view.Maximum -eq 31 -and $view.Value -eq 20 -and -not $view.Gsm) 'v3/no 112 use status CSQ'
}
# 2G и 3G: уровень RSSI в дБм из CSQ (TS 27.007 §8.5), оценки — прежние границы CSQ 10 и 20.
$view = Signal $gsm
Check ($view.Gsm -and -not $view.Lte -and $view.LevelText -eq 'Хорошо · −73 дБм' -and $view.QualityText -eq '' -and $view.Maximum -eq 31 -and $view.Value -eq 20 -and $view.Bars -eq 5) '2G level in dBm'
foreach ($case in @(@(31,51,'Хорошо',5), @(20,73,'Хорошо',5), @(19,75,'Средне',4), @(10,93,'Средне',3), @(9,95,'Плохо',2), @(0,113,'Плохо',1))) {
    $view = [NextConfig.LinkDiag]::SignalView($null, [byte]$case[0], [byte]1)
    Check ($view.Gsm -and [NextConfig.LinkDiag]::GsmLevel([byte]$case[0]) -eq $case[1] -and $view.LevelText -eq "$($case[2]) · −$($case[1]) дБм" -and $view.Bars -eq $case[3]) "2G CSQ $($case[0])"
}
$view = [NextConfig.LinkDiag]::SignalView($null, [byte]99, [byte]3)
Check ($view.Gsm -and $view.LevelText -eq 'Нет данных' -and $view.Grade -eq -1 -and $view.Bars -eq 0 -and [NextConfig.LinkDiag]::GsmLevel(99) -eq 255) '3G unknown CSQ keeps the dBm view'
Check (-not ([NextConfig.LinkDiag]::SignalView($null, [byte]20, [byte]4)).Gsm) '4G without 112 stays on CSQ'
# Плата №1, 13.10: пропуск одного замера в 4G не переключает экран на CSQ (было мигание).
foreach ($frame in @((V4 $online 255 255 255), (V4 $online 255 9 61))) {
    $view = Signal $frame
    Check ($view.Lte -and $view.LevelText -eq 'Нет данных' -and $view.Bars -eq 0 -and $view.Value -eq 0 -and $view.Grade -eq -1) 'LTE with unknown RSRP keeps LTE view'
}
$view = Signal (V4 $online 90 255 101)
Check ($view.LevelText -eq 'Средне · −90 дБм' -and $view.QualityText -eq 'Нет данных') 'independent unknown RSRQ'
foreach ($case in @(@(130,0), @(120,0), @(119,1), @(100,20), @(90,30), @(70,50), @(60,50))) {
    $view = Signal (V4 $online $case[0] 9)
    Check ($view.Maximum -eq 50 -and $view.Value -eq $case[1]) "RSRP meter $($case[0])"
}
foreach ($csq in 0,31,99,255) {
    $view = Signal $null $csq
    Check ($view.QualityText -eq '' -and $view.Value -eq $(if ($csq -le 31) {$csq} else {0}) -and $view.LevelText -eq $(if ($csq -le 31) {"$csq из 31"} else {'Не определён'})) "CSQ fallback $csq"
}
foreach ($raw in 0,54,61,250,255) {
    $snapshot = [NextConfig.LinkDiag]::Parse((V4 $online 93 9 $raw))
    $view = [NextConfig.LinkDiag]::SignalView($snapshot, 20)
    Check ($snapshot.Sinr -eq $raw -and $view.LevelText -eq 'Средне · −93 дБм' -and $view.QualityText -eq 'Хорошо · −9 дБ') "raw SINR $raw preserved but hidden"
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
function Field($name) {
    $field = $form.GetType().GetField($name, $flags)
    if ($field) { return $field.GetValue($form) }
    return $form.GetType().GetProperty($name, $flags).GetValue($form, $null)
}
function Display($frame) {
    $snapshot = if ($frame) { [NextConfig.LinkDiag]::Parse($frame) } else { $null }
    $form.GetType().GetMethod('DisplayLink', $flags).Invoke($form, @($snapshot)) | Out-Null
}
try {
    Check ((Field 'linkTimer').Interval -eq 5000 -and -not (Field 'linkTimer').Enabled) 'hidden screen timer stopped'
    Display $online
    Check ((Field 'linkState').Text.StartsWith('На связи') -and (Field 'linkBanner').ForeColor -eq [NextConfig.Theme]::Ok) 'online phrase and green banner'
    Check ((Field 'linkSignal').Value -eq 26) 'CSQ meter'
    Display $v4
    Check ((Field 'linkRsrp').Raw -eq 105 -and (Field 'linkRsrq').Raw -eq 10) 'LTE gauges use RSRP and RSRQ'
    Display $gsm
    Check ((Field 'linkRsrp').Gsm -and (Field 'linkRsrp').Raw -eq 61 -and (Field 'linkRsrq').Gsm -and (Field 'linkRsrq').Raw -eq 255 -and (Field 'linkRsrq').Network -eq '2G') '2G gauges: RSSI from CSQ, quality not measured'
    Display $sim
    Check ((Field 'linkCsq').Text -eq 'Не определён' -and (Field 'linkSignal').Value -eq 0) 'unknown CSQ is empty'
    Check ((Field 'linkBanner').ForeColor -eq [NextConfig.Theme]::Danger) 'error red banner'
    Display $waiting
    Check ((Field 'linkCountdown').Text -match '6 с$') 'rounded wait countdown'
    Start-Sleep -Milliseconds 1100
    $form.GetType().GetMethod('UpdateLinkCountdown', $flags).Invoke($form, @()) | Out-Null
    Check ((Field 'linkCountdown').Text -match '5 с$') 'countdown advances'
    Display $ring
    Check ((Field 'linkFailures').Rows.Count -eq 10 -and (Field 'linkFailures').Rows[0].Cells[2].Value -eq '70 с') 'history newest first'
    $row = (Field 'linkFailures').Rows[0]
    Display $ring
    Check ([Object]::ReferenceEquals($row, (Field 'linkFailures').Rows[0])) 'unchanged failures are not recreated'
    Display $null
    Check ((Field 'linkState').Text.StartsWith('Нет ответа') -and (Field 'linkCountdown').Text -eq '' -and (Field 'linkFailures').Rows.Count -eq 0 -and (Field 'linkCell').Text -eq 'Нет данных') 'timeout clears stale diagnostics'
    $form.GetType().GetField('bootOffered', $flags).SetValue($form, $true)
    $handle = $form.Handle
    $status = New-Object 'NextConfig.clOpros+s_status'
    $status.signal = 20; $status.gsm = 1; $status.gprs = 1; $status.connectToServer = 1
    $status.netType = 4; $status.moduleType = 2; $status.hw_version = 1; $status.fw_version = 50; $status.boot_version = 36
    $status.usedMem = 3151
    $post = $form.GetType().GetMethod('opros_onStatus', $flags)
    1..100 | ForEach-Object { $post.Invoke($form, @($status)) | Out-Null }
    Check ((Field 'statusPosted') -eq 1) 'status burst has one pending UI update'
    [Windows.Forms.Application]::DoEvents()
    Check ((Field 'statusPosted') -eq 0) 'status queue released'
    # Load удалён, рабочий опрос заблокирован: только синтетические кадры, без COM.
    $load = [Delegate]::CreateDelegate([EventHandler], $form, $form.GetType().GetMethod('Form2_Load', $flags))
    $form.remove_Load($load)
    $form.GetType().GetField('linkLoading', $flags).SetValue($form, $true)
    $form.ShowInTaskbar = $false
    $form.Show()
    [Windows.Forms.Application]::DoEvents()
    Check ((Field 'linkTimer').Enabled -and (Field 'linkTimer').Interval -eq 5000) 'Device polls every 5 seconds'
    function Status { $form.GetType().GetMethod('refOnStatus', $flags).Invoke($form, @($status)) | Out-Null }
    function Snapshot($name) {
        if (-not $SnapshotDirectory) { return }
        New-Item -ItemType Directory -Force -Path $SnapshotDirectory | Out-Null
        $form.PerformLayout(); [Windows.Forms.Application]::DoEvents()
        $bitmap = New-Object Drawing.Bitmap($form.Width, $form.Height)
        try {
            $form.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0, 0, $form.Width, $form.Height)))
            $bitmap.Save((Join-Path $SnapshotDirectory $name), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    }
    Status
    $lte = V4 $online 100 9
    Display $lte
    $overview = Field 'deviceConnection'
    Check ($overview.Signal.LevelText -eq 'Средне · −100 дБм' -and $overview.Signal.QualityText -eq 'Хорошо · −9 дБ' -and $overview.Signal.Lte) 'Device LTE v4 text and quality'
    Check ($overview.Signal.Maximum -eq 50 -and $overview.Signal.Value -eq 20 -and $overview.Steps -eq 4) 'Device LTE scale and connection chain'
    Check ((((Field 'deviceMemory').Text -replace [char]0xA0, ' ') -eq '3 151 из 50 000 · 6 %') -and (Field 'deviceMemoryMeter').Value -eq 3151) 'ID memory moved to the device card'
    Status
    Check ($overview.Signal.Lte) 'status 101 cannot overwrite LTE with CSQ'
    Snapshot 'device_lte.png'
    (Field 'tabs').SelectedTab = Field 'tabLink'
    [Windows.Forms.Application]::DoEvents()
    Check ((Field 'linkTimer').Enabled -and (Field 'linkTimer').Interval -eq 2000) 'Link keeps 2 second poll'
    Check ((Field 'linkGauges').Visible -and (Field 'linkPlotCard').Visible -and -not (Field 'linkCsqRow').Visible) 'LTE shows gauges and RSRP graph'
    Check ([Object]::ReferenceEquals((Field 'signalHistory'), (Field 'linkPlot').History)) 'Link graph keeps the polled history'
    $form.Height = 1050
    Snapshot 'link_lte.png'
    (Field 'tabs').SelectedTab = Field 'tabIds'
    [Windows.Forms.Application]::DoEvents()
    Check (-not (Field 'linkTimer').Enabled) 'other tabs stop diagnostic poll'
    (Field 'tabs').SelectedTab = Field 'tabState'
    [Windows.Forms.Application]::DoEvents()
    foreach ($frame in @($online, $null)) {
        Display $frame
        Check (-not $overview.Signal.Lte -and $overview.Signal.LevelText -eq '20 из 31' -and $overview.Signal.Maximum -eq 31) 'v3/timeout silently use status CSQ'
    }
    Display (V4 $online 255 9)
    Check ($overview.Signal.Lte -and $overview.Details -eq 'уровень нет данных · качество −9 дБ') 'unknown RSRP keeps LTE card'
    Display (V4 $online 100 9); Display (V4 $online 255 255)
    Check ($overview.Details -eq 'уровень −100 дБм · качество −9 дБ') 'single missed LTE sample holds last values'
    Display $lte
    $status.netType = 1
    Status
    Check (-not $overview.Signal.Lte) '2G status immediately discards cached LTE signal'
    Display $gsm
    Check ($overview.Details -eq 'уровень −73 дБм' -and $overview.Signal.Gsm -and $overview.Signal.Bars -eq 5) 'Device 2G in dBm'
    Snapshot 'device_2g.png'
    (Field 'tabs').SelectedTab = Field 'tabLink'
    [Windows.Forms.Application]::DoEvents()
    Display $gsm
    Check ((Field 'linkGauges').Visible -and (Field 'linkPlotCard').Visible -and -not (Field 'linkCsqRow').Visible -and (Field 'signalHistory').Gsm -and (Field 'signalHistory').Points.Count -gt 0) '2G shows the same gauges and an RSSI graph'
    Snapshot 'link_2g.png'
    (Field 'tabs').SelectedTab = Field 'tabState'
    [Windows.Forms.Application]::DoEvents()
    $status.netType = 4
    Status; Display $lte
    $form.GetType().GetField('lastStatus', $flags).SetValue($form, [DateTime]::Now.AddSeconds(-10))
    $form.GetType().GetMethod('TmrTick', $flags).Invoke($form, @()) | Out-Null
    Check ($overview.Steps -eq -1 -and $overview.Signal.Bars -eq 0 -and $overview.Problem -eq 'Нет свежих данных') 'stale status clears chain and signal'
} finally { $form.Dispose() }
$legacyReader = New-Object NextConfig.OldFirmware($null, [NextConfig.Demo]::OldFirmwareReply(0, $null), $true)
$legacy = New-Object NextConfig.frmOldFirmware($legacyReader, 'OFFLINE')
try {
    $pages = $legacy.GetType().GetField('tabs', $flags).GetValue($legacy).TabPages
    Check (@($pages | Where-Object Text -eq 'Связь').Count -eq 0) 'legacy board has no link section'
} finally { $legacy.Dispose() }
Write-Output "PASS: $script:checks checks (offline, no COM)"

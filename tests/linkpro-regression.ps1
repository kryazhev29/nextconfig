param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe", [string]$SnapshotDirectory = '')
$ErrorActionPreference = 'Stop'
$linkproSnapshots = $SnapshotDirectory
. "$PSScriptRoot\link-regression.ps1" -AssemblyPath $AssemblyPath
$SnapshotDirectory = $linkproSnapshots

function V5([byte]$rsrp = 100, [byte]$rsrq = 9, [byte]$band = 7, [byte]$width = 5) {
    $frame = New-Object byte[] 98
    [Array]::Copy((V4 $online $rsrp $rsrq), $frame, 94)
    $frame[4] = 90; $frame[9] = 5; $frame[94] = $band; $frame[95] = $width
    [Text.Encoding]::ASCII.GetBytes('25002').CopyTo($frame,15)
    return ,(Seal $frame)
}
$five = V5
$snapshot = [NextConfig.LinkDiag]::Parse($five)
Check ($snapshot.Version -eq 5 -and $snapshot.Rsrp -eq 100 -and $snapshot.Rsrq -eq 9 -and $snapshot.Band -eq 7 -and $snapshot.Bandwidth -eq 5 -and $snapshot.Plmn -eq '25002') 'v5 cell and old offsets'
foreach ($offset in 94..97) { $b=$five.Clone(); $b[$offset]=$b[$offset] -bxor 1; Reject $b "v5 corrupt tail $offset" }
Reject (V5 100 9 7 6) 'invalid bandwidth'
Reject ([byte[]]$five[0..96]) 'truncated v5'
foreach ($version in 3,4,6) { $b=$five.Clone(); $b[9]=$version; Reject (Seal $b) "v5 length/version $version" }
foreach ($case in @(@(80,5),@(81,4),@(89,4),@(90,4),@(91,3),@(100,3),@(101,2),@(110,2),@(111,1))) {
    $view=Signal (V5 $case[0] 9)
    Check ($view.Bars -eq $case[1]) "RSRP bars -$($case[0])"
}
foreach ($case in @(@(0,1),@(4,1),@(5,2),@(9,2),@(10,3),@(14,3),@(15,4),@(19,4),@(20,5),@(31,5),@(99,0),@(255,0))) {
    Check ((Signal $null $case[0]).Bars -eq $case[1]) "CSQ bars $($case[0])"
}
foreach ($case in @(@(89,9,'хороший'),@(90,10,'средний'),@(100,15,'средний'),@(101,16,'слабый'),@(80,16,'слабый'),@(100,9,'средний'),@(80,255,'хороший'))) {
    $text=[NextConfig.LinkDiag]::BannerText([NextConfig.LinkDiag]::Parse((V5 $case[0] $case[1])))
    Check ($text.StartsWith("На связи · сигнал $($case[2])")) "worst metric banner $($case[0])/$($case[1])"
}
foreach ($state in 0..7) {
    $b=$five.Clone(); $b[10]=$state
    $s=[NextConfig.LinkDiag]::Parse((Seal $b))
    $steps=[NextConfig.LinkDiag]::ConnectionSteps($s,1,1,1)
    $expected=@(3,0,1,2,3,3,3,4)[$state]
    Check ($steps -eq $expected) "break step state $state"
    $problem=[NextConfig.LinkDiag]::ConnectionProblem($s,$steps)
    Check (($state -eq 7 -and $problem -eq '') -or ($state -ne 7 -and $problem.Length -gt 10)) "action phrase state $state"
    if($state -ne 7) { Check ([NextConfig.LinkDiag]::BannerText($s) -eq [NextConfig.LinkDiag]::StateText($state)) "reason banner $state" }
}
foreach($case in @(@(0,0,0,1),@(1,0,0,2),@(1,1,0,3),@(1,1,1,4))) {
    Check ([NextConfig.LinkDiag]::ConnectionSteps($null,$case[0],$case[1],$case[2]) -eq $case[3]) '101-only chain'
}
Check ([NextConfig.LinkDiag]::ConnectionProblem($null,3) -eq 'Сервер не отвечает, интернет есть — проверьте адрес сервера в настройках') 'server break instruction'
foreach($case in @(@(1,'B1 2100'),@(3,'B3 1800'),@(7,'B7 2600'),@(8,'B8 900'),@(20,'B20 800'),@(31,'B31 450'),@(38,'B38 TDD 2600'),@(40,'B40 TDD 2300'),@(99,'B99'),@(255,''))) {
    Check ([NextConfig.LinkDiag]::BandText($case[0]) -eq $case[1]) "band $($case[0])"
}
foreach($case in @(@(0,'1,4'),@(1,'3'),@(2,'5'),@(3,'10'),@(4,'15'),@(5,'20'))) {
    Check ([NextConfig.LinkDiag]::BandwidthText($case[0]) -eq "полоса $($case[1]) МГц") "bandwidth $($case[0])"
}
Check ([NextConfig.LinkDiag]::BandwidthText(255) -eq '') 'unknown bandwidth omitted'
Check ([NextConfig.LinkDiag]::CellText($snapshot) -eq 'МегаФон · 4G · B7 2600') 'shared cell header'
Check (([NextConfig.LinkDiag]::CellChips($snapshot) -join '|') -eq 'МегаФон|4G · B7 2600|полоса 20 МГц|SIM-карта') 'cell chips'
Check ([NextConfig.LinkDiag]::BannerHeading($snapshot) -eq 'На связи · сигнал средний' -and [NextConfig.LinkDiag]::BannerDetail($snapshot) -eq 'В плохую погоду и при нагрузке связь может пропадать') 'short banner title and separate explanation'
Check ([NextConfig.LinkDrawing]::Amber.ToArgb() -eq [Drawing.ColorTranslator]::FromHtml('#F59E0B').ToArgb() -and [NextConfig.LinkDrawing]::AmberText.ToArgb() -eq [Drawing.ColorTranslator]::FromHtml('#92400E').ToArgb() -and [NextConfig.LinkDrawing]::AmberBack.ToArgb() -eq [Drawing.ColorTranslator]::FromHtml('#FEF3C7').ToArgb()) 'mock amber palette'
$demoFrame=$poller.GetType().GetMethod('DemoLinkReply',$flags).Invoke($poller,@([byte]37))
$demoSnapshot=[NextConfig.LinkDiag]::Parse([byte[]]$demoFrame)
Check ($demoSnapshot.Version -eq 5 -and $demoSnapshot.Band -eq 7 -and $demoSnapshot.Bandwidth -eq 5) 'demo emits v5 cell'
Check ([NextConfig.Demo]::SignalRsrp(0) -ne [NextConfig.Demo]::SignalRsrp(600)) 'demo level changes over ten minutes'
$history=New-Object NextConfig.LinkHistory
$history.Observe(0,100); $history.Observe(2,100)
Check ($history.Points.Count -eq 1) 'identical neighbours are not duplicated'
$history.Observe(4,90); $history.Observe(600,90)
Check ($history.Points.Count -eq 2) '10 minute inclusive boundary'
$history.Observe(601,90)
Check ($history.Points.Count -eq 2 -and $history.Points[0].Seconds -eq 1 -and $history.Points[0].EndSeconds -eq 2) 'interval intersecting ten minutes is clipped'
$history.Observe(603,90)
Check ($history.Points.Count -eq 1 -and $history.Points[0].Seconds -eq 4) 'expired interval removed'
$history.Observe(605,90); $history.Observe(606,255)
Check ($history.Points.Count -eq 1 -and $history.Points[0].Seconds -eq 6 -and $history.Points[0].EndSeconds -eq 605) 'constant signal preserves its recent interval; unknown is not plotted'
$history.Clear(); $history.Observe(0,100)
$plot=New-Object NextConfig.SignalPlot
$plot.History=$history; $plot.Width=500; $plot.Height=190; $plot.Seconds=0
$image=New-Object Drawing.Bitmap(500,190)
try {
    $plot.DrawToBitmap($image,(New-Object Drawing.Rectangle(0,0,500,190)))
    Check ($image.GetPixel(485,111).ToArgb() -eq [NextConfig.Theme]::Accent.ToArgb()) 'first sample renders immediately as a blue point'
    Check $plot.Collecting 'first sample has collecting caption'
    $history.Observe(600,100)
    Check (-not $plot.Collecting -and $history.Points.Count -eq 1 -and $history.Points[0].EndSeconds -eq 600) 'stable signal preserves ten minute history without duplicate samples'
} finally { $image.Dispose(); $plot.Dispose() }
$history.Clear()
Check ($history.Points.Count -eq 0) 'history clear'
$phases=New-Object NextConfig.ConnectionPhases
try {
    $phases.Width=1000
    Check ($phases.PhaseAt(0) -eq -1) 'zero stages do not divide by zero'
    $phases.Durations=[uint32[]]@(0,500,0,500,0,0)
    Check ($phases.PhaseAt(0) -eq 1 -and $phases.PhaseAt(499) -eq 1 -and $phases.PhaseAt(500) -eq 3 -and $phases.PhaseAt(999) -eq 3 -and $phases.PhaseAt(1000) -eq -1) 'phase proportions and zero segments'
} finally { $phases.Dispose() }

$form=New-Object NextConfig.Form2($poller,'offline','000000000000000',0)
try {
    $load=[Delegate]::CreateDelegate([EventHandler],$form,$form.GetType().GetMethod('Form2_Load',$flags))
    $form.remove_Load($load)
    $form.GetType().GetField('bootOffered',$flags).SetValue($form,$true)
    $form.GetType().GetField('linkLoading',$flags).SetValue($form,$true)
    $form.ShowInTaskbar=$false; $form.Show(); $form.Height=1050
    $status=New-Object 'NextConfig.clOpros+s_status'
    $status.gsm=1; $status.gprs=1; $status.connectToServer=1; $status.signal=23
    $status.netType=4; $status.moduleType=2; $status.hw_version=1; $status.fw_version=50; $status.boot_version=36; $status.usedMem=3151
    $status.time=[DateTimeOffset]::Now.ToUnixTimeSeconds(); $status.fw_build=$status.time
    $currentTime=$status.time
    $status.time=0; Status
    Check ((Field 'lblTime').Text -eq 'Время не задано' -and (Field 'lblTime').ForeColor -eq [NextConfig.Theme]::Danger) 'unset time is a red concise phrase'
    $status.time=$currentTime; Status
    Check ((Field 'lblTime').Text -ne 'Время не задано' -and (Field 'lblTime').ForeColor -eq [NextConfig.Theme]::TextMain) 'valid time restores normal color'
    $clock=(Field 'linkClock').Elapsed.TotalSeconds
    $history=Field 'signalHistory'
    foreach($case in @(@(80,9,'good'),@(100,9,'medium'),@(103,16,'poor'))) {
        $history.Clear(); $clock=(Field 'linkClock').Elapsed.TotalSeconds
        foreach($i in 0..299) { $history.Observe($clock-600+$i*2, [byte]($case[0]+[NextConfig.Demo]::SignalRsrp($i*2)-[NextConfig.Demo]::SignalRsrp(600))) }
        (Field 'tabs').SelectedTab=Field 'tabState'; Status
        Display (V5 $case[0] $case[1])
        $view=(Field 'deviceConnection').Signal
        Check ($view.Lte -and $view.Bars -gt 0) "Device LTE $($case[2])"
        Snapshot "device_lte_$($case[2]).png"
        (Field 'tabs').SelectedTab=Field 'tabLink'
        [Windows.Forms.Application]::DoEvents()
        Check ((Field 'linkGauges').Visible -and (Field 'linkPlot').Version -eq 5 -and (Field 'linkCell').Text -match 'B7 2600' -and (Field 'linkCell').Text -match 'полоса 20 МГц') "Link LTE $($case[2])"
        Snapshot "link_lte_$($case[2]).png"
    }
    (Field 'tabs').SelectedTab=Field 'tabState'
    $status.netType=1; Status; Display $gsm
    Snapshot 'device_2g.png'
    (Field 'tabs').SelectedTab=Field 'tabLink'
    [Windows.Forms.Application]::DoEvents()
    Check (-not (Field 'linkGauges').Visible -and -not (Field 'linkPlotCard').Visible -and (Field 'linkCsqRow').Visible) '2G has CSQ and no graph'
    Snapshot 'link_2g.png'
    $status.netType=4; $status.connectToServer=0
    (Field 'tabs').SelectedTab=Field 'tabState'; Status
    $b=V5; $b[10]=4; Display (Seal $b)
    Check ((Field 'deviceConnection').Steps -eq 3 -and (Field 'deviceConnection').Problem -match 'Сервер не отвечает') 'Device server break'
    Snapshot 'device_server_down.png'
    (Field 'tabs').SelectedTab=Field 'tabLink'; [Windows.Forms.Application]::DoEvents()
    Check ((Field 'linkBanner').ForeColor -eq [NextConfig.Theme]::Danger) 'no connection banner red'
    Snapshot 'link_no_connection.png'
    (Field 'tabs').SelectedTab=Field 'tabState'
    $status.gsm=0; $status.gprs=0; $status.signal=99; $status.netType=0; Status; Display $sim
    Check ((Field 'deviceConnection').Steps -eq 0 -and (Field 'deviceConnection').Signal.Bars -eq 0) 'no SIM break and empty signal'
    Snapshot 'device_no_sim.png'
    $click=New-Object Windows.Forms.MouseEventArgs([Windows.Forms.MouseButtons]::Left,1,10,([NextConfig.Theme]::Px(170)),0)
    (Field 'deviceConnection').GetType().GetMethod('OnMouseClick',$flags).Invoke((Field 'deviceConnection'),[object[]]@($click.PSObject.BaseObject)) | Out-Null
    Check ((Field 'tabs').SelectedTab -eq (Field 'tabLink')) 'More opens Link'
    Display $ring
    $form.GetType().GetMethod('ToggleLinkFailures',$flags).Invoke($form,@()) | Out-Null
    Check ((Field 'linkFailures').Visible -and (Field 'linkFailures').Rows.Count -eq 10) 'failures expand'
    $form.GetType().GetMethod('ToggleLinkFailures',$flags).Invoke($form,@()) | Out-Null
    Check (-not (Field 'linkFailures').Visible) 'failures collapse'
} finally { $form.Dispose() }
Write-Output "PASS: $script:checks linkpro checks (offline, no COM)"

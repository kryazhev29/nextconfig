param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe",[string]$SnapshotDirectory='')
# Карточка «Связь» на два канала (22.10.2026): итог, роли Wi-Fi и сотовой, цепочка активного канала,
# событие переключения. Ответ 113 — как в прошивке task/wifi-one (User/c/user_commands.c). Без COM.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
function View($wifi,$gsm=1,$internet=1,$server=1,$net=4,$snapshot=$null){
    [NextConfig.LinkChannels]::View([byte[]]$wifi,$snapshot,[byte]$gsm,[byte]$internet,[byte]$server,[byte]$net)
}
$cell=New-Object NextConfig.LinkSnapshot
$cell.Version=5; $cell.State=7; $cell.Network=4; $cell.Plmn='25002'; $cell.Band=7; $cell.Rsrp=95; $cell.Rsrq=9

$v=[NextConfig.LinkChannels]::Stale()
Check ($v.Headline -eq 'Нет свежих данных' -and $v.Header -eq 'Сервер: нет данных' -and $v.Steps -eq -1 -and $v.Rows.Length -eq 0) 'stale card'

# Нет ответа 113 (прошивка без Wi-Fi) и Wi-Fi-модем не выбран — один канал, карточка как раньше.
foreach($wifi in @($null,[byte[]](0,0,1,0,0,4))){
    $v=View $wifi 1 1 1 4 $cell
    Check ($v.Active -eq [NextConfig.eLinkChannel]::Cellular -and $v.Rows.Length -eq 0 -and $v.Policy -eq '') 'single cellular channel'
    Check ($v.Headline -eq 'На связи через 4G' -and $v.Header -eq 'Сервер через 4G' -and $v.Note -eq '') 'cellular headline'
    Check (($v.Labels -join '|') -eq 'SIM|Сеть 4G|Интернет|Сервер' -and $v.Steps -eq 4 -and $v.Problem -eq '') 'cellular chain'
}
Check (-not (View $null).Known -and (View ([byte[]](0,0,1,0,0,4))).Known) 'switch events only after 113 answered'
$v=View ([byte[]](0,0,1,0,0,4)) 1 1 0 1
Check ($v.Headline -eq 'Нет связи с сервером' -and $v.Note -eq '' -and $v.Header -eq 'Сервер не на связи' -and $v.Labels[1] -eq 'Сеть 2G') 'one channel offline has no attempt note'

# Сервер через Wi-Fi, сотовая в резерве.
$v=View ([byte[]](1,2,1,1,1,1)) 1 1 1 4 $cell
Check ($v.Active -eq [NextConfig.eLinkChannel]::Wifi -and $v.Headline -eq 'На связи через Wi-Fi' -and $v.Header -eq 'Сервер через Wi-Fi') 'Wi-Fi headline'
Check ($v.Policy -eq 'Логика: GSM, если нет Wi-Fi' -and ($v.Labels -join '|') -eq 'Мост|Сеть Wi-Fi|Сервер' -and $v.Steps -eq 3 -and $v.Problem -eq '') 'Wi-Fi chain'
Check ($v.Rows[0].Name -eq 'Wi-Fi · USR-DR164' -and $v.Rows[0].Role -eq 'Активный' -and $v.Rows[0].Active -and $v.Rows[0].State -eq 'соединение с сервером открыто' -and $v.Rows[0].Tone -eq [NextConfig.eLinkTone]::Good) 'Wi-Fi row active'
Check ($v.Rows[1].Name -eq 'Сотовая · МегаФон · 4G' -and $v.Rows[1].Role -eq 'Резерв' -and -not $v.Rows[1].Active -and $v.Rows[1].State -eq 'сеть есть') 'cellular row reserve'
# Главная ложная тревога старой карточки: модем без интернета (112 «Нет интернета», gprs 0), а сервер идёт через мост.
$noPdp=New-Object NextConfig.LinkSnapshot; $noPdp.Version=5; $noPdp.State=3; $noPdp.Network=4
$v=View ([byte[]](1,2,1,1,2,1)) 1 0 1 4 $noPdp
Check ($v.Steps -eq 3 -and $v.Problem -eq '' -and $v.HeadTone -eq [NextConfig.eLinkTone]::Good) 'modem without PDP is no alarm while Wi-Fi carries the server'
Check ($v.Rows[1].Role -eq 'Звонки и SMS' -and $v.Rows[1].Tone -eq [NextConfig.eLinkTone]::Good) 'only Wi-Fi: cellular keeps calls'
$v=View ([byte[]](1,2,1,1,2,1)) 0 0 1 0
Check ($v.Rows[1].State -eq 'нет сети — звонки и SMS недоступны' -and $v.Rows[1].Tone -eq [NextConfig.eLinkTone]::Bad -and $v.Rows[1].Name -eq 'Сотовая') 'only Wi-Fi without network: calls lost is red'
$v=View ([byte[]](1,2,1,1,1,1)) 0 0 1 0
Check ($v.Rows[1].State -eq 'нет сети — резерв недоступен' -and $v.Rows[1].Tone -eq [NextConfig.eLinkTone]::Warn) 'reserve without network is amber'

# Обрыв на канале Wi-Fi: место в цепочке и подсказка только про мост.
foreach($case in @(@(0,0,'Мост не отвечает'),@(1,1,'Мост отвечает, соединения нет'),@(3,1,'Мост отвечает, соединения нет'),@(2,2,'Соединение открыто, сервер не отвечает'))){
    $v=View ([byte[]](1,$case[0],1,1,1,1)) 1 1 0 4 $cell
    Check ($v.Steps -eq $case[1] -and $v.Problem.StartsWith($case[2])) "Wi-Fi break at $($case[1]) for link $($case[0])"
    Check ($v.Headline -eq 'Нет связи с сервером' -and $v.Note -eq 'попытка через Wi-Fi' -and $v.HeadTone -eq [NextConfig.eLinkTone]::Bad) 'offline headline names the attempt'
}
Check ((View ([byte[]](1,1,1,1,1,1)) 1 1 0).Rows[0].Tone -eq [NextConfig.eLinkTone]::Warn) 'active bridge without TCP is amber'

# Основной Wi-Fi упал, сервер ушёл на сотовую.
$v=View ([byte[]](0,0,1,1,1,4)) 1 1 1 4 $cell
Check ($v.Active -eq [NextConfig.eLinkChannel]::Cellular -and $v.Headline -eq 'На связи через 4G' -and $v.Labels[0] -eq 'SIM') 'fallback to cellular'
Check ($v.Rows[0].Role -eq 'Основной' -and $v.Rows[0].State -eq 'мост не отвечает' -and $v.Rows[0].Tone -eq [NextConfig.eLinkTone]::Bad -and -not $v.Rows[0].Active) 'primary Wi-Fi down is red'
Check ($v.Rows[1].Role -eq 'Активный' -and $v.Rows[1].Active -and $v.Rows[1].State -eq 'сервер на связи') 'cellular row active'
$v=View ([byte[]](0,3,1,1,1,4)) 1 1 1 4
Check ($v.Rows[0].State -eq 'соединение выключено, ожидание повтора' -and $v.Rows[0].Tone -eq [NextConfig.eLinkTone]::Warn) 'primary waits for retry'
$v=View ([byte[]](0,0,1,1,1,4)) 1 1 0 4 $null
Check ($v.Rows[1].State -eq 'сервер не отвечает' -and $v.Rows[1].Tone -eq [NextConfig.eLinkTone]::Bad -and $v.Note -eq 'попытка через 4G') 'active cellular break in row'
# «Только GSM» при выбранном модеме: мост не используется и не тревожит.
$v=View ([byte[]](0,3,1,1,0,4)) 1 1 1 4
Check ($v.Policy -eq 'Логика: только GSM' -and $v.Rows[0].Role -eq 'Не используется' -and $v.Rows[0].State -eq 'соединение выключено' -and $v.Rows[0].Tone -eq [NextConfig.eLinkTone]::Muted) 'Wi-Fi not used is grey'
Check ((View ([byte[]](0,0,1,1,0,4))).Rows[0].Tone -eq [NextConfig.eLinkTone]::Muted) 'unused silent bridge is grey'
Check ((View ([byte[]](0,4,1,0,0,4))).Rows.Length -eq 0) 'deselected modem shows one channel'
Check ((View ([byte[]](0,4,1,1,0,4))).Rows[0].Tone -eq [NextConfig.eLinkTone]::Bad) 'bridge shutdown without ACK is red'
# Двухбайтовый ответ старой прошивки: роль по факту, логика неизвестна.
$v=View ([byte[]](1,2)) 1 1 1 4
Check ($v.Rows[0].Name -eq 'Wi-Fi-мост' -and $v.Policy -eq '' -and $v.Rows[1].Role -eq 'Резерв') 'legacy reply'
Check ((View ([byte[]](0,0)) 1 1 1 4).Rows.Length -eq 0) 'legacy cellular is single channel'

# Событие переключения.
$wifi=View ([byte[]](1,2,1,1,1,1)); $gsm4=View ([byte[]](0,0,1,1,1,4)) 1 1 1 4; $gsm2=View ([byte[]](0,0,1,1,1,2)) 1 1 1 1
Check ([NextConfig.LinkChannels]::SwitchText($wifi,$gsm4) -eq 'переключение с Wi-Fi на сотовую сеть 4G') 'switch to cellular text'
Check ([NextConfig.LinkChannels]::SwitchText($gsm2,$wifi) -eq 'переключение с сотовой сети на Wi-Fi') 'switch to Wi-Fi text'
Check ([NextConfig.LinkChannels]::SwitchText($gsm4,$gsm2) -eq '') '4G to 2G is not a channel switch'
Check ([NextConfig.LinkChannels]::SwitchText($null,$wifi) -eq '' -and [NextConfig.LinkChannels]::SwitchText((View $null),$wifi) -eq '') 'no event before first 113 answer'

# Отрисовка и высота: строки каналов и подсказка раздвигают карточку, «Подробнее» остаётся на шкале сигнала.
$card=New-Object NextConfig.LinkOverview
try {
    $card.Width=[NextConfig.Theme]::Px(410) # как в окне 1296 px при 125 %: карточка в половину страницы
    $card.Signal=[NextConfig.LinkDiag]::SignalView($cell,26)
    $card.Cell=[NextConfig.LinkDiag]::CellText($cell,4); $card.Details='уровень −95 дБм'; $card.MemoryUsed=1274
    $cases=[ordered]@{
        'link_cellular.png'=@((View ([byte[]](0,0,1,0,0,4)) 1 1 1 4 $cell),301)
        'link_wifi.png'=@((View ([byte[]](1,2,1,1,1,1)) 1 1 1 4 $cell),403)
        'link_fallback.png'=@((View ([byte[]](0,0,1,1,1,4)) 1 1 1 4 $cell),403)
        'link_wifi_down.png'=@((View ([byte[]](1,1,1,1,2,1)) 1 0 0 4 $cell),447)
    }
    foreach($name in $cases.Keys){
        $card.Channels=$cases[$name][0]
        Check ($card.DesignHeight -eq $cases[$name][1]) "$name design height $($card.DesignHeight)"
        $card.Height=[NextConfig.Theme]::ControlPx($card,$card.DesignHeight)
        Check ($card.SignalTop + [NextConfig.Theme]::ControlPx($card,80) -lt $card.Height) "$name signal panel inside"
        $bitmap=New-Object Drawing.Bitmap($card.Width,$card.Height)
        try {
            $card.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$card.Width,$card.Height)))
            if($SnapshotDirectory){ New-Item -ItemType Directory -Force -Path $SnapshotDirectory | Out-Null; $bitmap.Save((Join-Path $SnapshotDirectory $name),[Drawing.Imaging.ImageFormat]::Png) }
        } finally { $bitmap.Dispose() }
    }
} finally { $card.Dispose() }

# Окно устройства: шапка, событие в строке состояния, сброс при устаревшем статусе.
$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$form=New-Object NextConfig.Form2($poller,'COM9','000000000000000',0)
try {
    function Field($name){$p=$form.GetType().GetProperty($name,$flags);if($p){return $p.GetValue($form,$null)};return $form.GetType().GetField($name,$flags).GetValue($form)}
    function Reply([byte[]]$bytes){$form.GetType().GetMethod('WifiApplyReply',$flags).Invoke($form,@($bytes,$false))|Out-Null}
    Check ($null -eq $form.GetType().GetField('wifiChannel',$flags)) 'old small channel line removed'
    Check ((Field 'lblHeadLink').Text -eq 'Сервер: нет данных' -and (Field 'headVersions').Controls.Count -eq 5) 'header pill present before status'
    $status=New-Object 'NextConfig.clOpros+s_status'
    $status.gsm=1; $status.gprs=1; $status.connectToServer=1; $status.netType=4
    $form.GetType().GetField('status',$flags).SetValue($form,$status)
    $form.GetType().GetField('lastStatus',$flags).SetValue($form,[DateTime]::Now)
    $initial=(Field 'lblMessage').Text
    Reply ([byte[]](1,2,1,1,1,1))
    Check ((Field 'lblHeadLink').Text -eq 'Сервер через Wi-Fi' -and (Field 'lblMessage').Text -eq $initial) 'first answer sets channel silently'
    Check ((Field 'deviceConnection').Height -eq [NextConfig.Theme]::ControlPx((Field 'deviceConnection'),403)) 'card grows for two channels'
    Reply $null
    Check ((Field 'lblHeadLink').Text -eq 'Сервер через Wi-Fi' -and (Field 'lblMessage').Text -eq $initial) 'missed 113 answer keeps channel'
    Reply ([byte[]](0,0,1,1,1,4))
    Check ((Field 'lblMessage').Text -match '^\d\d\.\d\d\.\d{4}, \d\d:\d\d:\d\d: Связь - переключение с Wi-Fi на сотовую сеть 4G$') 'switch event in status line'
    Check ((Field 'lblHeadLink').Text -eq 'Сервер через 4G') 'header follows switch'
    $form.GetType().GetMethod('ResetLinkChannels',$flags).Invoke($form,@())|Out-Null
    Check ((Field 'lblHeadLink').Text -eq 'Сервер: нет данных' -and (Field 'deviceConnection').Steps -eq -1) 'stale status resets channel'
    $before=(Field 'lblMessage').Text
    Reply ([byte[]](1,2,1,1,1,1))
    Check ((Field 'lblMessage').Text -eq $before -and (Field 'lblHeadLink').Text -eq 'Сервер через Wi-Fi') 'no switch event across stale gap'
} finally { $form.Dispose() }
Write-Output "PASS: $script:n link channel checks; no COM opened"

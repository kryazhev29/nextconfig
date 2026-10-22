param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe",[string]$Snapshot='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
function Field($f,$name){$p=$f.GetType().GetProperty($name,$flags);if($p){return $p.GetValue($f,$null)};return $f.GetType().GetField($name,$flags).GetValue($f)}
function Reply($f,[byte[]]$bytes,[bool]$reload=$false){$f.GetType().GetMethod('WifiApplyReply',$flags).Invoke($f,@($bytes,$reload))|Out-Null}
function Call($name){$a=New-Object object[] $args.Count;for($i=0;$i -lt $args.Count;$i++){$a[$i]=$args[$i].PSObject.BaseObject};$form.GetType().GetMethod($name,$flags).Invoke($form,$a)}
# Без конструктора clOpros, Form2_Load, фонового опроса и COM.
$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$form=New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',0)
try {
    # Свежий статус «на связи»: ответ 113 сразу перерисовывает карточку «Связь» и шапку (22.10.2026).
    $status=New-Object 'NextConfig.clOpros+s_status'
    $status.gsm=1; $status.gprs=1; $status.connectToServer=1; $status.netType=4
    $form.GetType().GetField('status',$flags).SetValue($form,$status)
    $form.GetType().GetField('lastStatus',$flags).SetValue($form,[DateTime]::Now)
    function Link { (Field $form 'deviceConnection').Channels }
    Check (-not (Field $form 'wifiModel').Enabled) 'no editing before protocol capability'
    Reply $form ([byte[]](0,0))
    Check (-not (Field $form 'wifiModel').Enabled) 'legacy reply cannot enable local Wi-Fi'
    Reply $form ([byte[]](0,0,1,0,0,4))
    Check ((Field $form 'wifiModel').SelectedIndex -eq 0) 'default is no modem'
    Check ((Field $form 'wifiPolicy').SelectedIndex -eq 0) 'default only GSM'
    Check ((Field $form 'lblHeadLink').Text -eq 'Сервер через 4G' -and (Link).Rows.Length -eq 0) 'actual cellular generation, one channel'
    Reply $form ([byte[]](1,2,1,1,1,1)) $true
    Check ((Field $form 'wifiModel').SelectedIndex -eq 1) 'read local model'
    Check ((Field $form 'wifiPolicy').SelectedIndex -eq 1) 'read local fallback logic'
    Check ((Field $form 'warning485').Text -eq 'Порт 485 занят Wi-Fi-модемом, камера отключена') 'exclusive port explanation'
    Check (-not (Field $form 'mode485').Enabled) 'server table does not own selected port'
    Check (-not (Field $form 'cmbVoLTE').Enabled) 'effective VoLTE forced off'
    Check ((Field $form 'lblHeadLink').Text -eq 'Сервер через Wi-Fi' -and (Link).Headline -eq 'На связи через Wi-Fi') 'Wi-Fi status'
    Check ((Link).Rows[0].Role -eq 'Активный' -and (Link).Rows[1].Role -eq 'Резерв') 'Wi-Fi active, cellular reserve'
    Check ((Field $form 'lblMessage').Text -match ': Связь - переключение с сотовой сети на Wi-Fi$') 'switch to Wi-Fi in status line'
    (Field $form 'wifiPolicy').SelectedIndex=2
    $status.netType=1; $form.GetType().GetField('status',$flags).SetValue($form,$status)
    Reply $form ([byte[]](0,0,1,1,1,2))
    Check ((Field $form 'wifiPolicy').SelectedIndex -eq 2) 'poll preserves unsaved choice'
    Check ((Field $form 'lblHeadLink').Text -eq 'Сервер через 2G' -and (Link).Rows[0].Role -eq 'Основной' -and (Link).Rows[0].Tone -eq [NextConfig.eLinkTone]::Bad) 'GSM fallback generation'
    Check ((Field $form 'lblMessage').Text -match ': Связь - переключение с Wi-Fi на сотовую сеть 2G$') 'switch to cellular in status line'
    Check ((Field $form 'wifiState').ForeColor -eq [NextConfig.Theme]::Danger) 'bridge fault red'
    Reply $form ([byte[]](0,3,1,1,0,4)) $true
    Check ((Field $form 'wifiState').Text.Contains('только GSM')) 'disabled bridge state'
    Reply $form ([byte[]](1,2,1,1,1,1)) $true
    Reply $form ([byte[]](0,4,1,0,0,4)) $true
    Check ((Field $form 'wifiState').Text -eq 'Мост не подтвердил выключение') 'timeout never claims bridge off'
    Check ((Field $form 'wifiState').ForeColor -eq [NextConfig.Theme]::Danger) 'shutdown failure red even after local deselection'
    Reply $form ([byte[]](1,2,1,1,1,1)) $true
    # Выбор Wi-Fi пишет общая кнопка «Сохранить изменения»; «Отменить» возвращает выбор платы (22.10.2026).
    Check ((Field $form 'wifiCheck').Parent.Controls.Count -eq 2) 'no separate Wi-Fi save button'
    Check (-not (Call 'WifiDiffers')) 'board choice loaded, nothing pending'
    (Field $form 'wifiPolicy').SelectedIndex=0
    Check (Call 'WifiDiffers') 'only GSM is a pending change'
    $list=New-Object 'System.Collections.Generic.List[string]'
    Call 'WifiChanges' $list | Out-Null
    Check ($list.Count -eq 1 -and $list[0] -eq '• Логика работы: GSM, если нет Wi-Fi → Только GSM') 'confirmation lists the logic change'
    Reply $form ([byte[]](1,2,1,1,1,1))
    Check ((Field $form 'wifiPolicy').SelectedIndex -eq 0 -and (Call 'WifiDiffers')) 'poll keeps the pending change'
    Call 'RevertWifi' | Out-Null
    Check ((Field $form 'wifiPolicy').SelectedIndex -eq 1 -and -not (Call 'WifiDiffers')) 'revert restores board logic'
    (Field $form 'wifiModel').SelectedIndex=0
    $list.Clear(); Call 'WifiChanges' $list | Out-Null
    Check ($list.Count -eq 2 -and $list[0] -eq '• Wi-Fi-модем: USR-DR164 → Не выбран' -and $list[1].EndsWith('→ Только GSM')) 'deselecting modem lists model and logic'
    Call 'RevertWifi' | Out-Null
    Check ((Field $form 'wifiModel').SelectedIndex -eq 1 -and (Field $form 'wifiPolicy').SelectedIndex -eq 1) 'revert restores model and logic'
    # Адрес без порта прошивка принимает с портом поля: проверка не блокирует сохранение (22.10.2026).
    Check ((Call 'ServerError' 'gate.example.ru' $true) -eq '') 'address without port accepted'
    Check ((Call 'ServerError' 'gate.example.ru:15010' $true) -eq '') 'address with port accepted'
    Check ((Call 'ServerError' 'a:1:2' $true) -ne '') 'two colons rejected'
    Check ((Call 'ServerError' ':15010' $true) -eq 'Не указан адрес узла') 'port without host rejected'
    Check ((Call 'ServerError' 'gate.example.ru:0' $true) -eq 'Порт от 1 до 65535') 'zero port rejected'
    Check ((Call 'ServerError' '' $false) -eq '' -and (Call 'ServerError' '' $true) -ne '') 'second server may be empty'
    if($Snapshot){
        $form.GetType().GetMethod('SetDefaultConfig',$flags).Invoke($form,@())|Out-Null
        $form.GetType().GetMethod('Load485',$flags).Invoke($form,@())|Out-Null
        $load=[Delegate]::CreateDelegate([EventHandler],$form,$form.GetType().GetMethod('Form2_Load',$flags))
        $form.remove_Load($load)
        $form.GetType().GetField('bootOffered',$flags).SetValue($form,$true)
        $form.GetType().GetField('linkLoading',$flags).SetValue($form,$true)
        $form.ShowInTaskbar=$false;$form.Opacity=0;$form.Show();$form.Size=New-Object Drawing.Size(1280,1200)
        (Field $form 'tabs').SelectedTab=Field $form 'tabSettings'
        $button=(Field $form 'tabSettings').Controls.Find('settingsSection1',$true)[0]
        $button.PerformClick();[Windows.Forms.Application]::DoEvents()
        $section=(Field $form 'wifiModel')
        while($section.Parent -and $section.Parent -ne (Field $form 'tabSettings')){$section=$section.Parent}
        $bitmap=New-Object Drawing.Bitmap($section.Width,$section.Height)
        try{$section.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$section.Width,$section.Height)));$bitmap.Save($Snapshot)}finally{$bitmap.Dispose()}
    }
}finally{$form.Dispose()}
# Карточка фото при выбранном Wi-Fi-модеме: камера в таблице есть, но порт занят мостом (22.10.2026).
$config=New-Object 'NextConfig.clOpros+s_config'
$config.rs485Mode=1; $p=$config.periph2; $p.rs485Adr=2; $p.rs485Type=3; $config.periph2=$p
$photo=New-Object NextConfig.PhotoPanel($poller,'000000000000000')
try {
    $photo.SetState($config,$false,$true,$false,$false)
    Check ((Field $photo 'hint').Text.StartsWith('Камера C328') -and (Field $photo 'shoot').Enabled) 'camera ready without Wi-Fi'
    $photo.SetState($config,$false,$true,$false,$true)
    Check ((Field $photo 'hint').Text -eq 'Камера недоступна: порт RS-485 занят Wi-Fi-мостом.' -and -not (Field $photo 'shoot').Enabled) 'photo blocked by Wi-Fi bridge'
}finally{$photo.Dispose()}
Write-Output "PASS: $script:n Wi-Fi UI checks; no COM opened"

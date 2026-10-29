param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
# Камера номеров (тип RS-485 8): пакет 8, карточка «Камера номеров», снимок через карточку фото (29.10.2026). COM не открывается.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
function Field($o,$name){$p=$o.GetType().GetProperty($name,$flags);if($p){return $p.GetValue($o,$null)};return $o.GetType().GetField($name,$flags).GetValue($o)}
function SetField($o,$name,$value){$o.GetType().GetField($name,$flags).SetValue($o,$value)}
function Call($o,$name){$a=New-Object object[] $args.Count;for($i=0;$i -lt $args.Count;$i++){if($null -ne $args[$i]){$a[$i]=$args[$i].PSObject.BaseObject}};$o.GetType().GetMethod($name,$flags).Invoke($o,$a)}
function Packet8([byte]$state,[string]$id){$b=New-Object byte[] 11;$b[0]=$state;[Text.Encoding]::ASCII.GetBytes($id).CopyTo($b,1);return ,([NextConfig.SldnStream]::Frame(8,$b,9))}
function PlateConfig([byte]$mode){$c=New-Object 'NextConfig.clOpros+s_config';$c.rs485Mode=$mode;$p=$c.periph3;$p.rs485Adr=48;$p.rs485Type=8;$c.periph3=$p;return $c}

# Пакет 8 (Протокол2): 1 — номер как от камеры, остаток нулями; 255 — время вышло.
$read=[NextConfig.PlateRead]::Parse((Packet8 1 'C020PM177'))
Check ($read.Read -and $read.Plate -eq 'C020PM177') 'plate from packet 8'
$timeout=[NextConfig.PlateRead]::Parse((Packet8 255 ''))
Check (-not $timeout.Read -and $timeout.Plate -eq '') 'timeout 255'
Check ($null -eq [NextConfig.PlateRead]::Parse([NextConfig.SldnStream]::Frame(11,(New-Object byte[] 9),9))) 'answer 11 is not a plate'
Check ($null -eq [NextConfig.PlateRead]::Parse([byte[]](1,2,3))) 'short frame ignored'
# Пакет 8 не становится ответом на чужой запрос даже с тем же номером: идёт событием ReceiveBytes.
Check (-not [NextConfig.SldnStream]::MatchesReply([NextConfig.SldnStream]::Command(101,$null,0),(Packet8 1 'A001XH197'))) 'not a status reply'
Check (-not [NextConfig.SldnStream]::MatchesReply([NextConfig.SldnStream]::Command(8,[byte[]](3,1,0),9),(Packet8 1 'A001XH197'))) 'not a command 8 reply'
Check (-not [NextConfig.SldnStream]::MatchesReply([NextConfig.SldnStream]::Command(6,[byte[]](15,3,48),9),(Packet8 1 'A001XH197'))) 'not the command 6 reply'

$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$status=New-Object 'NextConfig.clOpros+s_status'
$status.peripheryState=[byte[]](0,0,1,0,0,0,0,0)
$jpeg=New-Object 'NextConfig.clOpros+s_config';$jpeg.rs485Mode=1;$q=$jpeg.periph1;$q.rs485Adr=1;$q.rs485Type=9;$jpeg.periph1=$q
$panel=New-Object NextConfig.PlatePanel($poller)
try {
    $panel.SetState((PlateConfig 1),$status,$false,$true,$false)
    Check $panel.HasCamera 'type 8 in the table shows the card'
    Check ((Field $panel 'cameras').SelectedItem -eq 48) 'camera address from the table'
    Check ((Field $panel 'link').Text -eq 'Отвечает') 'link from the camera row of status 101'
    Check ((Field $panel 'read').Enabled) 'read enabled'
    $status.peripheryState=[byte[]](1,1,0,1,1,1,1,1)
    $panel.SetState((PlateConfig 1),$status,$false,$true,$false)
    Check ((Field $panel 'link').Text -eq 'Не отвечает') 'own row, not the others'
    $panel.SetState((PlateConfig 1),$status,$false,$false,$false)
    Check (-not (Field $panel 'read').Enabled -and (Field $panel 'note').Text -eq 'Ожидание свежего состояния платы…' -and (Field $panel 'link').Text -eq 'Нет свежих данных') 'stale status'
    $panel.SetState((PlateConfig 1),$status,$false,$true,$true)
    Check (-not (Field $panel 'read').Enabled -and (Field $panel 'note').Text.Contains('Wi-Fi-мостом') -and (Field $panel 'link').Text -eq 'Не опрашивается') 'Wi-Fi bridge owns the port'
    $panel.SetState((PlateConfig 0),$status,$false,$true,$false)
    Check (-not (Field $panel 'read').Enabled -and (Field $panel 'note').Text.Contains('активном режиме')) 'passive RS-485'
    $panel.SetState((PlateConfig 1),$status,$true,$true,$false)
    Check (-not (Field $panel 'read').Enabled -and (Field $panel 'note').Text.StartsWith('Плата занята')) 'busy board'
    $status.peripheryState=[byte[]](0,0,1,0,0,0,0,0)
    $panel.SetState((PlateConfig 1),$status,$false,$true,$false)
    # Ожидание после ответа 0 на команду 6: номер приходит пакетом 8 через ReceiveBytes порта.
    $null=$panel.Handle
    $port=New-Object NextConfig.ClComPort((New-Object IO.Ports.SerialPort('OFFLINE')),$false)
    $port.add_ReceiveBytes({param($frame) $panel.OnFrame($frame)})
    SetField $panel 'waitingAddress' ([byte]48)
    SetField $panel 'waitingUntil' ([DateTime]::Now.AddSeconds(18))
    Call $panel 'Apply'
    Check (-not (Field $panel 'read').Enabled -and -not (Field $panel 'cameras').Enabled -and (Field $panel 'note').Text.StartsWith('Ждём номер от камеры 48… 15 с')) 'waiting countdown'
    $port.CheckReceivedBytes((Packet8 1 'A001XH197'))
    [Windows.Forms.Application]::DoEvents()
    Check ((Field $panel 'plate').Text -eq 'A001XH197') 'plate shown'
    Check ((Field $panel 'note').Text.StartsWith('Считан в ') -and (Field $panel 'read').Enabled) 'read time, ready again'
    $port.CheckReceivedBytes((Packet8 1 'X999XX99'))
    [Windows.Forms.Application]::DoEvents()
    Check ((Field $panel 'plate').Text -eq 'A001XH197') 'packet without waiting is ignored'
    SetField $panel 'waitingUntil' ([DateTime]::Now.AddSeconds(18))
    $port.CheckReceivedBytes((Packet8 255 ''))
    [Windows.Forms.Application]::DoEvents()
    Check ((Field $panel 'note').Text.Contains('не прислала номер') -and (Field $panel 'note').ForeColor -eq [NextConfig.Theme]::Danger -and (Field $panel 'plate').Text -eq 'A001XH197') 'timeout red, last plate kept'
    SetField $panel 'waitingUntil' ([DateTime]::Now.AddSeconds(-1))
    Call $panel 'Tick' $null ([EventArgs]::Empty)
    Check ((Field $panel 'note').Text.Contains('до 29.10.2026') -and (Field $panel 'read').Enabled) 'no packet 8 at all: firmware hint'
} finally { $panel.Dispose() }
$other=New-Object NextConfig.PlatePanel($poller)
try {
    $other.SetState($jpeg,$status,$false,$true,$false)
    Check (-not $other.HasCamera -and -not (Field $other 'read').Enabled) 'JPEG camera alone: no plate card'
} finally { $other.Dispose() }

# Снимок с камеры номеров — через карточку фото; JPEG-камера как раньше.
$photo=New-Object NextConfig.PhotoPanel($poller,'000000000000000')
try {
    $photo.SetState((PlateConfig 1),$false,$true,$false,$false)
    Check ((Field $photo 'hint').Text.StartsWith('Камера номеров') -and (Field $photo 'shoot').Enabled -and (Field $photo 'addresses').SelectedItem -eq 48) 'photo card takes the plate camera'
    $photo.SetState($jpeg,$false,$true,$false,$false)
    Check ((Field $photo 'hint').Text.StartsWith('Камера C328') -and (Field $photo 'shoot').Enabled) 'JPEG camera unchanged'
} finally { $photo.Dispose() }
Check ([NextConfig.PeripheryNames]::IsCamera(8) -and [NextConfig.PeripheryNames]::IsCamera(9) -and -not [NextConfig.PeripheryNames]::IsCamera(16)) 'camera types 8 and 9'

# Главная страница: карточка рядом с фото и памятью ID, только когда камера номеров есть в таблице.
$form=New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',0)
try {
    $form.GetType().GetField('status',$flags).SetValue($form,$status)
    $form.GetType().GetField('lastStatus',$flags).SetValue($form,[DateTime]::Now)
    $form.GetType().GetField('onBoard',$flags).SetValue($form,(PlateConfig 1))
    Call $form 'TmrTick'
    $grid=Field $form 'mediaGrid'
    Check ($grid.Controls.Count -eq 3 -and $grid.Controls.Contains((Field $form 'plateCard'))) 'plate card on the main page'
    $form.GetType().GetField('onBoard',$flags).SetValue($form,$jpeg)
    Call $form 'TmrTick'
    Check ($grid.Controls.Count -eq 2 -and -not $grid.Controls.Contains((Field $form 'plateCard'))) 'no plate camera, no card'
} finally { $form.Dispose() }
Write-Output "PASS: $script:n plate camera checks; no COM opened"

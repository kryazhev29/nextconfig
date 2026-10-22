param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
# Выбор сети Wi-Fi (22.10.2026): разбор страниц USB 115, порядок «как в Windows», окно выбора без COM, демо-страницы.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
function Field($f,$name){$p=$f.GetType().GetProperty($name,$flags);if($p){return $p.GetValue($f,$null)};return $f.GetType().GetField($name,$flags).GetValue($f)}
function Net([string]$name,[int]$level){$x=New-Object NextConfig.WifiNetwork;$x.Name=$name;$x.Level=$level;$x}
function Page([byte[]]$bytes,$list){$next=0;$status=[NextConfig.WifiNetworks]::ParsePage($bytes,$list,[ref]$next);@($status,$next)}
$utf8=[Text.Encoding]::UTF8

# Страница: статус, следующая строка, строки [уровень][длина][имя]; запятая и пробел в имени; уровень выше 100 режется.
$list=New-Object 'System.Collections.Generic.List[NextConfig.WifiNetwork]'
$homeName=$utf8.GetBytes('Home'); $cafe=$utf8.GetBytes('Cafe, Bar')
$r=Page ([byte[]](@(0,255,100,4)+$homeName+@(47,9)+$cafe+@(120,2)+$utf8.GetBytes('Ok'))) $list
Check ($r[0] -eq [NextConfig.eWifiScan]::Ready -and $r[1] -eq 255 -and $list.Count -eq 3) 'page parsed'
Check ($list[0].Name -eq 'Home' -and $list[0].Level -eq 100 -and $list[1].Name -eq 'Cafe, Bar' -and $list[1].Level -eq 47 -and $list[2].Level -eq 100) 'rows and clamp'
$r=Page ([byte[]](@(0,3,6,8)+$utf8.GetBytes('Open Net'))) $list
Check ($r[0] -eq [NextConfig.eWifiScan]::Ready -and $r[1] -eq 3 -and $list.Count -eq 4) 'next row from page'
# Обрезанная строка в конце не ломает разбор; статусы 1/2/3 и отсутствие ответа.
$r=Page ([byte[]](0,255,50,5,65,66)) $list
Check ($r[0] -eq [NextConfig.eWifiScan]::Ready -and $list.Count -eq 4) 'truncated row skipped'
Check ((Page ([byte[]](1)) $list)[0] -eq [NextConfig.eWifiScan]::Searching) 'status searching'
Check ((Page ([byte[]](2)) $list)[0] -eq [NextConfig.eWifiScan]::NoModem) 'status no modem'
Check ((Page ([byte[]](3)) $list)[0] -eq [NextConfig.eWifiScan]::NoAnswer) 'status bridge silent'
Check ((Page $null $list)[0] -eq [NextConfig.eWifiScan]::NoReply -and (Page ([byte[]]@()) $list)[0] -eq [NextConfig.eWifiScan]::NoReply) 'no reply'
Check ((Page ([byte[]](9)) $list)[0] -eq [NextConfig.eWifiScan]::NoReply) 'unknown status is no reply'
Check ([NextConfig.WifiNetworks]::StatusText([NextConfig.eWifiScan]::NoAnswer).StartsWith('Мост не ответил') -and [NextConfig.WifiNetworks]::StatusText([NextConfig.eWifiScan]::NoReply).Contains('22.10.2026')) 'status texts'

# Порядок: подключённая первой с пометкой, остальные по уровню; повторы имён слиты; скрытая текущая сеть — из 113.
$found=@((Net 'Cafe' 47),(Net 'Home' 90),(Net 'Home' 100),(Net 'Weak' 6),(Net '' 80))
$o=[NextConfig.WifiNetworks]::Order([NextConfig.WifiNetwork[]]$found,'Home',55)
Check ($o.Count -eq 3 -and $o[0].Name -eq 'Home' -and $o[0].Connected -and $o[0].Level -eq 90) 'connected first, level from scan, duplicates merged, empty name dropped'
Check ($o[1].Name -eq 'Cafe' -and -not $o[1].Connected -and $o[2].Name -eq 'Weak') 'rest by level'
$o=[NextConfig.WifiNetworks]::Order([NextConfig.WifiNetwork[]]$found,'Hidden',62)
Check ($o.Count -eq 4 -and $o[0].Name -eq 'Hidden' -and $o[0].Connected -and $o[0].Level -eq 62 -and $o[1].Name -eq 'Home' -and $o[1].Level -eq 90) 'hidden current network first with 113 level'
$o=[NextConfig.WifiNetworks]::Order([NextConfig.WifiNetwork[]]$found,'',0)
Check ($o.Count -eq 3 -and -not $o[0].Connected -and $o[0].Name -eq 'Home') 'no current network'
Check ([NextConfig.WifiNetworks]::Detail($o[0]) -eq 'Сигнал хороший' -and [NextConfig.WifiNetworks]::Detail($o[2]) -eq 'Сигнал слабый') 'detail text'
$c=Net 'X' 10; $c.Connected=$true
Check ([NextConfig.WifiNetworks]::Detail($c) -eq 'Подключено') 'connected text'

# Окно выбора без платы: строки, «Другая сеть…» в конце, выбор открывает пароль, скрытой сети нужно имя, проверка как у 113.
$picker=New-Object NextConfig.frmWifiPicker($null,'Home',90)
try {
    $picker.ShowNetworks([NextConfig.WifiNetworks]::Order([NextConfig.WifiNetwork[]]$found,'Home',90))
    Check ($picker.RowCount -eq 4 -and $picker.Row(0).Network.Connected -and $picker.Row(3).Hidden) 'rows plus hidden network entry'
    Check (-not (Field $picker 'btnSave').Enabled -and -not $picker.EditorVisible) 'save needs a selection'
    $picker.SelectRow($picker.Row(1))
    Check ($picker.EditorVisible -and $picker.SelectedName -eq 'Cafe' -and (Field $picker 'btnSave').Enabled -and -not $picker.SelectedHidden) 'selected row shows password'
    Check ($picker.Validate() -eq '') 'open network allowed'
    (Field $picker 'keyBox').Text=('x'*64)
    Check ($picker.Validate() -ne '') 'long password rejected'
    $picker.SelectRow($picker.Row(3))
    Check ($picker.SelectedHidden -and -not (Field $picker 'btnSave').Enabled -and $picker.Validate() -eq 'Выберите сеть') 'hidden network needs a name'
    (Field $picker 'nameBox').Text='Secret'
    Check ((Field $picker 'btnSave').Enabled -and $picker.SelectedName -eq 'Secret' -and $picker.Validate() -eq '') 'hidden name accepted'
    $picker.ShowNetworks($null)
    Check ($picker.RowCount -eq 0 -and -not (Field $picker 'btnSave').Enabled) 'empty list while searching'
} finally { $picker.Dispose() }

# Демо-плата: 255 запускает поиск, ~1,5 с «ищется», затем страницы по 60 байт до конца, подключённая Office-5G из --demo-link.
Check ([NextConfig.Demo]::WifiScanReply(255).Length -eq 1 -and [NextConfig.Demo]::WifiScanReply(255)[0] -eq 1) 'demo search starts'
Start-Sleep -Milliseconds 1700
$all=New-Object 'System.Collections.Generic.List[NextConfig.WifiNetwork]'
$row=0; $pages=0
do { $r=Page ([NextConfig.Demo]::WifiScanReply([byte]$row)) $all; $pages++; $row=$r[1] } while ($r[0] -eq [NextConfig.eWifiScan]::Ready -and $row -ne 255 -and $pages -lt 10)
Check ($r[0] -eq [NextConfig.eWifiScan]::Ready -and $pages -ge 2 -and $all.Count -eq 7 -and $all[1].Name -eq 'TP-Link_15CB' -and $all[1].Level -eq 100) 'demo pages'
$o=[NextConfig.WifiNetworks]::Order($all,'Office-5G',78)
Check ($o[0].Name -eq 'Office-5G' -and $o[0].Connected -and $o[1].Name -eq 'TP-Link_15CB') 'demo order'
"PASS wifi-scan: $($script:n) checks"

param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
# Резервная выгрузка базы ID (26.10.2026): разбор страниц USB 116, демо-плата, CSV в формате сервера.
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
function Page([byte[]]$bytes,$list){$next=-1;$status=[NextConfig.IdsExport]::ParsePage($bytes,$list,[ref]$next);@($status,$next)}
function Cell([int]$index,[string]$id,[byte]$group,[uint32]$block,[uint16]$use,[byte]$kind,[byte]$perDay){
    $raw=New-Object byte[] 32
    [BitConverter]::GetBytes([uint16]$index).CopyTo($raw,0)
    [Text.Encoding]::ASCII.GetBytes($id).CopyTo($raw,2)
    for($i=2+$id.Length;$i -lt 12;$i++){$raw[$i]=255}
    $raw[12]=$group;[BitConverter]::GetBytes($block).CopyTo($raw,13);[BitConverter]::GetBytes($use).CopyTo($raw,17)
    $raw[19]=$kind;$raw[20]=$perDay;$raw
}

# Страница: код, следующая ячейка, записи по 32 Б; ID без хвоста FF/0; тип — номер бита.
$list=New-Object 'System.Collections.Generic.List[NextConfig.IdCell]'
$page=[byte[]](@(0,0x10,0,2)+(Cell 3 '9139631122' 1 0 0 1 255)+(Cell 9 '76764543' 2 1793000000 60 2 3))
$r=Page $page $list
Check ($r[0] -eq [NextConfig.eIdsPage]::Ready -and $r[1] -eq 16 -and $list.Count -eq 2) 'page parsed'
Check ($list[0].Cell -eq 3 -and $list[0].Id -eq '9139631122' -and $list[0].Kind -eq 0 -and $list[1].Id -eq '76764543' -and $list[1].Kind -eq 1) 'id and type'
Check ($list[1].Group -eq 2 -and $list[1].BlockTime -eq 1793000000 -and $list[1].UseTime -eq 60 -and $list[1].PerDay -eq 3) 'record fields'
Check ((Page ([byte[]](1,9,0,0)) $list)[0] -eq [NextConfig.eIdsPage]::Busy) 'busy'
Check ((Page $null $list)[0] -eq [NextConfig.eIdsPage]::NoReply -and (Page ([byte[]](0,0,0,2)) $list)[0] -eq [NextConfig.eIdsPage]::NoReply -and $list.Count -eq 2) 'short reply is no reply'

# CSV: заголовок и порядок колонок сервера, UTF-8 строки, булевы True/False, дата дд.ММ.гггг чч:мм:сс.
$csv=[NextConfig.IdsExport]::Csv($list,'+7')
$lines=$csv.TrimEnd() -split "`r`n"
Check ($lines[0] -eq 'countryCode;mainPhoneNumber;fullName;enableLifeTimeInMinutes;lifeTimeInMinutes;enableAutoBlockDate;autoBlockDate;restrictAccessRequestPerDay;accessRequestMaxCountPerDay;groupName;isBlocked;isDeleted;satelliteIdentificator;satelliteType;satelliteCellNumber;satelliteIsDeleted;satelliteIsBlocked') 'header'
Check ($lines[1] -eq '+7;9139631122;;False;0;False;;False;0;Группа 1;False;False;9139631122;0;3;False;False') 'unlimited phone row'
$date=[DateTimeOffset]::FromUnixTimeSeconds(1793000000).LocalDateTime.ToString('dd.MM.yyyy HH:mm:ss')
Check ($lines[2] -eq "+7;76764543;;True;60;True;$date;True;3;Группа 2;False;False;76764543;1;9;False;False") 'limited rfid row'
Check ($lines.Count -eq 3 -and ($lines[1] -split ';').Count -eq 17) 'column count'

# Демо-плата: страницами по 7 до конца, 30 записей в нечётных ячейках 1…59.
$all=New-Object 'System.Collections.Generic.List[NextConfig.IdCell]'
$cell=1;$pages=0
do { $r=Page ([NextConfig.Demo]::IdsReply([uint16]$cell)) $all; $pages++; $cell=$r[1] } while ($r[0] -eq [NextConfig.eIdsPage]::Ready -and $cell -ne 0 -and $pages -lt 500)
Check ($cell -eq 0 -and $all.Count -eq 30 -and $all[29].Cell -eq 59) 'demo walk ends'
Check (@($all | Select-Object -ExpandProperty Cell -Unique).Count -eq 30) 'no duplicates across pages'
"PASS ids-export: $script:n checks; no COM"

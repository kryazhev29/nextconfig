param([string]$Mode='--demo',[switch]$Dpi120,[switch]$FullWindow,[string]$Output="$PSScriptRoot\..\..\Стенд_Domovoi\NextConfig_QA\2026-09-29-photo-menu")
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$scripts=Join-Path (Split-Path $repo) 'Стенд_Domovoi\Скрипты'
if(Get-Process NextConfig -ErrorAction SilentlyContinue) {throw 'NextConfig уже открыт: окно владельца не трогаем'}
$tag=($Mode -replace '--demo:?','');if($tag -eq '') {$tag='next'}
$scale=if($Dpi120){'125'}else{'100'}
$out=Join-Path $Output ("$tag-$scale" + $(if($FullWindow){"-full"}else{""}))
New-Item -ItemType Directory -Force -Path $out | Out-Null
$arguments=@($Mode);if(-not $FullWindow){$arguments+='--demo-laptop'};if($Dpi120){$arguments+='--demo-dpi=120'}else{$arguments+='--demo-dpi=96'}
function Click([string]$caption) {
    $line=$null
    for($attempt=0;$attempt -lt 15 -and -not $line;$attempt++) {
        $list=& "$scripts\ui_click.ps1" -List
        $line=$list | Where-Object {($_ -split ': ',2)[-1] -eq $caption} | Select-Object -First 1
        if(-not $line){Start-Sleep -Milliseconds 300}
    }
    if(-not $line){throw "Button not found: $caption; $list"}
    & "$scripts\ui_click.ps1" -ButtonIndex ([int](($line -split ':',2)[0])) | Out-Null
    Start-Sleep -Milliseconds 500
}
function Snap([string]$name) {
    $live=@(Get-Process NextConfig -ErrorAction SilentlyContinue)
    if($live.Count -ne 1 -or $live[0].Id -ne $p.Id){throw 'Unexpected NextConfig window'}
    & "$scripts\ui_snap.ps1" -Smallest -Out (Join-Path $out "$name.png") | Out-Null
}
$p=Start-Process (Join-Path $repo 'NextConfig\bin\Release\NextConfig.exe') -ArgumentList $arguments -PassThru
try {
    Start-Sleep -Seconds 2
    if($Mode -eq '--demo' -or $Mode -eq '--demo:oldfw') {
        & "$scripts\ui_text.ps1" -Index 1 -Text '4321' | Out-Null
        Click 'Подключиться'
        Start-Sleep -Seconds 1
        if($Mode -eq '--demo:oldfw'){Snap 'boot-offer'; Click 'Позже'}
    }
    $sections=if($Mode -eq '--demo:oldfw48'){@('Устройство','Пользователи','Настройки','Расписания','Сервис')}elseif($Mode -in @('--demo:boot','--demo:oldboot')){@('Устройство')}else{@('Устройство','Пользователи','Журнал','Настройки','Сервис')}
    $names=@{Устройство='device';Пользователи='users';Журнал='journal';Настройки='settings';Расписания='schedules';Сервис='service'}
    foreach($section in $sections) {
        Click $section
        Snap $names[$section]
        if($section -in @('Устройство','Сервис')) {
            & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id | Out-Null
            Snap ($names[$section]+'-lower')
            & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Direction 6 | Out-Null
        }
        if($section -eq 'Настройки' -and $Mode -ne '--demo:oldfw48') {
            foreach($sub in @('Связь','Доступ','485 и Wiegand')) {
                Click $sub;$subName='settings-'+(@{'Связь'='connection';'Доступ'='access';'485 и Wiegand'='bus'}[$sub]);Snap $subName
                & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id | Out-Null;Snap ($subName+'-lower')
                & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Direction 6 | Out-Null
            }
        }
        if($section -eq 'Сервис' -and $Mode -in @('--demo','--demo:oldfw')) {
            foreach($anchor in @('Загрузчик','Настройки платы')) {
                & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Direction 6 | Out-Null
                Click $anchor;Snap ('service-'+(@{'Загрузчик'='boot';'Настройки платы'='snapshot';'Обслуживание'='maintenance'}[$anchor]))
            }
        }
    }
    if($Mode -eq '--demo:oldfw48'){
        Click 'Пользователи';Click 'Сохранить базу CSV'
        $csv=Join-Path $out ('users-'+(Get-Date -Format 'yyyyMMddHHmmssfff')+'.csv')
        & "$scripts\ui_filedialog.ps1" -Path $csv | Out-Null
        Start-Sleep -Milliseconds 500
        if(-not (Test-Path -LiteralPath $csv)){throw 'CSV not saved'}
        if(@(Import-Csv -LiteralPath $csv -Delimiter ';').Count -ne 35){throw 'Expected 35 CSV rows'}
        Snap 'users-exported'
        Click 'Устройство'; Click 'Перевести на новую систему';Snap 'migration-confirm';Click 'Отмена';Snap 'migration-cancel'
        Click 'Перевести на новую систему';Click 'Перейти в загрузчик';Start-Sleep -Seconds 1;Snap 'migration-boot'
    }
    "PASS UI: $Mode $scale; $out"
} finally {
    if(-not $p.HasExited){Stop-Process -Id $p.Id}
}

param([string]$Output = "$PSScriptRoot\..\artifacts\oldfw-review\after", [switch]$Dpi120)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
if(Get-Process NextConfig -ErrorAction SilentlyContinue){throw 'NextConfig уже открыт: канал владельца не трогаем'}
$out=Join-Path $Output $(if($Dpi120){'125'}else{'100'})
New-Item -ItemType Directory -Force -Path $out | Out-Null
$agent=Join-Path $repo 'tools\nc_agent.ps1'
function CallAgent([string[]]$Arguments){
 $r=(& $agent @Arguments | ConvertFrom-Json)
 if(-not $r.ok){throw ($Arguments -join ' ') + ': '+$r.error}
 return $r
}
function Click($target){CallAgent @('click',$target)|Out-Null}
function Snap($name){$r=CallAgent @('snap');Copy-Item -LiteralPath $r.file -Destination (Join-Path $out ($name+'.png'))}
function Denied($name){
 $r=& $agent click $name | ConvertFrom-Json
 if($r.ok -or $r.error -notlike 'запрещено каналом*'){throw "Write not blocked: $name $($r.error)"}
}
$arguments=@('--demo:oldfw45','--agent','--demo-laptop',$(if($Dpi120){'--demo-dpi=120'}else{'--demo-dpi=96'}))
$p=Start-Process (Join-Path $repo 'NextConfig\bin\Release\NextConfig.exe') -ArgumentList $arguments -WindowStyle Hidden -PassThru
try{
 Start-Sleep -Seconds 3
 Click 'btnConnect'
 $ready=$false
 for($attempt=0;$attempt -lt 40;$attempt++){
  $tree=CallAgent @('tree')
  if(@($tree.controls | Where-Object {$_.text -like 'База прочитана полностью*'}).Count){$ready=$true;break}
  Start-Sleep -Milliseconds 150
 }
 if(-not $ready){throw 'Initial demo read did not finish'}
 Snap 'device'
 & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id | Out-Null
 Snap 'device-inputs'
 Click 'Обновить состояние'
 Start-Sleep -Milliseconds 500
 Snap 'device-inputs-after-refresh'
 & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Direction 6 | Out-Null
 Denied 'oldTestOutputDevice'
 foreach($section in @('Пользователи','Настройки','Расписания','Сервис')){Click $section;Snap $section}
 Denied 'oldFactoryReset';Denied 'oldAutoregistration';Denied 'oldWriteRfid'
 Click 'oldSettingsDiagnostics';Snap 'service-settings-expanded';Click 'oldSettingsDiagnostics'
 Click 'oldMonitorDiagnostics';Snap 'service-monitor-expanded';Click 'oldMonitorDiagnostics'
 Click 'Настройки';Denied 'oldWriteSettings'
 $groups=(CallAgent @('tree')).controls | Where-Object name -eq 'oldSettingsGroup'
 foreach($group in $groups.items){
  CallAgent @('select','oldSettingsGroup',$group)|Out-Null;Snap ('settings-'+$group)
  & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id | Out-Null
  Snap ('settings-'+$group+'-lower')
  & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Direction 6 | Out-Null
 }
 CallAgent @('select','oldSettingsGroup','Баланс SIM-карты')|Out-Null
 Click 'oldSetting164_8'
 $tree=CallAgent @('tree')
 foreach($name in @('oldSetting172_255','oldSetting176_255')){
  $field=$tree.controls | Where-Object name -eq $name
  if($null -eq $field -or $field.enabled -ne $false){throw "Balance dependency not disabled: $name"}
 }
 Snap 'balance-disabled';Click 'oldSetting164_8'
 Click 'Расписания';Denied 'oldWriteSchedules'
 foreach($n in 1..7){CallAgent @('select','oldScheduleGroup',"Расписание $n")|Out-Null;Snap "schedule-$n"}
 Click 'Устройство';Snap 'device-return'
 "PASS UI: oldfw45; groups, schedules, balance dependencies, 6 blocked writes; $out"
}finally{
 if(-not $p.HasExited){Stop-Process -Id $p.Id}
}

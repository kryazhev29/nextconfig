param([string]$Scenario='loss',[switch]$Dpi120)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$scripts=Join-Path (Split-Path $repo) 'Стенд_Domovoi\Скрипты'
$qa=Join-Path (Split-Path $repo) 'Стенд_Domovoi\NextConfig_QA\2026-09-29-photo-menu'
if(Get-Process NextConfig -ErrorAction SilentlyContinue){throw 'NextConfig уже открыт'}
$folder=Join-Path $qa 'photos-menu';New-Item -ItemType Directory -Force -Path $folder|Out-Null
$scale=if($Dpi120){'120'}else{'96'}
$args=@('--demo','--demo-laptop',"--demo-dpi=$scale","--demo-photo=$Scenario","--demo-photo-folder=$folder")
function Click($caption){
 $line=$null
 for($i=0;$i -lt 15 -and -not $line;$i++){$list=& "$scripts\ui_click.ps1" -List;$line=$list|Where-Object{($_ -split ': ',2)[-1] -eq $caption}|Select-Object -First 1;if(-not $line){Start-Sleep -Milliseconds 300}}
 if(-not $line){throw "Button not found: $caption"}
 & "$scripts\ui_click.ps1" -ButtonIndex ([int](($line -split ':',2)[0]))|Out-Null;Start-Sleep -Milliseconds 500
}
function Snap($name){& "$scripts\ui_snap.ps1" -Smallest -Out (Join-Path $qa "photo-menu-$Scenario-$name-$scale.png")|Out-Null}
$p=Start-Process (Join-Path $repo 'NextConfig\bin\Release\NextConfig.exe') -ArgumentList $args -PassThru
try {
 Start-Sleep -Seconds 3
 & "$scripts\ui_text.ps1" -Index 1 -Text '4321'|Out-Null
 Click 'Подключиться';Start-Sleep -Seconds 2
 & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id |Out-Null
 Snap 'buttons'
 if($Scenario -eq 'loss'){
  Click 'Сделать снимок';Snap 'capture'
  Click 'Забрать фото';Snap 'folder';Click 'ОК';Start-Sleep -Seconds 2
  & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id |Out-Null
  Snap 'received'
  if(@(Get-ChildItem $folder -Filter '867442070217472_*.jpg').Count -ne 3){throw 'Expected 3 JPEG files'}
  Click 'Забрать фото';Click 'ОК';Start-Sleep -Milliseconds 600;Snap 'empty'
 }
 "PASS UI photo: $Scenario dpi=$scale"
}finally{if(-not $p.HasExited){Stop-Process -Id $p.Id}}

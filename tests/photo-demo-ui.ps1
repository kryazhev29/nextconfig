param([string]$Scenario='loss',[switch]$Dpi120,[string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe",
 [string]$Output="$PSScriptRoot\..\..\Стенд_Domovoi\NextConfig_QA\2026-09-29-photo-menu")
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$scripts=Join-Path (Split-Path $repo) 'Стенд_Domovoi\Скрипты'
$qa=$Output
if(Get-Process NextConfig -ErrorAction SilentlyContinue){throw 'NextConfig уже открыт'}
$folder=Join-Path $qa 'photos-menu';New-Item -ItemType Directory -Force -Path $folder|Out-Null
$scale=if($Dpi120){'120'}else{'96'}
$arguments=@('--demo','--demo-laptop',"--demo-dpi=$scale","--demo-photo=$Scenario")
# «Сделать снимок» сам забирает кадр в %TEMP%\NextConfig_photo\<IMEI>: даём программе свою TEMP,
# чтобы считать файлы только этого прогона. «Сохранить…» запоминает папку в oldphoto.txt владельца - вернём файл.
$temp=Join-Path $env:TEMP ('nextconfig-photo-demo-'+[guid]::NewGuid().ToString('N'))
$received=Join-Path $temp 'NextConfig_photo\867442070217472'
$prefs=Join-Path $env:LOCALAPPDATA 'NextConfig\oldphoto.txt'
$prefsBefore=if(Test-Path -LiteralPath $prefs){[IO.File]::ReadAllBytes($prefs)}else{$null}
function Click($caption,$window=-1){
 $line=$null
 for($i=0;$i -lt 15 -and -not $line;$i++){$list=& "$scripts\ui_click.ps1" -WindowIndex $window -List;$line=$list|Where-Object{($_ -split ': ',2)[-1] -eq $caption}|Select-Object -First 1;if(-not $line){Start-Sleep -Milliseconds 300}}
 if(-not $line){throw "Button not found: $caption"}
 & "$scripts\ui_click.ps1" -WindowIndex $window -ButtonIndex ([int](($line -split ':',2)[0]))|Out-Null;Start-Sleep -Milliseconds 500
}
function Snap($name,$smallest=$true){& "$scripts\ui_snap.ps1" -Smallest:$smallest -Out (Join-Path $qa "photo-menu-$Scenario-$name-$scale.png")|Out-Null}
function Received($count){
 for($i=0;$i -lt 60;$i++){$files=@(Get-ChildItem $received -Filter '867442070217472_*.jpg' -ErrorAction SilentlyContinue);if($files.Count -ge $count){break};Start-Sleep -Milliseconds 500}
 Start-Sleep -Milliseconds 800
 if($files.Count -ne $count){throw "Expected $count JPEG files, got $($files.Count)"}
}
New-Item -ItemType Directory -Force -Path $temp|Out-Null
$tmpBefore=$env:TMP;$tempBefore=$env:TEMP;$env:TMP=$temp;$env:TEMP=$temp
try{$p=Start-Process (Resolve-Path $AssemblyPath).Path -ArgumentList $arguments -PassThru}finally{$env:TMP=$tmpBefore;$env:TEMP=$tempBefore}
try {
 Start-Sleep -Seconds 3
 & "$scripts\ui_text.ps1" -Index 1 -Text '4321'|Out-Null
 Click 'Подключиться';Start-Sleep -Seconds 2
 & "$PSScriptRoot\ui_scroll_target.ps1" -DemoPid $p.Id -Caption 'Фото с камеры'|Out-Null
 Snap 'buttons'
 if($Scenario -eq 'loss'){
  # В очереди платы два старых снимка, третий - новый: выгружаются все три, в рамке последний.
  Click 'Сделать снимок';Snap 'capture'
  Received 3;Snap 'received'
  # Окно снимка - по размеру экрана: при 120 dpi оно больше главного и уже не самое маленькое.
  # Тогда ищем его верхним по Z-порядку (-WindowIndex 0): модальное окно лежит над владельцем.
  Click 'Открыть крупно'
  $view=$null
  for($i=0;$i -lt 15 -and $null -eq $view;$i++){foreach($w in -1,0){if(@(& "$scripts\ui_click.ps1" -WindowIndex $w -List) -like '*: Закрыть'){$view=$w;break}};if($null -eq $view){Start-Sleep -Milliseconds 300}}
  if($null -eq $view){throw 'Photo window not found'}
  Snap 'large' ($view -eq -1);Click 'Закрыть' $view
  $saved=Join-Path $folder ('867442070217472_'+(Get-Date -Format 'yyyyMMddHHmmssfff')+'.jpg')
  Click 'Сохранить…'
  for($i=0;$i -lt 10;$i++){if((& "$scripts\ui_filedialog.ps1" -Path $saved) -eq 'path sent'){break};Start-Sleep -Milliseconds 300}
  Start-Sleep -Milliseconds 800
  if(-not (Test-Path -LiteralPath $saved)){throw 'JPEG not saved'}
  if((Get-FileHash -LiteralPath $saved).Hash -ne (Get-FileHash -LiteralPath (Join-Path $received '867442070217472_483.jpg')).Hash){throw 'Saved JPEG is not the last photo'}
  Snap 'saved'
  # Второй снимок: подтверждённые кадры плата больше не отдаёт, приходит только новый.
  $before=@(Get-ChildItem $received -Filter '*.jpg'|ForEach-Object{$_.Name+' '+$_.LastWriteTimeUtc.Ticks})
  Click 'Сделать снимок';Received 4;Snap 'next'
  $after=@(Get-ChildItem $received -Filter '*.jpg'|ForEach-Object{$_.Name+' '+$_.LastWriteTimeUtc.Ticks})
  if(@($before|Where-Object{$after -notcontains $_}).Count -ne 0){throw 'Confirmed photos were downloaded again'}
 }
 "PASS UI photo: $Scenario dpi=$scale"
}finally{
 if($p -and -not $p.HasExited){Stop-Process -Id $p.Id;$p.WaitForExit(5000)|Out-Null}
 if($null -eq $prefsBefore){Remove-Item -LiteralPath $prefs -ErrorAction SilentlyContinue}else{[IO.File]::WriteAllBytes($prefs,$prefsBefore)}
 Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

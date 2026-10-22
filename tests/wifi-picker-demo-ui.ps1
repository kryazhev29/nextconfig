param([string]$Output="$PSScriptRoot\..\..\Стенд_Domovoi\NextConfig_QA\2026-10-22-wifi-scan",
    [string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe",[switch]$Dpi120)
# Окно выбора сети Wi-Fi в демо (22.10.2026): поиск, список с подключённой сетью первой, выбор строки и поле пароля.
# Снимки через канал --agent; пароль канал не вводит. Запускает свой экземпляр и закрывает его.
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot
$scripts=Join-Path (Split-Path $repo) 'Стенд_Domovoi\Скрипты'
$agent=Join-Path $repo 'tools\nc_agent.ps1'
if(Get-Process NextConfig -ErrorAction SilentlyContinue) {throw 'NextConfig уже открыт: окно владельца не трогаем'}
$scale=if($Dpi120){'125'}else{'100'}
$out=Join-Path $Output "picker-$scale"
New-Item -ItemType Directory -Force -Path $out | Out-Null
function Agent([string]$cmd,[string[]]$rest){
    $r=& $agent $cmd @rest | ConvertFrom-Json
    if(-not $r.ok){throw "agent $cmd $($rest -join ' '): $($r.error)"}
    $r
}
function Snap([string]$name){
    $r=Agent snap @('', (Join-Path $out "$name.png"))
    Write-Output "$name <- $($r.form)"
}
$arguments=@('--demo','--demo-link=wifi','--agent',$(if($Dpi120){'--demo-dpi=120'}else{'--demo-dpi=96'}))
$p=Start-Process (Resolve-Path $AssemblyPath).Path -ArgumentList $arguments -PassThru
try {
    Start-Sleep -Seconds 2
    & "$scripts\ui_text.ps1" -Index 1 -Text '4321' | Out-Null
    Agent click @('Подключиться') | Out-Null
    Start-Sleep -Seconds 2
    Agent click @('Настройки') | Out-Null
    Start-Sleep -Milliseconds 500
    Agent click @('485 и Wiegand') | Out-Null
    Start-Sleep -Seconds 4   # первый ответ 113 демо включает «Выбрать сеть…»
    Snap 'settings-wifi'
    Agent click @('wifiWrite') | Out-Null
    Start-Sleep -Milliseconds 600
    Snap 'picker-searching'
    Start-Sleep -Seconds 2
    Snap 'picker-list'
    Agent click @('TP-Link_15CB','frmWifiPicker') | Out-Null
    Start-Sleep -Milliseconds 500
    Snap 'picker-selected'
    Agent click @('Другая сеть…','frmWifiPicker') | Out-Null
    Start-Sleep -Milliseconds 500
    Snap 'picker-hidden'
    Agent close @('frmWifiPicker') | Out-Null
    Start-Sleep -Milliseconds 500
    "PASS wifi-picker-demo-ui: $out"
} finally {
    if(-not $p.HasExited){Stop-Process -Id $p.Id -Force}
}

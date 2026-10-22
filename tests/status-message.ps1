param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
# Строка состояния по образцу Алексея (Max, 21.10.2026 13:57): «дата, время: операция - итог»,
# вспышка белым по тёмно-синему. Без COM и без окон программы.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:n=0
function Check($v,$label){if(-not $v){throw "FAIL: $label"};$script:n++}
$time=New-Object DateTime 2026,10,21,11,55,59
Check ([NextConfig.StatusMessage]::Line('Проверка сервера','Соединение завершено',$time) -eq '21.10.2026, 11:55:59: Проверка сервера - Соединение завершено') 'sample format'
Check ([NextConfig.StatusMessage]::Line('Сохранение настроек',"Настройки записаны.`r`n  Плата перезагрузится.",$time) -eq '21.10.2026, 11:55:59: Сохранение настроек - Настройки записаны. Плата перезагрузится.') 'lines joined'
Check ([NextConfig.StatusMessage]::Line('','Готово',$time) -eq '21.10.2026, 11:55:59: Готово') 'no title'
$time=New-Object DateTime 2026,1,2,3,4,5
Check ([NextConfig.StatusMessage]::Line('Объект','Записано',$time) -eq '02.01.2026, 03:04:05: Объект - Записано') 'leading zeros'
$bar=New-Object Windows.Forms.Panel
$label=New-Object Windows.Forms.Label
$bar.Controls.Add($label)
try {
    $status=New-Object NextConfig.StatusMessage($bar,$label,[Windows.Forms.Label[]]@())
    $status.Show('Очистка памяти','Память очищена')
    Check ($label.Text -match '^\d\d\.\d\d\.\d{4}, \d\d:\d\d:\d\d: Очистка памяти - Память очищена$') 'shown with date, time and operation'
    Check ($status.Flashing -and $bar.BackColor -eq [NextConfig.Theme]::BrandEnd -and $label.ForeColor.ToArgb() -eq [Drawing.Color]::White.ToArgb()) 'flash white on dark blue'
} finally { $bar.Dispose() }
Write-Output "PASS: $script:n status line checks; no COM opened"

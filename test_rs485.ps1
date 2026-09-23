# Доработка: Кряжев А., 2026. Проверка сериализации без COM-порта.
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\NextConfig\bin\Release\NextConfig.exe")
$config = New-Object NextConfig.clOpros+s_config
$config.server1 = '192.0.2.1:15010'
$config.countryCode = '+7'
$config.useServer1 = 1
$config.rs485Mode = 1
$raw = [NextConfig.clOpros]::EncodeConfig($config)
foreach ($i in (175..177) + (221..511)) { $raw[$i] = [byte](($i * 17) % 256) }
$raw[39] = 67 # ненулевой хвост после нуля строки также сохраняется
$config.rawConfig = $raw
$unchanged = [NextConfig.clOpros]::EncodeConfig($config)
for ($i = 0; $i -lt 512; $i++) { if ($unchanged[$i] -ne $raw[$i]) { throw "Изменён исходный байт $i" } }
$config.rs485Baud = 4
$config.wiegandType = 255
$p = $config.periph1; $p.rs485Adr = 0; $p.rs485Type = 255; $config.periph1 = $p
$new = [NextConfig.clOpros]::EncodeConfig($config)
$allowed = @(174,178,180)
for ($i = 0; $i -lt 512; $i++) { if ($i -notin $allowed -and $new[$i] -ne $raw[$i]) { throw "Затронут чужой байт $i" } }
if ($new[174] -ne 4 -or $new[178] -ne 255 -or $new[180] -ne 255) { throw 'Неверное смещение полей' }
if ($raw[174] -ne 0) { throw 'Сериализация изменила исходный массив' }
$names = New-Object NextConfig.PeripheryNames
if ($names.Name('types',255) -ne '255' -or $names.Name('baud',4) -ne '9600') { throw 'INI / встроенные имена' }
'PASS: unchanged config, reserved bytes, string tails, baud offset174, unknown255, fallback names; no COM'

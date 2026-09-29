param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:checks = 0
function Check($ok, $label) {
    if (-not $ok) { throw "FAIL: $label" }
    $script:checks++
}
function Reject([scriptblock]$action, $label) {
    $failed = $false
    try { & $action | Out-Null } catch { $failed = $true }
    Check $failed $label
}
function Field($table, $name) {
    $rows = @($table.Rows | Where-Object { $_['Параметр'] -eq $name })
    if ($rows.Count -ne 1) { throw "Expected unique field: $name" }
    return $rows[0]['Значение']
}

# Без создания clOpros/Form1 и без открытия SerialPort.
$id = [NextConfig.Demo]::OldFirmwareReply(0, $null)
Check ([NextConfig.OldFirmware]::IsFirmware($id)) 'v5 identified'
Check (-not [NextConfig.OldFirmware]::IsBootloader($id)) 'application is not bootloader'
$boot = [byte[]](0x55, 0, 0, 0x21, 139, 48, 139, 0, 0, 0)
Check ([NextConfig.OldFirmware]::IsBootloader($boot)) 'old boot 2.x'
$boot[3] = 0x32
Check ([NextConfig.OldFirmware]::IsBootloader($boot)) 'NEXT boot 0x32'
$id[3] = 0x32
Check ([NextConfig.OldFirmware]::IsFirmware($id)) 'application discriminator independent of boot version'
$bad = [byte[]]$id.Clone(); $bad[2] = 0
Check (-not [NextConfig.OldFirmware]::IsFirmware($bad)) 'wrong mode rejected'
Check (-not [NextConfig.OldFirmware]::IsFirmware([byte[]]$id[0..24])) 'truncated identity'
Check (-not [NextConfig.OldFirmware]::IsFirmware($null)) 'no identity'
$bad = [byte[]]$id.Clone(); $bad[6] = 0x0B
Check (-not [NextConfig.OldFirmware]::IsFirmware($bad)) 'other device family excluded'

$reader = [NextConfig.OldFirmware]::new($null, $id, $true)
$cfg = [NextConfig.Demo]::OldFirmwareReply(0x20, [byte[]](0))
# Независимые граничные значения по structures.h:343-345,360,376,404-406.
$cfg[15] = 0x34; $cfg[16] = 0x12
$cfg[17] = 0x78; $cfg[18] = 0x56
$cfg[166] = 0xFD
$cfg[516] = 0xCD; $cfg[517] = 0xAB
$table = [NextConfig.OldFirmware]::ParseSettings($cfg)
Check ((Field $table 'Пароль администратора') -eq '4660') 'admin LE offset 9'
Check ((Field $table 'Пароль управляющего / SMS') -eq '22136') 'user LE offset 11'
Check ((Field $table 'Часовой пояс') -eq '-3') 'signed timezone'
Check ((Field $table 'CRC конфигурации') -eq '0xABCD') 'last config field'
Check ((Field $table 'Резерв, байты 195–509').Split(' ').Count -eq 315) 'complete reserved region'
Check (@($table.Rows | Where-Object { $_['Раздел'] -like 'Расписание *' }).Count -eq 21) 'all seven schedules'
Reject { [NextConfig.OldFirmware]::ParseSettings([byte[]]$cfg[0..516]) } 'truncated config'
$bad = [byte[]]$cfg.Clone(); $bad[4] = 1
Reject { [NextConfig.OldFirmware]::ParseSettings($bad) } 'wrong declared config length'
$bad = [byte[]]$cfg.Clone(); $bad[3] = 1
Reject { [NextConfig.OldFirmware]::ParseSettings($bad) } 'wrong config block'
$bad = [byte[]]$cfg.Clone(); $bad[2] = 5
Reject { [NextConfig.OldFirmware]::ParseSettings($bad) } 'config refused'

$status = [NextConfig.Demo]::OldFirmwareReply(0x22, [byte[]](0))
$status[66] = 0xFB # DEVICE temperature at 61
$status[84] = 0x34; $status[85] = 0x12 # logscount at 79
$table = [NextConfig.OldFirmware]::ParseStatus($status)
Check ((Field $table 'Температура, °C') -eq '-5') 'signed temperature'
Check ((Field $table 'Счётчик записей (служебный)') -eq '4660') 'log count offset 79'
Check ((Field $table 'Вход 1 / вход 2 / выход') -eq 'Да / Нет / Нет') 'input bitfields'
Check ((Field $table 'Внешнее питание, мВ') -eq '12150') 'supply LE offset 270'
Check ((Field $table 'Размер DEVICE, байт') -eq '513') 'actual DEVICE length'
Reject { [NextConfig.OldFirmware]::ParseStatus([byte[]]$status[0..10]) } 'short monitoring packet'
$bad = [byte[]]$status.Clone(); $bad[3] = 0
Reject { [NextConfig.OldFirmware]::ParseStatus($bad) } 'monitoring length mismatch'
$bad = [byte[]]$status.Clone(); $bad[2] = 1
Reject { [NextConfig.OldFirmware]::ParseStatus($bad) } 'unknown monitoring block'

Check ([NextConfig.OldFirmware]::ParseAuthentication([byte[]](0x55,0x25,0,0x20)) -eq 0x20) 'admin authorization'
Check ([NextConfig.OldFirmware]::ParseAuthentication([byte[]](0x55,0x25,0,0x10)) -eq 0x10) 'manager authorization'
Reject { [NextConfig.OldFirmware]::ParseAuthentication([byte[]](0x55,0x25,3,0)) } 'wrong password'
Reject { [NextConfig.OldFirmware]::ParseAuthentication([byte[]](0x55,0x25,0)) } 'short authorization'

$numbers = $reader.ReadNumbers()
Check ($numbers.Rows.Count -eq 35) 'complete database across two blocks'
Check ($numbers.Rows[1]['Номер / ID'] -eq '0000000001') 'ID leading zeros preserved'
Check ($numbers.Rows[0]['Имя'] -eq 'Администратор') 'CP1251 names decoded'
Check ($numbers.Rows[2]['Тип'] -eq 'RFID') 'ID type flags'
Check ($numbers.Rows[34]['№'] -eq '35') 'final block included'
$block = [NextConfig.Demo]::OldFirmwareReply(0x2C, [byte[]](1,0,0))
Check ($block.Length -gt 592) 'legacy firmware block exceeds boot frame limit'
Reject { [NextConfig.OldFirmware]::ParseNumbers($block, 1, 35) } 'wrong database index'
Reject { [NextConfig.OldFirmware]::ParseNumbers($block, 0, 1) } 'database exceeds advertised count'
Reject { [NextConfig.OldFirmware]::ParseNumbers([byte[]]$block[0..($block.Length-2)], 0, 35) } 'truncated database block'
Reject { [NextConfig.OldFirmware]::ParseNumbers([byte[]](0x55,0x2C,1,0,0,0,0), 0, 35) } 'zero-progress block'

$path = Join-Path ([IO.Path]::GetTempPath()) ('oldfw-test-' + [guid]::NewGuid() + '.csv')
try {
    $numbers.Rows[0]['Имя'] = "Иван; `"тест`"`r`nстрока"
    $numbers.Rows[1]['Имя'] = '=1+1'
    [NextConfig.OldFirmware]::SaveCsv($numbers, $path)
    $bytes = [IO.File]::ReadAllBytes($path)
    Check ($bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) 'UTF8 BOM'
    $csv = @(Import-Csv $path -Delimiter ';' -Encoding UTF8)
    Check ($csv.Count -eq 35) 'CSV row count'
    Check ($csv[0].Имя -eq $numbers.Rows[0]['Имя']) 'CSV quotes, semicolon and newline roundtrip'
    Check ($csv[1].Имя -eq "'=1+1") 'spreadsheet formula escaped'
    Check ($csv[1].'Номер / ID' -eq '0000000001') 'CSV ID exact'
} finally { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }

$flags = [Reflection.BindingFlags]'NonPublic,Static'
$transport = [NextConfig.clBoot].GetMethod('ReadOldFirmware', $flags)
foreach ($cmd in @(0x21,0x26,0x27,0x28,0x2A,0x2B,0x2D,0x2E,0x2F,0x04,0x40,0x41,0x61)) {
    try { $transport.Invoke($null, @($null,[byte]$cmd,$null,'')); throw 'unexpected allowed command' }
    catch {
        Check ($_.Exception.InnerException -is [InvalidOperationException]) "write/non-read command $cmd blocked before port access"
    }
}
# Известный Modbus CRC и кадр SLGT: тест существующего транспорта, без COM.
$crc = [NextConfig.clBoot].GetMethod('Crc16', $flags)
Check ($crc.Invoke($null, (,[Text.Encoding]::ASCII.GetBytes('123456789'))) -eq 0x4B37) 'CRC16 known vector'
$frame = [NextConfig.clBoot].GetMethod('Frame', $flags).Invoke($null, @([byte]0,$null))
Check ([BitConverter]::ToString($frame) -eq '53-4C-47-54-02-00-AA-00-7F-10') 'identification frame exact'
$reader.CancelRead()
Reject { $reader.ReadNumbers() } 'cancellation before any next packet'
"PASS: $script:checks checks; no COM ports opened."

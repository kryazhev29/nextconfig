param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:count=0
function Check($condition,$name) {if(-not $condition){throw "FAIL: $name"};$script:count++}
function Reject([scriptblock]$action,$name){$failed=$false;try{& $action | Out-Null}catch{$failed=$true};Check $failed $name}
$id=[NextConfig.Demo]::OldFirmwareReply(0,$null)
$reader=[NextConfig.OldFirmware]::new($null,$id,$true)
$locked=[NextConfig.OldFirmware]::new($null,$id,$false)
$denied=$false
try{$locked.RequireAdministrator()}catch{$denied=$_.Exception.InnerException -is [UnauthorizedAccessException]}
Check $denied 'explicit administrator authentication required before port access'
$table=$reader.ReadSettings()
$before=$reader.SettingsSnapshot()
$fields=[NextConfig.OldSettings]::Fields
Check ($table.Rows.Count -eq $fields.Count) 'schema matches all parsed fields'
# Unchanged field roundtrip must preserve every byte apart from CRC.
for($i=0;$i -lt $fields.Count;$i++) {
 if(-not $fields[$i].Editable){continue}
 $raw=[NextConfig.OldSettings]::RawValue($before,$i)
 $copy=[NextConfig.OldSettings]::Change($before,$i,$raw)
 Check ([Linq.Enumerable]::SequenceEqual([byte[]]$before,[byte[]]$copy)) "roundtrip field $i"
}
function Index($name){for($i=0;$i -lt $table.Rows.Count;$i++){if($table.Rows[$i][1] -eq $name){return $i}};throw $name}
$padded=$before.Clone();$padded[50]=167
$unchanged=[NextConfig.OldSettings]::Change($padded,(Index 'Точка доступа APN'),'internet')
Check ([Linq.Enumerable]::SequenceEqual([byte[]]$padded,[byte[]]$unchanged)) 'unchanged strings preserve padding'
foreach($version in @(45,47,48)){
 $known=$id.Clone();$known[5]=$version
 Check ([NextConfig.OldFirmware]::new($null,$known,$true).CanWrite) "known firmware $version"
}
$next=$id.Clone();$next[5]=49
Check (-not [NextConfig.OldFirmware]::IsFirmware($next)) 'NEXT 49 excluded'
$after=[NextConfig.OldSettings]::Change($before,(Index 'Пароль администратора'),'45678')
$after=[NextConfig.OldSettings]::Change($after,(Index 'Фотофиксация'),'1')
Check ([BitConverter]::ToUInt16($after,9) -eq 45678) 'password LE'
Check (($after[13] -band 16) -eq 16) 'photo bit set'
Check (($after[13] -band 239) -eq ($before[13] -band 239)) 'other flag bits preserved'
Check ([Linq.Enumerable]::SequenceEqual([byte[]]$before[195..509],[byte[]]$after[195..509])) 'reserved bytes preserved'
$changes=[NextConfig.OldSettings]::Changes($before,$after)
Check (-not $changes.Contains('45678')) 'password absent from preview'
Check (($changes -split "`r`n").Count -eq 2) 'only changed fields'
Reject {[NextConfig.OldSettings]::Change($before,(Index 'Пароль администратора'),'65536')} 'UInt16 overflow'
Reject {[NextConfig.OldSettings]::Change($before,(Index 'Фотофиксация'),'2')} 'bit overflow'
Reject {[NextConfig.OldSettings]::Change($before,(Index 'Точка доступа APN'),('a'*40))} 'string overflow'
Reject {[NextConfig.OldSettings]::Change($before,(Index 'Резерв, байты 195–509'),'0')} 'reserved immutable'
$unknown=$id.Clone();$unknown[5]=13
Check (-not [NextConfig.OldFirmware]::new($null,$unknown,$true).CanWrite) 'unconfirmed firmware write disabled'
$path=Join-Path $env:TEMP ('oldsettings-'+[guid]::NewGuid().ToString('N')+'.bin')
try {
 $reader.WriteSettings($before,$after,$path)
 Check ([Linq.Enumerable]::SequenceEqual([IO.File]::ReadAllBytes($path),$before)) 'backup original'
 Check ([Linq.Enumerable]::SequenceEqual($reader.SettingsSnapshot(),$after)) 'demo write/readback'
 Reject {$reader.WriteSettings($before,$after,$path)} 'stale source blocked'
}finally {if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
"PASS: $script:count settings checks; no COM ports opened."

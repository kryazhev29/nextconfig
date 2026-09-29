param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))|Out-Null
$script:count=0
function Check($v,$n){if(-not $v){throw "FAIL: $n"};$script:count++}
function Reject([scriptblock]$a,$n){$bad=$false;try{& $a|Out-Null}catch{$bad=$true};Check $bad $n}
$id=[NextConfig.Demo]::OldFirmwareReply(0,$null)
$reader=[NextConfig.OldFirmware]::new($null,$id,$true)
Reject {$reader.ReadChronology()} 'chronology unsupported'
Reject {$reader.ReadWorkLog()} 'log unsupported'
Check (-not $reader.AutoregistrationState()) 'initial autoreg off'
$reader.SetAutoregistration($false,$true)
Check ($reader.AutoregistrationState()) 'autoreg on verified'
Reject {$reader.SetAutoregistration($false,$true)} 'stale autoreg rejected'
$reader.SetAutoregistration($true,$false)
Check (-not $reader.AutoregistrationState()) 'autoreg off verified'
$reader.TestOutput()
$status=$reader.ReadStatus()
Check (@($status.Rows | Where-Object {$_[1] -eq 'Выход активен' -and $_[2] -eq 'Да'}).Count -eq 1) 'demo output'
$folder=Join-Path $env:TEMP ('oldservice-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder)|Out-Null
try {
 $reader.ReadSettings()|Out-Null
 $before=$reader.SettingsSnapshot();$base=$reader.ReadNumbers()
 $reader.FactoryReset($before,$base,$folder)
 Check ($reader.ReadNumbers().Rows.Count -eq 0) 'factory clears base'
 Check ((Get-ChildItem -LiteralPath $folder -File).Count -eq 2) 'factory backups settings and residents'
 $factory=[NextConfig.OldFirmware]::FactorySettings(48,139)
 Check ([Linq.Enumerable]::SequenceEqual($factory,$reader.SettingsSnapshot())) 'factory settings verified'
 Check ([BitConverter]::ToUInt16($factory,9) -eq 4321) 'factory admin offset'
 Check ($factory[13] -eq 3 -and $factory[159] -eq 0) 'v5 defaults'
 $legacy=[NextConfig.OldFirmware]::FactorySettings(45,139)
 Check ($legacy[13] -eq 2 -and $legacy[159] -eq 5) 'v1-v3 defaults differ'
 Check ($null -eq [NextConfig.OldFirmware]::FactorySettings(47,139)) '47 defaults not guessed'
 "PASS: $script:count service checks; no COM ports opened."
}finally {
 Get-ChildItem -LiteralPath $folder -File | Remove-Item
 Remove-Item -LiteralPath $folder
}

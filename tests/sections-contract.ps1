param([string]$AssemblyPath="$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:checks=0
function Check($value,$name){if(-not $value){throw "FAIL: $name"};$script:checks++}
$c=New-Object 'NextConfig.clOpros+s_config'
$c.rawConfig=New-Object byte[] 512
for($i=0;$i -lt 512;$i++){$c.rawConfig[$i]=[byte]($i%256)}
[Text.Encoding]::ASCII.GetBytes('4321').CopyTo($c.rawConfig,4)
$table=[NextConfig.ConfigSnapshot]::Parse($c,$false)
$coverage=New-Object int[] 512
foreach($row in $table.Rows){$parts=$row[0].Split([char]0x2013);$start=[int]$parts[0];$end=$start;if($parts.Length -eq 2){$end=[int]$parts[1]};for($i=$start;$i -le $end;$i++){$coverage[$i]++}}
Check (@($coverage | Where-Object {$_ -ne 1}).Count -eq 0) 'all 512 bytes covered exactly once'
Check ($table.Rows[1][2] -eq '••••') 'PIN masked'
Check ([NextConfig.ConfigSnapshot]::Parse($c,$true).Rows[1][2] -eq '4321') 'PIN explicitly revealed'
Check ([NextConfig.ConfigSnapshot]::Parse((New-Object 'NextConfig.clOpros+s_config'),$false).Rows.Count -eq 0) 'no invented packet before read'
$boot=[NextConfig.clBoot]
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$frame=$boot.GetMethod('Frame',$flags).Invoke($null,@([byte]4,[byte[]](1)))
Check ([Text.Encoding]::ASCII.GetString($frame,0,4) -eq 'SLGT') 'shared legacy frame'
Check ([BitConverter]::ToUInt16($frame,4) -eq 3) 'reset body length'
Check ($frame[6] -eq 0xAA -and $frame[7] -eq 4 -and $frame[8] -eq 1) 'reset flag at FW ptr[4]'
Check ([BitConverter]::ToUInt16($frame,9) -eq [NextConfig.ClComPort]::Crc16($frame,6,3)) 'legacy CRC'
$rejected=$false
try {$boot.GetMethod('ReadOldFirmware',$flags).Invoke($null,@($null,[byte]4,[byte[]](1),''))|Out-Null}catch{$rejected=$_.Exception.InnerException -is [InvalidOperationException] -or $_.Exception.InnerException.InnerException -is [InvalidOperationException]}
Check $rejected 'reset cannot enter read whitelist'
$device=[NextConfig.OldFirmware]::new($null,[NextConfig.Demo]::OldFirmwareReply(0,$null),$true)
$device.EnterBootloader()
Check $true 'demo reset needs no port'
Write-Output "PASS: $checks menu/protocol checks; no COM ports opened."

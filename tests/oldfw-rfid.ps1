param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))|Out-Null
$script:count=0
function Check($v,$n){if(-not $v){throw "FAIL: $n"};$script:count++}
function Reject([scriptblock]$a,$n){$bad=$false;try{& $a|Out-Null}catch{$bad=$true};Check $bad $n}
$reader=[NextConfig.OldFirmware]::new($null,[NextConfig.Demo]::OldFirmwareReply(0,$null),$true)
$before=$reader.ReadRfid(0,5)
Check ($before.Value -eq 20 -and $before.ReaderType -eq 0) 'RU5300 read power'
$after=$reader.WriteRfid($before,24)
Check ($after.Value -eq 24) 'RFID write/readback'
Reject {$reader.WriteRfid($before,23)} 'stale RFID value'
$address=$reader.ReadRfid(0,3)
$changed=$reader.WriteRfid($address,7)
Check ($changed.Address -eq 7 -and $changed.Value -eq 7) 'address change read at new address'
Check ($reader.ReadRfid(7,5).Value -eq 24) 'reader params follow address'
Reject {$reader.ReadRfid(255,3)} 'broadcast excluded'
Reject {$reader.ReadRfid(7,10)} 'RU area read unsupported'
Reject {[NextConfig.OldRfid]::ValidateParameter(1,3)} 'R1610 unsupported'
Reject {[NextConfig.OldRfid]::ValidateParameter(3,3)} 'WG unsupported'
Reject {[NextConfig.OldRfid]::ValidateParameter(2,63)} 'PRIME band unsupported'
Reject {[NextConfig.OldRfid]::ValidateValue(0,5,27)} 'RU power range'
Reject {[NextConfig.OldRfid]::ValidateValue(2,5,30)} 'PRIME power range'
Reject {[NextConfig.OldRfid]::ValidateValue(2,10,0)} 'PRIME area reserved'
Reject {[NextConfig.OldRfid]::ValidateValue(0,7,5)} 'baud enum'
Reject {[NextConfig.OldRfid]::Parse([byte[]]@(85,80,3,20),0,5,0)} 'wrong parameter reply'
Reject {[NextConfig.OldRfid]::Parse([byte[]]@(85,80,5),0,5,0)} 'short reply'
# Change demo's reader type through the same settings protocol, then test PRIME.
$table=$reader.ReadSettings();$block=$reader.SettingsSnapshot()
$index=0;for($i=0;$i -lt $table.Rows.Count;$i++){if($table.Rows[$i][1] -eq 'Тип считывателя'){$index=$i}}
$prime=[NextConfig.OldSettings]::Change($block,$index,'2')
$path=Join-Path $env:TEMP ('oldrfid-'+[guid]::NewGuid().ToString('N')+'.bin')
try {
 $reader.WriteSettings($block,$prime,$path)
 $area=$reader.ReadRfid(1,10);Check ($area.ReaderType -eq 2) 'PRIME type from settings'
 Check ($reader.WriteRfid($area,3).Value -eq 3) 'PRIME area write'
}finally {if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path}}
"PASS: $script:count RFID checks; no COM ports opened."

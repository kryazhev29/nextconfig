param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$script:count=0
function Check($v,$n){if(-not $v){throw "FAIL: $n"};$script:count++}
function Reject([scriptblock]$a,$n){$bad=$false;try{& $a|Out-Null}catch{$bad=$true};Check $bad $n}
[NextConfig.Demo].GetField('Mode').SetValue($null,[NextConfig.eDemoMode]::OldFw45)
$reader=[NextConfig.OldFirmware]::new($null,[NextConfig.Demo]::OldFirmwareReply(0,$null),$true)
$before=$reader.ReadNumbers()
Check ($before.Rows.Count -eq 35) 'v1 stale count is refreshed'
$folder=Join-Path $env:TEMP ('oldnumbers-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder)|Out-Null
try {
 $metadata=$before.Copy()
 $row=[NextConfig.OldNumbers]::Record($metadata.Rows[0]);$row[40]=167;$row[32]=$row[32] -bor 4
 $records=[NextConfig.OldNumbers]::Records($metadata);$records[0]=$row
 $metadata=[NextConfig.OldNumbers]::ToTable($records)
 $diff=[NextConfig.OldNumbers]::Changes($before,$metadata)
 Check ($diff.Contains('Резерв записи:') -and $diff.Contains('Изменён:')) 'all changed metadata shown'
 $csv=Join-Path $folder 'export.csv'
 [NextConfig.OldFirmware]::SaveCsv($before,$csv)
 $loaded=[NextConfig.OldNumbers]::LoadCsv($csv,15000)
 Check ([NextConfig.OldNumbers]::Same([NextConfig.OldNumbers]::Records($before),[NextConfig.OldNumbers]::Records($loaded))) 'CSV complete roundtrip'
 Reject {[NextConfig.OldNumbers]::LoadCsv($csv,1)} 'capacity'
 $new=[NextConfig.OldNumbers]::Edit($null,'9123456789','Тест; "CSV"',0,0,0,2,1)
 $reader.ChangeNumber($before,$null,$new,(Join-Path $folder 'add.csv'))
 $now=$reader.ReadNumbers(); Check ($now.Rows.Count -eq 36) 'add'
 $edit=[NextConfig.OldNumbers]::Edit($new,'9123456789','Изменён',0,1,1800000000,7,2)
 $reader.ChangeNumber($now,$new,$edit,(Join-Path $folder 'edit.csv'))
 $now=$reader.ReadNumbers(); Check ($now.Rows.Count -eq 36) 'edit replaces instead of duplicate'
 Check (@($now.Rows | Where-Object {$_[2] -eq 'Изменён'}).Count -eq 1) 'edit readback'
 Reject {$reader.ChangeNumber($before,$null,$new,(Join-Path $folder 'stale.csv'))} 'stale snapshot'
 $reader.ChangeNumber($now,$edit,$null,(Join-Path $folder 'delete.csv'))
 $now=$reader.ReadNumbers(); Check ($now.Rows.Count -eq 35) 'delete'
 $reader.ImportNumbers($now,$loaded,(Join-Path $folder 'import.csv'))
 $now=$reader.ReadNumbers(); Check ([NextConfig.OldNumbers]::Same([NextConfig.OldNumbers]::Records($now),[NextConfig.OldNumbers]::Records($loaded))) 'multi-block import'
 Check (@($now.Rows | Where-Object {$_[8] -ne '0'}).Count -eq 0) 'import passage counts reset'
 $reader.CompressNumbers($now); Check ($reader.ReadNumbers().Rows.Count -eq 35) 'compression'
 $empty=[NextConfig.OldFirmware]::NumbersTable()
 $reader.ImportNumbers($now,$empty,(Join-Path $folder 'empty.csv'))
 Check ($reader.ReadNumbers().Rows.Count -eq 0) 'empty CSV clears base'
 $duplicate=$before.Copy();$duplicate.ImportRow($before.Rows[0]);[NextConfig.OldFirmware]::SaveCsv($duplicate,$csv)
 Reject {[NextConfig.OldNumbers]::LoadCsv($csv,15000)} 'duplicate CSV'
 $bad=$before.Copy();$bad.Rows[0][2]='not raw';[NextConfig.OldFirmware]::SaveCsv($bad,$csv)
 Reject {[NextConfig.OldNumbers]::LoadCsv($csv,15000)} 'ambiguous edited CSV'
 Reject {[NextConfig.OldNumbers]::Edit($null,'9123456789','test',-1,0,0,0,0)} 'unselected type'
 Reject {[NextConfig.OldNumbers]::Edit($null,'9123456789','test',0,-1,0,0,0)} 'unselected role'
 Reject {[NextConfig.OldNumbers]::Edit($null,'9123456789',('a'*20),0,0,0,0,0)} 'name overflow'
 $raw=[NextConfig.OldNumbers]::Record($before.Rows[0]);$raw[0]=255
 Reject {[NextConfig.OldFirmware]::ParseNumbers([byte[]](@(85,44,1,0,0,1,0)+$raw),0,1)} 'v1 stale invalid cell'
 "PASS: $script:count database checks; no COM ports opened."
}finally {
 Get-ChildItem -LiteralPath $folder -File | Remove-Item
 Remove-Item -LiteralPath $folder
}

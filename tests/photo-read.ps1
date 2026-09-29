param([string]$Output = "$PSScriptRoot\..\..\Стенд_Domovoi\NextConfig_QA\2026-09-29-photo-menu\photo-tests")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")) | Out-Null
$script:checks=0
function Check($value,$label) { if (-not $value) {throw "FAIL: $label"}; $script:checks++ }
function Reject([scriptblock]$action,$label) { $failed=$false;try { & $action | Out-Null } catch {$failed=$true};Check $failed $label }
$run=Join-Path $Output (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
$token=[Threading.CancellationToken]::None
$wire=[NextConfig.PhotoDemoWire]::new('loss')
$session=[NextConfig.PhotoSession]::new($wire,$null)
$result=$session.Download('000000000000071',(Join-Path $run 'loss'),$token)
Check ($result.Saved.Count -eq 2) 'two JPEGs after gap'
Check ($result.Retries -gt 0) 'lost packet retried'
Check ($result.Duplicates -gt 0) 'duplicate checked'
foreach($file in $result.Saved) { [NextConfig.PhotoTransfer]::ValidateJpeg([IO.File]::ReadAllBytes($file)); Check ((Get-Item $file).Length -gt 512) 'decoded JPEG' }
$empty=[NextConfig.PhotoSession]::new($wire,$null).Download('000000000000071',(Join-Path $run 'loss'),$token)
Check ($empty.Saved.Count -eq 0) 'fully acknowledged queue empty'
Check ($wire.Acknowledgements.Contains(512)) 'gap cursor confirmed only durable prefix'
[NextConfig.PhotoSession]::new($wire,$null).Capture(1,$token)
$captured=[NextConfig.PhotoSession]::new($wire,$null).Download('000000000000071',(Join-Path $run 'capture'),$token)
Check ($captured.Saved.Count -eq 1) 'capture appends new photo'
foreach($mode in @('bad-jpeg','missing-prefix','updating')) {
  $w=[NextConfig.PhotoDemoWire]::new($mode)
  Reject { [NextConfig.PhotoSession]::new($w,$null).Download('000000000000071',(Join-Path $run $mode),$token) } $mode
  if($mode -eq 'updating') {Check (-not $w.Commands.Contains(21)) 'update blocks request';Reject {[NextConfig.PhotoSession]::new($w,$null).Capture(1,$token)} 'update blocks capture'; Check (-not $w.Commands.Contains(7)) 'no capture sent'}
  if($mode -eq 'bad-jpeg') {Check ($w.Acknowledgements.Count -eq 1) 'bad JPEG not acknowledged'}
}
$noCamera=[NextConfig.PhotoDemoWire]::new('no-camera')
Reject {[NextConfig.PhotoSession]::new($noCamera,$null).Capture(1,$token)} 'camera hint on refusal'
$frame=[NextConfig.SldnStream]::Frame(21,[byte[]](0,0,0,0,0,0),5)
$decoder=[NextConfig.SldnStream]::new()
$decoded=0
foreach($b in $frame) { $decoded+=$decoder.Feed([byte[]]($b)).Count }
Check ($decoded -eq 1) 'byte fragmentation'
$corrupt=$frame.Clone();$corrupt[9]=1
$both=[byte[]]($corrupt+$frame+$frame)
$decoder=[NextConfig.SldnStream]::new()
Check ($decoder.Feed($both).Count -eq 2) 'CRC rejection and concatenated recovery'
Check ($decoder.BadFrames -eq 1) 'bad CRC counted'
Check ([NextConfig.Demo]::OldFirmwareReply(0x30,$null)[2] -eq 250) 'old photo unsupported'
Check ($null -eq [NextConfig.Demo]::OldFirmwareReply(0x31,$null)) 'old info absent'
Check ($null -eq [NextConfig.Demo]::OldFirmwareReply(0x32,$null)) 'old ack absent'
# Resume after confirmed prefix survives a new receiver instance.
function Packet([uint32]$n,[byte[]]$jpeg,[int]$offset,[int]$count) {
    return [byte[]](@(1)+[BitConverter]::GetBytes($n)+[BitConverter]::GetBytes([uint32]$jpeg.Length)+[BitConverter]::GetBytes([uint16]$offset)+$jpeg[$offset..($offset+$count-1)])
}
$jpeg=[NextConfig.PhotoDemoWire]::Jpeg(900)
$resumeFolder=Join-Path $run 'resume'
$first=[NextConfig.PhotoTransfer]::new('71',$resumeFolder)
$first.Receive((Packet 900 $jpeg 0 512)) | Out-Null
Check ((Get-Item (Join-Path $resumeFolder '71_900.partial')).Length -eq 512) 'prefix on disk before ack'
$second=[NextConfig.PhotoTransfer]::new('71',$resumeFolder)
for($off=512;$off -lt $jpeg.Length;$off+=512) { $second.Receive((Packet 900 $jpeg $off ([Math]::Min(512,$jpeg.Length-$off)))) | Out-Null }
Check ($second.Saved.Count -eq 1) 'resume from existing partial'
$clash=[NextConfig.PhotoTransfer]::new('71',(Join-Path $run 'clash'))
$block=Packet 900 $jpeg 0 512
$clash.Receive($block) | Out-Null
$block[30]=$block[30] -bxor 1
Reject {$clash.Receive($block)} 'conflicting duplicate rejected'
$collision=[NextConfig.PhotoTransfer]::new('71',$resumeFolder)
$other=[NextConfig.PhotoDemoWire]::Jpeg(901)
Reject {for($off=0;$off -lt $other.Length;$off+=512) {$collision.Receive((Packet 900 $other $off ([Math]::Min(512,$other.Length-$off)))) | Out-Null }} 'existing different JPEG preserved'
Check ([Linq.Enumerable]::SequenceEqual([byte[]]$jpeg,[IO.File]::ReadAllBytes((Join-Path $resumeFolder '71_900.jpg')))) 'original JPEG unchanged'
Write-Output "PASS: $checks checks; $run"

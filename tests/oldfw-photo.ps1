param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$assembly = (Resolve-Path $AssemblyPath).Path
[Reflection.Assembly]::LoadFrom($assembly) | Out-Null
$ref = Join-Path $env:TEMP 'NextConfig-photo-test.dll'
Copy-Item -LiteralPath $assembly -Destination $ref -Force
Add-Type -ReferencedAssemblies $ref -TypeDefinition @'
using System;
using System.Threading;
using System.Collections.Generic;
using NextConfig;
public class OldPhotoTestWire : IPhotoWire {
 public Queue<byte[]> Frames = new Queue<byte[]>();
 public void Send(byte c, byte[] d, byte s) { if(c!=48 || d.Length!=3 || d[0]!=0) throw new Exception("request"); }
 public byte[] Receive(int timeout, CancellationToken cancel) { return Frames.Count==0 ? null : Frames.Dequeue(); }
}
'@
Remove-Item -LiteralPath $ref
$token = [Threading.CancellationToken]::None
$noState = $null  # фотофиксация неизвестна: настройки не читались
function Reject($wire, $label) {
    $failed = $false
    try { [NextConfig.OldPhotoSession]::Capture($wire,0,5,$token,$noState,$null) | Out-Null } catch { $failed=$true }
    if (-not $failed) { throw "FAIL $label" }
}
# Снимок возвращается в памяти, файл не пишется; ход приёма доходит до конца.
$script:last = @(0,0)
$progress = [Action[long,long]]{ param($got,$total) $script:last = @($got,$total) }
$jpeg = [NextConfig.OldPhotoSession]::Capture([NextConfig.OldPhotoDemoWire]::new(),0,5,$token,$noState,$progress)
if ($jpeg[0] -ne 255 -or $jpeg[1] -ne 216 -or $jpeg[-1] -ne 217) { throw 'JPEG' }
if ($script:last[0] -ne $jpeg.Length -or $script:last[1] -ne $jpeg.Length) { throw 'progress' }
# Код 250: три разных объяснения по тому, что известно о фотофиксации.
$texts = @([NextConfig.OldPhotoSession]::Refused250($noState), [NextConfig.OldPhotoSession]::Refused250($true), [NextConfig.OldPhotoSession]::Refused250($false))
if (($texts | Select-Object -Unique).Count -ne 3) { throw '250 texts' }
 foreach($code in 1,2,3,4,5,250) {
  $wire=[OldPhotoTestWire]::new(); $wire.Frames.Enqueue([byte[]]@($code)); Reject $wire "one-byte $code"
 }
 $wire=[OldPhotoTestWire]::new(); $wire.Frames.Enqueue([byte[]]@(0,4,0,0,0)); $wire.Frames.Enqueue([byte[]]@(1,4,0,1,2,3,4)); $wire.Frames.Enqueue([byte[]]@(10)); Reject $wire 'bad JPEG'
 $wire=[OldPhotoTestWire]::new(); $wire.Frames.Enqueue([byte[]]@(0,4,0,0,0)); $wire.Frames.Enqueue([byte[]]@(10)); Reject $wire 'truncated'
 $wire=[OldPhotoTestWire]::new(); $wire.Frames.Enqueue([byte[]]@(1,1,0,0)); Reject $wire 'missing length'
 $wire=[OldPhotoTestWire]::new(); Reject $wire 'timeout'
 $wire=[NextConfig.OldPhotoDemoWire]::new($false); Reject $wire 'v5'
 # Independent CRC fixture, fragmented and coalesced frames.
 function Frame([byte[]]$body) {
  $crc=65535
  foreach($b in $body) { $crc=$crc -bxor $b; 1..8 | ForEach-Object { if($crc -band 1){$crc=($crc -shr 1) -bxor 0xA001}else{$crc=$crc -shr 1} } }
  return [byte[]](@(83,76,71,84,$body.Length,0) + $body + @(($crc -band 255),($crc -shr 8)))
 }
 $a=Frame ([byte[]]@(85,48,0,4,0,0,0)); $b=Frame ([byte[]]@(85,48,10))
 $pending=[Collections.Generic.List[byte]]::new()
 if([NextConfig.OldPhotoWire]::Decode($pending,[byte[]]$a[0..3]).Count -ne 0){throw 'fragment'}
 if([NextConfig.OldPhotoWire]::Decode($pending,[byte[]]($a[4..($a.Length-1)]+$b)).Count -ne 2){throw 'coalesced'}
 $b[-1]=$b[-1] -bxor 1
 $failed=$false; try{[NextConfig.OldPhotoWire]::Decode($pending,$b)|Out-Null}catch{$failed=$true}
 if(-not $failed){throw 'CRC'}
 'PASS: JPEG in memory, progress, 250 texts, stream fragmentation/coalescing, CRC, errors 1-5/250, truncation, order, timeout, v5'

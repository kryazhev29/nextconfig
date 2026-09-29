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
$folder = Join-Path ([IO.Path]::GetTempPath()) ('oldphoto-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
$token = [Threading.CancellationToken]::None
function Reject($wire, $label, $enabled=$true) {
    $failed = $false
    try { [NextConfig.OldPhotoSession]::Capture($wire,$folder,'demo',0,5,$token,$enabled) | Out-Null } catch { $failed=$true }
    if (-not $failed) { throw "FAIL $label" }
}
try {
 $file = [NextConfig.OldPhotoSession]::Capture([NextConfig.OldPhotoDemoWire]::new(),$folder,'00045',0,5,$token)
 $jpeg = [IO.File]::ReadAllBytes($file)
 if ($jpeg[0] -ne 255 -or $jpeg[1] -ne 216 -or $jpeg[-1] -ne 217) { throw 'JPEG' }
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
 'PASS: JPEG, stream fragmentation/coalescing, CRC, errors 1вЂ“5/250, truncation, order, timeout, v5'
} finally {
 # Delete only files produced by this test in its unique directory.
 Get-ChildItem -LiteralPath $folder -File | Remove-Item
 Remove-Item -LiteralPath $folder
}

param([Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$checks=0
foreach($old in @($true,$false)) {
 foreach($code in @(0,1,2,4,5,254,255)) {
  $payload=[byte[]](@($code)+[BitConverter]::GetBytes([uint32]0x78563412)+[BitConverter]::GetBytes([uint16]2048))
  if($old){$payload=[byte[]](@(21)+$payload)}
  $wire=[NextConfig.SldnStream]::Frame(11,$payload,42)
  $request=[NextConfig.SldnStream]::Command(21,[byte[]](0,0,0,0,0,0),42)
  if(-not [NextConfig.SldnStream]::MatchesReply($request,$wire)){throw 'ACK rejected'}
  if([NextConfig.SldnStream]::AnswerCode($wire) -ne $code){throw 'wrong result'}
  foreach($chunk in @(1,2,7,64)){
   $decoder=New-Object NextConfig.SldnStream
   $joined=[byte[]]($wire+$wire);$count=0
   for($i=0;$i -lt $joined.Length;$i+=$chunk){$end=[Math]::Min($i+$chunk,$joined.Length)-1;$count+=$decoder.Feed([byte[]]$joined[$i..$end]).Count}
   if($count -ne 2){throw 'fragmented/glued replies lost'}
   $checks++
  }
  $wire[7]=41
  if([NextConfig.SldnStream]::MatchesReply($request,$wire)){throw 'late ACK accepted'}
 }
}
foreach($n in @(0,1,2,6,9)) {
 $payload=New-Object byte[] $n;$wire=[NextConfig.SldnStream]::Frame(11,$payload,42)
 if([NextConfig.SldnStream]::IsAnswer($wire,21)){throw 'invalid ACK length accepted'}
 $checks++
}
"PASS: $checks old/new answer11 transport cases; no ports opened."

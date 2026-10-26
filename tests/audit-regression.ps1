param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Review\NextConfig.exe")
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$static = [Reflection.BindingFlags]'Static,NonPublic,Public'
$script:checks = 0
function Check($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; $script:checks++ }
function Reject([scriptblock]$action, $name) { $failed=$false; try { & $action | Out-Null } catch { $failed=$true }; Check $failed $name }
function Control($form,$name) { $form.GetType().GetProperty($name,$flags).GetValue($form,$null) }
function Invoke($object,$name,[object[]]$parameters=@()) { try { $object.GetType().GetMethod($name,$flags).Invoke($object,$parameters) } catch { throw ("Method " + $name + ": " + $_.Exception.GetBaseException().ToString()) } }

# Never construct clOpros: its constructor starts scanning real ports.
$poller = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$pause = [NextConfig.clOpros].GetField('_isPause',$flags)
$empty = New-Object 'NextConfig.clOpros+s_config'
Check (-not $poller.SetConfig($empty)) 'unread configuration rejected before transport'
$empty.rawConfig = New-Object byte[] 511
Check (-not $poller.SetConfig($empty)) 'incomplete original configuration rejected'
foreach ($name in @('SetOut','ResetController','SetDefault','SetTransparentMode','SetTime','Autorize','SetModuleType','EnableLog','StartUpdate','WriteFWBlock','BootManual')) {
    $method = [NextConfig.clOpros].GetMethod($name,$flags)
    $arguments = @($method.GetParameters() | ForEach-Object {
        $t=$_.ParameterType; if($t.IsByRef){$t=$t.GetElementType()}
        if($t -eq [byte[]]){ ,(New-Object byte[] 1024) }
        elseif($t.IsValueType){[Activator]::CreateInstance($t)} else {$null}
    })
    Reject { $method.Invoke($poller,$arguments) } "$name missing transport"
    Check (-not $pause.GetValue($poller)) "$name releases pause after exception"
}

$request = [NextConfig.SldnStream]::Command(2,[byte[]]@(0),42)
Check ([NextConfig.SldnStream]::MatchesReply($request,[NextConfig.SldnStream]::Frame(11,[byte[]]@(2,0,0,0,0,0,0,8),42))) 'matching reset ACK'
Check (-not [NextConfig.SldnStream]::MatchesReply($request,[NextConfig.SldnStream]::Frame(11,[byte[]]@(3,0,0,0,0,0,0,8),42))) 'foreign command ACK rejected'
Check (-not [NextConfig.SldnStream]::MatchesReply($request,[NextConfig.SldnStream]::Frame(11,[byte[]]@(2,0,0,0,0,0,0,8),41))) 'late ACK sequence rejected'
Check (-not [NextConfig.SldnStream]::MatchesReply($request,[NextConfig.SldnStream]::Frame(11,[byte[]]@(2),42))) 'CRC bytes cannot supply ACK status'
Check (-not [NextConfig.SldnStream]::MatchesReply($request,[NextConfig.SldnStream]::Frame(2,$null,42))) 'reset requires ACK'
foreach($cmd in @(100,101,106)) {
    $payload = New-Object byte[] 512
    Check ([NextConfig.SldnStream]::MatchesReply([NextConfig.SldnStream]::Command($cmd,$null,42),[NextConfig.SldnStream]::Frame($cmd,$payload,0))) "legacy zero sequence $cmd"
}
$first = Invoke $poller 'SetCmd' @([byte]23,$null)
$second = Invoke $poller 'SetCmd' @([byte]23,$null)
Check ($first[7] -ne $second[7]) 'requests receive distinct sequences'

# Exercise the real stream decoder with fragmented reads, noise and foreign frames.
Add-Type -TypeDefinition @'
using System;
using System.IO;
public class FragmentedAuditStream : MemoryStream {
    readonly int fragment;
    public FragmentedAuditStream(byte[] bytes, int size) : base(bytes) { fragment = size; }
    public override int Read(byte[] buffer,int offset,int count) { return base.Read(buffer,offset,Math.Min(count,fragment)); }
}
'@
$serverRequest = [NextConfig.SldnStream]::Command(17,(New-Object byte[] 15),0)
$body = New-Object byte[] 281; $body[0]=3
[Text.Encoding]::ASCII.GetBytes('12345678').CopyTo($body,1)
$reply = [NextConfig.SldnStream]::Frame(17,$body,0)
$wrong = [NextConfig.SldnStream]::Frame(18,[byte[]]@(2),0)
$bad = [byte[]]$reply.Clone(); $bad[-1] = $bad[-1] -bxor 1
foreach($size in @(1,2,5,26,128,1024)) {
    $stream = New-Object FragmentedAuditStream([byte[]](@(0,1,83)+$bad+$wrong+$reply),$size)
    try { $received=[NextConfig.SldnStream]::ReadReply($stream,$serverRequest,2000); Check ([Convert]::ToBase64String($received) -eq [Convert]::ToBase64String($reply)) "complete validated reply, fragment $size" } finally {$stream.Dispose()}
}
foreach($bytes in @([byte[]]$reply[0..100],[NextConfig.SldnStream]::Frame(17,[byte[]]@(3),0))) {
    $stream = New-Object FragmentedAuditStream($bytes,7)
    try { Reject { [NextConfig.SldnStream]::ReadReply($stream,$serverRequest,2000) } 'truncated reply rejected' } finally {$stream.Dispose()}
}

$f = New-Object NextConfig.Form2($poller,'offline','000000000000000',0)
try {
    Check (-not (Control $f 'btnSave').Enabled) 'save disabled before first read'
    Check (-not (Control $f 'btnDefault').Enabled) 'defaults cannot overwrite unread fields'
    (Control $f 'txtServer1').Text='invalid-server:port'
    Check (-not (Invoke $f 'ValidateFields')) 'invalid hidden server rejected'
    (Control $f 'txtServer1').Text='invalid-server'
    Check (Invoke $f 'ValidateFields') 'host without port takes firmware default port'
    (Control $f 'txtServer1').Text='192.0.2.1:15010'
    Check (Invoke $f 'ValidateFields') 'valid server accepted'
    $config = New-Object 'NextConfig.clOpros+s_config'
    $config.rawConfig=New-Object byte[] 512
    $config.pin=[BitConverter]::ToUInt32([Text.Encoding]::ASCII.GetBytes('0123'),0)
    $f.GetType().GetField('config',$flags).SetValue($f,$config)
    Invoke $f 'ConfirmConfigSnapshot'
    Check ($f.ConfirmedPin -eq '0123') 'confirmed PIN preserves leading zero'
    Check ((Control $f 'btnSave').Enabled) 'read snapshot enables save'
    $config.pin=[BitConverter]::ToUInt32([Text.Encoding]::ASCII.GetBytes('4567'),0)
    $config.opType='apn,user,demo-secret'
    $f.GetType().GetField('config',$flags).SetValue($f,$config)
    $changes = (Invoke $f 'ChangeList') -join ' '
    Check ($changes -notmatch '0123|4567|demo-secret|apn,user') 'confirmation contains no credentials'
    Check ($changes.Contains('ПИН') -and $changes.Contains('APN')) 'masked changes still listed'
    Invoke $f 'ConfirmConfigSnapshot'
    Check ($f.ConfirmedPin -eq '4567') 'successful change refreshes reconnect PIN'
    Check ($f.GetType().GetField('pinCode',$flags).GetValue($f) -eq $config.pin) 'next PIN dialog receives confirmed PIN bits'
    foreach($outgoing in @($true,$false)) {
        $frame = [NextConfig.SldnStream]::Frame($(if($outgoing){12}else{106}),[NextConfig.clOpros]::EncodeConfig($config),0)
        $hex = [NextConfig.AgentLog].GetMethod('SldnHex',$static).Invoke($null,@([byte[]]$frame,$outgoing))
        foreach($secret in @('demo-secret','4567')) {
            $secretHex=[BitConverter]::ToString([Text.Encoding]::ASCII.GetBytes($secret)).Replace('-',' ')
            Check (-not $hex.Contains($secretHex)) "secret redacted outgoing=$outgoing"
        }
    }
    $panel = $f.GetType().GetField('photoCard',$flags).GetValue($f)
    $progress = New-Object NextConfig.PhotoProgress; $progress.Received=31; $progress.Total=100
    Invoke $panel 'Display' @([NextConfig.PhotoProgress]$progress)
    $meter=$panel.GetType().GetField('meter',$flags).GetValue($panel)
    Check ($meter.Value -eq 31 -and $meter.Maximum -eq 100) 'photo 31 percent is not full'
    $f.GetType().GetField('bootOffered',$flags).SetValue($f,$true)
    $status=New-Object 'NextConfig.clOpros+s_status'; $status.signal=99; $status.gsm=1; $status.gprs=1; $status.connectToServer=1
    Invoke $f 'refOnStatus' @([NextConfig.clOpros+s_status]$status)
    Check ((Control $f 'pbSig').Value -eq 0 -and (Control $f 'lblSig').Text -notmatch '99') 'unknown signal is neutral'
    $f.GetType().GetField('lastStatus',$flags).SetValue($f,[DateTime]::Now.AddSeconds(-20))
    Invoke $f 'TmrTick'
    foreach($name in @('lblGSM','lblInt','lblServerConnect','lbIN1','lblIN2','lblOUT1','lbOUT2')) { Check ((Control $f $name).Text -eq 'Нет свежих данных') "stale $name invalidated" }
    Check (-not (Control $f 'btnTestOUT1').Enabled) 'stale output controls disabled'
    $status.signal=15
    Invoke $f 'refOnStatus' @([NextConfig.clOpros+s_status]$status)
    Check ((Control $f 'pbSig').Value -eq 15 -and (Control $f 'btnTestOUT1').Enabled) 'fresh status restores controls'
    Invoke $f 'PhotoBusyChanged' @($true)
    Invoke $f 'TmrTick'
    Check (-not (Control $f 'btnTestOUT1').Enabled) 'timer cannot enable controls during photo'
    Invoke $f 'PhotoBusyChanged' @($false)
} finally {$f.Dispose()}
$cache=[NextConfig.Theme].GetField('minWidths',$static).GetValue($null)
$before=$cache.Count
1..3 | ForEach-Object { $temporary=New-Object NextConfig.Form1; $temporary.Dispose() }
[GC]::Collect(); [GC]::WaitForPendingFinalizers()
Check ($cache.Count -eq $before) 'disposed forms leave no static button roots'
$serial=[NextConfig.clOpros].GetMethod('NewSerial',$static).Invoke($null,@('OFFLINE'))
try { Check ($serial.WriteTimeout -gt 0) 'finite default write timeout' } finally {$serial.Dispose()}
Write-Output "PASS: $script:checks audit regression checks; no ports opened or server commands sent."

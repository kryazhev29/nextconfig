param([string]$Assembly = "$PSScriptRoot\..\NextConfig\bin\Review\NextConfig.exe")
$ErrorActionPreference = 'Stop'
[Reflection.Assembly]::LoadFrom((Resolve-Path $Assembly)) | Out-Null
$script:checks = 0
function Check($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; $script:checks++ }
function Reject([scriptblock]$action, $name) {
    $rejected = $false
    try { & $action | Out-Null } catch { $rejected = $true }
    Check $rejected $name
}
$buffer = New-Object byte[] 512
$value = 'a' * 31
[NextConfig.clOpros]::CopyStrToBuffer($value, [ref]$buffer, 104, 32)
Check ([Text.Encoding]::UTF8.GetString($buffer,104,31) -eq $value) '31 bytes preserved'
Check ($buffer[135] -eq 0) 'NUL terminator'
$before = [Convert]::ToBase64String($buffer)
Reject { [NextConfig.clOpros]::CopyStrToBuffer(('b' * 32), [ref]$buffer, 104, 32) } '32 bytes rejected'
Check ([Convert]::ToBase64String($buffer) -eq $before) 'rejection leaves buffer intact'
$multi = ([string][char]0x044F) * 16
Reject { [NextConfig.clOpros]::CopyStrToBuffer($multi, [ref]$buffer, 104, 32) } 'UTF-8 byte length enforced'
Reject { [NextConfig.clOpros]::DecodeConfig($null) } 'null response rejected'
Reject { [NextConfig.clOpros]::DecodeConfig((New-Object byte[] 522)) } 'short response rejected'
$frame = New-Object byte[] 523
$enum = [NextConfig.clOpros].GetNestedType('eCmd', [Reflection.BindingFlags]'Public,NonPublic')
$frame[8] = [byte][Enum]::Parse($enum, 'CMD_GETCONFIG')
[Array]::Copy($buffer, 0, $frame, 9, 512)
$decoded = [NextConfig.clOpros]::DecodeConfig($frame)
Check ($decoded.opType -eq $value) 'full configuration decoded'
$encoded = [NextConfig.clOpros]::EncodeConfig($decoded)
Check ([Convert]::ToBase64String($encoded) -eq [Convert]::ToBase64String($buffer)) 'configuration round trip'
$frame[8] = 0
Reject { [NextConfig.clOpros]::DecodeConfig($frame) } 'wrong command rejected'
# Bypass the constructor: it normally starts scanning physical ports.
$poller = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$config = New-Object 'NextConfig.clOpros+s_config'
Reject { $poller.GetConfig([ref]$config) } 'missing transport raises error'
$pause = [NextConfig.clOpros].GetField('_isPause', [Reflection.BindingFlags]'Instance,NonPublic')
Check (-not $pause.GetValue($poller)) 'polling restored after actual exception'
# Source contracts for serial paths: no hardware or real commands in this test.
$source = Get-Content "$PSScriptRoot\..\NextConfig\clComPort.vb" -Raw
$core = $source.Substring($source.IndexOf('Private Function SendRecCmdCore'))
Check ($core -match 'Do While deadline.ElapsedMilliseconds < timeout') 'absolute receive deadline'
Check ($core -notmatch 'TimeStart = Environment.TickCount') 'incoming bytes cannot extend deadline'
Check ($core -match 'If deadline.ElapsedMilliseconds >= timeout Then Return False') 'transmit drain bounded'
$source = Get-Content "$PSScriptRoot\..\NextConfig\clOprosCommands.vb" -Raw
foreach ($name in @('GetConfig', 'SetConfig')) {
    $part = $source.Substring($source.IndexOf('Public Function ' + $name + '('))
    $part = $part.Substring(0, $part.IndexOf('End Function'))
    Check ($part -match 'Finally\s+_isPause = False') "$name restores polling"
}
Write-Output "PASS: $script:checks checks (offline)"

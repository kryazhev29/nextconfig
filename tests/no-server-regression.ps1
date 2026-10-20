param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$script:checks = 0
function Check($ok, $name) { if (-not $ok) { throw "FAIL: $name" }; $script:checks++ }
function Control($form, $name) { $form.GetType().GetProperty($name,$flags).GetValue($form,$null) }
function Invoke($form, $name, [object[]]$parameters=@()) { $form.GetType().GetMethod($name,$flags).Invoke($form,$parameters) }
# Конструктор clOpros сканирует реальные порты: в стенде он не вызывается.
$poller = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$form = New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',0)
try {
    Check (-not (Control $form 'btnUpdateFW').Enabled) 'update disabled before first USB status'
    Check ((Invoke $form 'FirmwareBlockReason').Contains('USB')) 'initial reason names USB status'
    Check ((Control $form 'lblFwHint').Text.Contains('Нет свежего статуса')) 'reason visible beside disabled button'
    $form.GetType().GetField('bootOffered',$flags).SetValue($form,$true)
    $status = New-Object 'NextConfig.clOpros+s_status'
    $status.signal=99; $status.gsm=0; $status.gprs=0; $status.connectToServer=0
    Invoke $form 'refOnStatus' @($status)
    Check ((Control $form 'btnUpdateFW').Enabled -and (Control $form 'btnUpdateBoot').Enabled) 'fresh USB status permits update without server or signal'
    Check ((Invoke $form 'FirmwareBlockReason') -eq '') 'offline network is not an update blocker'
    Invoke $form 'PhotoBusyChanged' @($true)
    Check (-not (Control $form 'btnUpdateFW').Enabled) 'busy operation blocks update immediately'
    Check ((Control $form 'lblFwHint').Text.Contains('занята')) 'busy reason visible'
    Invoke $form 'PhotoBusyChanged' @($false)
    Check ((Control $form 'btnUpdateFW').Enabled) 'operation completion releases update'
    $form.GetType().GetField('lastStatus',$flags).SetValue($form,[DateTime]::Now.AddSeconds(-7))
    Invoke $form 'TmrTick'
    Check (-not (Control $form 'btnUpdateFW').Enabled) 'USB status older than six seconds blocks update'
    Check ((Control $form 'lblFwHint').Text.Contains('подключение')) 'stale reason tells operator what to check'
    Invoke $form 'refOnStatus' @($status)
    Check ((Control $form 'btnUpdateFW').Enabled) 'new USB status restores update'
} finally { $form.Dispose() }
Write-Output "PASS: $script:checks no-server checks; offline, no COM."

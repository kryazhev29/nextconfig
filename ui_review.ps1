# Доработка: Кряжев А., 2026. Только отдельный просмотр форм, без COM-портов.
param([string]$Firmware = '', [switch]$NativeScale)
$ErrorActionPreference = 'Stop'
if (@(Get-Process | Where-Object Path -like '*NextConfig*').Count -gt 0) {
    throw 'Перед просмотром закройте NextConfig; рабочее окно владельца трогать нельзя.'
}
$release = Join-Path $PSScriptRoot 'NextConfig\bin\Release'
$output = Join-Path $PSScriptRoot 'screenshots\all'
$snap = Join-Path (Split-Path $PSScriptRoot) 'Стенд_Domovoi\Скрипты\ui_snap.ps1'
$harness = Join-Path $release 'UiReview.exe'
$errorFile = Join-Path $release 'ui-review-error.txt'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe "/out:$harness" "/reference:$release\NextConfig.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'ui_dialog_review.cs')
if ($LASTEXITCODE -ne 0) { throw 'Не собрался просмотрщик форм' }
New-Item -ItemType Directory -Force $output | Out-Null
if (Test-Path -LiteralPath $errorFile) { Remove-Item -LiteralPath $errorFile }
$cases = @('pin','pin-error','text','select','changes','long','confirm','error','info','fw-error')
if ($Firmware) { $cases += @('fw','fw-progress') }
foreach ($case in $cases) {
    $arguments = @($case, '--demo')
    if ($case -in @('fw','fw-progress')) { $arguments = @($case, ('"' + $Firmware + '"'), '--demo') }
    if (-not $NativeScale) { $arguments += '--demo-dpi=120' }
    $process = Start-Process -FilePath $harness -ArgumentList $arguments -PassThru
    try {
        Start-Sleep -Milliseconds 450
        if (Test-Path -LiteralPath $errorFile) { throw (Get-Content -LiteralPath $errorFile -Raw) }
        if ($process.HasExited) { throw "Окно $case не открылось" }
        $suffix = if ($NativeScale) { 'native' } else { '125' }
        & $snap -ProcName UiReview -Out (Join-Path $output "$case-$suffix.png")
    } finally {
        if (-not $process.HasExited) {
            [void]$process.CloseMainWindow()
            if (-not $process.WaitForExit(1000)) { Stop-Process -Id $process.Id }
        }
    }
}

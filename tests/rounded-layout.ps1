param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\RoundedReview\NextConfig.exe",
      [string]$Output = "$PSScriptRoot\..\artifacts\rounded-review\layout")
$ErrorActionPreference = 'Stop'
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
$binary = Join-Path (Split-Path $assembly) 'RoundedLayout.exe'
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$outputPath = (Resolve-Path -LiteralPath $Output).Path
$manifest = (Resolve-Path "$PSScriptRoot\..\NextConfig\app.manifest").Path
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$binary" "/reference:$assembly" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/win32manifest:$manifest" (Join-Path $PSScriptRoot 'rounded-layout.cs')
if ($LASTEXITCODE) { throw 'Layout harness compilation failed' }
Copy-Item -LiteralPath ($assembly + '.config') -Destination ($binary + '.config')
& $binary $outputPath
$code = $LASTEXITCODE
Get-Content -LiteralPath (Join-Path $outputPath 'result.txt')
Get-Content -LiteralPath (Join-Path $outputPath 'dpi.txt')
if ($code) { throw 'Layout checks failed' }

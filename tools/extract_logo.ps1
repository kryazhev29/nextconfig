Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot
[void][Reflection.Assembly]::LoadFrom("$repo\NextConfig\bin\Release\NextConfig.exe")
$resources = New-Object System.ComponentModel.ComponentResourceManager([NextConfig.Form1])
$logo = [NextConfig.Theme]::LogoHorizontal($resources.GetObject('PictureBox1.Image'))
$logo.Save("$repo\NextConfig\obj\naviset-source.png")
$logo.Dispose()

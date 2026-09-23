# Доработка: Кряжев А., 2026. Только наше демоокно, без платы.
param([ValidateSet('size','baud','types','wiegand','mode')][string]$Action = 'size')
$ErrorActionPreference = 'Stop'
$demo = @(Get-CimInstance Win32_Process -Filter "Name='NextConfig.exe'")
if ($demo.Count -ne 1 -or $demo[0].CommandLine -notlike '*--demo*') { throw 'Нужен ровно один NextConfig --demo; окно владельца трогать нельзя' }
$stand = Join-Path (Split-Path $PSScriptRoot) 'Стенд_Domovoi\Скрипты'
if ($Action -eq 'size') {
    . "$stand\ui_click.ps1" -List
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public class DemoSize {
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr w,int command);
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr w,IntPtr after,int x,int y,int width,int height,uint flags);
}
'@
    [void][DemoSize]::SetProcessDPIAware()
    $w=[Ui]::Tops([uint32]$demo[0].ProcessId)[0]
    [void][DemoSize]::ShowWindow($w,9)
    [void][DemoSize]::SetWindowPos($w,[IntPtr]::Zero,300,150,1197,1120,4)
} else {
    . "$stand\ui_combo.ps1" -List
    $indexes=@{mode=2;baud=3;types=5;wiegand=4}
    $w=[Combos]::Find([uint32]$demo[0].ProcessId)[$indexes[$Action]]
    [void][Combos]::SendMessage($w,0x14F,[IntPtr]1,[IntPtr]::Zero)
}

param([switch]$Dpi120)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$scripts = Join-Path (Split-Path $repo -Parent) 'Стенд_Domovoi\Скрипты'
$out = Join-Path $repo 'artifacts\oldfw48'
if (Get-Process NextConfig -ErrorAction SilentlyContinue) { throw 'NextConfig уже открыт. Окно владельца не трогаем.' }
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class OldFwDemoUi {
  public delegate bool Callback(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Callback cb, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, Callback cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string s);
  public static IntPtr Window(uint pid) {
    var found = new List<IntPtr>();
    EnumWindows((h,l)=>{uint p;GetWindowThreadProcessId(h,out p);var s=new StringBuilder(256);GetWindowText(h,s,256);
      if(p==pid && s.ToString().Contains("старая прошивка"))found.Add(h);return true;},IntPtr.Zero);
    if(found.Count!=1)throw new Exception("Expected one demo window");return found[0];
  }
  public static void Filter(IntPtr window,string text) {
    var edits=new List<IntPtr>();
    EnumChildWindows(window,(h,l)=>{var s=new StringBuilder(256);GetClassName(h,s,256);
      if(s.ToString().Contains("EDIT") && SendMessage(h,0xD2,IntPtr.Zero,null)==IntPtr.Zero)edits.Add(h);return true;},IntPtr.Zero);
    if(edits.Count!=1)throw new Exception("Expected one unmasked search field");
    SendMessage(edits[0],12,IntPtr.Zero,text);
  }
}
'@
$arguments = @('--demo:oldfw48')
if ($Dpi120) { $arguments += '--demo-dpi=120' }
# Это видимое тестовое окно, открытое по поручению владельца.
$demoProcess = Start-Process (Join-Path $repo 'NextConfig\bin\Release\NextConfig.exe') -ArgumentList $arguments -PassThru
try {
    Start-Sleep -Seconds 3
    $window = [OldFwDemoUi]::Window([uint32]$demoProcess.Id)
    $suffix = if ($Dpi120) { '-125pct' } else { '' }
    & "$scripts\ui_snap.ps1" -Out (Join-Path $out "01-main$suffix.png")
    if (-not (Test-Path (Join-Path $out "01-main$suffix.png"))) { throw 'Snapshot failed' }
    [OldFwDemoUi]::Filter($window, 'Расписание')
    & "$scripts\ui_snap.ps1" -Out (Join-Path $out "04-schedules$suffix.png")
    [OldFwDemoUi]::Filter($window, 'RFID')
    & "$scripts\ui_snap.ps1" -Out (Join-Path $out "05-rfid$suffix.png")
    [OldFwDemoUi]::Filter($window, 'CRC')
    & "$scripts\ui_snap.ps1" -Out (Join-Path $out "06-last-field$suffix.png")
    [OldFwDemoUi]::Filter($window, '')
    & "$scripts\ui_click.ps1" -List
    & "$scripts\ui_click.ps1" -ButtonIndex 4
    if (-not $demoProcess.WaitForExit(5000)) { throw 'Demo did not close normally' }
    'PASS: demo main, settings filters, final field, normal close.'
} finally {
    if (-not $demoProcess.HasExited) {
        # Только запущенный этим скриптом PID, ни одного процесса владельца.
        Stop-Process -Id $demoProcess.Id
    }
}

# Изменить размер главного окна запущенной NextConfig (для проверки раскладки при маленьком окне).
# Плату не трогает: работает только с окном. Берёт самое большое видимое окно процесса.
#   ui_resize.ps1 -Width 1000 -Height 700
#   ui_resize.ps1 -Width 1000 -Height 700 -ProcId 1234   # конкретный экземпляр
param([string]$ProcName = 'NextConfig', [int]$ProcId = 0, [Parameter(Mandatory = $true)][int]$Width, [Parameter(Mandatory = $true)][int]$Height)

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Resizer {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  public static List<IntPtr> Windows(uint pid) {
    List<IntPtr> list = new List<IntPtr>();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p == pid && IsWindowVisible(h)) list.Add(h);
      return true;
    }, IntPtr.Zero);
    return list;
  }
}
"@

[Resizer]::SetProcessDPIAware() | Out-Null
$p = if ($ProcId) { Get-Process -Id $ProcId -ErrorAction SilentlyContinue } else { Get-Process $ProcName -ErrorAction SilentlyContinue | Select-Object -First 1 }
if (-not $p) { "no process"; exit 1 }
$best = [IntPtr]::Zero; $bestArea = 0
foreach ($h in [Resizer]::Windows([uint32]$p.Id)) {
  $r = New-Object Resizer+RECT
  [Resizer]::GetWindowRect($h, [ref]$r) | Out-Null
  $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
  if ($area -gt $bestArea) { $bestArea = $area; $best = $h }
}
if ($best -eq [IntPtr]::Zero) { "no window"; exit 1 }
# SWP_NOMOVE 0x2, SWP_NOZORDER 0x4, SWP_NOACTIVATE 0x10
[Resizer]::SetWindowPos($best, [IntPtr]::Zero, 0, 0, $Width, $Height, 0x16) | Out-Null
Start-Sleep -Milliseconds 400
$r = New-Object Resizer+RECT
[Resizer]::GetWindowRect($best, [ref]$r) | Out-Null
"window $($r.Right - $r.Left)x$($r.Bottom - $r.Top)"

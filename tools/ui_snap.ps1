# Доработка: Кряжев А., 2026. Снимок окна без невидимой рамки Windows.
param([string]$ProcName = 'NextConfig', [string]$Out = "$env:TEMP\shot.png", [switch]$Smallest, [switch]$RawFrame, [switch]$Screen)

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class Shot {
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out RECT rect, int size);
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr w, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr w, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
  // без этого PowerShell считает координаты в масштабе 100 % и снимок выходит обрезанным
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

[Shot]::SetProcessDPIAware() | Out-Null

Add-Type -AssemblyName System.Drawing

$p = Get-Process $ProcName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { "no process"; exit 1 }

$best = $null
$bestArea = $(if ($Smallest) { [int]::MaxValue } else { 0 })
foreach ($h in [Shot]::Windows([uint32]$p.Id)) {
  $r = New-Object Shot+RECT
  [Shot]::GetWindowRect($h, [ref]$r) | Out-Null
  $area = ($r.Right - $r.Left) * ($r.Bottom - $r.Top)
  if ($area -lt 10000) { continue }
  if (($Smallest -and $area -lt $bestArea) -or (-not $Smallest -and $area -gt $bestArea)) {
    $bestArea = $area; $best = $h; $bestRect = $r
  }
}
if (-not $best) { "no visible window"; exit 1 }

$w = $bestRect.Right - $bestRect.Left
$ht = $bestRect.Bottom - $bestRect.Top
$bmp = New-Object System.Drawing.Bitmap $w, $ht
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Shot]::PrintWindow($best, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$g.Dispose()
$frame = New-Object Shot+RECT
$hr = [Shot]::DwmGetWindowAttribute($best, 9, [ref]$frame, 16)
$crop = [Drawing.Rectangle]::new(0,0,$w,$ht)
if (-not $RawFrame -and $hr -eq 0 -and $frame.Right -gt $frame.Left) {
    $visible = [Drawing.Rectangle]::new(($frame.Left-$bestRect.Left),($frame.Top-$bestRect.Top),($frame.Right-$frame.Left),($frame.Bottom-$frame.Top))
    $crop = [Drawing.Rectangle]::Intersect($crop,$visible)
}
# PrintWindow закрашивает невидимую resize-рамку чёрным. Оставляем клиентскую
# ширину и заголовок, без технической рамки; координаты берём у Windows.
if (-not $RawFrame -and -not $Screen) {
    $client = New-Object Shot+RECT
    $origin = New-Object Shot+POINT
    [void][Shot]::GetClientRect($best,[ref]$client)
    [void][Shot]::ClientToScreen($best,[ref]$origin)
    $painted = [Drawing.Rectangle]::new(($origin.X-$bestRect.Left),0,$client.Right,($origin.Y-$bestRect.Top+$client.Bottom))
    $crop = [Drawing.Rectangle]::Intersect($crop,$painted)
}
$visibleBmp = $bmp.Clone($crop,$bmp.PixelFormat)
if ($Screen) {
    $screenGraphics = [Drawing.Graphics]::FromImage($visibleBmp)
    $screenGraphics.CopyFromScreen($bestRect.Left+$crop.X,$bestRect.Top+$crop.Y,0,0,$visibleBmp.Size)
    $screenGraphics.Dispose()
}
$visibleBmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
$visibleBmp.Dispose(); $bmp.Dispose()
"{0}: окно {1}x{2}; видимая рамка {3}x{4}; отступ ({5},{6})" -f $Out,$w,$ht,$crop.Width,$crop.Height,$crop.X,$crop.Y
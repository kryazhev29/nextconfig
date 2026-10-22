param([string]$AssemblyPath='', [string]$Output='',
      [Parameter(ValueFromRemainingArguments=$true)][string[]]$DemoArgs)
# Только локальная форма: без Form2_Load, фонового опроса и открытия COM.
$ErrorActionPreference='Stop'
if(-not $AssemblyPath){$AssemblyPath=Join-Path $PSScriptRoot '..\NextConfig\bin\OperatorReview\NextConfig.exe'}
if(-not $Output){$Output=Join-Path $PSScriptRoot '..\artifacts\operator-review'}
# Theme.Px берёт масштаб с экрана (150 % → 1,5), а DeviceDpi формы в PowerShell остаётся 96: WinForms без своего
# app.config высокий DPI не включает, и ControlPx расходится с Px (колонки не складывались, поля сжимались до 33 px).
# Один масштаб даёт --demo-dpi: без него тест перезапускает себя при 100 %; 125 % — вызов с --demo --demo-dpi=120.
if(-not ($DemoArgs -match '^--demo-dpi=')){
    & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -AssemblyPath $AssemblyPath -Output $Output --demo --demo-dpi=96
    exit $LASTEXITCODE
}
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class OperatorDpi {
 [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
'@
[OperatorDpi]::SetProcessDpiAwarenessContext([IntPtr](-4)) | Out-Null
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::EnableVisualStyles()
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
New-Item -ItemType Directory -Force $Output | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:checks=0
function Check($ok,$label){if(-not $ok){throw "FAIL: $label"};$script:checks++}
function Field($name){
    $p=$form.GetType().GetProperty($name,$flags)
    if($p){return $p.GetValue($form,$null)}
    $form.GetType().GetField($name,$flags).GetValue($form)
}
function Call($name,[object[]]$values=@()){$form.GetType().GetMethod($name,$flags).Invoke($form,$values)}
function Reply([byte[]]$bytes){Call 'WifiApplyReply' @($bytes,$true) | Out-Null}
function LayoutCheck($parent){
    foreach($control in $parent.Controls){
        if(-not $control.Visible){continue}
        if($control -is [Windows.Forms.ComboBox] -or $control -is [Windows.Forms.TextBox]){
            Check ($control.Top -ge 0 -and $control.Bottom -le $parent.ClientSize.Height) "field vertical clipping: $($control.Name) $($control.GetType().Name) top=$($control.Top) bottom=$($control.Bottom) parent=$($parent.ClientSize.Height)"
            Check ($control.Left -ge 0 -and $control.Right -le $parent.ClientSize.Width) "field horizontal clipping: $($control.Name)"
            $minimum=if($control -is [NextConfig.AddressEditor]){
                [Windows.Forms.TextRenderer]::MeasureText('255',$control.Font).Width
            }else{[NextConfig.Theme]::Px(65)}
            Check ($control.Width -ge $minimum) "field too narrow: $($control.Name) $($control.GetType().Name) $($control.Width) < $minimum"
        }
        if($parent.Name -eq 'operatorColumns'){
            Check ($control.Right -le $parent.ClientSize.Width) 'card overflows columns'
        }
        LayoutCheck $control
    }
}
function Snapshot($control,$name){
    $bitmap=New-Object Drawing.Bitmap($control.Width,$control.Height)
    try {
        $control.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$control.Width,$control.Height)))
        $bitmap.Save((Join-Path $Output "$name.png"))
    }finally{$bitmap.Dispose()}
}
$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$form=New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',0)
try {
    $load=[Delegate]::CreateDelegate([EventHandler],$form,$form.GetType().GetMethod('Form2_Load',$flags))
    $form.remove_Load($load)
    $form.GetType().GetField('bootOffered',$flags).SetValue($form,$true)
    $form.GetType().GetField('linkLoading',$flags).SetValue($form,$true)
    Call 'SetDefaultConfig' | Out-Null
    Call 'Load485' | Out-Null
    $form.ShowInTaskbar=$false;$form.Opacity=0;$form.Show()
    (Field 'tabs').SelectedTab=Field 'tabSettings'
    (Field 'tabSettings').Controls.Find('settingsSection1',$true)[0].PerformClick()
    $page=(Field 'wifiModel').Parent
    while($page.Parent -and $page.Dock -ne [Windows.Forms.DockStyle]::Top){$page=$page.Parent}
    foreach($width in @(1920,1280,$form.MinimumSize.Width)){
        $form.Size=New-Object Drawing.Size($width,1000)
        foreach($mode in @('gsm','wifi','none','unsupported')){
            switch($mode){
                'gsm' {Reply ([byte[]](0,3,1,1,0,4))}
                'wifi' {Reply ([byte[]](1,2,1,1,1,1))}
                'none' {Reply ([byte[]](0,0,1,0,0,4))}
                'unsupported' {Reply ([byte[]](0,0))}
            }
            [Windows.Forms.Application]::DoEvents()
            LayoutCheck $page
            Check ((Field 'wiegand485').Enabled) 'Wiegand stays available'
            if($mode -in @('gsm','wifi')){
                Check (-not (Field 'mode485').Enabled -and -not (Field 'baud485').Enabled) 'bridge owns RS-485'
                Check (-not (Field 'deviceTable485').Visible) 'bridge hides unused peripheral table'
                Check ((Field 'wifiWrite').Enabled) 'saved bridge can receive network'
            }
            if($mode -eq 'none'){
                Check ((Field 'deviceTable485').Visible) 'peripherals available without bridge'
                Check (-not (Field 'wifiOptions').Visible -and -not (Field 'wifiWrite').Enabled) 'no modem cannot write network'
            }
            if($mode -eq 'unsupported'){
                Check (-not (Field 'wifiModel').Enabled -and -not (Field 'wifiWrite').Enabled) 'legacy firmware cannot write Wi-Fi'
            }
            Snapshot $page "$mode-$width"
            if($mode -eq 'gsm'){Snapshot (Field 'tabSettings') "viewport-$width"}
        }
    }
    "PASS: $script:checks layout/state checks, DPI $($form.DeviceDpi), scale $([NextConfig.Theme]::Px(100))%; 1920/1280/minimum $($form.MinimumSize.Width) px; no COM"
} finally {$form.Dispose()}

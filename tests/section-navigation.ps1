param([string]$AssemblyPath='', [string]$Output='')
$ErrorActionPreference='Stop'
if(-not $AssemblyPath){$AssemblyPath=Join-Path $PSScriptRoot '..\NextConfig\bin\OperatorReview\NextConfig.exe'}
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::EnableVisualStyles()
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath)) | Out-Null
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$script:checks=0;$script:failures=@()
function Check($ok,$name){$script:checks++;if(-not $ok){$script:failures+=$name}}
function Field($name){
    $p=$form.GetType().GetProperty($name,$flags)
    if($p){return $p.GetValue($form,$null)}
    $form.GetType().GetField($name,$flags).GetValue($form)
}
function Call($name,[object[]]$values=@()){$form.GetType().GetMethod($name,$flags).Invoke($form,$values)}
function Reply([byte[]]$bytes){Call 'WifiApplyReply' @($bytes,$true) | Out-Null}
function HostOf($control){while($control.Parent.GetType() -ne [Windows.Forms.Panel]){$control=$control.Parent};$control.Parent}
function Snap($control,$name){
    if(-not $Output){return}
    $bitmap=New-Object Drawing.Bitmap($control.Width,$control.Height)
    try{$control.DrawToBitmap($bitmap,(New-Object Drawing.Rectangle(0,0,$control.Width,$control.Height)));$bitmap.Save((Join-Path $Output "$name.png"))}finally{$bitmap.Dispose()}
}
$poller=[Runtime.Serialization.FormatterServices]::GetUninitializedObject([NextConfig.clOpros])
$form=New-Object NextConfig.Form2($poller,'OFFLINE','000000000000000',0)
try {
    # Не запускаем Form2_Load, USB и фоновые запросы. Показывается только локальная форма.
    $load=[Delegate]::CreateDelegate([EventHandler],$form,$form.GetType().GetMethod('Form2_Load',$flags))
    $form.remove_Load($load)
    $form.GetType().GetField('bootOffered',$flags).SetValue($form,$true)
    $form.GetType().GetField('linkLoading',$flags).SetValue($form,$true)
    Call 'SetDefaultConfig' | Out-Null
    $form.ShowInTaskbar=$false;$form.Opacity=0;$form.Show()
    $tabs=Field 'tabs';$settings=Field 'tabSettings';$busHost=HostOf (Field 'wifiModel')
    Check ($tabs.TabPages.Count -eq 6) 'only six current main pages, no old registrations'
    $hosts=@($busHost.Parent.Controls)
    $tabs.SelectedTab=$settings
    Check (-not $busHost.Visible) 'settings initially opens Connection, not RS-485'
    Call 'ShowSettingsSection' @(1) | Out-Null
    Check (-not (Field 'deviceTable485').Visible) 'no peripheral table before first Wi-Fi reply'
    Check (-not (Field 'mode485').Enabled) 'unknown port ownership disables RS-485 editing'
    Reply $null
    Check (-not (Field 'deviceTable485').Visible) 'failed first request does not reveal peripheral table'
    Reply ([byte[]](0,3,1,1,0,4))
    Check (-not (Field 'deviceTable485').Visible -and -not (Field 'mode485').Enabled) 'saved bridge keeps RS-485 unavailable'
    Check ($busHost.BackColor -eq [NextConfig.Theme]::WindowBack) 'scroll host matches bus page, no white tail'
    $tabs.SelectedTab=Field 'tabState'
    $script:busAtEntry=$false
    $entry=[EventHandler]{if($settings.Visible){$script:busAtEntry=$busHost.Visible}}
    $settings.add_VisibleChanged($entry)
    Call 'ShowSettingsSection' @(2) | Out-Null
    $settings.remove_VisibleChanged($entry)
    Check (-not $script:busAtEntry) 'direct Access navigation never exposes previous RS-485 section'
    Check (@($hosts | Where-Object Visible).Count -eq 1) 'one settings host visible'
    Call 'ShowSettingsSection' @(1) | Out-Null
    Reply ([byte[]](0,0,1,0,0,4))
    Check ((Field 'deviceTable485').Visible -and (Field 'mode485').Enabled) 'no modem shows configured peripherals'
    Check ((Field 'hint485').Visible) 'address hint follows confirmed free port'
    if($Output){New-Item -ItemType Directory -Force -Path $Output | Out-Null}
    foreach($width in @(1920,1280,$form.MinimumSize.Width)){
        $form.Size=New-Object Drawing.Size($width,1000)
        foreach($tab in $tabs.TabPages){
            $tabs.SelectedTab=$tab;[Windows.Forms.Application]::DoEvents()
            Check (@($tabs.TabPages | Where-Object Visible).Count -eq 1) "one main page at $width/$($tab.Text)"
            Check ($tabs.SelectedTab -eq $tab) "main navigation $width/$($tab.Text)"
            if($tab -eq $settings){
                foreach($index in @(0,1,2,1,0)){
                    Call 'ShowSettingsSection' @($index) | Out-Null
                    [Windows.Forms.Application]::DoEvents()
                    Check (@($hosts | Where-Object Visible).Count -eq 1) "one settings page $width/$index"
                    Snap $settings "settings-$index-$width"
                }
            }else{
                Check ($tab.Controls[0].BackColor -eq $tab.BackColor) "page background matches scrolling area $($tab.Text)"
                Snap $tab "main-$($tabs.TabPages.IndexOf($tab))-$width"
            }
        }
    }
    Call 'ShowSettingsSection' @(1) | Out-Null
    Reply ([byte[]](0,0))
    Check ((Field 'deviceTable485').Visible -and (Field 'mode485').Enabled) 'legacy reply keeps RS-485 usable'
    Check (-not (Field 'wifiModel').Enabled) 'legacy reply cannot edit Wi-Fi model'
    if($script:failures.Count){throw ($script:failures -join "`n")}
    "PASS: $script:checks startup/navigation checks; six main pages, three settings pages; no COM"
}finally{$form.Dispose()}

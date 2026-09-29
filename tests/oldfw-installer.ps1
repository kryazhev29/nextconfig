param([string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))|Out-Null
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$id=[NextConfig.Demo]::OldFirmwareReply(0,$null)
$reader=[NextConfig.OldFirmware]::new($null,$id,$true)
$reader.ReadSettings()|Out-Null
$before=$reader.SettingsSnapshot()
$form=[NextConfig.frmOldFirmware]::new($reader,'DEMO, без COM')
$script:checks=0
function Check($value,$name){if(-not $value){throw "FAIL: $name"};$script:checks++}
function Field($name){return ,$form.GetType().GetField($name,$flags).GetValue($form)}
function SetField($name,$value){$form.GetType().GetField($name,$flags).SetValue($form,$value)}
function InvokeUi($name,[object[]]$values){return $form.GetType().GetMethod($name,$flags).Invoke($form,$values)}
try{
 $form.Show();[Windows.Forms.Application]::DoEvents()
 SetField 'originalSettings' $before.Clone();SetField 'editedSettings' $before.Clone()
 InvokeUi 'ShowSettings' @(,$before)|Out-Null
 $editors=Field 'settingEditors';$schema=[NextConfig.OldSettings]::Fields
 $balanceFlag=-1;$threshold=-1;$ussd=-1
 for($i=0;$i -lt $schema.Count;$i++){
  $f=$schema[$i]
  if([NextConfig.OldSettingsPresentation]::ServiceOnly($f)){Check (-not $editors.ContainsKey($i)) "hidden service field $i"}
  if($f.Offset -eq 164 -and $f.Mask -eq 8){$balanceFlag=$i}
  if($f.Offset -eq 172){$threshold=$i}
  if($f.Offset -eq 176){$ussd=$i}
 }
 Check ([Linq.Enumerable]::SequenceEqual([byte[]]$before,[byte[]](Field 'editedSettings'))) 'render preserves every byte'
 Check ((Field 'settingsGroup').Items.Contains('Баланс SIM-карты')) 'balance group'
 (Field 'settingsGroup').SelectedItem='Баланс SIM-карты'
 (Field 'tabs').SelectedTab=Field 'tabSettings'
 $editors[$balanceFlag].Checked=$false
 Check (-not $editors[$threshold].Enabled -and -not $editors[$ussd].Enabled) 'balance fields disabled'
 $editors[$balanceFlag].Checked=$true
 Check ($editors[$threshold].Enabled -and $editors[$ussd].Enabled) 'balance fields enabled'
 $editors[$ussd].Text='*101#'
 Check ([NextConfig.OldSettings]::RawValue((Field 'editedSettings'),$ussd) -eq '*101#') 'USSD draft changed'
 Check ([Linq.Enumerable]::SequenceEqual([byte[]]$before,[byte[]]$reader.SettingsSnapshot())) 'editing never writes device'
 $after=Field 'editedSettings'
 Check ([Linq.Enumerable]::SequenceEqual([byte[]]$before[174..175],[byte[]]$after[174..175])) 'legacy USSD number preserved'
 Check ((Field 'scheduleGroup').Items.Count -eq 7) 'seven schedules'
 Check (-not (Field 'diagnosticSettings').Visible -and -not (Field 'statusGrid').Visible) 'diagnostics collapsed'
 $unknown=$before.Clone();$unknown[169]=231
 SetField 'originalSettings' $unknown.Clone();SetField 'editedSettings' $unknown.Clone()
 InvokeUi 'ShowSettings' @(,$unknown)|Out-Null
 Check ((Field 'editedSettings')[169] -eq 231) 'unknown input function preserved'
 $badIndex=0..($schema.Count-1)|Where-Object {$schema[$_].Offset -eq 9}|Select-Object -First 1
 (Field 'settingEditors')[$badIndex].Text='abc'
 Check ((Field 'invalidSettings').Contains([int]$badIndex)) 'invalid secret blocks save without logging value'
 (Field 'tabs').SelectedTab=Field 'tabDevice'
 $form.ClientSize=[Drawing.Size]::new(1000,650)
 [Windows.Forms.Application]::DoEvents()
 $overview=(Field 'tabDevice').Controls[0]
 Check ($overview.AutoScrollPosition.Y -eq 0) 'device opens at top'
 $overview.AutoScrollPosition=[Drawing.Point]::new(0,150)
 $scrollBefore=$overview.AutoScrollPosition.Y
 (Field 'testOutput').Focus()|Out-Null
 InvokeUi 'SetBusy' @($true)|Out-Null
 InvokeUi 'SetBusy' @($false)|Out-Null
 [Windows.Forms.Application]::DoEvents()
 Check ($overview.AutoScrollPosition.Y -eq $scrollBefore) 'focus and busy transitions preserve scroll'
 $status=$reader.TestOutputAndReadStatus()
 Check (@($status.Rows|Where-Object {$_[1] -eq 'Выход активен' -and $_[2] -eq 'Да'}).Count -eq 1) 'output observed after demo command'
 "PASS: $script:checks installer checks; demo object only, no COM ports opened."
}finally{
 (Field 'poll').Stop();$form.Dispose()
}

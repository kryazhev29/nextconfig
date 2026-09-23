# Доработка: Кряжев А., 2026. Проверки только UI, без открытия формы и COM.
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom("$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$editor = New-Object NextConfig.AddressEditor
foreach ($value in @(0,1,127,255)) {
    $editor.Text = [string]$value
    if ($editor.Value -ne $value) { throw "Адрес $value не принят" }
}
foreach ($text in @('256','999','-1','1.5','abc',' 2','٢')) {
    $editor.Text = $text
    if ($editor.Value -ne 255 -or $editor.Text -ne '255') { throw "Принят неверный адрес: $text" }
}
$editor.Text = ''
if ($editor.Value -ne 255) { throw 'Очистка поля потеряла прежний адрес' }
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$editor.GetType().GetMethod('OnLostFocus',$flags).Invoke($editor,@([EventArgs]::Empty)) | Out-Null
if ($editor.Text -ne '255') { throw 'Пустое поле не восстановлено при уходе фокуса' }
$editor.Dispose()
$form = New-Object NextConfig.Form1
$pin = $form.GetType().GetProperty('txtPIN',$flags).GetValue($form,$null)
if (-not $pin.UseSystemPasswordChar) { throw 'ПИН не скрыт по умолчанию' }
$eye = $form.Controls.Find('showPin',$true)[0]
foreach ($expected in @($false,$true)) {
    $eye.GetType().GetMethod('OnClick',$flags).Invoke($eye,@([EventArgs]::Empty)) | Out-Null
    if ($pin.UseSystemPasswordChar -ne $expected) { throw 'Кнопка показа ПИН не переключается' }
}
if ($null -eq $form.Icon) { throw 'Нет значка формы' }
$form.Dispose()
'PASS: адреса 0..255, неверный ввод/вставка, пустое поле, скрытый ПИН и кнопка показа, значок; без COM'

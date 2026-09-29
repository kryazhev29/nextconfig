param([switch]$ShowConfirmation,[string]$AssemblyPath = "$PSScriptRoot\..\NextConfig\bin\Release\NextConfig.exe")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))|Out-Null
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$memory=[IO.MemoryStream]::new()
$writer=[IO.StreamWriter]::new($memory,[Text.UTF8Encoding]::new($false),1024,$true)
$writer.AutoFlush=$true
# Capture only synthetic test data in memory, without reading/writing owner logs.
[NextConfig.AgentLog].GetField('writer',$flags).SetValue($null,$writer)
[NextConfig.AgentLog].GetField('fileDay',$flags).SetValue($null,(Get-Date -Format yyyyMMdd))
$frame=[NextConfig.clBoot].GetMethod('Frame',$flags)
$block=New-Object byte[] 512
foreach($i in @(9,10,11,12,55,56,57,58,59)){$block[$i]=0xA7}
$read=[byte[]](@(85,32,0,0,0,2)+$block)
[NextConfig.AgentLog]::SlgtExchange('TEST',32,$frame.Invoke($null,@([byte]32,[byte[]]@(0))),$read,0,'')
[NextConfig.AgentLog]::SlgtExchange('TEST',33,$frame.Invoke($null,@([byte]33,[byte[]](@(0,0,2)+$block))),[byte[]]@(85,33,0,0),0,'')
[NextConfig.AgentLog]::SlgtExchange('TEST',37,$frame.Invoke($null,@([byte]37,[byte[]]@(0xA7,0xA7))),[byte[]]@(85,37,0,32),0,'')
foreach($cmd in @(41,46)){
 $privateData=[Text.Encoding]::ASCII.GetBytes('9876543210_PRIVATE_RESIDENT')
 [NextConfig.AgentLog]::SlgtExchange('TEST',[byte]$cmd,$frame.Invoke($null,@([byte]$cmd,[byte[]](@(0)+$privateData))),[byte[]](@(85,$cmd,0,0,0,0,0)+$privateData),0,'')
}
[NextConfig.AgentChannel].GetField('token',$flags).SetValue($null,'test-token')
[NextConfig.AgentChannel].GetMethod('Handle',$flags).Invoke($null,@("test-token`ttype`toldPassword`tSECRET_SAMPLE"))|Out-Null
$writer.Flush()
$text=[Text.Encoding]::UTF8.GetString($memory.ToArray())
$lines=@($text -split "`n" | Where-Object {$_} | ForEach-Object {$_|ConvertFrom-Json})
if($lines.Count -ne 6){throw 'Expected six synthetic log records'}
$rx=$lines[0].rx -split ' ';foreach($i in @(15,16,17,18,61,62,63,64,65)){if($rx[$i] -ne '**'){throw "0x20 offset $i"}}
$tx=$lines[1].tx -split ' ';foreach($i in @(20,21,22,23,66,67,68,69,70)){if($tx[$i] -ne '**'){throw "0x21 offset $i"}}
if($lines[2].tx -notlike '* ** ** *'){throw '0x25'}
if($text.Contains('39 38 37 36') -or $text.Contains('SECRET_SAMPLE')){throw 'Sensitive data in log'}
# All writes excluded from the original read whitelist, before any port access.
$readMethod=[NextConfig.clBoot].GetMethod('ReadOldFirmware',$flags)
foreach($cmd in @(33,38,39,40,42,43,45,47,81)){
 $failed=$false
 try{$readMethod.Invoke($null,@($null,[byte]$cmd,[byte[]]@(0),''))|Out-Null}catch{$failed=$_.Exception.ToString().Contains('Команда не разрешена')}
 if(-not $failed){throw "Write allowed via read API: $cmd"}
}
'PASS: secret offsets, private log/chronology payloads, agent typed values, 9 write commands outside read whitelist.'
if($ShowConfirmation){
 [NextConfig.AgentChannel]::Start()
 $form=[NextConfig.OldChangeDialog]::new('Проверка блокировки подтверждения','Синтетическое значение: 1 → 2')
 [Windows.Forms.Application]::Run($form)
}
$writer.Dispose();$memory.Dispose()

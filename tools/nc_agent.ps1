# Клиент канала NextConfig (программа запущена с ключом --agent).
# Примеры:
#   nc_agent.ps1 windows
#   nc_agent.ps1 tree              # верхнее окно, только видимое
#   nc_agent.ps1 tree Form2 all
#   nc_agent.ps1 snap              # снимок верхнего окна, путь в ответе
#   nc_agent.ps1 click "Считать с платы"
#   nc_agent.ps1 click Настройки   # вкладка
#   nc_agent.ps1 type txtServer1 "1.2.3.4:15010"
#   nc_agent.ps1 log 100
param([Parameter(Mandatory = $true)][string]$Command, [Parameter(ValueFromRemainingArguments = $true)][string[]]$Rest)

$info = Join-Path $env:LOCALAPPDATA 'NextConfig\agent.json'
if (-not (Test-Path $info)) { Write-Output '{"ok":false,"error":"NextConfig не запущен с ключом --agent"}'; exit 1 }
$a = Get-Content $info -Raw -Encoding UTF8 | ConvertFrom-Json

$utf8 = New-Object System.Text.UTF8Encoding($false)
$client = New-Object System.Net.Sockets.TcpClient
try {
    $client.Connect('127.0.0.1', [int]$a.port)
} catch {
    Write-Output '{"ok":false,"error":"канал не отвечает: программа закрыта?"}'; exit 1
}
$stream = $client.GetStream()
$line = (@($a.token, $Command) + @($Rest | Where-Object { $_ -ne $null })) -join "`t"
$bytes = $utf8.GetBytes($line + "`n")
$stream.Write($bytes, 0, $bytes.Length)
$reader = New-Object System.IO.StreamReader($stream, $utf8)
$reply = $reader.ReadLine()
$client.Close()

[Console]::OutputEncoding = $utf8
Write-Output $reply

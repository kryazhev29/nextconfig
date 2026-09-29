param([string]$Output = "$PSScriptRoot\..\..\Стенд_Domovoi\NextConfig_QA\oldfw48", [switch]$Dpi120)
# Старый единый экран заменён разделами; проверка использует общий безопасный сценарий.
& "$PSScriptRoot\sections-demo-ui.ps1" -Mode '--demo:oldfw48' -Output $Output -Dpi120:$Dpi120

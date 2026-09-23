# Проверка настроек 485

Доработка: Кряжев А., 2026.

MSBuild Release — 0 ошибок. `powershell -File test_rs485.ps1` — PASS без COM.
Проверены неизменённая сериализация 512 байт, резервы, хвосты строк, неизвестные
значения, встроенные имена. Скорость: payload 174 / полный SLDN 183 (с нуля).

`NextConfig.exe --demo --demo-dpi=120`: активный режим, 115200, адрес 1,
тип 3 отвечает. `--demo-dpi=120` работает только с --demo, задаёт масштаб
содержимого 125% для проверки; Windows 150% остаётся без изменений.

Перед запуском `Get-Process | Where Path -like '*NextConfig*'` был пуст.
Снимки сделаны штатным Стенд_Domovoi/Скрипты/ui_snap.ps1:

- [Блок целиком](screenshots/rs485/block-125.png)
- [Режим](screenshots/rs485/mode-list-125.png), [скорость](screenshots/rs485/baud-list-125.png), [типы](screenshots/rs485/types-list-125.png), [Wiegand](screenshots/rs485/wiegand-list-125.png)
- [Предупреждение C328](screenshots/rs485/c328-warning-125.png)
- [Было → станет](screenshots/rs485/changes-125.png)

На плату ничего не записывалось. На реальном USB и физической шине не проверено.
periphery.ini — UTF-8 рядом с exe; встроенный ресурс содержит тот же список.
Типы/адреса 0–255 сохраняются, неизвестные индексы отображаются числом.

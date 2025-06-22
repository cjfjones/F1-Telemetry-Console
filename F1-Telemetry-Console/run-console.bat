@echo off
echo Starting F1 2024 Telemetry Console...
echo.
echo Make sure F1 2024 UDP telemetry is enabled:
echo Game Options ^> Settings ^> Telemetry Settings
echo - UDP Telemetry: On
echo - UDP IP Address: 127.0.0.1 (if same PC) or this PC's IP
echo - UDP Port: 20777
echo - UDP Format: 2024
echo.
echo Press any key to start the console telemetry receiver...
pause > nul

REM Temporarily change project to console mode
powershell -Command "(Get-Content F1-Telemetry-Console.csproj) -replace '<OutputType>WinExe</OutputType>', '<OutputType>Exe</OutputType>' -replace '<UseWPF>true</UseWPF>', '' | Set-Content F1-Telemetry-Console.csproj"

REM Run console version
dotnet run --project . -- console

REM Restore WPF project settings
powershell -Command "(Get-Content F1-Telemetry-Console.csproj) -replace '<OutputType>Exe</OutputType>', '<OutputType>WinExe</OutputType>' | Set-Content F1-Telemetry-Console.csproj"
powershell -Command "(Get-Content F1-Telemetry-Console.csproj) -replace '</TargetFramework>', '</TargetFramework>\n    <UseWPF>true</UseWPF>' | Set-Content F1-Telemetry-Console.csproj"

echo.
echo Console application closed. Press any key to exit...
pause > nul 
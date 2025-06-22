@echo off
echo Starting F1 2024 Telemetry Receiver...
echo.
echo Make sure F1 2024 UDP telemetry is enabled:
echo Game Options ^> Settings ^> Telemetry Settings
echo - UDP Telemetry: On
echo - UDP IP Address: 127.0.0.1 (if same PC) or this PC's IP
echo - UDP Port: 20777
echo - UDP Format: 2024
echo.
echo Press any key to start the telemetry receiver...
pause > nul
dotnet run
echo.
echo Application closed. Press any key to exit...
pause > nul 
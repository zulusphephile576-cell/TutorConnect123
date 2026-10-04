@echo off
rem TutorConnect: let phones on the same Wi-Fi open the site (needed for QR check-in during development).
rem Double-click this file. Windows asks for administrator permission once.
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0EnablePhoneAccess.ps1"
echo.
pause

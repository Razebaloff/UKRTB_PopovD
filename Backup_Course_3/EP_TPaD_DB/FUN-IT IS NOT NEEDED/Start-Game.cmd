@echo off
cd /d "%~dp0"
if not exist "Ready\VerdantGuard.exe" (
    echo Game build is missing. Open the solution in Visual Studio and build Verdant.Game.
    pause
    exit /b 1
)
start "Verdant Guard" "Ready\VerdantGuard.exe"

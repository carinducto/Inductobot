@echo off
echo Starting Inductobot with Debug Console
echo =====================================

REM Build the application first
echo Building application...
dotnet build --configuration Debug
if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Starting with debug console...
echo Press Ctrl+C to stop
echo.

REM Run with trace mode (both console and file logging)
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Inductobot.exe --trace
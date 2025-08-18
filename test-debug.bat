@echo off
echo Testing Inductobot Debug Console System
echo =====================================

echo.
echo 1. Testing basic debug console:
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Inductobot.exe --debug --debug-level Information

echo.
echo 2. Testing verbose mode (debug level + console):
timeout /t 3
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Inductobot.exe --verbose

echo.
echo 3. Testing trace mode (trace level + file logging):
timeout /t 3
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Inductobot.exe --trace

echo.
echo 4. Testing combined options:
timeout /t 3
bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Inductobot.exe --debug --debug-level Debug --log-file --log-file-level Trace

echo.
echo Debug console testing completed!
pause
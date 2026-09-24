@echo off
setlocal
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0Collect-BuildReferences.ps1"
set "collector_exit=%ERRORLEVEL%"
echo.
pause
exit /b %collector_exit%

@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\build-installer.ps1" %*
exit /b %ERRORLEVEL%

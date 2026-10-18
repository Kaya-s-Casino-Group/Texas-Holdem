@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\BattleToolkitSync\sync.ps1" %*
set "sync_status=%ERRORLEVEL%"
if "%~1"=="" pause
exit /b %sync_status%

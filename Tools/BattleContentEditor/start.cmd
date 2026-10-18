@echo off
if not exist "%~dp0..\..\External\BattleToolkit\start.cmd" (
  echo Please run: git submodule update --init --recursive
  pause
  exit /b 1
)
call "%~dp0..\..\External\BattleToolkit\start.cmd" %*
exit /b %errorlevel%

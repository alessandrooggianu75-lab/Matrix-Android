@echo off
setlocal
if exist "%~dp0Matrix.scr" (
  start "" "%~dp0Matrix.scr" /c
  exit /b 0
)
if exist "%LOCALAPPDATA%\MatrixScreensaver\Matrix.scr" (
  start "" "%LOCALAPPDATA%\MatrixScreensaver\Matrix.scr" /c
  exit /b 0
)
echo Matrix.scr non trovato.
pause

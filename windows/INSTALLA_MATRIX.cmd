@echo off
setlocal
set "SRC=%~dp0Matrix.scr"
set "DEST=%LOCALAPPDATA%\MatrixScreensaver"
if not exist "%SRC%" (
  echo ERRORE: Matrix.scr non trovato nella stessa cartella.
  pause
  exit /b 1
)
if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%SRC%" "%DEST%\Matrix.scr" >nul
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%DEST%\Matrix.scr" /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d "1" /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut /t REG_SZ /d "300" /f >nul
echo.
echo Matrix Screensaver installato per il tuo utente Windows.
echo Apertura impostazioni salvaschermo...
start "" rundll32.exe shell32.dll,Control_RunDLL desk.cpl,,1
exit /b 0

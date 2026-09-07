@echo off
if exist "%TEMP%\DiX_GO.CMD" del "%TEMP%\DiX_GO.CMD" >nul 2>nul
"%~dp0DiX.exe" GO %*
if exist "%TEMP%\DiX_GO.CMD" call "%TEMP%\DiX_GO.CMD"
if exist "%TEMP%\DiX_GO.CMD" del "%TEMP%\DiX_GO.CMD" >nul 2>nul

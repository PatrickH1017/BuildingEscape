@echo off
rem Starts the game that is in the Game folder next to this file, with that folder as its working folder. Every word given to this file is passed on to the game (the self-play uses that).
rem The game is started and this window closes at once; with ETR_WAIT set (the self-play does) this file waits for the game and gives back its exit code.
cd /d "%~dp0Game" 2>nul
if errorlevel 1 goto missing
if not exist "EmployeesTakeRevenge.exe" goto missing
if defined ETR_WAIT goto waiting
start "" "EmployeesTakeRevenge.exe" %*
exit /b 0
:waiting
start "" /wait "EmployeesTakeRevenge.exe" %*
exit /b %errorlevel%
:missing
echo Game\EmployeesTakeRevenge.exe was not found. Unzip the whole folder first, then run this again.
pause
exit /b 2

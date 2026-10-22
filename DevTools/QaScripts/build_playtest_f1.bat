@echo off
rem Builds the Windows player of the game for the owner's own playtest, with the F1 cheat panel switched on (a testmode.txt next to the exe).
rem Double-click it. The Unity Editor must be CLOSED on this project. Result: Builds\Playtest_F1_2026-10-21\ (and a .zip of it).
rem What happened is written to Builds\build_playtest_f1_status.txt, Unity's own log to Builds\build_playtest_f1.log. Written 2026-10-21, a Claude session.
setlocal EnableExtensions
for %%I in ("%~dp0..\..") do set "PROJ=%%~fI"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe"
set "OUT=%PROJ%\Builds\Playtest_F1_2026-10-21"
set "LOG=%PROJ%\Builds\build_playtest_f1.log"
set "ST=%PROJ%\Builds\build_playtest_f1_status.txt"
if not exist "%PROJ%\Builds" mkdir "%PROJ%\Builds"
echo Build started %date% %time% > "%ST%"
if not exist "%UNITY%" goto :nounity
if exist "%PROJ%\Temp\UnityLockfile" goto :editoropen
if exist "%OUT%" goto :exists
echo.
echo Building the game. This takes several minutes: do not close this window.
echo.
start "" /wait "%UNITY%" -batchmode -nographics -projectPath "%PROJ%" -executeMethod BuildingEscape.EditorTools.DemoBuild.Build -buildOutput "%OUT%" -logFile "%LOG%"
echo Unity exit code %ERRORLEVEL% >> "%ST%"
findstr /c:"[DemoBuild] result" "%LOG%" >> "%ST%"
findstr /c:"error CS" "%LOG%" >> "%ST%"
if not exist "%OUT%\EmployeesTakeRevenge.exe" goto :failed
echo F1 cheat panel is ON in this build because this file is next to the exe. Delete this file to switch it off. > "%OUT%\testmode.txt"
for /d %%D in ("%OUT%\*DoNotShip*") do rd /s /q "%%D"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUT%' -DestinationPath '%OUT%.zip' -CompressionLevel Optimal"
xcopy "%OUT%" "%USERPROFILE%\Downloads\Playtest_F1_2026-10-21\" /E /I /Y /Q
copy /Y "%OUT%.zip" "%USERPROFILE%\Downloads\" 
echo DONE OK: %OUT%\EmployeesTakeRevenge.exe (zip: %OUT%.zip) and a copy in %USERPROFILE%\Downloads >> "%ST%"
echo.
echo DONE. The game is in %OUT% and in your Downloads folder (Playtest_F1_2026-10-21).
goto :done
:nounity
echo STOPPED: Unity 6000.0.45f1 was not found at %UNITY% >> "%ST%"
echo Unity 6000.0.45f1 was not found where expected.
goto :done
:editoropen
echo STOPPED: the Unity Editor looks open on this project (Temp\UnityLockfile exists). Close it and run this again. >> "%ST%"
echo Close the Unity Editor first, then run this again.
goto :done
:exists
echo STOPPED: %OUT% already exists (not overwriting a build). >> "%ST%"
echo That build folder already exists.
goto :done
:failed
echo FAILED: no exe was built. See Builds\build_playtest_f1.log >> "%ST%"
echo The build failed. See Builds\build_playtest_f1.log
:done
echo Finished %date% %time% >> "%ST%"
echo.
pause

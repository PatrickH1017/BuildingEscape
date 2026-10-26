# The friends' build of 2026-10-23 (the demo goes to floor 6): the real project into the build copy C:\bqfinal, an IL2CPP player, the send copy with the README of the day, the safety scan, a zip, then the built game plays itself (solo, then two built players over the loopback).
# It is ship.ps1 without its first half (the work is applied with apply_checked.ps1 now, the old QA tree C:\bqm is stale) and with the README as a parameter. A disk watch runs meanwhile (C: was nearly full: below 1.5 GB free it stops OUR build processes, never the owner's Editor).
# Summary: C:\bqs\ship_summary.txt (read that). Usage: .\build_third.ps1 [-Name 2026-10-23a] [-Raw Final32_2026-10-23] [-Tag fm1] [-Backend il2cpp]
param([string]$Name = '2026-10-23a', [string]$Raw = 'Final32_2026-10-23', [string]$Tag = 'fm1', [string]$Backend = 'il2cpp', [string]$ReadmeName = 'README_SEND_2026-10-23.txt')
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$readme = "$real\DevTools\QaScripts\asks_2026-10-23\third_agent\$ReadmeName"
$sum = 'C:\bqs\ship_summary.txt'
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Build $Name started $(Get-Date -Format 'HH:mm:ss'); free on C: $([math]::Round((Get-PSDrive C).Free / 1GB, 1)) GB" -Encoding UTF8
Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'C:\bqs\disk_watch.ps1', '-MinGB', '1.5', '-Seconds', '10800' -WindowStyle Hidden
Remove-Item C:\bqs\final_summary.txt -ErrorAction SilentlyContinue
& 'C:\bqs\final_build.ps1' -Raw $Raw -Send "Playtest_$Name" -Backend $Backend -Readme $readme
foreach ($l in (Get-Content C:\bqs\final_summary.txt)) { Out-Sum $l }
if (Test-Path 'C:\bqs\DISK_LOW.txt') { Out-Sum ('DISK LOW: ' + (Get-Content 'C:\bqs\DISK_LOW.txt')) }
$zip = "$real\Builds\Playtest_$Name.zip"
if (-not (Test-Path $zip)) { Out-Sum 'STOPPED: no zip'; Out-Sum 'finished'; return }
$dest = "C:\bqs\Unzipped_$Tag"
if (Test-Path $dest) { Out-Sum "STOPPED: $dest exists (use another -Tag)"; Out-Sum 'finished'; return }
Expand-Archive -Path $zip -DestinationPath $dest -Force
Remove-Item C:\bqs\final_ap_summary.txt -ErrorAction SilentlyContinue
& 'C:\bqs\final_autopilot.ps1' -Tag $Tag -Exe "$dest\Playtest_$Name\EmployeesTakeRevenge.exe"
foreach ($l in (Get-Content C:\bqs\final_ap_summary.txt)) { Out-Sum $l }
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"

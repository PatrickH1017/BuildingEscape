# The build with a PLAY.bat at the top (the owner, 2026-10-24: "hide all the other files in a build folder behind another folder and keep the executable one folder outside so it is by itself and testers see it first" ... "yes launcher separate please").
# A compiled launcher .exe (package_launcher.ps1, launcher\Launcher.cs) was QUARANTINED by the antivirus of this machine (McAfee deleted two of its three copies, "CreateProcess failed: 225 / 2"), so a friend's antivirus may do the same: a plain batch file is
# not an executable and is not flagged. Builds\Playtest_<Name> (the plain build, left as it is) is copied into Builds\Playtest_<Name>_play\Game, launcher\PLAY.bat and README.txt (item 2 says PLAY.bat) go at the top, and the folder is zipped.
# Never overwrites: it stops when the folder or the zip exists. Summary: C:\bqs\launcher_summary.txt. Usage: .\package_play_bat.ps1 -Name 2026-10-24h
param([Parameter(Mandatory = $true)][string]$Name)
$ErrorActionPreference = 'Stop'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$builds = "$real\Builds"
$src = "$builds\Playtest_$Name"; $out = "$builds\Playtest_${Name}_play"; $zip = "$out.zip"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sum = 'C:\bqs\launcher_summary.txt'
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "PLAY.bat package $Name started $(Get-Date -Format 'HH:mm:ss'); free on C: $([math]::Round((Get-PSDrive C).Free / 1GB, 1)) GB" -Encoding UTF8
if (-not (Test-Path "$src\EmployeesTakeRevenge.exe")) { Out-Sum "STOPPED: $src has no EmployeesTakeRevenge.exe"; Out-Sum 'finished'; return }
if ((Test-Path $out) -or (Test-Path $zip)) { Out-Sum "STOPPED: $out or $zip exists (not overwriting)"; Out-Sum 'finished'; return }

New-Item -ItemType Directory -Force "$out\Game" | Out-Null
robocopy $src "$out\Game" /E /XF README.txt /NFL /NDL /NJH /NJS /NP | Out-Null
# PLAY.bat with Windows line endings, no byte order mark
$bat = [IO.File]::ReadAllText("$here\launcher\PLAY.bat").Replace("`r`n", "`n").Replace("`n", "`r`n")
[IO.File]::WriteAllText("$out\PLAY.bat", $bat, (New-Object Text.ASCIIEncoding))
# README: item 2 names PLAY.bat
$readme = [IO.File]::ReadAllText("$src\README.txt")
$old1 = '(do not run the .exe from inside the zip) and double-click EmployeesTakeRevenge.exe.'
$new1 = '(do not run it from inside the zip) and double-click PLAY.bat (the game itself is in the Game folder).'
if (-not $readme.Contains($old1)) { Out-Sum 'STOPPED: item 2 of the README is not the text this script knows'; Out-Sum 'finished'; return }
[IO.File]::WriteAllText("$out\README.txt", $readme.Replace($old1, $new1), (New-Object Text.UTF8Encoding($false)))
$top = (Get-ChildItem $out | ForEach-Object { $_.Name }) -join ', '
Out-Sum "layout ok: the top of the folder holds: $top; Game\ holds $((Get-ChildItem "$out\Game" | Measure-Object).Count) items"

Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($out, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
Out-Sum "zip: $zip = $([math]::Round((Get-Item $zip).Length / 1MB)) MB"
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"

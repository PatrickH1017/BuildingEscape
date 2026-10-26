# The build with a launcher (the owner, 2026-10-24: "hide all the other files in a build folder behind another folder and keep the executable one folder outside so it is by itself and testers see it first" ... "yes launcher separate please"):
# Builds\Playtest_<Name> (the plain build, left as it is) is copied into Builds\Playtest_<Name>_launcher\Game, a small launcher (launcher\Launcher.cs, compiled with the csc that comes with Windows, the game's icon) is put at the top
# as EmployeesTakeRevenge.exe next to README.txt, and the folder is zipped. Never overwrites: it stops when the folder or the zip exists. Summary: C:\bqs\launcher_summary.txt.
# Usage: .\package_launcher.ps1 -Name 2026-10-24h
param([Parameter(Mandatory = $true)][string]$Name)
$ErrorActionPreference = 'Stop'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$builds = "$real\Builds"
$src = "$builds\Playtest_$Name"; $out = "$builds\Playtest_${Name}_launcher"; $zip = "$out.zip"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$sum = 'C:\bqs\launcher_summary.txt'
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Launcher package $Name started $(Get-Date -Format 'HH:mm:ss'); free on C: $([math]::Round((Get-PSDrive C).Free / 1GB, 1)) GB" -Encoding UTF8
if (-not (Test-Path "$src\EmployeesTakeRevenge.exe")) { Out-Sum "STOPPED: $src has no EmployeesTakeRevenge.exe"; Out-Sum 'finished'; return }
if ((Test-Path $out) -or (Test-Path $zip)) { Out-Sum "STOPPED: $out or $zip exists (not overwriting)"; Out-Sum 'finished'; return }

# the layout: Game\ holds everything of the plain build but its README; the launcher and the README are at the top
New-Item -ItemType Directory -Force "$out\Game" | Out-Null
robocopy $src "$out\Game" /E /XF README.txt /NFL /NDL /NJH /NJS /NP | Out-Null
if (Test-Path "$src\README.txt") { Copy-Item "$src\README.txt" "$out\README.txt" } else { Out-Sum 'WARNING: the plain build has no README.txt' }

# the launcher, with the game's own icon
Add-Type -AssemblyName System.Drawing
$icoFile = Join-Path $env:TEMP 'etr_launcher.ico'
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon("$out\Game\EmployeesTakeRevenge.exe")
$fs = [IO.File]::Create($icoFile); $icon.Save($fs); $fs.Close()
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$cscOut = & $csc /nologo /target:winexe /optimize+ "/out:$out\EmployeesTakeRevenge.exe" "/win32icon:$icoFile" /r:System.Windows.Forms.dll /r:System.dll "$here\launcher\Launcher.cs" 2>&1
if (-not (Test-Path "$out\EmployeesTakeRevenge.exe")) { Out-Sum ('STOPPED: the launcher did not compile: ' + ($cscOut -join ' ')); Out-Sum 'finished'; return }
$top = (Get-ChildItem $out | ForEach-Object { $_.Name }) -join ', '
Out-Sum "layout ok: the top of the folder holds: $top; Game\ holds $((Get-ChildItem "$out\Game" | Measure-Object).Count) items; launcher $([math]::Round((Get-Item "$out\EmployeesTakeRevenge.exe").Length / 1KB)) KB"

# the zip (forward slashes in the entries, the folder as the top of it, as the plain build's zip)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($out, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
Out-Sum "zip: $zip = $([math]::Round((Get-Item $zip).Length / 1MB)) MB"
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"

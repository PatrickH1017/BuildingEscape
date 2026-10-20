# Builds the player from BE_qa and lets the BUILT game play the Intro once, taking two pictures of every beat (see QaAutopilot.RunIntroShots).
# Usage: .\qa_shots.ps1 -Tag sh1 [-Model male_b] [-NoBuild]   -> pictures in <Tag>_shots\s0a.png ... s6b.png, summary in qa_summary_<Tag>.txt
param([string]$Tag = 'sh1', [string]$Model = '', [switch]$NoBuild)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\BE_qa"
$sum = "$here\qa_summary_$Tag.txt"
$exeDir = "$here\Builds\Intro_shots"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8; Write-Output $t }
Set-Content -Path $sum -Value "Intro shots $Tag started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8

if (-not $NoBuild) {
  robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  New-Item -ItemType Directory -Force $exeDir | Out-Null
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-nographics', '-projectPath', $proj, '-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', $exeDir, '-logFile', "$here\${Tag}_build.log" -PassThru
  if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'build TIMED OUT' }
  $line = Select-String -Path "$here\${Tag}_build.log" -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
  Out-Sum "=== build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() -replace '\\.*$', '' } else { 'NO RESULT LINE' })"
  $errs = @(Select-String -Path "$here\${Tag}_build.log" -Pattern 'error CS')
  Out-Sum "    compile errors in the build log: $($errs.Count)"
  $errs | Select-Object -First 12 | ForEach-Object { Out-Sum ('    ' + $_.Line.Trim()) }
}
$exe = "$exeDir\EmployeesTakeRevenge.exe"
if (-not (Test-Path $exe)) { Out-Sum 'STOPPED: no exe'; return }

$dir = "$here\${Tag}_shots"; New-Item -ItemType Directory -Force $dir | Out-Null
$modelArg = if ($Model) { "-be-intro-model $Model" } else { '' }
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$here\hd.ps1" -Exe $exe -Arguments "-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -be-autopilot -be-autopilot-net introshots $modelArg -be-out `"$dir`" -logFile `"$here\${Tag}_player.log`"" -TimeoutSeconds 300
$rep = "$dir\autopilot_report.txt"
$pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
$fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
Out-Sum "=== shots ($r, $([int]$sw.Elapsed.TotalSeconds) s): PASS $pass FAIL $fail"
if (Test-Path $rep) { Select-String -Path $rep -Pattern '^FAIL|^logged|^finished' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length))) } }
Out-Sum "pictures: $((Get-ChildItem $dir -Filter *.png -ErrorAction SilentlyContinue).Count); finished $(Get-Date -Format 'HH:mm:ss')"

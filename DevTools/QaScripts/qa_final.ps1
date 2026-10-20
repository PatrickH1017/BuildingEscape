# The final stage of the demo Q/A, unattended: put the newest tooling into BE_qa, compile + pure tests, build the Windows player from BE_qa, package it
# (zip, no "DoNotShip" folder, a README for the friends), then PLAY the unzipped build: solo (menu -> docks -> floors 1-4 -> demo complete) and as
# host + client over the loopback. Summary: qa_summary_<Tag>.txt. Nothing else may use the loopback port or %TEMP%\be_sig_* meanwhile.
# Usage: .\qa_final.ps1 -Tag f1 [-Date 2026-10-16]
param([string]$Tag = 'f1', [string]$Date = '2026-10-16')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\BE_qa"
$sum = "$here\qa_summary_$Tag.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Final stage $Tag started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
function Refill-Cache { robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null }

# 1 + 2: the newest tooling, compile, pure tests
& "$here\qa_new_apply.ps1" -Tree $proj | Out-Null
& "$here\qa_regress.ps1" -Prefix "${Tag}c" -Methods @() | Out-Null
Get-Content "$here\qa_summary_${Tag}c.txt" | Select-Object -Skip 1 | ForEach-Object { Out-Sum $_ }

# 3: the build
$out = "$here\Builds\Demo_$Date"
if (Test-Path $out) { Out-Sum "(an older build folder $out exists: building into ${out}_new)"; $out = "${out}_new" }
New-Item -ItemType Directory -Force $out | Out-Null
Refill-Cache
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-projectPath',$proj,'-executeMethod','BuildingEscape.EditorTools.DemoBuild.Build','-buildOutput',$out,'-logFile',"$here\${Tag}_build.log" -PassThru
if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'build TIMED OUT' }
$line = Select-String -Path "$here\${Tag}_build.log" -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
Out-Sum "=== build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() -replace '\\.*$', '' } else { 'NO RESULT LINE' })"
$cserr = (Select-String -Path "$here\${Tag}_build.log" -Pattern 'error CS').Count
Out-Sum "    compile errors in the build log: $cserr"
if (-not (Test-Path "$out\EmployeesTakeRevenge.exe")) { Out-Sum 'STOPPED: no exe'; return }

# 4: package: a clean copy without the Burst debug folder, a README, a zip
$pkg = "$here\Package\EmployeesTakeRevenge_Demo_$Date"
if (Test-Path $pkg) { Remove-Item $pkg -Recurse -Force }
robocopy $out $pkg /MIR /MT:16 /XD BuildingEscape_BurstDebugInformation_DoNotShip /NFL /NDL /NJH /NJS /NP | Out-Null
Copy-Item "$here\qa_new\README_DEMO.txt" "$pkg\README.txt" -Force
$zip = "$here\Package\EmployeesTakeRevenge_Demo_$Date.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
$mb = [int]((Get-Item $zip).Length / 1MB)
Out-Sum "=== package: $pkg ; zip $zip = $mb MB"

# 5: play the UNZIPPED build solo (a fresh folder, as a friend would have it)
$test = "$here\Unzipped\EmployeesTakeRevenge_Demo_$Date"
if (Test-Path "$here\Unzipped") { Remove-Item "$here\Unzipped" -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath "$here\Unzipped" -Force
$exe = "$test\EmployeesTakeRevenge.exe"
$ap1 = "$here\${Tag}_ap_solo"; New-Item -ItemType Directory -Force $ap1 | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$here\hd.ps1" -Exe $exe -Arguments "-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -be-autopilot -be-out `"$ap1`" -logFile `"$here\${Tag}_ap_solo_player.log`"" -TimeoutSeconds 900
$rep = "$ap1\autopilot_report.txt"
$pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
$fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
$fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
Out-Sum "=== autopilot solo on the unzipped build: $r in $([int]$sw.Elapsed.TotalSeconds) s; PASS $pass FAIL $fail; $fin"
if (Test-Path $rep) { Select-String -Path $rep -Pattern '^perf|^FAIL|^logged' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length))) } }

# 6: host + client over the loopback, two built players (one multi-process thing at a time on this machine: wait for the other session, take the lock)
$lock = Join-Path $env:TEMP 'be_mp_busy.txt'
$waited = 0
while ($waited -lt 1800) {
  $busyFile = (Test-Path $lock) -and ((Get-Item $lock).LastWriteTime -gt (Get-Date).AddMinutes(-20))
  $busyProc = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'executeMethod' }).Count -gt 0
  if (-not $busyFile -and -not $busyProc) { break }
  Start-Sleep -Seconds 10; $waited += 10
}
Set-Content -Path $lock -Value "bad83f09 qa_final.ps1 net autopilot $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
$apH = "$here\${Tag}_ap_host"; $apC = "$here\${Tag}_ap_client"
New-Item -ItemType Directory -Force $apH, $apC | Out-Null
$argsH = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net host -be-out `"$apH`" -logFile `"$here\${Tag}_ap_host_player.log`""
$argsC = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net client -be-out `"$apC`" -logFile `"$here\${Tag}_ap_client_player.log`""
$sw = [Diagnostics.Stopwatch]::StartNew()
$jobH = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command',"& '$here\hd.ps1' -Exe '$exe' -Arguments '$argsH' -TimeoutSeconds 900 -Desktop ClaudeHiddenH *> '$here\${Tag}_ap_host_hd.txt'" -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 12
$jobC = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command',"& '$here\hd.ps1' -Exe '$exe' -Arguments '$argsC' -TimeoutSeconds 900 -Desktop ClaudeHiddenC *> '$here\${Tag}_ap_client_hd.txt'" -WindowStyle Hidden -PassThru
$jobH.WaitForExit(1000000) | Out-Null; $jobC.WaitForExit(1000000) | Out-Null
Remove-Item $lock -Force -ErrorAction SilentlyContinue
foreach ($who in 'host', 'client') {
  $d = if ($who -eq 'host') { $apH } else { $apC }
  $rep = "$d\autopilot_report.txt"
  $pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
  $fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
  $fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
  Out-Sum "=== autopilot $who (two built players, loopback) in $([int]$sw.Elapsed.TotalSeconds) s; PASS $pass FAIL $fail; $fin"
  if (Test-Path $rep) { Select-String -Path $rep -Pattern '^perf|^FAIL|^logged' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length))) } }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"

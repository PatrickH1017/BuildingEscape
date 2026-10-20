# Puts the tested friends' build (zip + the unzipped copy + the Q/A results) into the repo's Builds\ folder (git-ignored; OneDrive syncs it) and checks the copies by SHA-256.
# Usage: .\deliver_build.ps1 -Name Demo_2026-10-16_intro -Date 2026-10-16-intro -Tag f4 [-IntroTag in3]
param([string]$Name = 'Demo_2026-10-16_intro', [string]$Date = '2026-10-16-intro', [string]$Tag = 'f4', [string]$IntroTag = 'in3')
$ErrorActionPreference = 'Stop'
$A = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-Eternum\bad83f09-2f5c-45af-91fd-630f53fb6650\scratchpad'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$dest = "$real\Builds\$Name"
if (Test-Path $dest) { throw "$dest already exists: not overwriting a delivered build" }
New-Item -ItemType Directory -Force $dest | Out-Null
$zipName = "EmployeesTakeRevenge_Demo_$Date.zip"
$zipSrc = "$A\Package\$zipName"
Copy-Item $zipSrc "$dest\$zipName"
robocopy "$A\Package\EmployeesTakeRevenge_Demo_$Date" "$dest\EmployeesTakeRevenge_Demo_$Date" /MIR /MT:16 /NFL /NDL /NJH /NJS /NP | Out-Null

# the Q/A results: summaries, reports, and a handful of pictures
$qa = "$dest\QA_results"
New-Item -ItemType Directory -Force $qa | Out-Null
Copy-Item "$A\qa_summary_$Tag.txt" "$qa\QA_summary_final_stage.txt" -Force
if (Test-Path "$A\qa_summary_$IntroTag.txt") { Copy-Item "$A\qa_summary_$IntroTag.txt" "$qa\QA_summary_intro_on_the_unzipped_build.txt" -Force }
foreach ($m in @(@('solo', "${Tag}_ap_solo"), @('host', "${Tag}_ap_host"), @('client', "${Tag}_ap_client"), @('intro_solo', "${IntroTag}_solo"), @('intro_skip', "${IntroTag}_skip"), @('intro_offline', "${IntroTag}_offline"), @('intro_offlineskip', "${IntroTag}_offlineskip"), @('intro_host', "${IntroTag}_host"), @('intro_client', "${IntroTag}_client"))) {
  $dir = "$A\$($m[1])"
  if (-not (Test-Path $dir)) { continue }
  if (Test-Path "$dir\autopilot_report.txt") { Copy-Item "$dir\autopilot_report.txt" "$qa\autopilot_$($m[0])_report.txt" -Force }
}
$pics = @(
  @("${Tag}_ap_solo", '01_menu'), @("${Tag}_ap_solo", '02_docks_arrival'), @("${Tag}_ap_solo", '03_docks_vendor'), @("${Tag}_ap_solo", 'floor1_2_outpost'),
  @("${Tag}_ap_solo", 'floor4_2_outpost'), @("${Tag}_ap_solo", 'floor4_5_pew'), @("${Tag}_ap_solo", 'docks_demo_complete'),
  @("${IntroTag}_solo", 'i1_boss_first_line'), @("${IntroTag}_solo", 'i2_desk_bonuses'), @("${IntroTag}_solo", 'i3_window_jet'), @("${IntroTag}_solo", 'i4_last_line'), @("${IntroTag}_solo", 'i5_docks_after_intro'),
  @("${IntroTag}_offline", 'io1_boss_first_line'), @("${IntroTag}_offline", 'io2_window_jet'), @("${IntroTag}_offline", 'io3_docks_after_intro'),
  @("${IntroTag}_host", 'ih1_host_intro'), @("${IntroTag}_client", 'ic1_client_intro'))
foreach ($p in $pics) {
  $src = "$A\$($p[0])\$($p[1]).png"
  if (Test-Path $src) { Copy-Item $src "$qa\$($p[0])_$($p[1]).png" -Force } else { "(no picture $($p[0])\$($p[1]).png)" }
}
Get-ChildItem "$A\${Tag}_ap_host\*.png", "$A\${Tag}_ap_client\*.png" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'n3_|n4_' } | ForEach-Object { Copy-Item $_.FullName "$qa\$($_.Directory.Name)_$($_.Name)" -Force }

# integrity
$h1 = (Get-FileHash $zipSrc -Algorithm SHA256).Hash
$h2 = (Get-FileHash "$dest\$zipName" -Algorithm SHA256).Hash
$srcFiles = (Get-ChildItem "$A\Package\EmployeesTakeRevenge_Demo_$Date" -Recurse -File).Count
$dstFiles = (Get-ChildItem "$dest\EmployeesTakeRevenge_Demo_$Date" -Recurse -File).Count
"zip: $zipName  $([math]::Round((Get-Item "$dest\$zipName").Length / 1MB, 1)) MB  SHA-256 $h2  (source copy equal: $($h1 -eq $h2))"
"unzipped build: $dstFiles files (source $srcFiles)"
"Q/A results: $((Get-ChildItem $qa -File).Count) files"

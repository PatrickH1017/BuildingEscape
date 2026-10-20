# Sets LastWriteTime to now for every file under the given roots that is older than a day (Windows removes old files from %TEMP%; the real project is in OneDrive and is not affected).
param([string[]]$Roots)
$cut = (Get-Date).AddDays(-1); $now = Get-Date; $n = 0
foreach ($r in $Roots) {
  if (-not (Test-Path $r)) { continue }
  Get-ChildItem $r -Recurse -File -Force -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $cut } | ForEach-Object { try { $_.LastWriteTime = $now; $n++ } catch {} }
}
"touched $n files"

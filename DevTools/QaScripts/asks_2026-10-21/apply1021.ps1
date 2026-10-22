# Applies the files I changed in the scratch tree BE_dev (against the baseline fingerprint of the mirror) to the REAL project, with a backup of every replaced file.
# A file I changed that somebody else has changed in the real project since the mirror is NOT overwritten (the script stops and lists it); files I did not touch are never copied.
# Usage: .\apply1021.ps1 -WhatIf      (lists)      |      .\apply1021.ps1      (applies)
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev',
      [string]$Real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape',
      [string]$Baseline = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\baseline_1021_dev.tsv',
      [string]$Backup = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaBackups\before_asks_2026-10-21',
      [string[]]$Roots = @('Assets\Scripts', 'Assets\Editor'),
      [string[]]$Exclude = @('Assets\Editor\NetDev\NetDevShaderProbe.cs', 'Assets\Editor\NetDev\NetDevShaderProbe.cs.meta'),   # (scratch-only: it re-imported the Synty shader graphs in the scratch copy)
      [switch]$WhatIf)
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p)
  $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0
  $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)).Replace('-', '')
  $ms.Dispose()
  return $h
}
$base = @{}
foreach ($l in [IO.File]::ReadAllLines($Baseline)) { $k = $l.Split("`t"); if ($k.Length -ge 2) { $base[$k[0]] = $k[1] } }
$todo = @(); $conflicts = @(); $same = 0
foreach ($root in $Roots) {
  $rt = Join-Path $Tree $root
  if (-not (Test-Path $rt)) { continue }
  Get-ChildItem $rt -Recurse -File -Force | ForEach-Object {
    $rel = $_.FullName.Substring($Tree.Length + 1)
    if ($Exclude -contains $rel) { return }
    $th = Norm-Hash $_.FullName
    $bh = if ($base.ContainsKey($rel)) { $base[$rel] } else { $null }
    if ($bh -eq $th) { $same++; return }                                   # (not changed by me)
    $realPath = Join-Path $Real $rel
    if (-not (Test-Path $realPath)) { $todo += [pscustomobject]@{ rel = $rel; kind = 'new' }; return }
    $rh = Norm-Hash $realPath
    if ($rh -eq $th) { return }                                           # (already the same in the real project)
    if ($bh -ne $null -and $rh -eq $bh) { $todo += [pscustomobject]@{ rel = $rel; kind = 'changed' } }
    else { $conflicts += $rel }                                           # (the real file is not what I started from: somebody else changed it)
  }
}
"unchanged by me: $same files; to apply: $($todo.Count) ($(@($todo | Where-Object kind -eq 'new').Count) new)"
$todo | Sort-Object kind, rel | ForEach-Object { "  {0,-8} {1}" -f $_.kind, $_.rel }
if ($conflicts.Count) { "CONFLICT: somebody else changed these in the real project since my copy was made - nothing was applied:"; $conflicts | ForEach-Object { "  $_" }; return }
if ($WhatIf) { return }
foreach ($c in $todo) {
  $src = Join-Path $Tree $c.rel; $dst = Join-Path $Real $c.rel
  if ($c.kind -eq 'changed') {
    $bk = Join-Path $Backup $c.rel
    New-Item -ItemType Directory -Force (Split-Path $bk) | Out-Null
    if (-not (Test-Path $bk)) { Copy-Item $dst $bk -Force }               # (the first backup is the original)
  }
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  Copy-Item $src $dst -Force
}
$bad = 0
foreach ($c in $todo) { if ((Norm-Hash (Join-Path $Tree $c.rel)) -ne (Norm-Hash (Join-Path $Real $c.rel))) { $bad++; "MISMATCH after copy: $($c.rel)" } }
"applied $($todo.Count) files, mismatches: $bad (backups of the replaced files in $Backup)"

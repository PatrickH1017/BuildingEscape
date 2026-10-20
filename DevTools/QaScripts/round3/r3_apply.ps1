# Applies the verified work of the scratch tree BE_dev to the REAL project: every file that differs from the real project (plus new ones), with a backup.
# Safety: a file the real project has changed since the baseline manifest (somebody else's work) is NOT overwritten (the script stops and lists them).
# Usage: .\r3_apply.ps1 -WhatIf          (lists)      |      .\r3_apply.ps1 -Backup <dir>      (applies)
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev',
      [string]$Real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape',
      [string]$Baseline = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\baseline_real_1745.tsv',
      [string]$Backup = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaBackups\before_round3_2026-10-17',
      [string[]]$Roots = @('Assets\Scripts', 'Assets\Editor', 'Assets\Resources', 'ProjectSettings', 'Packages', 'DevTools\QaScripts'),
      [string[]]$Exclude = @(),
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
$changed = @(); $conflicts = @(); $removed = @()
foreach ($root in $Roots) {
  $rt = Join-Path $Tree $root
  if (Test-Path $rt) {
    Get-ChildItem $rt -Recurse -File -Force | ForEach-Object {
      $rel = $_.FullName.Substring($Tree.Length + 1)
      if ($Exclude | Where-Object { $rel -like $_ }) { return }
      $realPath = Join-Path $Real $rel
      $th = Norm-Hash $_.FullName
      if (-not (Test-Path $realPath)) { $changed += [pscustomobject]@{ rel = $rel; kind = 'new' }; return }
      $rh = Norm-Hash $realPath
      if ($rh -eq $th) { return }
      # the real file differs from mine: is that because I changed it (it still equals the baseline) or because somebody else did?
      if ($base.ContainsKey($rel) -and $base[$rel] -eq $rh) { $changed += [pscustomobject]@{ rel = $rel; kind = 'changed' } }
      else { $conflicts += $rel }
    }
  }
  $rr = Join-Path $Real $root
  if (Test-Path $rr) {
    Get-ChildItem $rr -Recurse -File -Force | ForEach-Object {
      $rel = $_.FullName.Substring($Real.Length + 1)
      if (-not (Test-Path (Join-Path $Tree $rel)) -and $base.ContainsKey($rel)) { $removed += $rel }
    }
  }
}
"to apply: $($changed.Count) files ($(@($changed | Where-Object kind -eq 'new').Count) new); in the real project but not in the tree: $($removed.Count)"
$changed | ForEach-Object { "  {0,-8} {1}" -f $_.kind, $_.rel }
if ($removed.Count) { "REMOVED in the tree (not applied: do it by hand if meant):"; $removed | ForEach-Object { "  $_" } }
if ($conflicts.Count) { "CONFLICT: the real project has changes of somebody else in:"; $conflicts | ForEach-Object { "  $_" }; "nothing was applied"; return }
if ($WhatIf) { return }
foreach ($c in $changed) {
  $src = Join-Path $Tree $c.rel; $dst = Join-Path $Real $c.rel
  if ($c.kind -eq 'changed') {
    $bk = Join-Path $Backup $c.rel
    New-Item -ItemType Directory -Force (Split-Path $bk) | Out-Null
    if (-not (Test-Path $bk)) { Copy-Item $dst $bk -Force }          # (the first backup is the original: a second apply does not replace it)
  }
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  Copy-Item $src $dst -Force
}
# check
$bad = 0
foreach ($c in $changed) { if ((Norm-Hash (Join-Path $Tree $c.rel)) -ne (Norm-Hash (Join-Path $Real $c.rel))) { $bad++; "MISMATCH after copy: $($c.rel)" } }
# the baseline follows what was applied (the next apply must not take my own earlier work for somebody else's)
foreach ($c in $changed) { $base[$c.rel] = Norm-Hash (Join-Path $Real $c.rel) }
$lines = New-Object System.Collections.Generic.List[string]
foreach ($k in ($base.Keys | Sort-Object)) { $lines.Add("$k`t$($base[$k])") }
[IO.File]::WriteAllLines($Baseline, $lines)
"applied $($changed.Count) files, mismatches: $bad  (backups of the replaced files in $Backup)"

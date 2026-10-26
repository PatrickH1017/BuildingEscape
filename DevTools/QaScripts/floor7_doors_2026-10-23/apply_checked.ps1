# Applies the scratch copy's own changes (files that differ from the T0 snapshot of the real project) to the real project, with a backup of every file it replaces,
# and REFUSES any file the real project itself changed since T0 (somebody else's edit must never be overwritten by an older scratch file).
# Scripts (.cs, .ps1) keep the EOL and BOM of the file they replace (new ones get CRLF, the project's convention); every other file is copied byte for byte.
# Usage: .\apply_checked.ps1 -Backup <folder> [-WhatIf]
param([Parameter(Mandatory = $true)][string]$Backup, [switch]$WhatIf,
      [string]$Scratch = 'C:\bd',
      [string]$Snapshot = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-Eternum\2cfb45a0-f269-4edd-ba06-8b8b2fc18a76\scratchpad\doorwork\snapshot_real_t0.tsv')
$ErrorActionPreference = 'Stop'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$subs = 'Assets\Scripts', 'Assets\Editor', 'Assets\Resources'
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p); $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0; $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)).Replace('-', ''); $ms.Dispose(); return $h
}
$snap = @{}
foreach ($line in [IO.File]::ReadAllLines($Snapshot)) { $p = $line.Split("`t"); if ($p.Count -eq 2) { $snap[$p[0]] = $p[1] } }
$new = 0; $changed = 0; $same = 0; $conflicts = @(); $skipSame = 0
foreach ($sub in $subs) {
  $root = Join-Path $Scratch $sub
  foreach ($f in Get-ChildItem $root -Recurse -File) {
    $rel = $f.FullName.Substring($Scratch.Length + 1)
    $hs = Norm-Hash $f.FullName
    $t0 = $snap[$rel]
    if ($t0 -and $hs -eq $t0) { $same++; continue }                 # (not mine: the scratch file is what the real one was at T0)
    $dst = Join-Path $real $rel
    $exists = Test-Path $dst
    if ($exists) {
      $hr = Norm-Hash $dst
      if ($hr -eq $hs) { $skipSame++; continue }                    # (already applied)
      if ($t0 -and $hr -ne $t0) { $conflicts += $rel; continue }    # (the real file changed since T0: not overwritten)
    } elseif ($t0) { $conflicts += $rel; continue }                 # (it existed at T0 and is gone from the real project now)
    if ($exists) { $changed++ } else { $new++ }
    if ($WhatIf) { "{0}  {1}" -f $(if ($exists) { 'change' } else { 'new   ' }), $rel; continue }
    if ($exists) { $b = Join-Path $Backup $rel; New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null; Copy-Item $dst $b -Force }
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    if ($f.Extension -in '.cs', '.ps1' -or $f.Name -eq 'OfficeCatalog.asset') {       # (the catalog is text and the real one has CRLF lines: keep them, so the change shows as the new entries only)
      $text = [IO.File]::ReadAllText($f.FullName)
      $crlf = $true
      if ($exists) { $old = [IO.File]::ReadAllText($dst); $crlf = ([regex]::Matches($old, "`r`n")).Count -gt ([regex]::Matches($old, "(?<!`r)`n")).Count }
      $text = $text.Replace("`r`n", "`n"); if ($crlf) { $text = $text.Replace("`n", "`r`n") }
      $bom = $false; if ($exists) { $ob = [IO.File]::ReadAllBytes($dst); $bom = ($ob.Length -ge 3 -and $ob[0] -eq 0xEF -and $ob[1] -eq 0xBB -and $ob[2] -eq 0xBF) }
      [IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding($bom)))
    } else { Copy-Item $f.FullName $dst -Force }
  }
}
"{0}: {1} new, {2} changed, {3} untouched since T0, {4} already in the real project (backup of the replaced files: {5})" -f $(if ($WhatIf) { 'WOULD APPLY' } else { 'applied' }), $new, $changed, $same, $skipSame, $Backup
if ($conflicts.Count -gt 0) { "CONFLICTS (the real project changed these since T0; NOT overwritten):"; $conflicts | ForEach-Object { '  ' + $_ } }
# FYI: what the real project gained since T0 on its own
$gone = 0; $fyi = @()
foreach ($sub in $subs) {
  foreach ($f in Get-ChildItem (Join-Path $real $sub) -Recurse -File) {
    $rel = $f.FullName.Substring($real.Length + 1)
    if (-not $snap.ContainsKey($rel)) { $fyi += "new in real: $rel"; continue }
    if ((Norm-Hash $f.FullName) -ne $snap[$rel]) { $fyi += "changed in real: $rel" }
  }
}
if ($fyi.Count -gt 0) { "FYI the real project changed by itself since T0 ($($fyi.Count)); includes my own applied files if this was not a -WhatIf run:"; $fyi | Select-Object -First 40 | ForEach-Object { '  ' + $_ } }

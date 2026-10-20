# Applies the verified QA tree (BE_qa) to the real project: every script, editor tool, resource, setting and DevTools file that differs or is new, with a backup of what is
# replaced. Scripts (.cs, .ps1) keep the EOL of the file they replace (new ones get CRLF, the project's convention); every other file is copied byte for byte.
# Usage: .\etr_apply.ps1 -Backup <folder> [-WhatIf]        (the owner's rules: no git, they recompile, never touch Library / Scenes / art)
param([Parameter(Mandatory = $true)][string]$Backup, [switch]$WhatIf, [string[]]$Remove = @('Assets\Scripts\Outpost\NoteProp.cs', 'Assets\Scripts\Outpost\NoteProp.cs.meta'))
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$q = "$here\BE_qa"
$subs = 'Assets\Scripts', 'Assets\Editor', 'Assets\Resources', 'ProjectSettings', 'Packages', 'DevTools'
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p); $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0; $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)); $ms.Dispose(); return $h
}
$new = 0; $changed = 0; $same = 0
foreach ($sub in $subs) {
  $root = Join-Path $q $sub
  if (-not (Test-Path $root)) { continue }
  foreach ($f in Get-ChildItem $root -Recurse -File) {
    $rel = $f.FullName.Substring($q.Length + 1)
    $dst = Join-Path $real $rel
    $isScript = $f.Extension -in '.cs', '.ps1'
    $exists = Test-Path $dst
    if ($exists -and (Norm-Hash $f.FullName) -eq (Norm-Hash $dst)) { $same++; continue }
    if ($exists) { $changed++ } else { $new++ }
    if ($WhatIf) { "{0}  {1}" -f $(if ($exists) { 'change' } else { 'new   ' }), $rel; continue }
    if ($exists) { $b = Join-Path $Backup $rel; New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null; Copy-Item $dst $b -Force }
    New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
    if ($isScript) {
      $text = [IO.File]::ReadAllText($f.FullName)
      $crlf = $true
      if ($exists) { $old = [IO.File]::ReadAllText($dst); $crlf = ([regex]::Matches($old, "`r`n")).Count -gt ([regex]::Matches($old, "(?<!`r)`n")).Count }
      $text = $text.Replace("`r`n", "`n"); if ($crlf) { $text = $text.Replace("`n", "`r`n") }
      $bom = $false; if ($exists) { $ob = [IO.File]::ReadAllBytes($dst); $bom = ($ob.Length -ge 3 -and $ob[0] -eq 0xEF -and $ob[1] -eq 0xBB -and $ob[2] -eq 0xBF) }
      [IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding($bom)))
    } else { Copy-Item $f.FullName $dst -Force }
  }
}
"applied: $new new, $changed changed, $same already identical (backup of the replaced files: $Backup)"
# files that are gone from the QA tree on purpose (round 2: the paper note is replaced by the outpost computer): MOVED to the backup folder, never deleted
foreach ($rel in $Remove) {
  $dst = Join-Path $real $rel
  if (-not (Test-Path $dst)) { continue }
  if (Test-Path (Join-Path $q $rel)) { "keeps   $rel (it still exists in the QA tree)"; continue }
  if ($WhatIf) { "remove  $rel (moved to the backup)"; continue }
  $b = Join-Path $Backup $rel; New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null
  Move-Item $dst $b -Force
  "moved   $rel to the backup"
}

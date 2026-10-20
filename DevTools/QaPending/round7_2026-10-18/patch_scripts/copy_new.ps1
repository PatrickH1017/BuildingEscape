# Copies new / rewritten source files from newfiles\ into the QA tree (BE_qa = C:\bq), as CRLF (the project's convention), keeping the BOM state of a file it replaces.
# Usage: .\copy_new.ps1 -Map @{ 'SettingsPanel.cs' = 'Scripts\Hud\SettingsPanel.cs'; ... }     (value: the path under Assets\)     [-WhatIf]
param([Parameter(Mandatory)][hashtable]$Map, [switch]$WhatIf)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
foreach ($k in $Map.Keys) {
  $src = Join-Path "$here\newfiles" $k
  $dst = Join-Path "$here\BE_qa\Assets" $Map[$k]
  $text = [IO.File]::ReadAllText($src).Replace("`r`n", "`n").Replace("`n", "`r`n")
  $bom = $false
  if (Test-Path $dst) { $b = [IO.File]::ReadAllBytes($dst); $bom = ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) }
  if ($WhatIf) { "would write $dst (exists=$(Test-Path $dst), bom=$bom)"; continue }
  New-Item -ItemType Directory -Force (Split-Path $dst) | Out-Null
  [IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding($bom)))
  "wrote $dst (bom=$bom)"
}

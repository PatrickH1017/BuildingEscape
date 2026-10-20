# Compares two project trees (code, resources, settings) ignoring line endings: prints files that differ, and files that exist on one side only.
# Usage: .\cmp_trees.ps1 -A <real project> -B <scratch tree>
param([string]$A = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape', [string]$B, [string[]]$Sub = @('Assets\Scripts', 'Assets\Editor', 'Assets\Resources', 'ProjectSettings', 'Packages'))
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p)
  $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0
  $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms))
  $ms.Dispose()
  return $h
}
$diff = @(); $onlyA = @(); $onlyB = @(); $same = 0
foreach ($s in $Sub) {
  $ra = Join-Path $A $s; $rb = Join-Path $B $s
  $fa = @{}; $fb = @{}
  if (Test-Path $ra) { Get-ChildItem $ra -Recurse -File | ForEach-Object { $fa[$_.FullName.Substring($ra.Length)] = $_.FullName } }
  if (Test-Path $rb) { Get-ChildItem $rb -Recurse -File | ForEach-Object { $fb[$_.FullName.Substring($rb.Length)] = $_.FullName } }
  foreach ($k in $fa.Keys) {
    if (-not $fb.ContainsKey($k)) { $onlyA += "$s$k"; continue }
    if ((Get-Item $fa[$k]).Length -eq 0 -and (Get-Item $fb[$k]).Length -eq 0) { $same++; continue }
    if ((Norm-Hash $fa[$k]) -eq (Norm-Hash $fb[$k])) { $same++ } else { $diff += "$s$k" }
  }
  foreach ($k in $fb.Keys) { if (-not $fa.ContainsKey($k)) { $onlyB += "$s$k" } }
}
"identical: $same"
"DIFFER ($($diff.Count)):"; $diff | Sort-Object | ForEach-Object { '  ' + $_ }
"ONLY IN A ($($onlyA.Count)):"; $onlyA | Sort-Object | Select-Object -First 40 | ForEach-Object { '  ' + $_ }
"ONLY IN B ($($onlyB.Count)):"; $onlyB | Sort-Object | Select-Object -First 40 | ForEach-Object { '  ' + $_ }

# Hashes (SHA-256, line endings ignored) of the real project's code folders: the "T0 snapshot" apply_checked.ps1 compares against. Take it right after the scratch copy was brought up to date (and cmp_trees.ps1 showed it identical).
# Usage: .\snapshot_real.ps1 -Out snapshot_real_t0.tsv
param([Parameter(Mandatory = $true)][string]$Out, [string]$Real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape')
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p); $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0; $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)).Replace('-', ''); $ms.Dispose(); return $h
}
$rows = New-Object System.Collections.Generic.List[string]
foreach ($s in 'Assets\Scripts', 'Assets\Editor', 'Assets\Resources', 'ProjectSettings', 'Packages') {
  $root = Join-Path $Real $s
  foreach ($f in Get-ChildItem $root -Recurse -File) { $rows.Add("$($f.FullName.Substring($Real.Length + 1))`t$(Norm-Hash $f.FullName)") }
}
[IO.File]::WriteAllLines($Out, $rows)
"snapshot of $($rows.Count) files -> $Out"

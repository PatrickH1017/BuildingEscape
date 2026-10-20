# Writes a SHA-256 manifest (line endings ignored) of a project's code, resources and settings: <path>\t<hash> per line.
# Usage: .\manifest.ps1 -Root <project> -Out <file.tsv>
param([string]$Root = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape', [string]$Out, [string[]]$Sub = @('Assets', 'ProjectSettings', 'Packages', 'DevTools'))
function Norm-Hash([string]$p) {
  $b = [IO.File]::ReadAllBytes($p)
  $ms = New-Object IO.MemoryStream
  foreach ($x in $b) { if ($x -ne 13) { $ms.WriteByte($x) } }
  $ms.Position = 0
  $h = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($ms)).Replace('-', '')
  $ms.Dispose()
  return $h
}
$lines = New-Object System.Collections.Generic.List[string]
foreach ($s in $Sub) {
  $r = Join-Path $Root $s
  if (-not (Test-Path $r)) { continue }
  Get-ChildItem $r -Recurse -File -Force -ErrorAction SilentlyContinue | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($Root.Length + 1)
    try { $lines.Add("$rel`t$(Norm-Hash $_.FullName)") } catch { $lines.Add("$rel`tERROR") }
  }
}
if ($Out) { [IO.File]::WriteAllLines($Out, $lines) ; "manifest: $($lines.Count) files -> $Out" } else { $lines }

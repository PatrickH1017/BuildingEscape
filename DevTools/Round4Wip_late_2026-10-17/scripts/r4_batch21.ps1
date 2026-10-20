# Round 4, batch 21: the clearance test names the rooms of the piece and the gear it blocks (diagnosis only). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep 'has {q.key} in front of it ({cols}x{rows} seed {sd} floor {floor})");' 'has {q.key} at ({q.x:0.0}, {q.z:0.0}) in front of it; rooms {g.room} / {q.room} ({cols}x{rows} seed {sd} floor {floor})");'
Save-Src
Write-Output 'round 4 batch 21 patched'

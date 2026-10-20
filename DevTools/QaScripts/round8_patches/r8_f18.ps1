# Round 8, batch F18: the protocol number. Build j differs from build i in what a floor holds (a safe may also hold a gold watch: one more item with a network id of its own), so old and new cannot share a run.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\Net\NetConst.cs'
Rep 'public const string ProtocolVersion = "19";   // 19:' 'public const string ProtocolVersion = "20";   // 20: a safe may also hold a gold watch (one more item with a network id: a floor holds other things, so old and new cannot share a run); 19:'
Save-Src
Write-Output 'round 8 batch F18 (protocol 20) patched'

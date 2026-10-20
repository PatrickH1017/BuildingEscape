# Round 6, batch A5: the punch scenario put the officer 1 m above the NavMesh (his transform is his middle, the agent's base offset is the rest), so SamplePosition found nothing. BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6.cs')
Rep @'
                    var spot = player.transform.position + ahead.normalized * PunchRules.Reach; spot.y = home.y;
                    placed = UnityEngine.AI.NavMesh.SamplePosition(spot, out var navHit, 0.4f, UnityEngine.AI.NavMesh.AllAreas) && agent.Warp(navHit.position);
'@ @'
                    var spot = player.transform.position + ahead.normalized * PunchRules.Reach; spot.y = home.y - agent.baseOffset;       // (his transform is his middle: the NavMesh is baseOffset lower)
                    placed = UnityEngine.AI.NavMesh.SamplePosition(spot, out var navHit, 0.6f, UnityEngine.AI.NavMesh.AllAreas) && agent.Warp(navHit.position);
'@
Save-Src
Write-Output 'round 6 batch A5 patched'

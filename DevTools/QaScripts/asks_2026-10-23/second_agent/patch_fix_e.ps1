# Test flaw found by the first run of Asks1023E: the player arrives in the elevator car, which is not part of the floor plan, so "the player stands on the floor plan" failed on every floor (also those without the new art). Check the guards instead.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = 'C:\bd\Assets\Editor\NetDev\NetDevAsks1023E.cs'; Open-Src $SC
Rep '                var player = WobblyPlayerRegistry.All[0];' '                // (the player arrives in the elevator car, which is no part of the floor plan: the guards are what is checked)'
Rep '                bool onMesh = NavMesh.SamplePosition(player.transform.position, out _, 2.5f, NavMesh.AllAreas);' '                bool onMesh = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss).All(g => !g.TryGetComponent(out NavMeshAgent ag) || ag.isOnNavMesh);'
Rep 'still has its guards and the player stands on its floor plan"' 'still has all its guards and every one of them stands on its floor plan"'
Rep '                Log($"[Asks1023E] INFO floor {f} took {built:0.0} s to build ({lay.pieces.Count} pieces, {lay.walls.Count} walls)");' @'
                Log($"[Asks1023E] INFO floor {f} took {built:0.0} s to build ({lay.pieces.Count} pieces, {lay.walls.Count} walls)");
                // the cost of the new art: the average frame time over 1.5 s with the player at the elevator (meaningful in a real window - ui.ps1 -Methods Asks1023E -, only a sample in batch mode)
                float tSum = 0f; int frames = 0; float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 1.5f) { yield return null; tSum += Time.unscaledDeltaTime; frames++; }
                Log($"[Asks1023E] INFO floor {f}: {(frames > 0 ? tSum / frames * 1000f : 0f):0.0} ms a frame over {frames} frames, {UnityEditor.UnityStats.batches} batches, {UnityEditor.UnityStats.triangles} triangles");
'@.TrimEnd()
Save-Src
# the door pictures may be taken on any floor that has the new art now (BE_DOOR_FLOOR)
$SC = 'C:\bd\Assets\Editor\NetDev\NetDevDoorLooks.cs'; Open-Src $SC
Rep 'Check("pack doors: the floor on show is floor 7 with its walls skinned", floor == 7 && gen.WallSkinActive);' 'Check("pack doors: the floor on show has the new art (floors 1 to 7) with its walls skinned", FloorArtRules.AppliesTo(floor) && gen.WallSkinActive);'
Save-Src
"fix E patched"

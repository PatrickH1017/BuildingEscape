# Round 11, batch G08: two test checks of G07 learn the new rules: the aim check of the open safes counts what the safes really hold (a safe may hold nothing), and the sniper picked up on floor 10 is only looked for in the pack (the pew taken on floor 3 is still in the hand); the sniper's pictures get a light.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Ed\NetDevOutpost.cs"
Rep @'
wsItemsWanted = wsSafes.Count;
'@ @'
wsItemsWanted = 0;
'@
Rep @'
wsItemsWanted += xCount.BundleCount + xCount.ClipCount + xCount.WatchCount;
'@ @'
wsItemsWanted += xCount.GunCount + xCount.BundleCount + xCount.ClipCount + xCount.WatchCount;
'@
Save-Src

Open-Src "$Ed\NetDevPewLoot.cs"
Rep @'
picked up, the sniper rifle is in the pack and in the hand", pack.Contains(sn) && gc.Gun != null && gc.Gun.kind == RangedWeapon.Kind.Sniper);
'@ @'
picked up, the sniper rifle is in the pack (the pew taken on floor 3 is still in the hand)", pack.Contains(sn));
'@
Rep @'
                            eyeS.transform.LookAt(sn.transform.position);
'@ @'
                            eyeS.transform.LookAt(sn.transform.position);
                            var eyeLight = eyeS.gameObject.AddComponent<Light>();          // (the outpost is dim: a lamp on the camera)
                            eyeLight.type = LightType.Point; eyeLight.intensity = 2.5f; eyeLight.range = 7f;
'@
Save-Src

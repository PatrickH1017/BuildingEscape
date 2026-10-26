# The built-player autopilot's guard drop test (QaAutopilot.GuardsNet, floor 4, two built players) still expected the old drop: ONE CLIP of pistol ammunition that the client takes with E. Since T24 / T25 (the owner, 2026-10-23) an eliminated guard drops 0 to 3 BULLETS as a pile of brass that
# is taken only by running over it. The host now forces the roll of two bullets (GuardDrops.TestRoll, a plain static: the autopilot runs in a built player), the client runs over the pile (teleports onto it) and the host decides (AmmoRunOver / RunSync.HostRunOver).
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept. Compile-checked by the next run; the autopilot itself only runs in a BUILT player (final_autopilot.ps1).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = $Root + 'Scripts\QaAutopilot.cs'; Open-Src $SC

Rep 'he drops a clip of pistol ammunition, which the client has too (the host''s network id) and takes with' 'he drops two bullets (a pile of brass: GuardDrops.TestRoll forces the roll), which the client has too (the host''s network id) and takes by running over them (2026-10-23: no E)'
Rep @'
                    victim.BulletHitHost(50f, false, null);
                    yield return Wait(1.0f);
                    var made = new List<AmmoClip>();
'@ @'
                    GuardDrops.TestRoll = 70;                                            // (the owner, 2026-10-23: a guard drops 0 to 3 bullets; the roll of 70 is two of them)
                    victim.BulletHitHost(50f, false, null);
                    GuardDrops.TestRoll = -1;
                    yield return Wait(1.0f);
                    var made = new List<AmmoClip>();
'@
Rep @'
                    Check($"{who}: the armed guard went down for good and dropped one clip of pistol ammunition where he fell", victim.Dead && made.Count == 1 && made[0].rounds == pewSpec.magazine && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0 + 1,
'@ @'
                    Check($"{who}: the armed guard went down for good and dropped two bullets of pistol ammunition where he fell (a pile that is only taken by running over it)", victim.Dead && made.Count == 1 && made[0].rounds == 2 && made[0].GetComponent<Pickup>().runOverOnly && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0 + 1,
'@
Rep @'
                        rGun.Magazine.SetReserve(0);                                     // (room for a clip: the client does the same to his copy)
                        yield return Until(() => rGun.Magazine.Reserve >= pewSpec.magazine, 90f, "the client to take the dropped clip");
                        yield return Wait(0.5f);
                        Check($"{who}: the client took the dropped clip (his E, my answer by its id): its rounds are in his pistol's reserve here too and the clip is gone", rGun.Magazine.Reserve == pewSpec.magazine && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0,
'@ @'
                        rGun.Magazine.SetReserve(0);                                     // (the client does the same to his copy)
                        yield return Until(() => rGun.Magazine.Reserve >= 2, 90f, "the client to run over the dropped bullets");
                        yield return Wait(0.5f);
                        Check($"{who}: the client ran over the bullets (my copy of him came within reach: I decided): they are in his pistol's reserve here too and the pile is gone", rGun.Magazine.Reserve == 2 && FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length == clips0,
'@
Rep '                }, 90f, "the clip the host''s armed guard dropped");' '                }, 90f, "the bullets the host''s armed guard dropped");'
Rep @'
                Check($"{who}: the host's armed guard went down and his clip lies here too - eight rounds for the pistol, with a network id", dropped != null && dropped.GetComponent<AmmoClip>().rounds == pewSpec.magazine && dropped.NetId >= 0,
'@ @'
                Check($"{who}: the host's armed guard went down and his two bullets lie here too (a pile of brass with a network id)", dropped != null && dropped.GetComponent<AmmoClip>().rounds == 2 && dropped.NetId >= 0,
'@
Rep @'
                    cGun.Magazine.SetReserve(0);                                         // (room for a clip: the host does the same to his copy of my pistol)
                    wob.TeleportTo(dropped.transform.position + Vector3.up * 1.0f, wob.transform.rotation);
                    rb.linearVelocity = Vector3.zero;
                    yield return Wait(2.5f);                                             // (the host's copy of me has to arrive at the clip)
                    cPack.TryStore(dropped);
                    yield return Until(() => cGun.Magazine.Reserve >= pewSpec.magazine, 30f, "the dropped clip's rounds to reach my pistol");
                    yield return Wait(0.5f);
                    Check($"{who}: the dropped clip's rounds went into my pistol's reserve (the host found the clip by its id) and the clip is gone here", cGun.Magazine.Reserve == pewSpec.magazine && !dropped, $"(reserve {cGun.Magazine.Reserve})");
                }
                else Check($"{who}: a pistol and a body to take the dropped clip with", false);
'@ @'
                    cGun.Magazine.SetReserve(0);                                         // (the host does the same to his copy of my pistol)
                    wob.TeleportTo(dropped.transform.position + Vector3.up * 1.0f, wob.transform.rotation);          // (running over the bullets: the host's copy of me comes within reach of the pile and the host takes them for me)
                    rb.linearVelocity = Vector3.zero;
                    yield return Wait(2.5f);
                    yield return Until(() => cGun.Magazine.Reserve >= 2, 30f, "the run-over bullets to reach my pistol");
                    yield return Wait(0.5f);
                    Check($"{who}: running over the bullets put them into my pistol's reserve (the host decided) and the pile is gone here", cGun.Magazine.Reserve == 2 && !dropped, $"(reserve {cGun.Magazine.Reserve})");
                }
                else Check($"{who}: a pistol and a body to run over the dropped bullets with", false);
'@
Save-Src
"autopilot guard drop test patched"

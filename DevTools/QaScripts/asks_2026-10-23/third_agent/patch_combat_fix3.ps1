# The pew over the network (GunNet): a light bullet is 25 HP now (the owner, 2026-10-27), so the client needs FOUR bullets for the guard on duty, and the client's own hit marker is checked: a hit for each bullet he sees land and, for the last, the elimination's marker and sound the host sends him.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevPewLoot.cs')
Rep 'GunNetCheck("host: it accepted all three shots of the client", RunSync.Instance.GunShotsAccepted - accepted0 == 3,' 'GunNetCheck("host: it accepted all four shots of the client", RunSync.Instance.GunShotsAccepted - accepted0 == 4,'
Rep 'GunNetCheck("host: the guard on duty is down for good (three bullets, four hits each)", duty.Dead && duty.Hits >= 10,' 'GunNetCheck("host: the guard on duty is down for good (four bullets, 25 HP each: light ammo)", duty.Dead && duty.Hits >= 10,'
Rep 'GunNetCheck("host: the client''s three shots were shown on this machine", cgc != null && cgc.ShotsSeen == 3,' 'GunNetCheck("host: the client''s four shots were shown on this machine", cgc != null && cgc.ShotsSeen == 4,'
RepRegion '                bool f3 = gc.Fire();' '$"({f1}, {f2}, {f3}, fired {gc.ShotsFired}, rounds {gc.Gun.Magazine.Rounds})");' @'
                bool f3 = gc.Fire();
                yield return new WaitForSeconds(0.4f);
                AimCamera(cam, wob, duty != null ? duty.transform.position + Vector3.up * 0.3f : chest);
                bool f4 = gc.Fire();
                GunNetCheck("client: all four bullets left the gun", f1 && f2 && f3 && f4 && gc.ShotsFired == 4 && gc.Gun.Magazine.Rounds == 4, $"({f1}, {f2}, {f3}, {f4}, fired {gc.ShotsFired}, rounds {gc.Gun.Magazine.Rounds})");
'@
Rep '            yield return WaitSig("h_shot_ok", 60);' @'
            yield return WaitSig("h_shot_ok", 60);
            yield return new WaitForSeconds(0.8f);
            GunNetCheck("client: a hit marker flashed for the bullets that landed, and the last - the elimination - had its own marker and sound, which the host sent him (the owner, 2026-10-27)", HitFeedback.Count >= 4 && HitFeedback.Last == HitKind.Kill && HitFeedback.LastSound == SoundId.HitKill, $"(markers {HitFeedback.Count}, last {HitFeedback.Last}, {HitFeedback.LastSound})");
'@
Save-Src
"combat fix 3 patched"

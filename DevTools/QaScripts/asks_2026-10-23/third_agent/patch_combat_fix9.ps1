# BulletDamage's body-shot step made as exact as the headshot's (d13 / d15: the first bullet of a gun sometimes missed the officer - a door opening in the line, another officer standing behind him who took it - and the check saw 0 damage): a try
# counts as LANDED when the target took damage from it; a miss is tried again (up to four times, after the gun's own interval), and the checks are made on the try that landed: its damage is the ammunition's, it made ONE marker (the shot's).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevBulletDamage.cs')
RepRegion '                float before = HpLost(g);' '$"({HitFeedback.Last}, {HitFeedback.LastSound})");' @'
                float before = HpLost(g);
                int count0 = HitFeedback.Count;
                float took = 0f; bool fired = false, bodyLanded = false; int markersOfShot = 0; string bwhy = "";
                for (int attempt = 0; attempt < 4 && !bodyLanded; attempt++)
                {
                    float beforeTry = HpLost(g); int countTry = HitFeedback.Count;
                    GunController.TestAimPoint = bodyPt(); AimCamera(cam, player, bodyPt());
                    yield return null;
                    fired = gc.Fire();
                    yield return new WaitForSeconds(0.15f);
                    var bimp = gc.LastImpact;
                    bool landed = HpLost(g) - beforeTry > 0.01f;
                    bwhy += $"[try {attempt}: fired {fired}, bullet hit {(bimp.hit ? (bimp.guard != null ? bimp.guard.name : bimp.collider ? bimp.collider.name : "?") : "nothing")}, he took {HpLost(g) - beforeTry:0.0}] ";
                    if (landed) { bodyLanded = true; took = HpLost(g) - beforeTry; markersOfShot = HitFeedback.Count - countTry; }
                    else if (!g.Dead) yield return new WaitForSeconds(Mathf.Max(0.3f, spec.fireInterval + 0.2f));
                }
                float expectBody = BulletDamageRules.Damage(kind, false);
                if (kind == RangedWeapon.Kind.Sniper) BdCheck($"{tag} a body shot takes the heavy ammunition's 100 HP and eliminates the officer (the one shot)", bodyLanded && g.Dead && took >= 99.9f, $"(took {took:0.0}, dead {g.Dead}) {bwhy}");
                else BdCheck($"{tag} a body shot takes {expectBody} HP{(kind == RangedWeapon.Kind.Shotgun ? " once, though eight pellets were fired" : "")}", bodyLanded && Mathf.Abs(took - expectBody) < 0.01f && !g.Dead, $"(took {took:0.0}, fired {fired}) {bwhy}");
                BdCheck($"{tag} the hit marker flashed: ONE for the shot, a hit (or the elimination), with its sound", bodyLanded && markersOfShot == 1 && (HitFeedback.Last == (g.Dead ? HitKind.Kill : HitKind.Hit) || HitFeedback.Last == HitKind.Kill) && (HitFeedback.LastSound == (g.Dead ? SoundId.HitKill : SoundId.HitMarker) || HitFeedback.LastSound == SoundId.HitKill), $"({HitFeedback.Last}, {HitFeedback.LastSound}, markers {markersOfShot})");
'@
Save-Src
"combat fix 9 patched"

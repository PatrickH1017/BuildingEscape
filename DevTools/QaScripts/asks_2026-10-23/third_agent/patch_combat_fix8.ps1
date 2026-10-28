# BulletDamage's headshot step made exact (d12: a pistol bullet aimed at the head of an officer 2.1 m away landed on his body, took 25 and the check failed, as it can with any aim): each try reads where the bullet LANDED (gc.LastImpact: the guard, the height
# over his middle) and whether that is the head by the game's own rule (GuardAI.IsHeadHit); it tries again, up to five times, until a bullet lands in the head, and the check is then that THAT bullet took the ammunition's headshot damage.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevBulletDamage.cs')
RepRegion '                string why = "";' '                took = HpLost(g) - before;' @'
                string why = "";
                float headTook = -1f; bool headLanded = false;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    float beforeTry = HpLost(g);
                    GunController.TestAimPoint = headPt(); AimCamera(cam, player, headPt());
                    yield return null;
                    fired = gc.Fire();
                    yield return new WaitForSeconds(0.15f);
                    var imp = gc.LastImpact;
                    bool landed = fired && imp.hit && imp.guard == g;
                    bool headNow = landed && g.IsHeadHit(imp.point.y);
                    why += $"[try {attempt}: fired {fired}, landed {landed}, {(landed ? imp.point.y - g.transform.position.y : float.NaN):0.00} m over his middle, head {headNow}, took {HpLost(g) - beforeTry:0.0}, state {g.State}] ";
                    if (headNow) { headLanded = true; headTook = HpLost(g) - beforeTry; break; }
                    if (g.Dead) break;
                    yield return new WaitForSeconds(Mathf.Max(0.3f, spec.fireInterval + 0.2f));
                }
                took = HpLost(g) - before;
'@
Rep '                else BdCheck($"{tag} a headshot takes {expectHead} HP, more than the {expectBody} of a body shot", fired && (Mathf.Abs(took - expectHead) < 0.01f || g.Dead) && took > expectBody - 0.01f, $"(took {took:0.0}, dead {g.Dead}) {why}");' @'
                else BdCheck($"{tag} a bullet that lands in the head takes {expectHead} HP, more than the {expectBody} of a body shot", headLanded && (Mathf.Abs(headTook - expectHead) < 0.01f || g.Dead) && expectHead > expectBody, $"(the headshot took {headTook:0.0}, dead {g.Dead}) {why}");
'@
Save-Src
"combat fix 8 patched"

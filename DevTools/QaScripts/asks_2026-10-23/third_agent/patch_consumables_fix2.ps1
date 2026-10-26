# The floor part of the Consumables scenario: a guard who had a second to look at the player already had his radio call going when the control was read. Now: the invisible phase first (a guard who has just been put there), then, once the effect is over, the control.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcFile = Join-Path $here 'consumables\NetDevConsumables.cs'
Open-Src $srcFile
RepRegion '            GuardAI watcher = null; Vector3 spotAt = default;' '            Log($"[Consumables] DONE fails={coFails}");' @'
            GuardAI watcher = null; GuardState state0 = GuardState.Patrol;
            foreach (var cand in GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup && g.name != "Guard_Outpost").Take(6))
            {
                if (!OpenSpot(cand.transform.position + Vector3.up * 0.9f, 6f, out var spot, true)) continue;
                fState.SetValue(cand, GuardState.Patrol);
                var to = spot - cand.transform.position; to.y = 0f;
                GuardSightFreeze(cand);
                cand.transform.rotation = Quaternion.LookRotation(to.normalized);
                player.TeleportTo(spot + Vector3.up * 1.1f, Quaternion.identity);
                yield return null;
                yield return null;
                if (GsCanSee(cand, player)) { watcher = cand; state0 = cand.State; break; }
            }
            CoCheck("(test) a guard on duty looks at the player 6 m away and sees him", watcher != null);
            if (watcher != null)
            {
                int round = PlayerBuffs.Round;
                watcher.ForgetNotices();
                buffs.StartLocal(ConsumableKind.Invisible);                  // (at once: the guard has had two frames to look at him)
                bool blind = !GsCanSee(watcher, player);
                yield return new WaitForSeconds(2.5f);
                CoCheck("invisible: the same guard, the same spot: he does not see him, his notice meter stays empty for 2.5 s, nobody is marked, the guard is still on his round", blind && watcher.NoticeProgress(player) == 0f && !hm.IsMarked(player) && watcher.State == state0, $"(blind {blind}, meter {watcher.NoticeProgress(player):0.00}, marked {hm.IsMarked(player)}, state {watcher.State})");
                CoCheck("invisible: used in this round, so the next can has to wait for the next floor", buffs.Check(ConsumableKind.Invisible) == UseCheck.UsedThisRound && PlayerBuffs.Round == round);
                float waited = 0f;
                while (PlayerBuffs.IsInvisible(player) && waited < 14f) { waited += Time.deltaTime; yield return null; }
                CoCheck("invisible: after its 10 s the effect is over", !PlayerBuffs.IsInvisible(player) && waited > 6f, $"(waited {waited:0.0} s)");
                bool seesAgain = GsCanSee(watcher, player);
                watcher.ForgetNotices();
                float t1 = Time.time;
                while (Time.time - t1 < 4f && !(watcher.NoticeProgress(player) > 0f || hm.IsMarked(player) || watcher.State != state0)) yield return null;
                CoCheck("invisible: and then the same guard sees him again and starts to notice him (control)", seesAgain && (watcher.NoticeProgress(player) > 0f || hm.IsMarked(player) || watcher.State != state0), $"(sees {seesAgain}, meter {watcher.NoticeProgress(player):0.00}, marked {hm.IsMarked(player)}, state {watcher.State})");
                yield return LoadLevel(hm, 2);
                yield return new WaitForSeconds(0.5f);
                var buffs2 = WobblyPlayerRegistry.All[0].GetComponent<PlayerBuffs>();
                CoCheck("a new floor is a new round: invisible can be used again once its 10 s are over", PlayerBuffs.Round > round && buffs2.Check(ConsumableKind.Invisible) == UseCheck.Ok, $"(round {PlayerBuffs.Round}, was {round}, check {buffs2.Check(ConsumableKind.Invisible)})");
            }

            Log($"[Consumables] DONE fails={coFails}");
'@
if ($Dry) { Write-Output "dry ok $script:path" } else { Save-Src; Copy-Item $srcFile ($Root + 'Editor\NetDev\NetDevConsumables.cs') -Force; Write-Output 'copied NetDevConsumables.cs' }
"consumables fix 2 patched"

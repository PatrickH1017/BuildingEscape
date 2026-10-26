# The first built-player self-play of the floor-6 build (2026-10-23, Playtest_2026-10-23a: solo 116 PASS / 7 FAIL, host 36 / 2, client 44 / 1) found checks that were out of date, not game bugs:
# 1. "first person works": since 2026-10-20 night the body is NOT drawn in first person (CameraFollow.ShowArms = false: only what is in the hands), the check still expected arms.
# 2. "every main hallway door has its windows" counted the OLD windows' panes (12 per door); floors 1 to 7 have the pack's doors since T1 (a wide hall door is the pack's glass door: its leaves hold "PackPanel"s), so every wide door counted as undressed.
# 3. The guard drop test of two machines (GuardsNet): the pile of bullets is made at once and the host's own mirror of the client's gun was zeroed AFTER the client could already have run over it; the victim is now the guard farthest from every player, the mirror is zeroed BEFORE he goes
#    down, the first look is one frame later, and what stands near is logged (diagnostics: where every player is, how many piles this machine made).
# Patches the scratch copy C:\bd (QaAutopilot.cs); exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = $Root + 'Scripts\QaAutopilot.cs'; Open-Src $SC

# ---- 1
Rep '                    bool fpArms = fpRig.IsFirstPerson && fpRig.ArmsShown;' '                    bool fpArms = fpRig.IsFirstPerson && (CameraFollow.ShowArms ? fpRig.ArmsShown : !fpRig.ArmsShown);          // (since 2026-10-20 night nothing of the body is drawn in first person unless ShowArms is on: only what is in the hands)'
Rep 'Check("first person works in the built game: the arms shader is in the build, the body is drawn as its arms; third person brings it back",' 'Check("first person works in the built game: the arms shader is in the build, the body is gone from the picture in first person (its arms only when CameraFollow.ShowArms asks); third person brings it back",'

# ---- 2
Rep '                    if (d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") == 12) dressed++;' @'
                    int panes = d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane");
                    int packs = d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "PackPanel") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "PackPanel");
                    if (panes == 12 || packs >= 2) dressed++;                                        // (the old windows, or since 2026-10-23 on floors 1 to 7 the pack's own door: a wide hall door is its glass door)
'@.TrimEnd()
Rep '// the hallway doors: every wide door has its windows (2 leaves x 2 faces x (2 windows and a push bar) = 12 pictures), the rooms'' doors have none' '// the hallway doors: every wide door has its windows (the old dressing: 2 leaves x 2 faces x (2 windows and a push bar) = 12 pictures; the pack''s glass door on floors 1 to 7), the rooms'' doors have none'

# ---- 3
Rep @'
                foreach (var g in GuardAI.All) if (g && !g.IsPuppet && !g.IsBoss && g.Armed && !g.Dead && g.name != "Guard_Outpost") { victim = g; break; }
'@ @'
                float victimFar = -1f;                                                      // (the armed guard farthest from every player: nobody runs over his bullets by chance before the test looks)
                foreach (var g in GuardAI.All)
                {
                    if (!g || g.IsPuppet || g.IsBoss || !g.Armed || g.Dead || g.name == "Guard_Outpost") continue;
                    float nearest = float.MaxValue;
                    foreach (var np in NetPlayer.All) if (np) nearest = Mathf.Min(nearest, Vector3.Distance(np.transform.position, g.transform.position));
                    if (nearest > victimFar) { victimFar = nearest; victim = g; }
                }
'@.TrimEnd()
Rep @'
                    GuardDrops.TestRoll = 70;                                            // (the owner, 2026-10-23: a guard drops 0 to 3 bullets; the roll of 70 is two of them)
                    victim.BulletHitHost(50f, false, null);
                    GuardDrops.TestRoll = -1;
                    yield return Wait(1.0f);
'@ @'
                    if (rGun != null) rGun.Magazine.SetReserve(0);                       // (before he goes down: the bullets that come are counted from nothing, whenever they come)
                    GuardDrops.TestRoll = 70;                                            // (the owner, 2026-10-23: a guard drops 0 to 3 bullets; the roll of 70 is two of them)
                    victim.BulletHitHost(50f, false, null);
                    GuardDrops.TestRoll = -1;
                    yield return null;                                                   // (one frame: the pile is made at once)
                    foreach (var np in NetPlayer.All) if (np) Say($"{who}: [diag] player {np.Slot} (mine {np.IsOwner}) is {Vector3.Distance(np.transform.position, at):0.0} m from the guard who went down");
                    Say($"{who}: [diag] this machine has made {GuardDrops.Dropped} pile(s) of bullets ({GuardDrops.DroppedBullets} bullets); {FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length} clip objects lie about now");
'@.TrimEnd()
Rep @'
                        rGun.Magazine.SetReserve(0);                                     // (the client does the same to his copy)
                        yield return Until(() => rGun.Magazine.Reserve >= 2, 90f, "the client to run over the dropped bullets");
'@ @'
                        yield return Until(() => rGun.Magazine.Reserve >= 2, 90f, "the client to run over the dropped bullets");
'@.TrimEnd()
Save-Src
"autopilot floor 6 fixes patched"

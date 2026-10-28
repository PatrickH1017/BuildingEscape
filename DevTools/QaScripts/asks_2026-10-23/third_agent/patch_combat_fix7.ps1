# BulletDamage's two flaky checks (they passed in d8 and failed in d10 with nothing of the game's code changed between): (1) the shotgun's pellets can reach a SECOND officer standing near the target (the log showed Guard_3 and Guard_0 both going down), so the
# retry loop fired more than once and the headshot's marker count was 3: the exact count is the body shot's check (one shot, one marker); the headshot's is "at least one, and the stronger kind"; (2) after the dev menu's button an officer can notice the dev
# player again within 0.6 s (the player stands in view) and start a new call: the nobody-is-after-anybody check is made at once.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\NetDev\NetDevBulletDamage.cs')
Rep '                bool headFeedback = HitFeedback.Count == count0 + 1 && (HitFeedback.Last == HitKind.Head || HitFeedback.Last == HitKind.Kill);' '                bool headFeedback = HitFeedback.Count >= count0 + 1 && (HitFeedback.Last == HitKind.Head || HitFeedback.Last == HitKind.Kill);          // (at least one: a retry or a second officer in the line of the pellets gives more; the exact count is the body shot''s check)'
Rep '            bool cleared = hm.DevClearAlert();
            yield return new WaitForSeconds(0.6f);' @'
            bool cleared = hm.DevClearAlert();
            yield return null; yield return null;                                          // (the officers the backup called are destroyed at the end of the frame; nobody has had the time to notice the player again)
'@
Save-Src
"combat fix 7 patched"

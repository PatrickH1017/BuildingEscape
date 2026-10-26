# The first self-play of Playtest_2026-10-24a: client 68 PASS / 1 FAIL ("I see him put it away"): a flaw of the TEST's order. The round got longer (the keys: patch_autopilot_pack2.ps1) but the watcher only followed the first half of it, so the host went on to its own round while the client was still
# doing the second half, and the client began watching the host's round half way through (the "put away" it caught was the drop). The watcher now follows the keys half too, up to the end of the round (the crowbar in the pack, the hand empty), and only then does the other machine go on.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\QaAutopilot.cs')
Rep '            Check($"{who}: I see him take it up again", rPack.Items.Contains(cb));' @'
            Check($"{who}: I see him take it up again", rPack.Items.Contains(cb));
            // the second half of his round: the keys
            yield return Until(() => rPack.Equipped == null, 60f, $"{whose} slot key putting the crowbar away");
            Check($"{who}: I see the slot key put it away (it stays in his pack)", rPack.Equipped == null && rPack.Items.Contains(cb));
            yield return Until(() => HoldsMelee(rPack), 60f, $"{whose} slot key taking the crowbar out");
            Check($"{who}: I see the slot key take it out again", HoldsMelee(rPack));
            yield return Until(() => !rPack.Items.Contains(cb), 60f, $"{whose} drop key");
            Check($"{who}: I see the drop key drop it onto the floor", !rPack.Items.Contains(cb) && cb && cb.gameObject.activeInHierarchy && rPack.Equipped != cb);
            yield return Until(() => rPack.Items.Contains(cb) && rPack.Equipped == null, 60f, $"{whose} round to end with the crowbar in his pack and his hand empty");
            Check($"{who}: his round ends with the crowbar in his pack and his hand empty", rPack.Items.Contains(cb) && rPack.Equipped == null);
'@
Save-Src
"autopilot pack watcher follows the keys"

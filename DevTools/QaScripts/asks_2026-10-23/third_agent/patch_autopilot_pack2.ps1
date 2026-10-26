# The built-player pack round (patch_autopilot_pack.ps1) ended with the crowbar IN HAND (a pack takes the first weapon it is given into the hand), so the two-player test of the safe's pistol that follows it waited in vain for the pistol to come into the client's hand:
# a flaw of the test, not of the game. The round now ends with the hand empty, and it also goes through the KEYS (the slot key puts the weapon in hand away and takes it out again, G drops it: patch_pack_keys.ps1).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\QaAutopilot.cs')
Rep '            Check($"{who}: E on the dropped crowbar takes it up again", crowbar && pack.Items.Contains(crowbar));
            yield return Wait(1.0f);' @'
            Check($"{who}: E on the dropped crowbar takes it up again", crowbar && pack.Items.Contains(crowbar));
            yield return Wait(1.0f);

            // the keys: the slot key of the weapon in hand puts it away and takes it out again, the drop key drops it
            if (pack.Equipped != crowbar) { pack.SetEquipped(crowbar); yield return Until(() => pack.Equipped == crowbar, 30f, "the crowbar in my hand for the keys"); }
            int slot = pack.BarSlotOf(crowbar);
            Check($"{who}: the crowbar is on a slot of the weapon bar", slot >= 0, $"(slot {slot})");
            pack.PressSlot(slot);
            yield return Until(() => pack.Equipped == null, 30f, "the slot key puts it away");
            Check($"{who}: the slot key of the weapon in hand puts it away (it stays in the pack)", pack.Equipped == null && pack.Items.Contains(crowbar));
            yield return Wait(1.0f);
            pack.PressSlot(slot);
            yield return Until(() => pack.Equipped == crowbar, 30f, "the slot key takes it out");
            Check($"{who}: the slot key again takes it out", pack.Equipped == crowbar);
            yield return Wait(1.0f);
            pack.DropHeld();
            yield return Until(() => !pack.Items.Contains(crowbar), 30f, "the drop key drops it");
            Check($"{who}: the drop key drops the weapon in hand onto the floor", !pack.Items.Contains(crowbar) && pack.Equipped == null && crowbar && crowbar.gameObject.activeInHierarchy);
            yield return Wait(1.5f);
            if (crowbar) pack.TryStore(crowbar);
            yield return Until(() => crowbar && pack.Items.Contains(crowbar), 30f, "the crowbar taken up once more");
            pack.SetEquipped(null);                                                                  // (the hand is empty again: whatever comes next, a pistol, comes into it)
            yield return Until(() => pack.Equipped == null, 30f, "the hand empty");
            Check($"{who}: the round ends with the crowbar in the pack and the hand empty", crowbar && pack.Items.Contains(crowbar) && pack.Equipped == null);
            yield return Wait(1.0f);
'@
Save-Src
"autopilot pack round: keys, ends empty-handed"

# The owner, 2026-10-24: "Add the Outpost to the journal checklist". A fifth line of the floor's checklist: the guards' outpost. The owner did not say what completes it; the line is done when the outpost's lock has been hacked open
# (OutpostController.Unlocked: the state every machine shares), and stays done (like the other lines) even if somebody locks the door again with a code. A floor without an outpost has the line done at once.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\FloorChecklist.cs')
Rep '    public enum ScanKind { Valuable = 0, Unique = 1, Safe = 2, Cash = 3 }' @'
    public enum ScanKind { Valuable = 0, Unique = 1, Safe = 2, Cash = 3, Outpost = 4 }
'@
Rep '    /// <summary>What the scan and the journal''s checklist tell apart.' '    /// <summary>What the scan and the journal''s checklist tell apart (the Outpost line, the owner, 2026-10-24, is the checklist''s only: the scan never marks it).'
Rep '        public const int Count = 4;' '        public const int Count = 5;'
Rep '            k == ScanKind.Valuable ? "All valuables collected" : k == ScanKind.Unique ? "All unique items collected" : k == ScanKind.Safe ? "All safes opened" : "All cash collected";' @'
            k == ScanKind.Valuable ? "All valuables collected" : k == ScanKind.Unique ? "All unique items collected" : k == ScanKind.Safe ? "All safes opened" : k == ScanKind.Outpost ? OutpostLabel : "All cash collected";

        /// <summary>The line for the guards' outpost (the owner, 2026-10-24: "Add the Outpost to the journal checklist"): done once its lock is hacked open. The words are a placeholder: the owner writes them.</summary>
        public const string OutpostLabel = "Outpost hacked";
'@
Rep '        readonly int[] first = { -1, -1, -1, -1 };' @'
        readonly int[] first = NoCounts();

        static int[] NoCounts()
        {
            var a = new int[ChecklistRules.Count];
            for (int i = 0; i < a.Length; i++) a[i] = -1;
            return a;
        }
'@
Rep @'
            var gun = GunSafeProp.Active;
            if (gun && !gun.IsOpen) { into[(int)ScanKind.Safe]++; into[(int)ScanKind.Unique]++; }
'@ @'
            var gun = GunSafeProp.Active;
            if (gun && !gun.IsOpen) { into[(int)ScanKind.Safe]++; into[(int)ScanKind.Unique]++; }
            var outpost = OutpostController.Active;
            if (outpost && !outpost.Unlocked) into[(int)ScanKind.Outpost] = 1;                 // (the guards' outpost: its lock is not hacked yet; a floor without one has nothing to do here)
'@
Save-Src

# ---- the pure test
Open-Src ($A + 'Editor\NetDev\NetDevChecklistTest.cs')
Rep 'Check("checklist: the four lines are ''All valuables collected'', ''All unique items collected'', ''All safes opened'' and ''All cash collected'', in that order",' 'Check("checklist: the five lines are ''All valuables collected'', ''All unique items collected'', ''All safes opened'', ''All cash collected'' and ''Outpost hacked'', in that order",'
Rep 'ChecklistRules.Count == 4 &&' 'ChecklistRules.Count == 5 &&'
Rep '&& (int)ScanKind.Valuable == 0 && (int)ScanKind.Unique == 1 && (int)ScanKind.Safe == 2 && (int)ScanKind.Cash == 3);' '&& (int)ScanKind.Valuable == 0 && (int)ScanKind.Unique == 1 && (int)ScanKind.Safe == 2 && (int)ScanKind.Cash == 3 && (int)ScanKind.Outpost == 4 && ChecklistRules.Label(ScanKind.Outpost) == "Outpost hacked");'
Rep 'safe = ScanKind.Safe, cash = ScanKind.Cash;' 'safe = ScanKind.Safe, cash = ScanKind.Cash, post = ScanKind.Outpost;'
Rep 'st.Update(new[] { 5, 2, 1, 3 }, 10f);' 'st.Update(new[] { 5, 2, 1, 3, 1 }, 10f);'
Rep 'bool early = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && st.First(val) == -1;' 'bool early = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && !st.Done(post) && st.First(val) == -1;'
Rep 'st.Update(new[] { 5, 2, 1, 3 }, 10f + ChecklistRules.SettleSeconds + 0.1f);' 'st.Update(new[] { 5, 2, 1, 3, 1 }, 10f + ChecklistRules.SettleSeconds + 0.1f);'
Rep 'st.First(cash) == 3 && !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash);' 'st.First(cash) == 3 && st.First(post) == 1 && !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && !st.Done(post);'
Rep 'st.Update(new[] { 0, 2, 1, 3 }, 14f);' 'st.Update(new[] { 0, 2, 1, 3, 1 }, 14f);'
Rep 'bool oneDone = st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash);' 'bool oneDone = st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && !st.Done(post);'
Rep 'st.Update(new[] { 0, 0, 0, 3 }, 15f);' 'st.Update(new[] { 0, 0, 0, 3, 1 }, 15f);'
Rep 'bool threeDone = st.Done(val) && st.Done(uni) && st.Done(safe) && !st.Done(cash);' 'bool threeDone = st.Done(val) && st.Done(uni) && st.Done(safe) && !st.Done(cash) && !st.Done(post);'
Rep 'st.Update(new[] { 4, 0, 0, 0 }, 16f);' 'st.Update(new[] { 4, 0, 0, 0, 0 }, 16f);'
Rep 'bool latched = st.Done(val) && st.Done(uni) && st.Done(safe) && st.Done(cash);' 'bool latched = st.Done(val) && st.Done(uni) && st.Done(safe) && st.Done(cash) && st.Done(post);'
Rep 'bool reset = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && st.First(val) == -1;' 'bool reset = !st.Done(val) && !st.Done(uni) && !st.Done(safe) && !st.Done(cash) && !st.Done(post) && st.First(val) == -1 && st.First(post) == -1;'
Rep 'st.Update(new[] { 3, 0, 2, 0 }, 20f); st.Update(new[] { 3, 0, 2, 0 }, 22f);' 'st.Update(new[] { 3, 0, 2, 0, 0 }, 20f); st.Update(new[] { 3, 0, 2, 0, 0 }, 22f);'
Rep 'bool vacuous = st.Done(uni) && st.Done(cash) && !st.Done(val) && !st.Done(safe);' 'bool vacuous = st.Done(uni) && st.Done(cash) && st.Done(post) && !st.Done(val) && !st.Done(safe);'
Save-Src

# ---- the scenario: floor 1 and floor 2
Open-Src ($A + 'Editor\NetDev\NetDevChecklist.cs')
Rep 'First(ScanKind.Valuable) > 0 && First(ScanKind.Unique) > 0 && First(ScanKind.Safe) == 1 && First(ScanKind.Cash) > 0,' 'First(ScanKind.Valuable) > 0 && First(ScanKind.Unique) > 0 && First(ScanKind.Safe) == 1 && First(ScanKind.Cash) > 0 && First(ScanKind.Outpost) == 1,'
Rep 'ChecklistCheck("floor 1: nothing is done yet", !Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash));' 'ChecklistCheck("floor 1: nothing is done yet (the outpost''s lock is shut)", !Done(ScanKind.Valuable) && !Done(ScanKind.Unique) && !Done(ScanKind.Safe) && !Done(ScanKind.Cash) && !Done(ScanKind.Outpost));'
Rep 'ChecklistCheck("all four lines are done", Done(ScanKind.Valuable) && Done(ScanKind.Unique) && Done(ScanKind.Safe) && Done(ScanKind.Cash));' @'
var outpost = OutpostController.Active;
            ChecklistCheck("the first four lines are done and the outpost's, whose lock is still shut, is not (the owner, 2026-10-24)", outpost != null && !outpost.Unlocked && !Done(ScanKind.Outpost) && Done(ScanKind.Valuable) && Done(ScanKind.Unique) && Done(ScanKind.Safe) && Done(ScanKind.Cash));
            if (outpost != null) { outpost.NetApply((byte)(outpost.Bits | 1)); yield return new WaitForSeconds(1.2f); }          // (the lock is hacked open)
            ChecklistCheck("the outpost's lock is hacked open: its line is done, and all five lines are done", outpost != null && outpost.Unlocked && Done(ScanKind.Outpost) && Done(ScanKind.Valuable) && Done(ScanKind.Unique) && Done(ScanKind.Safe) && Done(ScanKind.Cash));
'@
Rep 'ChecklistCheck("floor 2: a new list - the safe is shut and its line is not done", !Done(ScanKind.Safe) && FloorChecklistTracker.FirstCount(ScanKind.Safe) == 1);' @'
ChecklistCheck("floor 2: a new list - the safe is shut and its line is not done", !Done(ScanKind.Safe) && FloorChecklistTracker.FirstCount(ScanKind.Safe) == 1);
            ChecklistCheck("floor 2: the outpost's line starts again too (its lock is shut)", !Done(ScanKind.Outpost) && FloorChecklistTracker.FirstCount(ScanKind.Outpost) == 1);
'@
Save-Src
"outpost checklist patched"

# The real-floor check of the carry rule now compares with the rule of before (one side was enough, and the walls round the room did not count): how many treasures each would keep.
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p
if ($s.Contains('OldRoomForCarry')) { "already"; return }
Rep '            int heavyFloor = 0, passedAsPlanned = 0, rescued = 0, lost = 0, badMoves = 0;' '            int heavyFloor = 0, passedAsPlanned = 0, rescued = 0, lost = 0, badMoves = 0, oldKept = 0;'
Rep '                    heavyFloor++;
                    if (LootPlan.RoomForCarry(layout, s, lifters)) { passedAsPlanned++; continue; }' '                    heavyFloor++;
                    if (OldRoomForCarry(layout, s, lifters)) oldKept++;
                    if (LootPlan.RoomForCarry(layout, s, lifters)) { passedAsPlanned++; continue; }'
Rep '            Debug.Log($"[NetTest] carry both sides: {heavyFloor} treasures for two or three on the floor in 15 floors: {passedAsPlanned} fine as planned, {rescued} moved to a spot with room on two sides, {lost} left out");' '            Debug.Log($"[NetTest] carry both sides: {heavyFloor} treasures for two or three on the floor in 15 floors: {passedAsPlanned} fine as planned, {rescued} moved to a spot with room on two sides, {lost} left out (the rule of before, one open side, would have kept {oldKept})");'
Rep '            Check("carry both sides: on the real floors the rule is neither empty nor blind (some are fine as planned, some are moved, and moving saves most of them)", heavyFloor > 0 && passedAsPlanned + rescued > 0 && rescued >= lost, $"({passedAsPlanned} as planned, {rescued} moved, {lost} lost of {heavyFloor})");' '            Check("carry both sides: on the real floors the rule is neither empty nor blind: some are fine as planned, some are moved, and a floor keeps most of what the rule of before kept (three in four)", heavyFloor > 0 && passedAsPlanned > 0 && rescued > 0 && (passedAsPlanned + rescued) * 4 >= oldKept * 3, $"({passedAsPlanned} as planned, {rescued} moved, {lost} lost of {heavyFloor}; before {oldKept})");'
Rep '        static void CarryBothSidesTest()' @'
        /// <summary>The rule as it was until 2026-10-21 (room on ONE side was enough, and the walls round the room did not count): for comparing how many treasures each keeps.</summary>
        static bool OldRoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && LootPlan.FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            for (int k = 0; k < 4; k++)
            {
                float fx = k == 0 ? 1f : k == 2 ? -1f : 0f, fz = k == 1 ? 1f : k == 3 ? -1f : 0f;
                if (LootPlan.FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need) return true;
            }
            return false;
        }

        static void CarryBothSidesTest()
'@
Save-Src
$t = [IO.File]::ReadAllText($p).Replace('public partial class NetDevTest', 'public static partial class NetDevTest')
$tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev'
[IO.File]::WriteAllText("$tree\Assets\Editor\NetDev\NetDevAsks1021Test.cs", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
"p13 done"

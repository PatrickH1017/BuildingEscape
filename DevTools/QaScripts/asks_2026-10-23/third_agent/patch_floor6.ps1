# The new build goes until floor 6 instead of floor 4 (the owner, 2026-10-23: "When making the new build it should go until floor 6 instead of floor 4").
# The limit is one constant, TowerRules.DemoLastFloor; this patch changes it and every test / autopilot loop that assumed four floors (they now read the constant).
# Exact-match patch of the scratch copy C:\bd (patchlib.ps1); -Dry only checks that every anchor still matches.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$bd = 'C:\bd\Assets'

# ---------------------------------------------------------------- the rule
Open-Src "$bd\Scripts\TowerRules.cs"
Rep @'
        /// basically making this a demo up to the 4th floor (the full game will remove this limitation)". The service elevator goes no
        /// higher than this floor and the panel keeps the floors above it dark. To remove the limit, set this to <see cref="BossFloor"/>.
'@ @'
        /// basically making this a demo up to the 4th floor (the full game will remove this limitation)"; and 2026-10-23, for the new build: "When making the new build it should go until floor 6 instead of floor 4".
        /// The service elevator goes no higher than this floor and the panel keeps the floors above it dark. To remove the limit, set this to <see cref="BossFloor"/>.
'@
Rep 'public const int DemoLastFloor = 4;' 'public const int DemoLastFloor = 6;'
Rep '(<see cref="DemoLastFloor"/>): from floor 4 there is no way up, and once it is finished the docks have no floor to go up to.' '(<see cref="DemoLastFloor"/>): from the last floor there is no way up, and once it is finished the docks have no floor to go up to.'
Save-Src

# ---------------------------------------------------------------- the pure checks
Open-Src "$bd\Editor\NetDev\NetDevDemoTest.cs"
Rep 'Check("demo: the tower ends at floor 4 for now", TowerRules.DemoLastFloor == 4);' 'Check("demo: the tower ends at floor 6 for now (the owner, 2026-10-23: the new build goes until floor 6)", TowerRules.DemoLastFloor == 6);'
Rep 'Check("demo: from the docks the elevator resumes at floors 1 to 4", TowerRules.UpFrom(0, false, 1) == 1 && TowerRules.UpFrom(0, false, 2) == 2' 'Check("demo: from the docks the elevator resumes at floors 1 to 6", TowerRules.UpFrom(0, false, 1) == 1 && TowerRules.UpFrom(0, false, 2) == 2'
Rep '&& TowerRules.UpFrom(0, false, 3) == 3 && TowerRules.UpFrom(0, false, 4) == 4);' '&& TowerRules.UpFrom(0, false, 3) == 3 && TowerRules.UpFrom(0, false, 4) == 4 && TowerRules.UpFrom(0, false, 5) == 5 && TowerRules.UpFrom(0, false, 6) == 6);'
Rep 'Check("demo: from floor 4 there is no way up, finished or not", TowerRules.UpFrom(4, true, 5) == -1 && TowerRules.UpFrom(4, false, 4) == -1);' 'Check("demo: from floor 6 there is no way up, finished or not (floors 4 and 5 still lead up once their quota is met, not before)", TowerRules.UpFrom(6, true, 7) == -1 && TowerRules.UpFrom(6, false, 6) == -1
                && TowerRules.UpFrom(4, true, 5) == 5 && TowerRules.UpFrom(5, true, 6) == 6 && TowerRules.UpFrom(4, false, 4) == -1 && TowerRules.UpFrom(5, false, 5) == -1);'
Rep 'Check("demo: with all four floors done the docks have nothing to go up to", TowerRules.UpFrom(0, false, 5) == -1 && TowerRules.UpFrom(0, true, 5) == -1' 'Check("demo: with all six floors done the docks have nothing to go up to", TowerRules.UpFrom(0, false, 7) == -1 && TowerRules.UpFrom(0, true, 7) == -1'
Rep 'Check("demo: nothing above floor 4 is ever a destination, from any floor in any state", never);' 'Check("demo: nothing above floor 6 is ever a destination, from any floor in any state", never);'
Rep 'Check("demo: floor 4 is the end of the demo and nothing else is", TowerRules.IsDemoEnd(4) && !TowerRules.IsDemoEnd(3) && !TowerRules.IsDemoEnd(5) && !TowerRules.IsDemoEnd(0));' 'Check("demo: floor 6 is the end of the demo and nothing else is", TowerRules.IsDemoEnd(6) && !TowerRules.IsDemoEnd(3) && !TowerRules.IsDemoEnd(5) && !TowerRules.IsDemoEnd(7) && !TowerRules.IsDemoEnd(0));'
Rep 'Check("demo: floors 5 and up are beyond it, the docks and floors 1-4 are not", TowerRules.Beyond(5) && TowerRules.Beyond(13) && !TowerRules.Beyond(4) && !TowerRules.Beyond(0) && !TowerRules.Beyond(1));' 'Check("demo: floors 7 and up are beyond it, the docks and floors 1-6 are not", TowerRules.Beyond(7) && TowerRules.Beyond(13) && !TowerRules.Beyond(6) && !TowerRules.Beyond(4) && !TowerRules.Beyond(0) && !TowerRules.Beyond(1));'
Rep 'Check("demo: it is over once the floor to climb to is 5", TowerRules.DemoOver(5) && !TowerRules.DemoOver(4) && !TowerRules.DemoOver(1));' 'Check("demo: it is over once the floor to climb to is 7", TowerRules.DemoOver(7) && !TowerRules.DemoOver(6) && !TowerRules.DemoOver(1));'
Rep 'Check("demo: the HUD counts the docks and floors 1-4 only (5 stops), or all 14 without the limit", TowerRules.DemoStops(14) == 5 &&' 'Check("demo: the HUD counts the docks and floors 1-6 only (7 stops), or all 14 without the limit", TowerRules.DemoStops(14) == 7 &&'
Rep 'string above = ElevatorExtraction.WhyNot(5, 4, -1, 0), fromDocks' 'string above = ElevatorExtraction.WhyNot(7, 6, -1, 0), fromDocks'
Rep 'Check("demo: the panel says why floor 5 is closed (from floor 4 and from the docks)", above.Contains("demo") && ElevatorExtraction.WhyNot(5, 0, -1, -1).Contains("demo") && high.Contains("demo"));' 'Check("demo: the panel says why floor 7 is closed (from floor 6 and from the docks)", above.Contains("demo") && ElevatorExtraction.WhyNot(7, 0, -1, -1).Contains("demo") && high.Contains("demo"));'
Save-Src

# ---------------------------------------------------------------- the Play scenario Demo (offline and two processes)
Open-Src "$bd\Editor\NetDev\NetDevDemo.cs"
Rep 'floors 1 to 4, each finished and left for the docks;' 'floors 1 to 6 (the demo''s last: TowerRules.DemoLastFloor), each finished and left for the docks;'
Rep '// ---- floors 1 to 4: finish each one and go down to the docks' '// ---- floors 1 to the demo''s last (6): finish each one and go down to the docks'
Rep 'for (int floor = 1; floor <= 4; floor++)' 'for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)'
Rep 'bool expectUp = floor < 3;          // (floor 3 ends its group, floor 4 ends the demo)' 'bool expectUp = !TowerRules.IsGroupEnd(floor) && floor < TowerRules.DemoLastFloor;          // (floors 3 and 6 end their group, floor 6 ends the demo)'
Rep @'
                if (floor == 4)
                {
                    tm.RideTo(5);
                    yield return new WaitForSeconds(1.5f);
                    DemoCheck("floor 4: the elevator refuses floor 5 (nothing is built, the team stays)", hm.Floor == 4 && !tm.Loading);
                    tm.RideTo(13);
                    yield return new WaitForSeconds(0.5f);
                    DemoCheck("floor 4: and the boss floor", hm.Floor == 4 && !tm.Loading);
                }
'@ @'
                if (floor == TowerRules.DemoLastFloor)
                {
                    tm.RideTo(TowerRules.DemoLastFloor + 1);
                    yield return new WaitForSeconds(1.5f);
                    DemoCheck($"floor {floor}: the elevator refuses floor {floor + 1} (nothing is built, the team stays)", hm.Floor == floor && !tm.Loading);
                    tm.RideTo(13);
                    yield return new WaitForSeconds(0.5f);
                    DemoCheck($"floor {floor}: and the boss floor", hm.Floor == floor && !tm.Loading);
                }
'@
Rep @'
            DemoCheck("floor 4 done: the demo is over", TowerRules.DemoOver(tm.ResumeLevel) && tm.ResumeLevel == 5);
            DemoCheck("floor 4 done: from the docks the elevator goes nowhere", TowerRules.UpFrom(0, false, tm.ResumeLevel) == -1);
            tm.RideElevator();
            tm.RideTo(5);
            yield return new WaitForSeconds(1.5f);
            DemoCheck("floor 4 done: neither the elevator's own up nor a ride to floor 5 builds a floor", hm.Floor == 0 && hm.Phase == HeistPhase.Hub && !tm.Loading);
'@ @'
            DemoCheck("floor 6 done: the demo is over", TowerRules.DemoOver(tm.ResumeLevel) && tm.ResumeLevel == TowerRules.DemoLastFloor + 1);
            DemoCheck("floor 6 done: from the docks the elevator goes nowhere", TowerRules.UpFrom(0, false, tm.ResumeLevel) == -1);
            tm.RideElevator();
            tm.RideTo(TowerRules.DemoLastFloor + 1);
            yield return new WaitForSeconds(1.5f);
            DemoCheck("floor 6 done: neither the elevator's own up nor a ride to floor 7 builds a floor", hm.Floor == 0 && hm.Phase == HeistPhase.Hub && !tm.Loading);
'@
Rep 'DemoCheck("floor 4 done: the pew is for sale", Vendor.PewForSale(tm.ResumeLevel));' 'DemoCheck("floor 6 done: the pew is for sale", Vendor.PewForSale(tm.ResumeLevel));'
Rep '// the demo''s last floor is finished (the same state the elevator leaves behind): the next floor to climb to is 5' '// the demo''s last floor is finished (the same state the elevator leaves behind): the next floor to climb to is the one after it (7)'
Rep 'typeof(TowerManager).GetProperty("ResumeLevel").SetValue(tm, 5);' 'typeof(TowerManager).GetProperty("ResumeLevel").SetValue(tm, TowerRules.DemoLastFloor + 1);'
Rep 'yield return WaitFor(() => tm.ResumeLevel == 5, 30, "the host''s resume level");' 'yield return WaitFor(() => tm.ResumeLevel == TowerRules.DemoLastFloor + 1, 30, "the host''s resume level");'
Save-Src

# ---------------------------------------------------------------- the loot Q/A (floors built for several seeds, then climbed the real way)
Open-Src "$bd\Editor\NetDev\NetDevLootQa.cs"
Rep @'
                tm.baseSeed = seed;
                for (int floor = 1; floor <= 4; floor++)
'@ @'
                tm.baseSeed = seed;
                for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
'@
Rep @'
            hm.godMode = true;
            for (int floor = 1; floor <= 4; floor++)
            {
                tm.RideTo(floor);
'@ @'
            hm.godMode = true;
            for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
            {
                tm.RideTo(floor);
'@
Rep 'LqCheck("after floor 4 the demo is over and the vendor has the pew",' 'LqCheck("after the demo''s last floor (6) the demo is over and the vendor has the pew",'
Rep 'Q/A of the demo''s game loop (floors 1 to 4) beyond the rules' 'Q/A of the demo''s game loop (floors 1 to 6) beyond the rules'
Save-Src

# ---------------------------------------------------------------- the built-player autopilot (solo loop, host and client of the network loop)
Open-Src "$bd\Scripts\QaAutopilot.cs"
Rep 'the docks, floors 1 to 4 (each one built' 'the docks, floors 1 to 6 (each one built'
Rep @'
            // ---- floors 1 to 4
            for (int floor = 1; floor <= 4; floor++)
'@ @'
            // ---- floors 1 to the demo's last (6)
            for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
'@
Rep 'if (floor == 4) yield return Shot("docks_demo_complete");' 'if (floor == TowerRules.DemoLastFloor) yield return Shot("docks_demo_complete");'
Rep 'Check("floor 4 done: the demo is over and the vendor has the pew",' 'Check("the last floor (6) done: the demo is over and the vendor has the pew",'
Rep @'
            for (int floor = 1; floor <= 4; floor++)
            {
                tm.RideTo(floor);
                yield return Until(() => tm.Loading || hm.Floor == floor, 15f, $"the ride to floor {floor}");
'@ @'
            for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
            {
                tm.RideTo(floor);
                yield return Until(() => tm.Loading || hm.Floor == floor, 15f, $"the ride to floor {floor}");
'@
Rep @'
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return Until(() => FloorIs(floor), 240f, $"floor {floor} (the host rides up)");
'@ @'
            for (int floor = 1; floor <= TowerRules.DemoLastFloor; floor++)
            {
                yield return Until(() => FloorIs(floor), 240f, $"floor {floor} (the host rides up)");
'@
Save-Src
Write-Output 'floor 6 patched'

# The pure carry tests for the walk-to-the-far-side rule (see p16): the dead-end aisle is refused, a wide aisle is fine, a server in a pocket moves, the narrow aisle room loses its server, the real floors by kind.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p
if ($s.Contains('dead end behind it')) { "already"; return }

Rep @'
            // the same room, the server in the middle of the aisle: open in front and behind (a carrier at each end)
            var aisle = new Slot { x = 1.7f, y = 0f, z = 2.1f, kind = "server", room = "r" };
            Check("carry both sides: the same aisle with the server in the middle of it, free in front and behind, is fine for two", LootPlan.RoomForCarry(l, aisle, 2));
'@ @'
            // the same room, the server in the middle of the aisle: room in front and behind it, but the aisle is only a little wider than the box, so nobody can walk past it to the far end (a dead end behind it)
            var aisle = new Slot { x = 1.7f, y = 0f, z = 2.1f, kind = "server", room = "r" };
            Check("carry both sides: in the middle of that narrow aisle there is room in front and behind the server, but it fills the aisle and nobody can walk round it to the far side (a dead end behind it): refused", !LootPlan.RoomForCarry(l, aisle, 2, 1.0f, 0.9f));
            // a wide room: the same racks along the walls, an aisle four metres wide: the server in the middle of it can be walked round
            var wide = ServerRoom(out var wideRoom, 6.0f, 6.5f);
            Check("carry both sides: in a room with a wide aisle the same server can be walked round and carried from both ends", LootPlan.RoomForCarry(wide, new Slot { x = 3.0f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2, 1.0f, 0.9f));
'@

Rep @'
            // the wedged server moves to the nearest spot of its room that has room on both sides
            bool moved = LootPlan.TryFindCarrySpot(l, wedged, 2, 1.0f, 0.9f, out float mx, out float mz);
            var movedSlot = new Slot { x = mx, y = 0f, z = mz, kind = "server", room = "r" };
            bool insideRoom = room.bounds.Grow(-0.4f).Contains(mx, mz);
            bool clearOfRacks = !l.pieces.Any(p => p.rc.Overlaps(new Rc(mx - 0.5f, mz - 0.45f, mx + 0.5f, mz + 0.45f)));
            Check($"carry both sides: the server wedged between the racks is moved to a spot of its room where two carriers can stand on opposite sides ({mx:0.00}, {mz:0.00})", moved && LootPlan.RoomForCarry(l, movedSlot, 2) && insideRoom && clearOfRacks);
            Check("carry both sides: ... the spot is in the same room, clear of the racks, and the same answer every time (no randomness)", moved && LootPlan.TryFindCarrySpot(l, wedged, 2, 1.0f, 0.9f, out float mx2, out float mz2) && mx2 == mx && mz2 == mz);
            Check("carry both sides: nothing is moved that can be left (a thing for one, a thing on a counter)", !LootPlan.TryFindCarrySpot(l, wedged, 1, 1.0f, 0.9f, out _, out _) && !LootPlan.TryFindCarrySpot(counterRoom, new Slot { x = 20f, y = 0.95f, z = 20f, host = counter }, 2, 0.9f, 0.8f, out _, out _));
'@ @'
            // a server in a pocket of a wide room (a rack east of it, the racks north of it) is moved to the nearest spot of its room where the carriers can stand on opposite sides and walk to them
            var pocket = ServerRoom(out var pocketRoom, 6.0f, 6.5f);
            pocket.pieces.Add(Rack(3.6f, 4.3f, 4.6f, 5.6f));
            var stuck = new Slot { x = 3.0f, y = 0f, z = 5.0f, kind = "server", room = "r" };
            Check("carry both sides: a server in a pocket (a rack beside it and the racks behind it) is refused for two", !LootPlan.RoomForCarry(pocket, stuck, 2, 1.0f, 0.9f));
            bool moved = LootPlan.TryFindCarrySpot(pocket, stuck, 2, 1.0f, 0.9f, out float mx, out float mz);
            var movedSlot = new Slot { x = mx, y = 0f, z = mz, kind = "server", room = "r" };
            bool insideRoom = pocketRoom.bounds.Grow(-0.4f).Contains(mx, mz);
            bool clearOfRacks = !pocket.pieces.Any(p => p.rc.Overlaps(new Rc(mx - 0.5f, mz - 0.45f, mx + 0.5f, mz + 0.45f)));
            Check($"carry both sides: ... it is moved to a spot of its room where two carriers can stand on opposite sides and walk to them ({mx:0.00}, {mz:0.00})", moved && LootPlan.RoomForCarry(pocket, movedSlot, 2, 1.0f, 0.9f) && insideRoom && clearOfRacks);
            Check("carry both sides: ... the spot is in the same room, clear of the racks, and the same answer every time (no randomness)", moved && LootPlan.TryFindCarrySpot(pocket, stuck, 2, 1.0f, 0.9f, out float mx2, out float mz2) && mx2 == mx && mz2 == mz);
            Check("carry both sides: nothing is moved that can be left (a thing for one, a thing on a counter)", !LootPlan.TryFindCarrySpot(l, wedged, 1, 1.0f, 0.9f, out _, out _) && !LootPlan.TryFindCarrySpot(counterRoom, new Slot { x = 20f, y = 0.95f, z = 20f, host = counter }, 2, 0.9f, 0.8f, out _, out _));
            Check("carry both sides: in the narrow aisle room there is nowhere a server can stand with both ends reachable, so it is left out, not squeezed in", !LootPlan.TryFindCarrySpot(l, wedged, 2, 1.0f, 0.9f, out _, out _));
'@

Rep @'
            int heavyFloor = 0, passedAsPlanned = 0, rescued = 0, lost = 0, badMoves = 0, oldKept = 0;
'@ @'
            int heavyFloor = 0, passedAsPlanned = 0, rescued = 0, lost = 0, badMoves = 0, oldKept = 0;
            int srvPlanned = 0, srvMoved = 0, srvLost = 0, safePlanned = 0, safeMoved = 0, safeLost = 0;
'@
Rep @'
                    if (LootPlan.RoomForCarry(layout, s, lifters)) { passedAsPlanned++; continue; }
                    var size = s.kind == "safe" ? new Vector2(1.1f, 1.1f) : new Vector2(1.0f, 0.9f);
                    if (!LootPlan.TryFindCarrySpot(layout, s, lifters, size.x, size.y, out float nx, out float nz)) { lost++; continue; }
                    rescued++;
'@ @'
                    var size = s.kind == "safe" ? new Vector2(1.1f, 1.1f) : new Vector2(1.0f, 0.9f);
                    bool isSafe = s.kind == "safe";
                    if (LootPlan.RoomForCarry(layout, s, lifters, size.x, size.y)) { passedAsPlanned++; if (isSafe) safePlanned++; else srvPlanned++; continue; }
                    if (!LootPlan.TryFindCarrySpot(layout, s, lifters, size.x, size.y, out float nx, out float nz)) { lost++; if (isSafe) safeLost++; else srvLost++; continue; }
                    rescued++; if (isSafe) safeMoved++; else srvMoved++;
'@
Rep '                    if (!LootPlan.RoomForCarry(layout, ns, lifters) ||' '                    if (!LootPlan.RoomForCarry(layout, ns, lifters, size.x, size.y) ||'
Rep '{lost} left out (the rule of before, one open side, would have kept {oldKept})");' '{lost} left out (the rule of before, one open side, would have kept {oldKept}); servers {srvPlanned} as planned / {srvMoved} moved / {srvLost} left out, safes {safePlanned} / {safeMoved} / {safeLost}");'
Rep 'and a floor keeps most of what the rule of before kept (three in four)", heavyFloor > 0 && passedAsPlanned > 0 && rescued > 0 && (passedAsPlanned + rescued) * 4 >= oldKept * 3,' 'and a floor keeps at least half of what the rule of before kept (that rule did not see the walls, nor that the far side had to be reachable)", heavyFloor > 0 && passedAsPlanned > 0 && rescued > 0 && (passedAsPlanned + rescued) * 2 >= oldKept,'
Save-Src
$t = [IO.File]::ReadAllText($p).Replace('public partial class NetDevTest', 'public static partial class NetDevTest')
[IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevAsks1021Test.cs", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
"p17 done"

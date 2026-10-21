# Round 12, batch G20 (the owner, 2026-10-19 night: "The PvP room can also be a bit larger with some storage containers for players to use as coverage, maybe it can somewhat be like a warehouse (but really there for PvP
# just looks like a warehouse to make sense for the Loading Dock vibe)"): the PVP room is an L now - the hall along the north wall AND the pocket west of the elevator room, which used to be closed (the wall between them is gone) -
# furnished like a warehouse: a full-length shipping container along the west wall of the annex, a small one in the hall, loaded pallets, crates, barrels and two blocks of boxes to hide behind, the way in from the doorway left open.
# The Battle Royale pack's props (Container_01, Container_Small_01, Crate_Large_01, Pallet_Loaded_02, Barrel_01) go in the prop table (their sizes were measured: PropMeasure). BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$A\Interior\PropTable.cs"
Rep @'
        const string Syn = "Assets/Synty/PolygonOffice/Prefabs/";
'@ @'
        const string Syn = "Assets/Synty/PolygonOffice/Prefabs/";
        const string Br = "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/";
'@
Rep @'
            Add("dartboard", Syn + "Props/Wall Props/SM_Prop_Darkboard_01", .71f, .68f, .33f, solid: false, cat: PropCat.Mount);
'@ @'
            Add("dartboard", Syn + "Props/Wall Props/SM_Prop_Darkboard_01", .71f, .68f, .33f, solid: false, cat: PropCat.Mount);
            // ---------- the Battle Royale pack's warehouse things for the PVP room (the owner, 2026-10-19 night: "storage containers for players to use as coverage ... somewhat like a warehouse"); measured by Editor\PropMeasure.cs
            Add("container", Br + "SM_Prop_Container_01", 2.45f, 2.61f, 6.19f, cat: PropCat.Deco);
            Add("container_small", Br + "SM_Prop_Container_Small_01", 2.45f, 2.61f, 3.14f, cat: PropCat.Deco);
            Add("crate_large", Br + "SM_Prop_Crate_Large_01", .81f, .79f, .81f, cat: PropCat.Deco);
            Add("pallet_loaded", Br + "SM_Prop_Pallet_Loaded_02", 1.44f, 1.14f, 1.42f, cat: PropCat.Deco);
            Add("barrel", Br + "SM_Prop_Barrel_01", .66f, .91f, .57f, cat: PropCat.Deco);
'@
Save-Src

Open-Src "$A\FloorGenerator.DockRooms.cs"
Rep @'
        public Rect PvpRoomArea { get; private set; }
'@ @'
        public Rect PvpRoomArea { get; private set; }
        /// <summary>The PVP room is an L: the hall of <see cref="PvpRoomArea"/> along the north wall and this part that goes down the west side of the elevator room (the pocket that used to be closed; the owner, 2026-10-19 night: "a bit larger ... like a warehouse").</summary>
        public Rect PvpRoomAnnex { get; private set; }
'@
Rep @'
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..9.3, z 13.2..17.8
'@ @'
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..9.3, z 13.2..17.8
            PvpRoomAnnex = new Rect(0.2f, southWallZ + 0.2f, westFace - 0.4f, (northWallZ + 0.2f) - (southWallZ + 0.2f));           // x 0.2..4.6, z 5.2..13.2: it joins the hall along z 13.2
'@
Rep @'
            WallX(westFace * 0.5f, southWallZ, westFace);
            WallX(westFace * 0.5f, northWallZ, westFace);
'@ @'
            WallX(westFace * 0.5f, southWallZ, westFace);
            // (no wall at the elevator room's north-west corner any more: the pocket west of the elevator room is part of the PVP room now)
'@
Rep @'
            // ================= the PVP room: a few boxes and a red mat to fight on
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), new Color(1f, 0.85f, 0.8f), 1.2f);
            var mat = Flat(new Color(0.55f, 0.08f, 0.08f), 0.1f);
            Box("PvpMat", new Vector3(4.75f, 0.012f, (PvpRoomArea.yMin + PvpRoomArea.yMax) * 0.5f), new Vector3(6.0f, 0.024f, 3.4f), mat, parent, collider: false);
            BoxBlock(parent, 1.3f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.1f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 1, true);
            BoxBlock(parent, 0.75f, PvpRoomArea.yMin + 0.85f, 0f, 1, 2, 2, false);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
        }
'@ @'
            // ================= the PVP room: a warehouse (the owner, 2026-10-19 night: "a bit larger with some storage containers for players to use as coverage, maybe it can somewhat be like a warehouse (but really there for PvP
            // just looks like a warehouse to make sense for the Loading Dock vibe)"): an L (the hall along the north wall and the old pocket west of the elevator room), shipping containers, loaded pallets, crates and barrels to hide
            // behind, and the lane in front of the doorway left open
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightWest", new Vector3(1.9f, 3.1f, PvpRoomArea.yMin + 1.2f), new Color(1f, 0.85f, 0.8f), 1.0f);
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), new Color(1f, 0.85f, 0.8f), 1.2f);
            // the hall: a small container along the north wall at its west end, loaded pallets and barrels on the south side, two blocks of boxes (the middle of the hall and the way in from the doorway stay open)
            PlaceKey("container_small", 1.87f, PvpRoomArea.yMax - 1.23f, 90f, parent, 0f, true);
            PlaceKey("pallet_loaded", 2.4f, PvpRoomArea.yMin + 0.75f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 6.6f, PvpRoomArea.yMin + 0.75f, 90f, parent, 0f, true);
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 0.45f, 0f, parent, 0f, true);
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 1.15f, 0f, parent, 0f, true);
            BoxBlock(parent, 6.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
            // the annex: a full-length container along the west wall, pairs of stacked crates on the east side, an aisle between them
            PlaceKey("container", 1.43f, PvpRoomAnnex.center.y, 0f, parent, 0f, true);
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 1.7f, PvpRoomAnnex.yMax - 1.7f })
            {
                PlaceKey("crate_large", 4.3f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.3f, cz, 20f, parent, 0.79f, true);          // (a second one on top)
            }
        }
'@
Save-Src

Open-Src "$A\Pvp.cs"
Rep @'
        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z);
'@ @'
        /// <summary>The part of the PVP room that goes down the west side of the elevator room (the room is an L), or an empty rectangle.</summary>
        public static Rect Annex
        {
            get
            {
                var hm = HeistManager.Instance;
                return hm && hm.Floor == 0 && gen ? gen.PvpRoomAnnex : default;       // (Arena looks the generator up)
            }
        }

        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z);
'@
Save-Src

Open-Src "$E\NetDevRound12.cs"
Rep @'
            // ---- the dart board: on the east wall, across from the break room's doorway, middle 1.7 m up, its face to the west
'@ @'
            // ---- the PVP room is a warehouse now (the owner, 2026-10-19 night): an L with containers to hide behind, the annex joined to the hall, the way in from the doorway open
            {
                var annexRect = gen.PvpRoomAnnex; var hallRect = gen.PvpRoomArea;
                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t && t.parent != null).ToList();
                bool InPvp(Vector3 p) => hallRect.Contains(new Vector2(p.x, p.z)) || annexRect.Contains(new Vector2(p.x, p.z));
                var full = warehouse.Where(t => t.name == "container" && InPvp(t.position)).ToList();
                var small = warehouse.Where(t => t.name == "container_small" && InPvp(t.position)).ToList();
                float area = hallRect.width * hallRect.height + annexRect.width * annexRect.height;
                R12Check("the PVP room is an L: the hall along the north wall and the old pocket west of the elevator room, joined, 70 m2 and more (it was 42)",
                    annexRect.width > 4f && annexRect.height > 7.5f && Mathf.Abs(annexRect.yMax - hallRect.yMin) < 0.3f && area > 70f, $"(hall {hallRect}, annex {annexRect}, {area:0} m2)");
                R12Check("a full-length shipping container and a small one stand in it, solid, as cover", full.Count == 1 && small.Count == 1 && full[0].GetComponentInChildren<Collider>() != null && small[0].GetComponentInChildren<Collider>() != null,
                    $"({full.Count} full, {small.Count} small)");
                int cover = warehouse.Count(t => (t.name == "pallet_loaded" || t.name == "crate_large" || t.name == "barrel") && InPvp(t.position));
                R12Check("loaded pallets, crates and barrels give more cover (at least 8 pieces)", cover >= 8, $"({cover} pieces)");
                var joint = new Vector3(4.0f, 0f, hallRect.yMin);
                bool jointOpen = !Physics.CheckCapsule(joint + Vector3.up * 0.5f, joint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the hall opens into the annex (no wall: a player fits through the joint) and the way in from the doorway is open", jointOpen && laneOpen, $"(joint {jointOpen}, lane {laneOpen})");
                R12Check("a fighter in the annex counts as inside the PVP room, one in the elevator room next to it does not",
                    PvpRules.Inside(annexRect, 2.5f, 9f) && !PvpRules.Inside(annexRect, 7f, 9f) && !PvpRules.Inside(hallRect, 7f, 9f));
            }

            // ---- the dart board: on the east wall, across from the break room's doorway, middle 1.7 m up, its face to the west
'@
Save-Src
Write-Output 'round 12 batch G20 patched'

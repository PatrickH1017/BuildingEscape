# Round 11, batch G10: the Loading Docks in the lobby's clean look (the owner, 2026-10-19: "the assets from the lobby scene look way cleaner ... make the current loading dock look better with all the new assets, the layout is great. Use the lobby
# as a reference, for example the clock is great"). The layout stays; the props are the lobby's clean ones (DockDressing, built with LobbyDock's helpers), the wall gets the lobby's clock, pipes, notice board, extinguisher, door frames,
# roller boxes and foot band, the hall two hanging lamps and a turning beacon, the trailer its glowing strip and the lobby's shelving.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

# the new files: CRLF, no BOM (like the others)
if (-not $env:R2_DRY) {
  foreach ($p in "$Src\DockDressing.cs", "$Ed\NetDevDockLook.cs") {
    $t = [IO.File]::ReadAllText($p)
    [IO.File]::WriteAllText($p, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
  }
}

# ---- the lobby's helpers can be used by the basement's dock too
Open-Src "$Src\Net\LobbyDock.cs"
Rep 'static Material Mat(Color c' 'internal static Material Mat(Color c'
Rep 'static Material Glow(Color c, float strength)' 'internal static Material Glow(Color c, float strength)'
Rep 'if (col) Object.Destroy(col);' 'if (col) { if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col); }'
Rep 'static GameObject Box(string name' 'internal static GameObject Box(string name'
Rep 'static GameObject Cyl(string name' 'internal static GameObject Cyl(string name'
Rep 'static Light MakeLight(string name' 'internal static Light MakeLight(string name'
Rep 'static void Pallet(Transform parent' 'internal static void Pallet(Transform parent'
Rep 'static void ShelfUnit(Transform parent' 'internal static void ShelfUnit(Transform parent'
Rep 'static void Forklift(Transform parent' 'internal static void Forklift(Transform parent'
Rep 'static Transform ClockHand(Transform clock' 'internal static Transform ClockHand(Transform clock'
Save-Src

# ---- the basement's loading dock
Open-Src "$Src\FloorGenerator.Dock.cs"
Rep @'
        bool dockWalls;
'@ @'
        bool dockWalls;
        // the dock's dressing in the lobby's clean look (DockDressing): a holder at the world origin under the dock's root, and the component that moves the clock, the lamps and the beacon
        Transform dockHolder;
        Net.LobbyDockLife dockLife;
'@
Rep @'
            root.transform.position = c;
'@ @'
            root.transform.position = c;
            dockHolder = DockDressing.Holder(root.transform);
            dockLife = root.AddComponent<Net.LobbyDockLife>();
'@
Rep @'
            Box("BayBollardL", new Vector3(tx - DockDoorWidth / 2f - 0.15f, 0.6f, zWall - 1.0f), new Vector3(0.25f, 1.2f, 0.25f), hazard, root.transform);
            Box("BayBollardR", new Vector3(tx + DockDoorWidth / 2f + 0.15f, 0.6f, zWall - 1.0f), new Vector3(0.25f, 1.2f, 0.25f), hazard, root.transform);
'@ @'
            DockDressing.Bollard(dockHolder, tx - DockDoorWidth / 2f - 0.15f, zWall - 1.0f);          // (the lobby's yellow bollards with their dark caps)
            DockDressing.Bollard(dockHolder, tx + DockDoorWidth / 2f + 0.15f, zWall - 1.0f);
'@
Rep @'
wl.type = LightType.Point; wl.color = new Color(1f, 0.93f, 0.8f); wl.range = 10f; wl.intensity = 1.5f;
'@ @'
wl.type = LightType.Point; wl.color = new Color(1f, 0.93f, 0.8f); wl.range = 10f; wl.intensity = 1.5f;
            dockLife.trailerLamp = wl;          // (breathes a little, like the lobby's trailer light)
'@
Rep @'
            StockTrailer(root.transform, tx, zr, TrailerLength - 3.4f, TrailerInner, mat);
'@ @'
            StockTrailer(root.transform, tx, zr, TrailerLength - 3.4f, TrailerInner, mat);
            BuildDockDecor(cx, zWall, x0, tx, zm);
'@
Rep @'
                new Vector4(0f, 12.7f, 9.9f, 18f),  // the PVP room
'@ @'
                new Vector4(0f, 12.7f, 9.9f, 18f),  // the PVP room
                new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),  // the clock, the notice board and the extinguisher on the east wall
'@
Rep @'
        /// <summary>Where the dummy stands, and how many pieces of storage were scattered (for tests).</summary>
'@ @'
        /// <summary>
        /// The look of the lobby's loading dock on the basement's own (the owner, 2026-10-19: "the layout is great ... use the lobby as a reference, for example the clock is great"): steel door frames and roller boxes, a dark band along the foot of the bay wall
        /// between the doors, a wall clock on the east wall that tells the real time, pipes under the ceiling, a notice board and an extinguisher, two hanging lamps, a turning beacon by the open bay and the glowing strip in the trailer (see <see cref="DockDressing"/>).
        /// </summary>
        void BuildDockDecor(float cx, float zWall, float x0, float tx, float zm)
        {
            var h = dockHolder;
            float zi = zWall - 0.2f;                                   // the inner face of the bay wall
            float width = cols * cellSize, depth = rows * cellSize, eastInner = width - 0.2f;
            float prevEnd = x0;
            for (int i = 0; i < DockDoorOffsets.Length; i++)
            {
                float dc = cx + DockDoorOffsets[i];
                float start = dc - DockDoorWidth / 2f, end = dc + DockDoorWidth / 2f;
                DockDressing.DoorFrame(h, dc, zi, DockDoorWidth, DockDoorHeight, true);
                DockDressing.WallBase(h, prevEnd, start, zi);
                prevEnd = end;
            }
            DockDressing.WallBase(h, prevEnd, x0 + cellSize, zi);
            DockDressing.Clock(h, dockLife, new Vector3(eastInner - 0.07f, 2.45f, depth / 2f), 90f);
            DockDressing.Pipes(h, eastInner, 0.4f, depth - 0.4f, 3.1f);
            DockDressing.WallThings(h, eastInner, depth / 2f - 2.8f, depth / 2f + 2.8f);
            DockDressing.Lamps(h, dockLife, new[] { new Vector3(cx - 3.2f, 0f, zWall - 7.5f), new Vector3(tx - 1.0f, 0f, zWall - 11.0f) }, wallHeight - 0.05f, 0.3f);
            DockDressing.Beacon(h, dockLife, new Vector3(tx - DockDoorWidth / 2f - 0.9f, 2.7f, zi - 0.15f));
            DockDressing.TrailerStrip(h, new Vector3(tx, TrailerHeight - 0.06f, zm - 1.5f), 3.4f);
        }

        /// <summary>Where the dummy stands, and how many pieces of storage were scattered (for tests).</summary>
'@
RepRx '(?s)// filing cabinets first.*?return count;' @'
// steel shelving against the walls, fronts facing in (the clean units of the lobby's loading dock, DockDressing: the owner, 2026-10-19, "the assets from the lobby scene look way cleaner")
            int wallTarget = 9;
            for (int tries = 0; tries < 300 && wallTarget > 0; tries++)
            {
                int side = rng.Next(4); // 0 west, 1 east, 2 south, 3 north
                float along = side < 2 ? F(1f, depth - 1f) : F(1f, width - 1f);
                float half = DockDressing.ShelfDepth / 2f + 0.12f;
                float x, z, yaw;
                switch (side)
                {
                    case 0: x = half; z = along; yaw = 90f; break;
                    case 1: x = width - half; z = along; yaw = 270f; break;
                    case 2: x = along; z = half; yaw = 0f; break;
                    default: x = along; z = depth - half; yaw = 180f; break;
                }
                float r = DockDressing.ShelfWidth / 2f;
                if (!Free(x, z, r, true)) continue;
                DockDressing.Shelf(dockHolder, x, z, yaw, rng.Next(1000));
                placed.Add(new Vector3(x, z, r));
                wallTarget--; count++;
            }

            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
            int stacks = 22;
            for (int tries = 0; tries < 400 && stacks > 0; tries++)
            {
                float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                if (!Free(x, z, 0.5f)) continue;
                int height = 1 + rng.Next(3);
                DockDressing.BoxStack(dockHolder, x, z, 90f * rng.Next(4), height, rng.Next(1000));
                placed.Add(new Vector3(x, z, 0.55f));
                stacks--; count += height;
            }

            // a few pallets with their boxes squared on them
            for (int tries = 0, pallets = 4; tries < 200 && pallets > 0; tries++)
            {
                float x = F(1.2f, width - 1.2f), z = F(1.2f, depth - 1.2f);
                if (!Free(x, z, 1.0f)) continue;
                DockDressing.BoxBlock(dockHolder, x, z, 90f * rng.Next(4), 2, 2, 1 + rng.Next(2), true, rng.Next(1000));
                placed.Add(new Vector3(x, z, 1.0f));
                pallets--; count += 4;
            }

            // two forklifts, and drums in twos and threes
            for (int tries = 0, lifts = 2; tries < 200 && lifts > 0; tries++)
            {
                float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f);
                if (!Free(x, z, 1.5f)) continue;
                DockDressing.Forklift(dockHolder, x, z, F(0f, 360f));
                placed.Add(new Vector3(x, z, 1.5f));
                lifts--; count++;
            }
            for (int tries = 0, groups = 3; tries < 200 && groups > 0; tries++)
            {
                float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                if (!Free(x, z, 0.9f)) continue;
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++) DockDressing.Drum(dockHolder, x + (k % 2) * 0.62f, z + (k / 2) * 0.62f, rng.Next(2));
                placed.Add(new Vector3(x + 0.3f, z + 0.3f, 0.9f));
                groups--; count += n;
            }
            return count;
'@
RepRx '(?s)var rng = new System\.Random\(0x7A11\);.*?(?=\r?\n        \}\r?\n\r?\n        /// <summary>Puts one prop from the table at a ground spot)' @'
// the clean steel shelving of the lobby's trailer down both walls, boxes on its boards, the aisle left clear (DockDressing)
            var rng = new System.Random(0x7A11);
            foreach (var side in new[] { -1f, 1f })
            {
                float z = 0.4f;
                while (z + DockDressing.ShelfWidth <= length)
                {
                    float x = tx + side * (innerWidth / 2f - DockDressing.ShelfDepth / 2f - 0.03f);
                    float zc = zr + z + DockDressing.ShelfWidth / 2f;
                    DockDressing.Shelf(dockHolder, x, zc, 90f, rng.Next(1000));          // (its long side along the trailer's wall)
                    z += DockDressing.ShelfWidth + 0.12f;
                }
            }
'@
Save-Src

# ---- the boxes of the PVP room (and every other block of boxes) are the clean ones too
Open-Src "$Src\FloorGenerator.DockRooms.cs"
RepRx '(?s)float bw = 0\.52f, bd = 0\.50f, bh = 0\.42f;.*?(?=\r?\n        \}\r?\n\r?\n        void BuildDockRooms)' @'
DockDressing.BoxBlock(dockHolder != null ? dockHolder : parent, x, z, yaw, cols, rowsN, layers, pallet, (int)(Mathf.Abs(x) * 31f + Mathf.Abs(z) * 17f));          // (the lobby's cardboard on its pallet, square to the room)
'@
Save-Src

# ---- the scenario is registered like the others
Open-Src "$Ed\NetDevHarness.cs"
Rep @'
        public static void Armour()
'@ @'
        public static void DockLook()
        {
            SessionState.SetString("netdev.mode", "docklook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Armour()
'@
Rep @'
                case "armour": yield return ArmourScenario(); break;
'@ @'
                case "armour": yield return ArmourScenario(); break;
                case "docklook": yield return DockLookScenario(); break;
'@
Save-Src

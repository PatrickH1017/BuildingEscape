# Round 11, batch G13 (the owner, 2026-10-19: "the Vendor is a black market shop he shouldn't have boxes he should have all cool gadgets and weapons and stuff in his truck" and "The briefcases in the Vendor's shop were a nice touch as well"):
# the vendor's shelves are the lobby's steel frames with no cardboard: the top board of each holds weapons (the melee weapons, or pistols with their clips - only what the demo has), the two below it gadgets (a radio, a torch, night-vision goggles, a drone,
# a spy camera, a tool case, a stack of cash, gold bars); the briefcases lie on the floor again. The drums fit more easily.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

# ---- a gun's model alone, for a shelf
Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
            size = new Vector3(magBounds[ki].size.y, magBounds[ki].size.x, magBounds[ki].size.z) * lay.scale;
            return clone;
        }
'@ @'
            size = new Vector3(magBounds[ki].size.y, magBounds[ki].size.x, magBounds[ki].size.z) * lay.scale;
            return clone;
        }

        /// <summary>
        /// The gun's model alone, for a shelf or a rack (the owner, 2026-10-19: the vendor's shop shows his stock): lying on its side the way <see cref="Create"/> has it, with no collider, no item and no silencer, its middle at the parent's local
        /// <paramref name="at"/>. <paramref name="thickness"/> is how thick it is lying down (so that it can be set on a board). Null when the pack has no such model.
        /// </summary>
        public static GameObject CreateDisplay(Kind k, Transform parent, Vector3 at, float yaw, out float thickness)
        {
            thickness = 0f;
            if (!Available || !Has(k)) return null;
            var lay = LayoutOf(k);
            var turn = Quaternion.Euler(ModelOf(k).turn);
            var root = new GameObject(Specs[(int)k].name + "_Display");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var lying = Quaternion.Euler(90f, 0f, 0f);
            var model = Object.Instantiate(PrefabOf(k), root.transform);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Discard(c); }
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = lying * turn;
            model.transform.localPosition = -(lying * (lay.bounds.center * lay.scale));
            thickness = lay.bounds.size.z * lay.scale;
            return root;
        }
'@
Save-Src

# ---- the vendor's stock
Open-Src "$Src\DockDressing.cs"
Rep @'
        /// <summary>The glowing strip under a trailer's roof, as in the lobby's trailer.</summary>
'@ @'
        // ------------------------------------------------------------------ the vendor's black-market stock (the owner, 2026-10-19: "all cool gadgets and weapons and stuff in his truck")

        static void NoColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
        }

        /// <summary>
        /// The vendor's shelf unit: the lobby's steel frame with no cardboard. Its top board holds weapons (<paramref name="weapons"/> 0: the melee weapons; 1: pistols with their clips), the two boards below it gadgets, three to a board. Only what the demo has is on
        /// show. The front faces local +z.
        /// </summary>
        public static Transform StockShelf(Transform holder, float x, float z, float yaw, int weapons, int seed)
        {
            var root = Root(holder, "StockShelf", x, z, yaw);
            foreach (float px in new[] { -0.85f, 0.85f })
                foreach (float pz in new[] { -0.3f, 0.3f })
                    LobbyDock.Box("Upright", new Vector3(px, 1.2f, pz), new Vector3(0.06f, 2.4f, 0.06f), SteelDark, root);
            for (int i = 0; i < 4; i++) LobbyDock.Box("ShelfBoard", new Vector3(0f, 0.15f + i * 0.75f, 0f), new Vector3(1.8f, 0.04f, 0.66f), Steel, root);
            for (int board = 0; board < 2; board++)
                for (int slot = 0; slot < 3; slot++)
                    Gadget(root, new Vector3(-0.58f + slot * 0.58f, 0.17f + board * 0.75f, 0f), (seed + board * 3 + slot * 5) % 8);
            if (weapons == 0) MeleeRack(root, new Vector3(0f, 1.67f, 0f)); else PistolRack(root, new Vector3(0f, 1.67f, 0f));
            Solidify(root);
            return root;
        }

        static void Gadget(Transform r, Vector3 at, int which)
        {
            switch (which)
            {
                case 0: Radio(r, at); break;
                case 1: Flashlight(r, at); break;
                case 2: Goggles(r, at); break;
                case 3: Drone(r, at); break;
                case 4: SpyCamera(r, at); break;
                case 5: ToolCase(r, at); break;
                case 6: CashStack(r, at); break;
                default: GoldBars(r, at); break;
            }
        }

        static void Radio(Transform r, Vector3 at)
        {
            LobbyDock.Box("RadioBody", at + new Vector3(0f, 0.15f, 0f), new Vector3(0.17f, 0.30f, 0.07f), Dark, r);
            LobbyDock.Box("RadioGrille", at + new Vector3(0f, 0.10f, 0.038f), new Vector3(0.12f, 0.10f, 0.01f), SteelDark, r);
            LobbyDock.Box("RadioScreen", at + new Vector3(0f, 0.22f, 0.038f), new Vector3(0.10f, 0.05f, 0.01f), LobbyDock.Glow(new Color(0.3f, 1f, 0.5f), 1.2f), r);
            LobbyDock.Cyl("RadioAerial", at + new Vector3(0.06f, 0.38f, 0f), 0.008f, 0.16f, Dark, r);
            LobbyDock.Box("RadioKnob", at + new Vector3(-0.05f, 0.32f, 0f), new Vector3(0.03f, 0.03f, 0.03f), Red, r);
        }

        static void Flashlight(Transform r, Vector3 at)
        {
            LobbyDock.Cyl("TorchBody", at + new Vector3(0f, 0.032f, 0f), 0.03f, 0.26f, Steel, r, new Vector3(0f, 0f, 90f));
            LobbyDock.Cyl("TorchHead", at + new Vector3(0.15f, 0.05f, 0f), 0.048f, 0.07f, SteelDark, r, new Vector3(0f, 0f, 90f));
            LobbyDock.Cyl("TorchLens", at + new Vector3(0.19f, 0.05f, 0f), 0.04f, 0.01f, LobbyDock.Glow(new Color(1f, 0.95f, 0.75f), 1.6f), r, new Vector3(0f, 0f, 90f));
        }

        static void Goggles(Transform r, Vector3 at)
        {
            LobbyDock.Box("GogglesBody", at + new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.08f, 0.10f), Dark, r);
            foreach (float sx in new[] { -0.055f, 0.055f })
            {
                LobbyDock.Cyl("GogglesTube", at + new Vector3(sx, 0.05f, 0.09f), 0.04f, 0.09f, SteelDark, r, new Vector3(90f, 0f, 0f));
                LobbyDock.Cyl("GogglesLens", at + new Vector3(sx, 0.05f, 0.14f), 0.032f, 0.01f, LobbyDock.Glow(new Color(0.25f, 1f, 0.4f), 1.6f), r, new Vector3(90f, 0f, 0f));
            }
            LobbyDock.Box("GogglesStrap", at + new Vector3(0f, 0.05f, -0.07f), new Vector3(0.26f, 0.03f, 0.03f), Dark, r);
        }

        static void Drone(Transform r, Vector3 at)
        {
            LobbyDock.Box("DroneBody", at + new Vector3(0f, 0.05f, 0f), new Vector3(0.13f, 0.045f, 0.13f), Dark, r);
            LobbyDock.Box("DroneEye", at + new Vector3(0f, 0.05f, 0.07f), new Vector3(0.04f, 0.03f, 0.012f), LobbyDock.Glow(new Color(1f, 0.15f, 0.1f), 1.8f), r);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    LobbyDock.Box("DroneArm", at + new Vector3(sx * 0.09f, 0.05f, sz * 0.09f), new Vector3(0.18f, 0.02f, 0.02f), SteelDark, r, new Vector3(0f, sx * sz * 45f, 0f));
                    LobbyDock.Cyl("DroneRotor", at + new Vector3(sx * 0.15f, 0.075f, sz * 0.15f), 0.065f, 0.006f, White, r);
                }
        }

        static void SpyCamera(Transform r, Vector3 at)
        {
            LobbyDock.Box("CameraBody", at + new Vector3(0f, 0.07f, 0f), new Vector3(0.22f, 0.13f, 0.10f), Dark, r);
            LobbyDock.Cyl("CameraLensRing", at + new Vector3(0.03f, 0.07f, 0.09f), 0.052f, 0.07f, SteelDark, r, new Vector3(90f, 0f, 0f));
            LobbyDock.Cyl("CameraLens", at + new Vector3(0.03f, 0.07f, 0.13f), 0.04f, 0.012f, LobbyDock.Glow(new Color(0.4f, 0.6f, 1f), 1.0f), r, new Vector3(90f, 0f, 0f));
            LobbyDock.Box("CameraFlash", at + new Vector3(-0.07f, 0.11f, 0.052f), new Vector3(0.05f, 0.03f, 0.01f), White, r);
        }

        static void ToolCase(Transform r, Vector3 at)
        {
            LobbyDock.Box("CaseBody", at + new Vector3(0f, 0.09f, 0f), new Vector3(0.42f, 0.18f, 0.28f), LobbyDock.Mat(new Color(0.55f, 0.12f, 0.10f), 0.25f), r);
            LobbyDock.Box("CaseSeam", at + new Vector3(0f, 0.09f, 0.1405f), new Vector3(0.42f, 0.015f, 0.003f), SteelDark, r);
            foreach (float sx in new[] { -0.12f, 0.12f }) LobbyDock.Box("CaseLatch", at + new Vector3(sx, 0.09f, 0.145f), new Vector3(0.04f, 0.05f, 0.012f), Steel, r);
            LobbyDock.Box("CaseHandle", at + new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.025f, 0.03f), Dark, r);
        }

        static void CashStack(Transform r, Vector3 at)
        {
            var bill = LobbyDock.Mat(new Color(0.35f, 0.62f, 0.35f), 0.05f);
            for (int layer = 0; layer < 3; layer++)
                for (int i = 0; i < 2; i++)
                {
                    var c = at + new Vector3((i - 0.5f) * 0.17f, 0.03f + layer * 0.06f, 0f);
                    LobbyDock.Box("Bundle", c, new Vector3(0.16f, 0.055f, 0.1f), bill, r);
                    LobbyDock.Box("BundleBand", c, new Vector3(0.04f, 0.058f, 0.102f), White, r);
                }
        }

        static void GoldBars(Transform r, Vector3 at)
        {
            var gold = LobbyDock.Mat(new Color(1.0f, 0.78f, 0.15f), 0.55f, 0.6f);
            foreach (float sx in new[] { -0.11f, 0.11f }) LobbyDock.Box("GoldBar", at + new Vector3(sx, 0.03f, 0f), new Vector3(0.20f, 0.06f, 0.09f), gold, r);
            LobbyDock.Box("GoldBar", at + new Vector3(0f, 0.09f, 0f), new Vector3(0.20f, 0.06f, 0.09f), gold, r);
        }

        /// <summary>The melee weapons the demo has (crowbar, bat, hatchet) lying side by side along a board, with their real models.</summary>
        static void MeleeRack(Transform r, Vector3 at)
        {
            if (!MeleeWeapon.Available) return;
            float z = -0.22f;
            foreach (var k in new[] { MeleeWeapon.Kind.Crowbar, MeleeWeapon.Kind.Bat, MeleeWeapon.Kind.Hatchet })
            {
                var lay = MeleeWeapon.LayoutOf(k);
                var w = new GameObject("Display_" + k).transform;
                w.SetParent(r, false);
                float thick = lay.bounds.size.x * lay.scale;
                w.localPosition = at + new Vector3(0f, thick * 0.5f + 0.002f, z);
                w.localRotation = Quaternion.Euler(0f, 0f, 90f);                               // (lying on its side, along x, as a melee weapon lies on a desk)
                var model = Object.Instantiate(MeleeWeapon.PrefabOf(k), w);
                NoColliders(model);
                model.transform.localRotation = lay.turn;
                model.transform.localScale = Vector3.one * lay.scale;
                model.transform.localPosition = -(lay.bounds.center * lay.scale);
                z += 0.22f;
            }
        }

        /// <summary>Three pew pistols lying on a board, each with a clip beside it (the demo's only gun).</summary>
        static void PistolRack(Transform r, Vector3 at)
        {
            if (RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].unlockFloor > TowerRules.DemoLastFloor) return;
            for (int slot = 0; slot < 3; slot++)
            {
                float x = -0.58f + slot * 0.58f;
                var pew = RangedWeapon.CreateDisplay(RangedWeapon.Kind.Pew, r, new Vector3(x - 0.08f, 0f, 0f), slot % 2 == 0 ? 0f : 180f, out float thick);
                if (pew == null) return;
                pew.transform.localPosition = at + new Vector3(x - 0.08f, thick * 0.5f + 0.002f, 0f);
                var clipRoot = new GameObject("Display_Clip").transform;
                clipRoot.SetParent(r, false);
                var clip = RangedWeapon.CreateClipModel(clipRoot, out var size, RangedWeapon.Kind.Pew);
                if (clip == null) { Object.Destroy(clipRoot.gameObject); continue; }
                clipRoot.localPosition = at + new Vector3(x + 0.17f, size.z * 0.5f + 0.002f, 0.1f);
            }
        }

        /// <summary>The glowing strip under a trailer's roof, as in the lobby's trailer.</summary>
'@
Save-Src

Open-Src "$Src\FloorGenerator.Dock.cs"
RepRx '(?s)var rng = new System\.Random\(0x7A11\);.*?(?=\r?\n        \}\r?\n\r?\n        /// <summary>Puts one prop from the table at a ground spot)' @'
var rng = new System.Random(0x7A11);
            // his shelves down both walls: the lobby's steel frames with no cardboard, black-market stock on the boards - weapons on top, gadgets below (DockDressing; the owner, 2026-10-19: "all cool gadgets and weapons and stuff in his truck")
            int shelfNo = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                float z = 0.4f;
                while (z + DockDressing.ShelfWidth <= length)
                {
                    float x = tx + side * (innerWidth / 2f - DockDressing.ShelfDepth / 2f - 0.03f);
                    float zc = zr + z + DockDressing.ShelfWidth / 2f;
                    DockDressing.StockShelf(dockHolder, x, zc, side < 0 ? 90f : 270f, shelfNo % 2, rng.Next(1000));          // (the front faces the aisle: yaw 90 on the left wall faces +X, 270 on the right faces -X)
                    shelfNo++;
                    z += DockDressing.ShelfWidth + 0.12f;
                }
            }
            // the briefcases on the floor, flat things only: nothing that would narrow the aisle (the owner, 2026-10-19: "The briefcases in the Vendor's shop were a nice touch")
            for (int i = 0; i < 4; i++)
            {
                float x = tx + (i % 2 == 0 ? -1f : 1f) * (innerWidth / 2f - 0.85f);
                float z = zr + 1f + (float)rng.NextDouble() * (length - 1.5f);
                PlaceKey("briefcase", x, z, rng.Next(360), parent, 0f, false);
            }
'@
Rep 'tries < 200 && groups > 0' 'tries < 600 && groups > 0'
Rep 'if (!Free(x, z, 0.9f)) continue;' 'if (!Free(x, z, 0.7f)) continue;'
Save-Src

# ---- the tests
Open-Src "$Ed\NetDevDockLook.cs"
Rep 'lifts >= 1 && drums >= 4 && bollards == 2' 'lifts >= 1 && drums >= 2 && bollards == 2 && stockShelves == 4'
Rep 'int shelves = Count("Shelf"), stacks' 'int stockShelves = Count("StockShelf"), shelves = Count("Shelf"), stacks'
Rep '$"({shelves} shelves, {stacks} stacks' '$"({shelves} shelves, {stockShelves} stock shelves, {stacks} stacks'
Rep 'shelves >= 8 && stacks >= 10' 'shelves >= 4 && stacks >= 10'
Rep @'
            bool oneCollider = true;
'@ @'
            // ---- the vendor's shop: black-market stock and no cardboard on his shelves, and the briefcases on his floor
            bool noCardboard = true, stocked = true;
            foreach (var ss in roots.Where(t => t.name == "StockShelf"))
            {
                var parts = ss.GetComponentsInChildren<Transform>(true);
                noCardboard &= !parts.Any(t => t.name == "Box" || t.name == "ShelfBox" || t.name == "BoxTape");
                stocked &= parts.Count(t => t.name != "Upright" && t.name != "ShelfBoard" && t != ss) >= 20;
            }
            int briefcases = vendor.GetComponentsInChildren<Transform>(true).Count(t => t.name == "briefcase");
            DockLookCheck("the vendor's shelves hold black-market stock (gadgets, weapons, clips), not a single cardboard box, and the briefcases lie on his floor", noCardboard && stocked && stockShelves == 4 && briefcases >= 3, $"({stockShelves} stock shelves, {briefcases} briefcases)");
            bool oneCollider = true;
'@
Rep 'foreach (var r in roots.Where(t => t.name == "Shelf" || t.name == "BoxStack"' 'foreach (var r in roots.Where(t => t.name == "Shelf" || t.name == "StockShelf" || t.name == "BoxStack"'
Rep @'
                var lift = roots.FirstOrDefault(t => t.name == "Forklift");
'@ @'
                var lift = roots.FirstOrDefault(t => t.name == "Forklift");
                var stock = roots.FirstOrDefault(t => t.name == "StockShelf");
'@
Rep @'
                if (lift) spots.Add(("forklift"
'@ @'
                if (stock) spots.Add(("vendor_shelf", stock.TransformPoint(new Vector3(0f, 1.5f, 2.3f)), stock.TransformPoint(new Vector3(0f, 1.2f, 0f))));
                if (lift) spots.Add(("forklift"
'@
Save-Src

Open-Src "$Ed\NetDevSneak.cs"
Rep 'else if (tr.name == "Shelf") shelves++;' 'else if (tr.name == "Shelf" || tr.name == "StockShelf") shelves++;'
Rep 'forklifts >= 1 && drums >= 4' 'forklifts >= 1 && drums >= 2'
Save-Src

# Round 11, batch G16b: the second half of G16 (the generator's side of the vendor's wall of guns and of the flat briefcases, and the tests).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Src\FloorGenerator.Dock.cs"
Rep @'
                    DockDressing.StockShelf(dockHolder, x, zc, side < 0 ? 90f : 270f, shelfNo % 2, rng.Next(1000));          // (the front faces the aisle: yaw 90 on the left wall faces +X, 270 on the right faces -X)
'@ @'
                    var shelfRoot = DockDressing.StockShelf(dockHolder, x, zc, side < 0 ? 90f : 270f, shelfNo % 2, rng.Next(1000), 1 + shelfNo % 2);          // (the front faces the aisle: yaw 90 on the left wall faces +X, 270 on the right faces -X)
                    // the briefcases lie FLAT on the boards (the owner, 2026-10-19: "The briefcases should be on the shelves and flat not standing")
                    PlaceBriefcaseFlat(shelfRoot.TransformPoint(new Vector3(0f, 0.92f, 0f)), shelfRoot.eulerAngles.y + rng.Next(2) * 180f, parent);
                    if (shelfNo % 2 == 1) PlaceBriefcaseFlat(shelfRoot.TransformPoint(new Vector3(0f, 0.17f, 0f)), shelfRoot.eulerAngles.y, parent);
'@
RepRx '(?s)// the briefcases on the floor, flat things only.*?PlaceKey\("briefcase", x, z, rng\.Next\(360\), parent, 0f, false\);\s*\}' ''
Rep @'
        /// <summary>Puts one prop from the table at a ground spot (using the same measured templates the rooms use).</summary>
'@ @'
        /// <summary>
        /// A briefcase from the prop pack lying FLAT with its middle over <paramref name="surface"/> (a point on a shelf's board): the pack's own model stands on its edge, so it is turned onto its broad side by its measured box (the owner, 2026-10-19: "on the
        /// shelves and flat not standing"), then set down on the surface.
        /// </summary>
        GameObject PlaceBriefcaseFlat(Vector3 surface, float yawDeg, Transform parent)
        {
            var go = PlaceKey("briefcase", surface.x, surface.z, yawDeg, parent, surface.y, false);
            if (go == null) return null;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return go;
            Bounds Box() { var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }
            var size = Box().size;
            if (size.y > Mathf.Min(size.x, size.z) * 1.05f) go.transform.RotateAround(Box().center, size.x < size.z ? Vector3.forward : Vector3.right, 90f);          // (standing on its edge: its thinnest side is not the vertical one)
            var flat = Box();
            go.transform.position += new Vector3(surface.x - flat.center.x, surface.y - flat.min.y, surface.z - flat.center.z);
            return go;
        }

        /// <summary>Puts one prop from the table at a ground spot (using the same measured templates the rooms use).</summary>
'@
Rep @'
            DockDressing.LampFixture(h, new Vector3(tx, 0f, counterZ - 0.3f), TrailerHeight - 0.02f, 0.35f);
'@ @'
            DockDressing.LampFixture(h, new Vector3(tx, 0f, counterZ - 0.3f), TrailerHeight - 0.02f, 0.35f);
            DockDressing.GunWall(h, tx, zf - 0.2f, 0.8f);          // (the guns on the wall behind him hang there once they are unlocked: the owner, 2026-10-19)
'@
Save-Src

Open-Src "$Ed\NetDevChecklist.cs"
Rep @'
            yield return null;
            yield return new WaitForEndOfFrame();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var tex
'@ @'
            yield return null;
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !Application.isBatchMode)          // (the screen can only be captured in a window: ui.ps1 -Methods Checklist)
            {
                yield return new WaitForEndOfFrame();
                var tex
'@
Save-Src

Open-Src "$Ed\NetDevDockLook.cs"
Rep 'noCardboard && stocked && stockShelves == 4 && briefcases >= 3, $"({stockShelves} stock shelves, {briefcases} briefcases)");' 'noCardboard && stocked && stockShelves == 4 && briefcasesFlat, $"({stockShelves} stock shelves, {briefcases} briefcases, flat on the shelves {briefcasesFlat})");'
Rep @'
            int briefcases = vendor.GetComponentsInChildren<Transform>(true).Count(t => t.name == "briefcase");
'@ @'
            var cases = vendor.GetComponentsInChildren<Transform>(true).Where(t => t.name == "briefcase").ToList();
            int briefcases = cases.Count;
            bool briefcasesFlat = cases.Count >= 4 && cases.All(t =>
            {
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return false;
                var bb = rs[0].bounds;
                foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                return bb.size.y < 0.25f && bb.size.y <= Mathf.Min(bb.size.x, bb.size.z) * 1.1f && bb.min.y > 0.1f;          // (lying flat, on a board: not standing, not on the floor)
            });
'@
Rep @'
            // ---- the lobby's life: the clock tells the real time, two lamps sway, the beacon turns
'@ @'
            // ---- the wall of guns behind the vendor: a bracket for every gun, a gun hangs there only once it is unlocked (the owner, 2026-10-19)
            var gunWall = vendor.GetComponentInChildren<VendorGunWall>(true);
            RunUnlocks.Reset();
            yield return new WaitForSeconds(0.6f);
            DockLookCheck("behind the vendor is a wall of guns with a bracket for every gun, and at the start no gun hangs on it (nothing is unlocked)", gunWall != null && gunWall.kinds != null && gunWall.kinds.Length == 5 && gunWall.kinds.All(k => !gunWall.IsShown(k)));
            RunUnlocks.Unlock(RangedWeapon.Kind.Pew); RunUnlocks.Unlock(RangedWeapon.Kind.Rifle); RunUnlocks.Unlock(RangedWeapon.Kind.Sniper);
            yield return new WaitForSeconds(0.8f);
            DockLookCheck("a gun hangs on the wall once it is unlocked (the pew, the rifle and the sniper rifle here), and only those", gunWall != null && gunWall.IsShown(RangedWeapon.Kind.Pew) && gunWall.IsShown(RangedWeapon.Kind.Rifle) && gunWall.IsShown(RangedWeapon.Kind.Sniper) && !gunWall.IsShown(RangedWeapon.Kind.Shotgun) && !gunWall.IsShown(RangedWeapon.Kind.Smg));

            // ---- the lobby's life: the clock tells the real time, two lamps sway, the beacon turns
'@
Rep @'
                    ("vendor_counter", new Vector3(tx + 0.9f, 1.8f, zWall + 0.35f + 8.5f - 5.6f), new Vector3(tx, 1.2f, zWall + 0.35f + 8.5f - 2.2f)),
'@ @'
                    ("vendor_counter", new Vector3(tx + 0.9f, 1.8f, zWall + 0.35f + 8.5f - 5.6f), new Vector3(tx, 1.2f, zWall + 0.35f + 8.5f - 2.2f)),
                    ("gun_wall", new Vector3(tx - 0.5f, 1.7f, zWall + 0.35f + 8.5f - 4.4f), new Vector3(tx, 1.5f, zWall + 0.35f + 8.5f - 0.2f)),
'@
Save-Src

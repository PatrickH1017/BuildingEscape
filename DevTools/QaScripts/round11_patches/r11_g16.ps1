# Round 11, batch G16 (the owner, 2026-10-19): "On the wall behind the Vendor should be guns" / "The guns should only appear as you unlock them though since he will then sell those specific guns" / "The briefcases should be on the shelves
# and flat not standing". A peg board with a bracket for every gun hangs behind the vendor; a gun hangs on it once it is unlocked (the pew once the dealer sells it); the pistols leave the shelves (a board of gadgets instead); the briefcases lie flat on
# the shelves. The journal picture is taken only where the screen can be captured.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Src\Guns\RangedWeapon.cs"
Rep 'public static GameObject CreateDisplay(Kind k, Transform parent, Vector3 at, float yaw, out float thickness)' 'public static GameObject CreateDisplay(Kind k, Transform parent, Vector3 at, float yaw, out float thickness, bool upright = false)'
Rep @'
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var lying = Quaternion.Euler(90f, 0f, 0f);
'@ @'
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var lying = upright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);          // (upright: hung on a wall, its barrel along x, its flat side to the viewer)
'@
Save-Src

Open-Src "$Src\DockDressing.cs"
Rep 'public static Transform StockShelf(Transform holder, float x, float z, float yaw, int weapons, int seed)' 'public static Transform StockShelf(Transform holder, float x, float z, float yaw, int weapons, int seed, int briefcases)'
Rep @'
                for (int slot = 0; slot < 3; slot++)
                    Gadget(root, new Vector3(-0.58f + slot * 0.58f, 0.17f + board * 0.75f, 0f), (seed + board * 3 + slot * 5) % 8);
            if (weapons == 0) MeleeRack(root, new Vector3(0f, 1.67f, 0f)); else PistolRack(root, new Vector3(0f, 1.67f, 0f));
'@ @'
                for (int slot = 0; slot < 3; slot++)
                {
                    if (slot == 1 && (board == 1 && briefcases >= 1 || board == 0 && briefcases >= 2)) continue;          // (a briefcase lies flat here: the owner, 2026-10-19)
                    Gadget(root, new Vector3(-0.58f + slot * 0.58f, 0.17f + board * 0.75f, 0f), (seed + board * 3 + slot * 5) % 8);
                }
            if (weapons == 0) MeleeRack(root, new Vector3(0f, 1.67f, 0f));
            else for (int slot = 0; slot < 3; slot++) Gadget(root, new Vector3(-0.58f + slot * 0.58f, 1.67f, 0f), (seed + 4 + slot * 3) % 8);          // (the guns hang on the wall behind the vendor, and only once they are unlocked)
'@
RepRx '(?s)        /// <summary>Three pew pistols lying on a board.*?\n        \}\r?\n\r?\n(?=        /// <summary>The glowing strip)' @'
        /// <summary>
        /// The wall of guns behind the vendor (the owner, 2026-10-19: "On the wall behind the Vendor should be guns ... The guns should only appear as you unlock them though since he will then sell those specific guns"): a steel peg board with a pair of brackets
        /// for every gun the game has. A gun hangs on its brackets once it is unlocked (<see cref="RunUnlocks"/>: found and brought back to the docks) or, the pew, once the dealer sells it (<see cref="Vendor.PewForSale"/>): <see cref="VendorGunWall"/> hangs them
        /// and takes them down. <paramref name="zWall"/>: the inner face of the wall (the board's face looks along -z); <paramref name="y0"/>: the foot of the board.
        /// </summary>
        public static VendorGunWall GunWall(Transform holder, float x, float zWall, float y0)
        {
            var root = new GameObject("GunWall").transform;
            root.SetParent(holder, false);
            root.localPosition = new Vector3(x, 0f, zWall);
            root.localRotation = Quaternion.identity;
            LobbyDock.Box("GunBoard", new Vector3(0f, y0 + 0.65f, -0.02f), new Vector3(3.2f, 1.3f, 0.03f), SteelDark, root);
            LobbyDock.Box("GunRailTop", new Vector3(0f, y0 + 1.32f, -0.04f), new Vector3(3.26f, 0.05f, 0.05f), Hazard, root);
            LobbyDock.Box("GunRailBottom", new Vector3(0f, y0 - 0.02f, -0.04f), new Vector3(3.26f, 0.05f, 0.05f), Hazard, root);
            var wall = root.gameObject.AddComponent<VendorGunWall>();
            // each gun's place: its kind, where its middle hangs (x, and the height above the board's foot); the big guns at the sides, out of the way of the vendor who stands in front of the middle
            var slots = new[]
            {
                (RangedWeapon.Kind.Sniper, -1.0f, 1.08f), (RangedWeapon.Kind.Rifle, 1.0f, 1.08f),
                (RangedWeapon.Kind.Shotgun, -1.0f, 0.68f), (RangedWeapon.Kind.Smg, 1.05f, 0.68f),
                (RangedWeapon.Kind.Pew, -1.0f, 0.26f),
            };
            wall.kinds = new RangedWeapon.Kind[slots.Length];
            wall.shown = new GameObject[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var (kind, sx, sy) = slots[i];
                float len = RangedWeapon.Specs[(int)kind].length;
                foreach (float off in new[] { -len * 0.28f, len * 0.28f })
                    LobbyDock.Cyl("Bracket", new Vector3(sx + off, y0 + sy - 0.05f, -0.06f), 0.012f, 0.07f, Steel, root, new Vector3(90f, 0f, 0f));
                wall.kinds[i] = kind;
                var gun = RangedWeapon.CreateDisplay(kind, root, new Vector3(sx, y0 + sy, -0.07f), 0f, out _, true);
                wall.shown[i] = gun;
                if (gun) gun.SetActive(false);
            }
            wall.Refresh();
            return wall;
        }

'@
Rep @'
    public static class DockDressing
'@ @'
    /// <summary>
    /// The guns on the wall behind the vendor (<see cref="DockDressing.GunWall"/>): each hangs there once it is unlocked in this run, the pew once the dealer sells it too, and not before (the owner, 2026-10-19). Looked at twice a second, the same on every
    /// machine (the unlocks are mirrored).
    /// </summary>
    public sealed class VendorGunWall : MonoBehaviour
    {
        public RangedWeapon.Kind[] kinds;
        public GameObject[] shown;
        float next;

        /// <summary>Does this gun hang on the wall now?</summary>
        public static bool Hangs(RangedWeapon.Kind kind)
        {
            if (RunUnlocks.Has(kind)) return true;
            var tm = TowerManager.Instance;
            return kind == RangedWeapon.Kind.Pew && tm != null && Vendor.PewForSale(tm.ResumeLevel);
        }

        /// <summary>Is this gun on show (hanging on its brackets) at the moment?</summary>
        public bool IsShown(RangedWeapon.Kind kind)
        {
            if (kinds == null) return false;
            for (int i = 0; i < kinds.Length; i++) if (kinds[i] == kind) return shown[i] && shown[i].activeSelf;
            return false;
        }

        public void Refresh()
        {
            if (kinds == null) return;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (!shown[i]) continue;
                bool on = Hangs(kinds[i]);
                if (shown[i].activeSelf != on) shown[i].SetActive(on);
            }
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.4f;
            Refresh();
        }
    }

    public static class DockDressing
'@
Save-Src


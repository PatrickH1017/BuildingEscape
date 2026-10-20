# Round 11, batch G06: vertical safes (the owner, 2026-10-19: "The safes for snipers and shotguns should be vertical with the gun vertical inside the safe"): a safe that holds a shotgun or a sniper rifle is 1.7 m tall (the same footprint) and the gun stands upright in it.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Src\Outpost\FloorSafe.cs"
Rep @'
        public const float Width = 0.9f, Height = 0.85f, Depth = 0.65f;
'@ @'
        public const float Width = 0.9f, Height = 0.85f, Depth = 0.65f;
        /// <summary>The height of a vertical safe (the owner, 2026-10-19): one that holds a shotgun or a sniper rifle, which stands in it on end. The footprint is the same.</summary>
        public const float TallHeight = 1.7f;
'@
Rep @'
        public int Floor { get; private set; }
'@ @'
        public int Floor { get; private set; }
        /// <summary>A vertical safe: it holds a shotgun or a sniper rifle, standing upright (<see cref="TallHeight"/> high).</summary>
        public bool Tall { get; private set; }
'@
Rep @'
public static FloorSafe Build(Transform parent, float x, float z, float yaw, int floor, int index, int code, Mats m)
'@ @'
public static FloorSafe Build(Transform parent, float x, float z, float yaw, int floor, int index, int code, Mats m, bool tall = false)
'@
Rep @'
            safe.Code = code;
'@ @'
            safe.Code = code;
            safe.Tall = tall;
'@
Rep @'
            float w = Width, h = Height, d = Depth, t = WallThickness;
'@ @'
            float w = Width, h = tall ? TallHeight : Height, d = Depth, t = WallThickness;
'@
Rep @'
            // the combination dial
'@ @'
            float cy = tall ? 0.30f : 0f;                      // (on a tall door the dial, the handle and the keypad sit higher, at the height of a hand)
            // the combination dial
'@
Rep @'
new Vector3(leafW * 0.30f, 0.10f, DoorThickness * 0.5f + 0.02f)
'@ @'
new Vector3(leafW * 0.30f, 0.10f + cy, DoorThickness * 0.5f + 0.02f)
'@
Rep @'
new Vector3(leafW * 0.50f, -0.17f, DoorThickness * 0.5f + 0.028f)
'@ @'
new Vector3(leafW * 0.50f, -0.17f + cy, DoorThickness * 0.5f + 0.028f)
'@
Rep @'
new Vector3(leafW * 0.50f - 0.12f, -0.17f, DoorThickness * 0.5f + 0.012f)
'@ @'
new Vector3(leafW * 0.50f - 0.12f, -0.17f + cy, DoorThickness * 0.5f + 0.012f)
'@
Rep @'
new Vector3(leafW * 0.50f + 0.12f, -0.17f, DoorThickness * 0.5f + 0.012f)
'@ @'
new Vector3(leafW * 0.50f + 0.12f, -0.17f + cy, DoorThickness * 0.5f + 0.012f)
'@
Rep @'
new Vector3(kx, 0.10f, DoorThickness * 0.5f + 0.014f)
'@ @'
new Vector3(kx, 0.10f + cy, DoorThickness * 0.5f + 0.014f)
'@
Rep @'
new Vector3(kx, 0.19f, DoorThickness * 0.5f + 0.026f)
'@ @'
new Vector3(kx, 0.19f + cy, DoorThickness * 0.5f + 0.026f)
'@
Rep @'
new Vector3(kx + (col - 1) * 0.045f, 0.145f - row * 0.042f, DoorThickness * 0.5f + 0.027f)
'@ @'
new Vector3(kx + (col - 1) * 0.045f, 0.145f - row * 0.042f + cy, DoorThickness * 0.5f + 0.027f)
'@
Rep @'
                bool slanted = length > 0.46f;
'@ @'
                bool slanted = length > 0.46f;
                if (Tall && (kind == RangedWeapon.Kind.Shotgun || kind == RangedWeapon.Kind.Sniper))
                {
                    // the owner, 2026-10-19: "The safes for snipers and shotguns should be vertical with the gun vertical inside the safe": the gun stands on end in the middle of the tall hollow, its barrel up and its flat side to the door
                    var upright = RangedWeapon.Create(kind, transform.TransformPoint(new Vector3(0f, WallThickness + length * 0.5f + 0.012f, -0.04f)), parent, 0f, 0);
                    upright.transform.rotation = transform.rotation * Quaternion.LookRotation(Vector3.forward, Vector3.left);          // (the gun's +X is its barrel: up; its +Z, a flat side, to the door)
                    if (upright.TryGetComponent(out Rigidbody uprightBody)) uprightBody.isKinematic = true;
                    upright.SetActive(false);
                    guns.Add(upright);
                    continue;
                }
'@
Save-Src

Open-Src "$Src\Outpost\SafeRules.cs"
Rep @'
        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@ @'
        /// <summary>Does this weapon safe hold a gun too long to lie in it - a shotgun or a sniper rifle (the owner, 2026-10-19: those safes are vertical, the gun standing in them)?</summary>
        public static bool HoldsTallGun(int runSeed, int floor, int index)
        {
            var items = WeaponContents(runSeed, floor, index);
            if (items.Length == 0 || !IsGunItem(items[0])) return false;
            var kind = GunOfItem(items[0]);
            return kind == RangedWeapon.Kind.Shotgun || kind == RangedWeapon.Kind.Sniper;
        }

        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@
Save-Src

Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
            var safe = FloorSafe.Build(parent, p.x, p.z, p.yaw, floor, index, SafeRules.Code(intelNow, floor, index), mats);
'@ @'
            bool tallSafe = SafeRules.IsWeaponFloor(floor) && SafeRules.HoldsTallGun(intelNow, floor, index);          // (a shotgun or a sniper rifle stands upright in a vertical safe: the owner, 2026-10-19)
            var safe = FloorSafe.Build(parent, p.x, p.z, p.yaw, floor, index, SafeRules.Code(intelNow, floor, index), mats, tallSafe);
'@
Save-Src

# ---- the tests: the Outpost scenario knows the vertical safes, the pictures show them, a pure check of which safes are tall
Open-Src "$Ed\NetDevOutpost.cs"
Rep @'
wsLocal.y > 0.04f && wsLocal.y < 0.5f && wsLocal.z > -0.30f
'@ @'
wsLocal.y > 0.04f && wsLocal.y < (wsS.Tall ? 1.6f : 0.5f) && wsLocal.z > -0.30f
'@
Rep @'
                                wsBarrels &= Vector3.Dot(wsGun.transform.right, wsS.transform.forward) > (wsSlanted ? 0.55f : 0.95f);
'@ @'
                                wsBarrels &= wsS.Tall && (wsGun.kind == RangedWeapon.Kind.Shotgun || wsGun.kind == RangedWeapon.Kind.Sniper) ? Vector3.Dot(wsGun.transform.right, Vector3.up) > 0.95f : Vector3.Dot(wsGun.transform.right, wsS.transform.forward) > (wsSlanted ? 0.55f : 0.95f);          // (a shotgun or a sniper rifle stands upright in a vertical safe)
'@
Rep @'
$"({wsTotal} pistols, {wsInSafe} inside)");
'@ @'
$"({wsTotal} pistols, {wsInSafe} inside)");
                    { bool wsTallOk = true; foreach (var wsT in wsSafes) wsTallOk &= wsT.Tall == SafeRules.HoldsTallGun(seed, wsFloor, wsT.Index); OutpostCheck($"{wsTag} a safe is vertical (1.7 m) exactly when it holds a shotgun or a sniper rifle, the gun standing upright in it (the owner, 2026-10-19)", wsTallOk); }
'@
Save-Src

Open-Src "$Ed\NetDevGunSheet.cs"
Rep @'
                var safe = FloorSafe.Build(holder, 0f, at.z + 2.6f, 180f, 7, 0, 1234, mats);
'@ @'
                var safe = FloorSafe.Build(holder, 0f, at.z + 2.6f, 180f, 7, 0, 1234, mats, kind == RangedWeapon.Kind.Shotgun || kind == RangedWeapon.Kind.Sniper);          // (those two stand upright in a vertical safe)
'@
Rep @'
                cam.fieldOfView = 38f;
                cam.transform.position = st.TransformPoint(new Vector3(0.85f, 1.25f, 1.45f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.30f, 0.1f)));
'@ @'
                cam.fieldOfView = safe.Tall ? 46f : 38f;
                cam.transform.position = safe.Tall ? st.TransformPoint(new Vector3(0.95f, 1.45f, 2.5f)) : st.TransformPoint(new Vector3(0.85f, 1.25f, 1.45f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, safe.Tall ? 0.80f : 0.30f, 0.1f)));
'@
Rep @'
                cam.fieldOfView = 34f;
                cam.transform.position = st.TransformPoint(new Vector3(0.10f, 1.55f, 0.95f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.12f, 0.0f)));
'@ @'
                cam.fieldOfView = safe.Tall ? 40f : 34f;
                cam.transform.position = safe.Tall ? st.TransformPoint(new Vector3(0.05f, 2.1f, 1.2f)) : st.TransformPoint(new Vector3(0.10f, 1.55f, 0.95f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, safe.Tall ? 0.45f : 0.12f, 0.0f)));
'@
Save-Src

Open-Src "$Ed\NetDevArmourTest.cs"
Rep @'
&& Vendor.Catalog[il].price == ArmourRules.Price(light) && Vendor.Catalog[im].price == ArmourRules.Price(medium) && Vendor.Catalog[ih].price == ArmourRules.Price(heavy) && offBefore && offAfter);
'@ @'
&& Vendor.Catalog[il].price == ArmourRules.Price(light) && Vendor.Catalog[im].price == ArmourRules.Price(medium) && Vendor.Catalog[ih].price == ArmourRules.Price(heavy) && offBefore && offAfter);

            // ---- vertical safes (the owner, 2026-10-19): a safe that holds a shotgun or a sniper rifle
            bool tallOk = true; int tallSeen = 0, flatSeen = 0;
            for (int sd = 0; sd < 150; sd++)
                for (int f = 1; f <= 12; f++)
                {
                    int rs = sd * 61 + 7, n = SafeRules.SafeCount(rs, f);
                    for (int i = 0; i < n; i++)
                    {
                        var items = SafeRules.WeaponContents(rs, f, i);
                        bool gunTall = items.Length > 0 && SafeRules.IsGunItem(items[0]) && (SafeRules.GunOfItem(items[0]) == RangedWeapon.Kind.Shotgun || SafeRules.GunOfItem(items[0]) == RangedWeapon.Kind.Sniper);
                        tallOk &= SafeRules.HoldsTallGun(rs, f, i) == gunTall;
                        if (gunTall) tallSeen++; else flatSeen++;
                    }
                }
            Check("safes: a safe is the vertical kind exactly when it holds a shotgun or a sniper rifle (never with armour, a pistol, an SMG or a rifle; never on floors 1-2, nor before floor 6)", tallOk && tallSeen > 0 && flatSeen > tallSeen && !SafeRules.HoldsTallGun(1, 2, 0) && !SafeRules.HoldsTallGun(5, 4, 0) && !SafeRules.HoldsTallGun(5, 5, 0), $"({tallSeen} vertical, {flatSeen} not)");
'@
Save-Src

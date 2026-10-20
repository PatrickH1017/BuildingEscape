using UnityEngine;
using BuildingEscape.Net;

namespace BuildingEscape
{
    /// <summary>
    /// The clean look of the lobby's loading dock (<see cref="LobbyDock"/>) for the basement's own loading dock (the owner, 2026-10-19: "the assets from the lobby scene look way cleaner ... make the current loading dock look better with all the
    /// new assets, the layout is great. Use the lobby as a reference, for example the clock is great"): cardboard on pallets and in stacks, steel shelving, forklifts, drums, bollards, door frames, a wall clock, pipes, a notice board, an extinguisher,
    /// hanging lamps and a turning beacon - the same flat matt primitives in the same colours. Everything is placed in WORLD coordinates (the holder sits at the world origin); a prop gets ONE box collider on its root (the primitives have none).
    /// </summary>
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
    {
        /// <summary>The steel shelf unit: 1.8 m along its length, 0.66 m deep, 2.4 m high.</summary>
        public const float ShelfWidth = 1.8f, ShelfDepth = 0.66f;

        static Material Card => LobbyDock.Mat(new Color(0.62f, 0.46f, 0.30f), 0.02f);
        static Material Card2 => LobbyDock.Mat(new Color(0.55f, 0.40f, 0.26f), 0.02f);
        static Material Tape => LobbyDock.Mat(new Color(0.78f, 0.66f, 0.45f), 0.1f);
        static Material Wood => LobbyDock.Mat(new Color(0.48f, 0.36f, 0.23f), 0.03f);
        static Material Steel => LobbyDock.Mat(new Color(0.50f, 0.53f, 0.57f), 0.35f, 0.5f);
        static Material SteelDark => LobbyDock.Mat(new Color(0.24f, 0.27f, 0.31f), 0.3f, 0.4f);
        static Material Dark => LobbyDock.Mat(new Color(0.11f, 0.11f, 0.12f), 0.1f);
        static Material Red => LobbyDock.Mat(new Color(0.68f, 0.12f, 0.10f), 0.25f);
        static Material White => LobbyDock.Mat(new Color(0.88f, 0.89f, 0.91f), 0.3f);
        static Material Hazard => LobbyDock.Mat(new Color(0.93f, 0.74f, 0.10f), 0.15f);

        /// <summary>A holder at the world origin under <paramref name="parent"/>: whatever is put in it with a position is put there in world coordinates.</summary>
        public static Transform Holder(Transform parent)
        {
            var g = new GameObject("DockDressing");
            g.transform.SetParent(parent, false);
            g.transform.position = Vector3.zero;
            g.transform.rotation = Quaternion.identity;
            return g.transform;
        }

        static Transform Root(Transform holder, string name, float x, float z, float yaw)
        {
            var r = new GameObject(name).transform;
            r.SetParent(holder, false);
            r.localPosition = new Vector3(x, 0f, z);
            r.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return r;
        }

        /// <summary>Gives a prop ONE box collider over everything built under its root, so that a player bumps into the prop as a whole.</summary>
        static void Solidify(Transform root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (!mesh) continue;
                var mb = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + new Vector3((i & 1) == 0 ? -mb.extents.x : mb.extents.x, (i & 2) == 0 ? -mb.extents.y : mb.extents.y, (i & 4) == 0 ? -mb.extents.z : mb.extents.z);
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) return;
            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }

        // ------------------------------------------------------------------ things to stand about

        /// <summary>A steel shelf unit with cardboard boxes on its boards (its long side along the yaw's x axis).</summary>
        public static Transform Shelf(Transform holder, float x, float z, float yaw, int seed)
        {
            bool flip = seed % 2 == 0;
            LobbyDock.ShelfUnit(holder, new Vector3(x, 0f, z), Steel, SteelDark, flip ? Card : Card2, flip ? Card2 : Card);
            var root = holder.GetChild(holder.childCount - 1);
            root.localEulerAngles = new Vector3(0f, yaw, 0f);
            Solidify(root);
            return root;
        }

        /// <summary>A stack of one to three cardboard boxes with their tape, square to the room and right on top of each other.</summary>
        public static Transform BoxStack(Transform holder, float x, float z, float yaw, int boxes, int seed)
        {
            var root = Root(holder, "BoxStack", x, z, yaw);
            for (int i = 0; i < boxes; i++)
            {
                float s = 0.60f + ((seed * (i + 3)) % 4) * 0.04f;
                float crooked = 0f;                              // (at most two degrees: tidy, not crooked)
                var at = new Vector3(0f, 0.3f + i * 0.6f, 0f);
                LobbyDock.Box("Box", at, new Vector3(s, 0.6f, s * 0.9f), (i + seed) % 2 == 0 ? Card : Card2, root, new Vector3(0f, crooked, 0f));
                LobbyDock.Box("BoxTape", at + new Vector3(0f, 0.305f, 0f), new Vector3(0.1f, 0.01f, s * 0.9f + 0.01f), Tape, root, new Vector3(0f, crooked, 0f));
            }
            Solidify(root);
            return root;
        }

        /// <summary>One tidy block of cardboard boxes (a grid of cols x rows, layers high), on a pallet when asked, square to the room.</summary>
        public static Transform BoxBlock(Transform holder, float x, float z, float yaw, int cols, int rows, int layers, bool pallet, int seed)
        {
            var root = Root(holder, "BoxBlock", x, z, yaw);
            float bw = 0.52f, bd = 0.50f, bh = 0.42f, baseY = 0f;
            if (pallet) { LobbyDock.Pallet(root, Vector3.zero, 0f, Wood); baseY = 0.13f; }
            for (int layer = 0; layer < layers; layer++)
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rows; j++)
                    {
                        var at = new Vector3((i - (cols - 1) * 0.5f) * bw, baseY + layer * bh + bh * 0.5f, (j - (rows - 1) * 0.5f) * bd);
                        LobbyDock.Box("Box", at, new Vector3(bw - 0.02f, bh - 0.01f, bd - 0.02f), (i + j + layer + seed) % 2 == 0 ? Card : Card2, root);
                        LobbyDock.Box("BoxTape", at + new Vector3(0f, bh * 0.5f, 0f), new Vector3(0.09f, 0.01f, bd - 0.01f), Tape, root);
                    }
            Solidify(root);
            return root;
        }

        /// <summary>The lobby's yellow forklift, forks along the yaw's z axis.</summary>
        public static Transform Forklift(Transform holder, float x, float z, float yaw)
        {
            LobbyDock.Forklift(holder, new Vector3(x, 0f, z), yaw, LobbyDock.Mat(new Color(0.90f, 0.66f, 0.10f), 0.25f), SteelDark, Dark, Steel, LobbyDock.Mat(new Color(0.07f, 0.07f, 0.08f), 0.05f));
            var root = holder.GetChild(holder.childCount - 1);
            Solidify(root);
            return root;
        }

        /// <summary>A drum, red or blue by <paramref name="colour"/> (even: red).</summary>
        public static Transform Drum(Transform holder, float x, float z, int colour)
        {
            var root = Root(holder, "Drum", x, z, 0f);
            LobbyDock.Cyl("DrumBody", new Vector3(0f, 0.45f, 0f), 0.29f, 0.9f, colour % 2 == 0 ? Red : LobbyDock.Mat(new Color(0.15f, 0.30f, 0.58f), 0.25f), root);
            LobbyDock.Cyl("DrumLid", new Vector3(0f, 0.91f, 0f), 0.3f, 0.04f, SteelDark, root);
            Solidify(root);
            return root;
        }

        /// <summary>A yellow bollard with a dark cap.</summary>
        public static Transform Bollard(Transform holder, float x, float z)
        {
            var root = Root(holder, "Bollard", x, z, 0f);
            LobbyDock.Cyl("Bollard", new Vector3(0f, 0.6f, 0f), 0.13f, 1.2f, Hazard, root);
            LobbyDock.Cyl("BollardCap", new Vector3(0f, 1.22f, 0f), 0.14f, 0.06f, Dark, root);
            Solidify(root);
            return root;
        }

        // ------------------------------------------------------------------ on the walls and from the ceiling (nothing solid)

        /// <summary>A steel door frame (two posts and a header) on the inner face of the wall at <paramref name="zInner"/>, and the roller box over it.</summary>
        public static void DoorFrame(Transform holder, float centreX, float zInner, float width, float height, bool rollerBox)
        {
            LobbyDock.Box("FrameL", new Vector3(centreX - width * 0.5f + 0.07f, height * 0.5f, zInner - 0.04f), new Vector3(0.14f, height, 0.08f), SteelDark, holder);
            LobbyDock.Box("FrameR", new Vector3(centreX + width * 0.5f - 0.07f, height * 0.5f, zInner - 0.04f), new Vector3(0.14f, height, 0.08f), SteelDark, holder);
            LobbyDock.Box("FrameTop", new Vector3(centreX, height - 0.07f, zInner - 0.04f), new Vector3(width, 0.14f, 0.08f), SteelDark, holder);
            if (rollerBox) LobbyDock.Box("RollerBox", new Vector3(centreX, height + 0.12f, zInner - 0.2f), new Vector3(width + 0.2f, 0.26f, 0.4f), SteelDark, holder);
        }

        /// <summary>A dark painted band along the foot of a wall (the lobby's), from <paramref name="x0"/> to <paramref name="x1"/> on the inner face at <paramref name="zInner"/>.</summary>
        public static void WallBase(Transform holder, float x0, float x1, float zInner)
        {
            if (x1 - x0 < 0.05f) return;
            LobbyDock.Box("WallBase", new Vector3((x0 + x1) * 0.5f, 0.55f, zInner - 0.03f), new Vector3(x1 - x0, 1.1f, 0.06f), LobbyDock.Mat(new Color(0.20f, 0.22f, 0.25f), 0.1f), holder);
        }

        /// <summary>The lobby's wall clock at <paramref name="at"/>, its face looking along -z turned by <paramref name="yaw"/>; its hands tell the real time through <paramref name="life"/>.</summary>
        public static void Clock(Transform holder, LobbyDockLife life, Vector3 at, float yaw)
        {
            var clock = new GameObject("Clock").transform;
            clock.SetParent(holder, false);
            clock.localPosition = at;
            clock.localEulerAngles = new Vector3(0f, yaw, 0f);
            LobbyDock.Cyl("ClockFace", Vector3.zero, 0.42f, 0.06f, White, clock, new Vector3(90f, 0f, 0f));
            LobbyDock.Cyl("ClockRim", new Vector3(0f, 0f, 0.015f), 0.46f, 0.05f, Dark, clock, new Vector3(90f, 0f, 0f));
            life.clockHour = LobbyDock.ClockHand(clock, "Hour", 0.24f, 0.035f, Dark);
            life.clockMinute = LobbyDock.ClockHand(clock, "Minute", 0.34f, 0.025f, Dark);
            life.clockSecond = LobbyDock.ClockHand(clock, "Second", 0.36f, 0.012f, Red);
        }

        /// <summary>Two pipes (a grey one and a thin red one) along the east wall near the ceiling, with brackets, from <paramref name="z0"/> to <paramref name="z1"/>.</summary>
        public static void Pipes(Transform holder, float xInner, float z0, float z1, float y)
        {
            float len = z1 - z0, zc = (z0 + z1) * 0.5f, px = xInner - 0.12f;
            LobbyDock.Cyl("Pipe", new Vector3(px, y, zc), 0.09f, len, SteelDark, holder, new Vector3(90f, 0f, 0f));
            LobbyDock.Cyl("Pipe2", new Vector3(px, y + 0.21f, zc), 0.06f, len, LobbyDock.Mat(new Color(0.72f, 0.20f, 0.16f), 0.25f), holder, new Vector3(90f, 0f, 0f));
            for (float z = z0 + 0.6f; z < z1 - 0.3f; z += 3.4f) LobbyDock.Box("PipeBracket", new Vector3(px + 0.06f, y + 0.1f, z), new Vector3(0.12f, 0.45f, 0.1f), SteelDark, holder);
        }

        /// <summary>A fire extinguisher on a bracket and a cork notice board with three notes, on the east wall (their faces to -x).</summary>
        public static void WallThings(Transform holder, float xInner, float extinguisherZ, float boardZ)
        {
            LobbyDock.Cyl("Extinguisher", new Vector3(xInner - 0.2f, 1.25f, extinguisherZ), 0.11f, 0.5f, Red, holder);
            LobbyDock.Cyl("ExtinguisherTop", new Vector3(xInner - 0.2f, 1.55f, extinguisherZ), 0.05f, 0.1f, Dark, holder);
            LobbyDock.Box("ExtBracket", new Vector3(xInner - 0.1f, 1.15f, extinguisherZ), new Vector3(0.1f, 0.08f, 0.26f), SteelDark, holder);
            var paper = LobbyDock.Mat(new Color(0.9f, 0.9f, 0.85f), 0.05f);
            LobbyDock.Box("NoticeBoard", new Vector3(xInner - 0.03f, 1.9f, boardZ), new Vector3(0.06f, 1.0f, 1.4f), LobbyDock.Mat(new Color(0.45f, 0.33f, 0.2f), 0.02f), holder);
            LobbyDock.Box("Note1", new Vector3(xInner - 0.07f, 2.0f, boardZ - 0.32f), new Vector3(0.01f, 0.5f, 0.38f), paper, holder, new Vector3(3f, 0f, 0f));
            LobbyDock.Box("Note2", new Vector3(xInner - 0.07f, 1.9f, boardZ + 0.18f), new Vector3(0.01f, 0.34f, 0.42f), LobbyDock.Mat(new Color(0.95f, 0.85f, 0.3f), 0.05f), holder, new Vector3(-4f, 0f, 0f));
            LobbyDock.Box("Note3", new Vector3(xInner - 0.07f, 2.15f, boardZ + 0.45f), new Vector3(0.01f, 0.4f, 0.3f), paper, holder, new Vector3(6f, 0f, 0f));
        }

        /// <summary>Hanging lamps (a cord, a shade, a glowing tube and a warm light) under the ceiling at <paramref name="ceilingY"/>, swaying through <paramref name="life"/>.</summary>
        public static void Lamps(Transform holder, LobbyDockLife life, Vector3[] spots, float ceilingY, float drop)
        {
            life.lamps = new Transform[spots.Length];
            life.lampLights = new Light[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var pivot = new GameObject("LampPivot" + i).transform;
                pivot.SetParent(holder, false);
                pivot.localPosition = new Vector3(spots[i].x, ceilingY, spots[i].z);
                LobbyDock.Cyl("Cord", new Vector3(0f, -drop * 0.5f, 0f), 0.015f, drop, Dark, pivot);
                LobbyDock.Cyl("Shade", new Vector3(0f, -drop - 0.05f, 0f), 0.42f, 0.22f, SteelDark, pivot);
                LobbyDock.Box("Tube", new Vector3(0f, -drop - 0.18f, 0f), new Vector3(0.5f, 0.04f, 0.5f), LobbyDock.Glow(new Color(1f, 0.92f, 0.76f), 2.2f), pivot);
                life.lampLights[i] = LobbyDock.MakeLight("LampLight" + i, LightType.Point, new Vector3(0f, -drop - 0.4f, 0f), new Color(1f, 0.88f, 0.70f), 1.3f, 9f, pivot);
                life.lamps[i] = pivot;
            }
        }

        /// <summary>The turning orange beacon: a lamp that throws a spot of light across the floor, at <paramref name="at"/>.</summary>
        public static void Beacon(Transform holder, LobbyDockLife life, Vector3 at)
        {
            var beacon = new GameObject("Beacon").transform;
            beacon.SetParent(holder, false);
            beacon.localPosition = at;
            LobbyDock.Cyl("BeaconBase", Vector3.zero, 0.16f, 0.12f, Dark, beacon);
            LobbyDock.Cyl("BeaconGlass", new Vector3(0f, 0.16f, 0f), 0.13f, 0.2f, LobbyDock.Glow(new Color(1f, 0.42f, 0.05f), 1.1f), beacon);
            var spin = new GameObject("BeaconSpin").transform;
            spin.SetParent(beacon, false);
            spin.localPosition = new Vector3(0f, 0.16f, 0f);
            var spot = LobbyDock.MakeLight("BeaconSpot", LightType.Spot, Vector3.zero, new Color(1f, 0.55f, 0.15f), 3.0f, 10f, spin);
            spot.spotAngle = 48f;
            spot.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            life.beaconSpin = spin;
        }

        /// <summary>The look of the vendor's counter: a dark steel body with a wooden top (its top is at 1.1 m, where the counter's own hidden box has it) and a yellow safety stripe on the customer's side, two boxes on one end and a rubber mat in front.</summary>
        public static void CounterLook(Transform holder, float x, float z, float width, float depth)
        {
            LobbyDock.Box("CounterBody", new Vector3(x, 0.5f, z), new Vector3(width, 1.0f, depth), SteelDark, holder);
            LobbyDock.Box("CounterTop", new Vector3(x, 1.05f, z), new Vector3(width + 0.08f, 0.1f, depth + 0.08f), Wood, holder);
            LobbyDock.Box("CounterStripe", new Vector3(x, 0.72f, z - depth * 0.5f - 0.006f), new Vector3(width, 0.1f, 0.012f), Hazard, holder);
            LobbyDock.Box("CounterBoxA", new Vector3(x - width * 0.5f + 0.4f, 1.1f + 0.19f, z + 0.1f), new Vector3(0.46f, 0.38f, 0.4f), Card, holder, new Vector3(0f, 6f, 0f));
            LobbyDock.Box("CounterBoxB", new Vector3(x - width * 0.5f + 0.4f, 1.1f + 0.38f + 0.14f, z + 0.1f), new Vector3(0.34f, 0.28f, 0.3f), Card2, holder, new Vector3(0f, -9f, 0f));
            LobbyDock.Box("CounterMat", new Vector3(x, 0.012f, z - depth * 0.5f - 0.75f), new Vector3(width - 0.4f, 0.024f, 1.0f), LobbyDock.Mat(new Color(0.09f, 0.09f, 0.10f), 0.05f), holder);
        }

        /// <summary>A dark steel frame round a sign board of <paramref name="w"/> x <paramref name="h"/> centred at (x, y, z).</summary>
        public static void SignFrame(Transform holder, float x, float y, float z, float w, float h)
        {
            const float t = 0.07f;
            LobbyDock.Box("SignFrameTop", new Vector3(x, y + h * 0.5f + t * 0.5f, z), new Vector3(w + t * 2f, t, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameBottom", new Vector3(x, y - h * 0.5f - t * 0.5f, z), new Vector3(w + t * 2f, t, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameLeft", new Vector3(x - w * 0.5f - t * 0.5f, y, z), new Vector3(t, h, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameRight", new Vector3(x + w * 0.5f + t * 0.5f, y, z), new Vector3(t, h, 0.12f), SteelDark, holder);
        }

        /// <summary>A lamp that hangs still (a cord, a shade, a glowing tube; no light of its own) from the ceiling at <paramref name="ceilingY"/>.</summary>
        public static void LampFixture(Transform holder, Vector3 at, float ceilingY, float drop)
        {
            LobbyDock.Cyl("FixtureCord", new Vector3(at.x, ceilingY - drop * 0.5f, at.z), 0.015f, drop, Dark, holder);
            LobbyDock.Cyl("FixtureShade", new Vector3(at.x, ceilingY - drop - 0.05f, at.z), 0.34f, 0.2f, SteelDark, holder);
            LobbyDock.Box("FixtureTube", new Vector3(at.x, ceilingY - drop - 0.17f, at.z), new Vector3(0.42f, 0.04f, 0.42f), LobbyDock.Glow(new Color(1f, 0.9f, 0.7f), 2.0f), holder);
        }

        // ------------------------------------------------------------------ the vendor's black-market stock (the owner, 2026-10-19: "all cool gadgets and weapons and stuff in his truck")

        static void NoColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
        }

        /// <summary>
        /// The vendor's shelf unit: the lobby's steel frame with no cardboard. Its top board holds weapons (<paramref name="weapons"/> 0: the melee weapons; 1: pistols with their clips), the two boards below it gadgets, three to a board. Only what the demo has is on
        /// show. The front faces local +z.
        /// </summary>
        public static Transform StockShelf(Transform holder, float x, float z, float yaw, int weapons, int seed, int briefcases)
        {
            var root = Root(holder, "StockShelf", x, z, yaw);
            foreach (float px in new[] { -0.85f, 0.85f })
                foreach (float pz in new[] { -0.3f, 0.3f })
                    LobbyDock.Box("Upright", new Vector3(px, 1.2f, pz), new Vector3(0.06f, 2.4f, 0.06f), SteelDark, root);
            for (int i = 0; i < 4; i++) LobbyDock.Box("ShelfBoard", new Vector3(0f, 0.15f + i * 0.75f, 0f), new Vector3(1.8f, 0.04f, 0.66f), Steel, root);
            for (int board = 0; board < 2; board++)
                for (int slot = 0; slot < 3; slot++)
                {
                    if (slot == 1 && (board == 1 && briefcases >= 1 || board == 0 && briefcases >= 2)) continue;          // (a briefcase lies flat here: the owner, 2026-10-19)
                    Gadget(root, new Vector3(-0.58f + slot * 0.58f, 0.17f + board * 0.75f, 0f), (seed + board * 3 + slot * 5) % 8);
                }
            if (weapons == 0) MeleeRack(root, new Vector3(0f, 1.67f, 0f));
            else for (int slot = 0; slot < 3; slot++) Gadget(root, new Vector3(-0.58f + slot * 0.58f, 1.67f, 0f), (seed + 4 + slot * 3) % 8);          // (the guns hang on the wall behind the vendor, and only once they are unlocked)
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
        /// <summary>The glowing strip under a trailer's roof, as in the lobby's trailer.</summary>
        public static void TrailerStrip(Transform holder, Vector3 at, float length)
        {
            LobbyDock.Box("TrailerLamp", at, new Vector3(0.3f, 0.05f, length), LobbyDock.Glow(new Color(1f, 0.93f, 0.78f), 2.4f), holder);
        }
    }
}

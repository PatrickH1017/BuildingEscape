using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The guard outpost. The red room that used to stand in the farthest corner of every floor (the elevator the backup guards arrive in)
    /// is now a whole guard outpost: a room with a locked steel door and a keypad outside, a long one-way window (a mirror from the
    /// corridor, an ordinary window from inside), desks with monitors, lockers, a table with whatever the guards keep lying about (a note
    /// about a safe, weapons, valuables) and, in the back corner, the small red elevator that the backup arrives in. The room, the furniture
    /// and the places for the things are planned by <see cref="OfficePlanner"/> (<c>OfficePlanner.Outpost.cs</c>); this file builds them:
    ///  - before the NavMesh bake: the alcove (<see cref="BuildOutpostCar"/>), the dark walls and the lockers (FloorGenerator.Interior.cs);
    ///  - after it: the lock door (FloorGenerator.Doors.cs), the window, the keypad, the alarm lamp and the controller
    ///    (<see cref="BuildOutpostExtras"/>), and, last of all, the things lying about and the cash in the floor's safe (<see cref="FillOutpost"/>).
    /// What the outpost holds follows from the run's seed and the floor alone (<see cref="OutpostLoot.Roll"/>), never from the floor's random
    /// numbers, so the loot, the guards and the maze are the same as they would be without it.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Guard outpost")]
        [Tooltip("The long window in the outpost's front wall is a real one-way mirror from the corridor (it draws the scene a second time while you can see it). Off = dark glass.")]
        public bool outpostMirror = true;

        OutpostController outpostCtl;          // the controller of the floor that is being built / on show
        SlidingDoor outpostDoor;
        int outpostDoorIndex = -1;
        FloorSafe floorSafe;
        Rc outpostArea;                        // the outside of the outpost's walls (world), to keep random spots out of it
        bool hasOutpostArea;
        int intelNow;                          // the run's seed for the safes, as this floor was built

        // what SafeRoomLabel knows about floors that are not on show
        int baseSeedSeen, labelsIntel;
        bool haveBase;
        readonly Dictionary<int, string> safeLabels = new Dictionary<int, string>();

        // ------------------------------------------------------------------ seeds and the note's question: where is the safe?

        /// <summary>The run's seed for the safes and the notes (the tower manager's; without one - the editor's floor preview - the one the floor's own seed came from).</summary>
        int IntelSeedFor(int floor, int seed)
        {
            var tm = TowerManager.Instance;
            return tm != null ? tm.IntelSeed : seed - floor * 7919;
        }

        void NoteBaseSeed(int floor, int seed)
        {
            int baseSeed = seed - floor * 7919;
            int intel = IntelSeedFor(floor, seed);
            if (!haveBase || baseSeed != baseSeedSeen || intel != labelsIntel) safeLabels.Clear();
            baseSeedSeen = baseSeed; labelsIntel = intel; haveBase = true;
            intelNow = intel;
        }

        void ResetOutpost()
        {
            outpostCtl = null; outpostDoor = null; outpostDoorIndex = -1; floorSafe = null;
            hasOutpostArea = false;
        }

        bool InOutpost(Vector3 p) => hasOutpostArea && outpostArea.Contains(p.x, p.z);

        /// <summary>
        /// Runs one step of building the outpost. A mistake in it is reported and the floor goes on without that step (it is still a floor
        /// you can play; a lock that was never finished is just an ordinary door). Returns false when the step failed.
        /// </summary>
        bool OutpostStep(string what, int floor, System.Action step)
        {
            try { step(); return true; }
            catch (System.Exception e)
            {
                Debug.LogError("[Outpost] Could not build " + what + " on floor " + floor + ", so the floor goes on without it. Please report this:\n" + e);
                return false;
            }
        }

        /// <summary>
        /// The number of the room the safe of floor <paramref name="floor"/> stands in ("507"), or null if that floor has no safe or it cannot be
        /// worked out. A note in an outpost may name a safe on a floor that has not been built yet, so a floor that is not on show is planned
        /// again from its seed (the planner is pure and the floor's shape follows from the seed, so it is exactly the floor that will be built).
        /// </summary>
        public string SafeRoomLabel(int floor)
        {
            int intel = intelNow;
            var tm = TowerManager.Instance;
            if (tm != null) intel = tm.IntelSeed;
            if (!SafeRules.HasSafe(intel, floor)) return null;
            if (layout != null && layoutFloor == floor)
                return layout.safe != null ? RoomNumbers.LabelOf(layout, layout.safe.room, floor) : null;       // the floor on show: its own plan
            if (!haveBase || !furnishRooms || Mathf.Abs(cellSize - 18f) > 0.01f) return null;
            if (safeLabels.TryGetValue(floor, out var cached)) return cached.Length == 0 ? null : cached;

            string label = null;
            try
            {
                int seed = baseSeedSeen + floor * 7919;
                SizeOfFloor(out int c, out int r);
                var shape = FloorShape.Maze(new System.Random(seed), c, r, loopChance);
                var dry = OfficePlanner.Build(shape.PlanFor(floor, seed, cellSize, wallHeight, intel));
                if (dry.safe != null) label = RoomNumbers.LabelOf(dry, dry.safe.room, floor);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Outpost] Could not work out where the safe of floor " + floor + " is: " + e.Message);
            }
            safeLabels[floor] = label ?? "";
            return label;
        }

        // ------------------------------------------------------------------ materials

        readonly Dictionary<string, Material> outpostMats = new Dictionary<string, Material>();
        Material outpostWallMat, windowMat;
        Texture2D wireTex;

        void DestroyOutpostMaterials()
        {
            foreach (var m in outpostMats.Values) if (m) Kill(m);
            outpostMats.Clear();
            if (outpostWallMat) Kill(outpostWallMat);
            if (windowMat) Kill(windowMat);
            if (wireTex) Kill(wireTex);
            outpostWallMat = windowMat = null;
            wireTex = null;
        }

        /// <summary>The outpost's own walls: dark slate, copied from the wall material so it follows whatever shader the project uses.</summary>
        Material OutpostWallMat()
        {
            if (outpostWallMat) return outpostWallMat;
            if (!wallMat) return null;
            outpostWallMat = new Material(wallMat) { hideFlags = HideFlags.HideAndDontSave };
            if (outpostWallMat.HasProperty("_Color")) outpostWallMat.color = new Color(0.30f, 0.33f, 0.38f);
            return outpostWallMat;
        }

        Material OMat(string id)
        {
            if (outpostMats.TryGetValue(id, out var m) && m) return m;
            switch (id)
            {
                case "door": m = Surface("OpDoor", new Color(0.15f, 0.17f, 0.20f), 0.5f, false, 0.6f); break;
                case "frame": m = Surface("OpFrame", new Color(0.10f, 0.11f, 0.13f), 0.4f, false, 0.5f); break;
                case "dark": m = Surface("OpDark", new Color(0.07f, 0.08f, 0.09f), 0.3f, false, 0.2f); break;
                case "windowGlass": m = Surface("OpWindowGlass", new Color(0.05f, 0.08f, 0.10f), 0.95f, false, 0.9f); break;
                case "led": m = Surface("OpLed", new Color(1f, 0.12f, 0.08f), 0f, true); break;
                case "keycap": m = Surface("OpKey", new Color(0.55f, 0.57f, 0.60f), 0.3f, false, 0.2f); break;
                case "locker": m = Surface("OpLocker", new Color(0.30f, 0.38f, 0.46f), 0.4f, false, 0.4f); break;
                case "lockerDoor": m = Surface("OpLockerDoor", new Color(0.36f, 0.45f, 0.54f), 0.45f, false, 0.4f); break;
                case "safeSteel": m = Surface("OpSafe", new Color(0.30f, 0.32f, 0.35f), 0.5f, false, 0.7f); break;
                case "brass": m = Surface("OpBrass", new Color(0.78f, 0.62f, 0.25f), 0.7f, false, 0.8f); break;
                case "lining": m = Surface("OpLining", new Color(0.12f, 0.12f, 0.14f), 0.1f, false); break;
                case "paper": m = Surface("OpPaper", new Color(0.93f, 0.90f, 0.78f), 0.05f, false); break;
                case "ink": m = Surface("OpInk", new Color(0.18f, 0.20f, 0.30f), 0.05f, false); break;
                case "seal": m = Surface("OpSeal", new Color(0.65f, 0.08f, 0.08f), 0.3f, false); break;
                case "gold": m = Surface("OpGold", new Color(0.95f, 0.76f, 0.22f), 0.75f, false, 0.85f); break;
                case "leather": m = Surface("OpLeather", new Color(0.14f, 0.09f, 0.07f), 0.2f, false); break;
                case "chipRed": m = Surface("OpChipRed", new Color(0.75f, 0.10f, 0.10f), 0.35f, false); break;
                case "chipBlue": m = Surface("OpChipBlue", new Color(0.10f, 0.20f, 0.70f), 0.35f, false); break;
                case "chipGreen": m = Surface("OpChipGreen", new Color(0.10f, 0.50f, 0.20f), 0.35f, false); break;
                case "chipWhite": m = Surface("OpChipWhite", new Color(0.90f, 0.90f, 0.88f), 0.35f, false); break;
                case "bill": m = Surface("OpBill", new Color(0.45f, 0.66f, 0.45f), 0.1f, false); break;
                case "band": m = Surface("OpBand", new Color(0.86f, 0.80f, 0.52f), 0.1f, false); break;
                case "wired":
                    m = Surface("OpWired", Color.white, 0.7f, false);
                    m.mainTexture = WireTexture();
                    m.mainTextureScale = new Vector2(2f, 5f);
                    break;
                default: m = Surface("Op" + id, Color.grey, 0.3f, false); break;
            }
            outpostMats[id] = m;
            return m;
        }

        /// <summary>A pale wire mesh over dark green-grey glass (the panes in the outpost's door).</summary>
        Texture2D WireTexture()
        {
            if (wireTex) return wireTex;
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool wire = ((x + y) % 16) < 2 || ((x - y + n * 4) % 16) < 2;
                    float shade = 0.85f + 0.15f * (y / (float)n);
                    byte r, g, b;
                    if (wire) { r = 158; g = 173; b = 168; }
                    else { r = (byte)(31 * shade); g = (byte)(48 * shade); b = (byte)(54 * shade); }
                    px[y * n + x] = new Color32(r, g, b, 255);
                }
            wireTex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                name = "OutpostWire",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            wireTex.SetPixels32(px);
            wireTex.Apply(true, false);
            return wireTex;
        }

        /// <summary>The window's material: the mirror shader tinted like dark glass (or, without the shader, dark glossy glass).</summary>
        Material WindowMaterial(out bool real)
        {
            var baseMat = MirrorMaterial(out real);
            if (!real || !outpostMirror)
            {
                real = false;
                return OMat("windowGlass");
            }
            if (!windowMat)
            {
                windowMat = new Material(baseMat) { name = "OutpostWindow", hideFlags = HideFlags.HideAndDontSave };
                if (windowMat.HasProperty("_Tint")) windowMat.SetColor("_Tint", new Color(0.55f, 0.64f, 0.70f, 1f));
                if (windowMat.HasProperty("_Base")) windowMat.SetColor("_Base", new Color(0.02f, 0.035f, 0.045f, 1f));
            }
            return windowMat;
        }

        // ------------------------------------------------------------------ small helpers

        /// <summary>A box (or other primitive) hung on <paramref name="parent"/>: no shadow, and no collider unless asked for.</summary>
        static GameObject Part(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat,
                               PrimitiveType type = PrimitiveType.Cube, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (c) Kill(c);
            }
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ the elevator alcove (before the NavMesh bake)

        /// <summary>
        /// The small red elevator that the backup arrives in, in the back corner of the outpost: three solid walls and a fourth with a gap
        /// and two sliding panels. Its door opens into the room; the officers walk out through the room and the (locked) front door, which
        /// opens for them. It stands open while the NavMesh is baked (the way out is part of the mesh) and shuts afterwards.
        /// </summary>
        void BuildOutpostCar(OutpostInfo op)
        {
            outpostArea = op.outer;
            hasOutpostArea = true;

            var car = op.car;
            var inner = op.carInner;
            float t = (car.W - inner.W) * 0.5f;                  // the thickness of the walls
            int dx = op.carDoorX, dz = op.carDoorZ;              // the way the door faces: one step along x or z
            float half = op.carDoorHalf;                         // half the width of the doorway
            float h = wallHeight;

            var root = new GameObject("GuestElevator");
            root.transform.SetParent(current, false);
            var panelMat = guardMat ? guardMat : wallMat;
            var door = root.AddComponent<ElevatorDoor>();
            door.chimeOnOpen = true;                             // it rings when it opens for the backup officers

            void Wall(string name, Rc rc, bool hasDoor, bool alongZ)
            {
                if (!hasDoor)
                {
                    Box(name, new Vector3(rc.Cx, h * 0.5f, rc.Cz), new Vector3(rc.W, h, rc.D), wallMat, root.transform);
                    return;
                }
                float dt = t * 0.8f;
                if (alongZ)
                {
                    float c = rc.Cz;
                    Box(name + "_A", new Vector3(rc.Cx, h * 0.5f, (rc.z0 + c - half) * 0.5f), new Vector3(rc.W, h, c - half - rc.z0), wallMat, root.transform);
                    Box(name + "_B", new Vector3(rc.Cx, h * 0.5f, (rc.z1 + c + half) * 0.5f), new Vector3(rc.W, h, rc.z1 - c - half), wallMat, root.transform);
                    var pa = Box("GuestPanel_A", new Vector3(rc.Cx, h * 0.5f, c - half * 0.5f), new Vector3(dt, h, half), panelMat, root.transform);
                    var pb = Box("GuestPanel_B", new Vector3(rc.Cx, h * 0.5f, c + half * 0.5f), new Vector3(dt, h, half), panelMat, root.transform);
                    door.Setup(pa.transform, pb.transform, new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half));
                }
                else
                {
                    float c = rc.Cx;
                    Box(name + "_A", new Vector3((rc.x0 + c - half) * 0.5f, h * 0.5f, rc.Cz), new Vector3(c - half - rc.x0, h, rc.D), wallMat, root.transform);
                    Box(name + "_B", new Vector3((rc.x1 + c + half) * 0.5f, h * 0.5f, rc.Cz), new Vector3(rc.x1 - c - half, h, rc.D), wallMat, root.transform);
                    var pa = Box("GuestPanel_A", new Vector3(c - half * 0.5f, h * 0.5f, rc.Cz), new Vector3(half, h, dt), panelMat, root.transform);
                    var pb = Box("GuestPanel_B", new Vector3(c + half * 0.5f, h * 0.5f, rc.Cz), new Vector3(half, h, dt), panelMat, root.transform);
                    door.Setup(pa.transform, pb.transform, new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f));
                }
            }

            // west and east own the corners; south and north fill in between
            Wall("GuestWall_W", new Rc(car.x0, car.z0, car.x0 + t, car.z1), dx < 0, true);
            Wall("GuestWall_E", new Rc(car.x1 - t, car.z0, car.x1, car.z1), dx > 0, true);
            Wall("GuestWall_S", new Rc(car.x0 + t, car.z0, car.x1 - t, car.z0 + t), dz < 0, false);
            Wall("GuestWall_N", new Rc(car.x0 + t, car.z1 - t, car.x1 - t, car.z1), dz > 0, false);

            Box("GuestFloor", new Vector3(inner.Cx, 0.03f, inner.Cz), new Vector3(inner.W, 0.1f, inner.D), panelMat, root.transform, collider: false);

            guestDoor = door;
            guestSpawn = new Vector3(inner.Cx, 0f, inner.Cz);
            guestSide = dx != 0 ? Vector3.forward : Vector3.right;       // along the wall the door is in
            guestOut = new Vector3(dx, 0f, dz);
            GuestDoorOpen = false;
        }

        // ------------------------------------------------------------------ the lock door (called from BuildDoors)

        /// <summary>
        /// The outpost's front door: the same sliding double door as every other, in dark steel. Each leaf is dressed like the reference
        /// picture's door: a tall wired-glass pane over a shorter one, in a dark frame, on both faces. The panes are flat pictures hung
        /// on the leaves, so the room behind a shut door stays hidden.
        /// </summary>
        SlidingDoor MakeOutpostDoor(Transform parent, WallBox beam)
        {
            var door = MakeDoor(parent, beam, OMat("door"));
            // (the panes are decoration: if they fail the door is a plain steel one, but it is still a door)
            OutpostStep("the door's glass panes", layoutFloor, () =>
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = side < 0 ? door.LowLeaf : door.HighLeaf;
                    for (int face = -1; face <= 1; face += 2)
                    {
                        // (measured in the leaf's own unit cube: -0.5 is the floor, +0.5 the top of the door)
                        door.AddDecor(side, Decal(leaf, face, 0.25f, 0.70f, 0.38f, 0.03f, OMat("frame")));
                        door.AddDecor(side, Decal(leaf, face, 0.25f, 0.58f, 0.30f, 0.06f, OMat("wired")));
                        door.AddDecor(side, Decal(leaf, face, -0.22f, 0.70f, 0.38f, 0.03f, OMat("frame")));
                        door.AddDecor(side, Decal(leaf, face, -0.22f, 0.58f, 0.30f, 0.06f, OMat("wired")));
                    }
                }
            });
            return door;
        }

        /// <summary>A flat picture on a door leaf (measured in the leaf's own unit cube): faces outward on side <paramref name="face"/> (-1 or +1).</summary>
        static Renderer Decal(Transform leaf, int face, float cy, float w, float h, float lift, Material mat)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Pane";
            var c = quad.GetComponent<Collider>();
            if (c) Kill(c);
            quad.transform.SetParent(leaf, false);
            quad.transform.localPosition = new Vector3(0f, cy, face * (0.5f + lift));
            quad.transform.localRotation = face > 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;   // (a Quad is seen from its back)
            quad.transform.localScale = new Vector3(w, h, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        // ------------------------------------------------------------------ the outside of the outpost (after the bake)

        /// <summary>
        /// The one-way window, the keypad, the alarm lamp and the controller that runs the lock, the alarm and the safe. Built after the NavMesh
        /// bake and after the doors, because the lock needs the door.
        /// </summary>
        void BuildOutpostExtras(int floor)
        {
            if (layout == null || !Application.isPlaying) return;
            var op = layout.outpost;
            if (op == null && floorSafe == null) return;

            var root = new GameObject("Outpost");
            root.transform.SetParent(current, false);
            var ctl = root.AddComponent<OutpostController>();
            outpostCtl = ctl;
            ctl.Floor = floor;
            ctl.RunSeed = intelNow;
            ctl.Safe = floorSafe;

            if (op != null)
            {
                BuildOneWayWindow(op, root.transform);
                BuildDoorTrim(op, root.transform);
                if (outpostDoor != null)
                {
                    BuildKeypad(op, ctl, root.transform);
                    BuildBeacon(op, ctl, root.transform);
                    ctl.Door = outpostDoor;
                    ctl.DoorIndex = outpostDoorIndex;
                }
            }
            ctl.Begin();
        }

        /// <summary>
        /// The long window along the front wall. Inside, it is just an opening in the wall (you see the corridor). Outside, a mirror quad on the
        /// face of the wall shows the corridor back (<see cref="PlanarMirror"/>) and everything behind it is cut off, so nobody can see in. An
        /// invisible pane in the opening keeps people, thrown things and the officers' eyes from passing through.
        /// </summary>
        void BuildOneWayWindow(OutpostInfo op, Transform parent)
        {
            var win = op.window;
            bool alongX = win.W >= win.D;
            float length = alongX ? win.W : win.D, thick = alongX ? win.D : win.W;
            float y0 = op.windowY0, y1 = op.windowY1, height = y1 - y0;
            var outward = new Vector3(op.frontX, 0f, op.frontZ);
            var along = alongX ? Vector3.right : Vector3.forward;
            var middle = new Vector3(win.Cx, (y0 + y1) * 0.5f, win.Cz);
            var face = middle + outward * (thick * 0.5f);                     // the middle of the window on the wall's outer face

            var glass = new GameObject("OneWayGlass");
            glass.transform.SetParent(parent, false);
            glass.transform.position = middle;
            var bc = glass.AddComponent<BoxCollider>();
            bc.size = new Vector3(win.W, height, win.D);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "OneWayMirror";
            var qc = quad.GetComponent<Collider>();
            if (qc) Kill(qc);
            quad.transform.SetParent(parent, false);
            quad.transform.position = face + outward * 0.006f;
            quad.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);       // (a Quad is seen from its back: this shows its face outward)
            quad.transform.localScale = new Vector3(length, height, 1f);
            var rend = quad.GetComponent<Renderer>();
            rend.sharedMaterial = WindowMaterial(out bool real);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (real)
            {
                var mirror = quad.AddComponent<PlanarMirror>();
                mirror.normal = outward;
                mirror.quality = mirrorQuality * 0.85f;
                mirror.maxDistance = 14f;
                mirror.blankWhenIdle = true;
            }

            // a steel frame round the glass on the outside, with a ledge at the bottom
            var frame = OMat("frame");
            Vector3 Size(float a, float b, float depth) => alongX ? new Vector3(a, b, depth) : new Vector3(depth, b, a);
            Part(parent, "WindowTop", face + Vector3.up * (height * 0.5f + 0.05f) + outward * 0.035f, Size(length + 0.2f, 0.1f, 0.07f), frame);
            Part(parent, "WindowSill", face - Vector3.up * (height * 0.5f + 0.04f) + outward * 0.07f, Size(length + 0.24f, 0.08f, 0.14f), frame);
            Part(parent, "WindowLeft", face - along * (length * 0.5f + 0.05f) + outward * 0.035f, Size(0.1f, height + 0.2f, 0.07f), frame);
            Part(parent, "WindowRight", face + along * (length * 0.5f + 0.05f) + outward * 0.035f, Size(0.1f, height + 0.2f, 0.07f), frame);
            // two thin posts split the long window into three panes
            for (int i = -1; i <= 1; i += 2)
                Part(parent, "WindowPost", face + along * (i * length / 6f) + outward * 0.02f, Size(0.04f, height, 0.04f), frame);
        }

        /// <summary>A dark steel frame round the doorway on the corridor side.</summary>
        void BuildDoorTrim(OutpostInfo op, Transform parent)
        {
            var d = op.door;
            bool alongX = d.W >= d.D;
            float width = alongX ? d.W : d.D, thick = alongX ? d.D : d.W;
            var outward = new Vector3(op.frontX, 0f, op.frontZ);
            var along = alongX ? Vector3.right : Vector3.forward;
            float top = OfficePlanner.OutpostBeamY;
            var face = new Vector3(d.Cx, 0f, d.Cz) + outward * (thick * 0.5f);
            var frame = OMat("frame");
            Vector3 Size(float a, float b, float depth) => alongX ? new Vector3(a, b, depth) : new Vector3(depth, b, a);
            Part(parent, "DoorJambL", face - along * (width * 0.5f + 0.04f) + Vector3.up * (top * 0.5f) + outward * 0.025f, Size(0.08f, top, 0.05f), frame);
            Part(parent, "DoorJambR", face + along * (width * 0.5f + 0.04f) + Vector3.up * (top * 0.5f) + outward * 0.025f, Size(0.08f, top, 0.05f), frame);
            Part(parent, "DoorLintel", face + Vector3.up * (top - 0.02f) + outward * 0.025f, Size(width + 0.16f, 0.1f, 0.05f), frame);
        }

        /// <summary>
        /// The keypad on the wall beside the door: a dark box with a lit display, a lamp and twelve keys. Its lit parts show the state of the
        /// lock (red: locked, flashing: alarm, green: open - see <see cref="OutpostController"/>). E on it starts the hack.
        /// </summary>
        void BuildKeypad(OutpostInfo op, OutpostController ctl, Transform parent)
        {
            var outward = new Vector3(op.frontX, 0f, op.frontZ);
            var pos = new Vector3(op.keypadX, op.keypadY, op.keypadZ);
            var root = new GameObject("Keypad");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(outward, Vector3.up));   // (its +z points out of the wall)

            var dark = OMat("dark");
            var housing = Part(root.transform, "Housing", new Vector3(0f, 0f, 0.03f), new Vector3(0.26f, 0.38f, 0.06f), dark);
            var leds = new List<Renderer>();
            var display = Part(root.transform, "Display", new Vector3(0f, 0.13f, 0.063f), new Vector3(0.18f, 0.05f, 0.008f), OMat("led"));
            leds.Add(display.GetComponent<Renderer>());
            var lamp = Part(root.transform, "Lamp", new Vector3(0.09f, 0.165f, 0.063f), new Vector3(0.03f, 0.03f, 0.008f), OMat("led"));
            leds.Add(lamp.GetComponent<Renderer>());
            var cap = OMat("keycap");
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                    Part(root.transform, "Key", new Vector3((col - 1) * 0.06f, 0.05f - row * 0.056f, 0.065f), new Vector3(0.042f, 0.042f, 0.012f), cap);

            var box = housing.GetComponent<Renderer>().bounds;
            box.Expand(0.1f);
            root.AddComponent<KeypadProp>().Init(ctl, box);

            ctl.KeypadPosition = pos + outward * 0.07f;
            ctl.Outward = outward;
            ctl.LedRenderers = leds.ToArray();
        }

        /// <summary>The red lamp over the door that flashes (with a red light on the corridor) while the alarm sounds.</summary>
        void BuildBeacon(OutpostInfo op, OutpostController ctl, Transform parent)
        {
            var d = op.door;
            bool alongX = d.W >= d.D;
            float thick = alongX ? d.D : d.W;
            var outward = new Vector3(op.frontX, 0f, op.frontZ);
            var face = new Vector3(d.Cx, 3.0f, d.Cz) + outward * (thick * 0.5f);

            Part(parent, "BeaconBase", face + outward * 0.015f, alongX ? new Vector3(0.34f, 0.34f, 0.03f) : new Vector3(0.03f, 0.34f, 0.34f), OMat("dark"));
            var dome = Part(parent, "Beacon", face + outward * 0.05f, new Vector3(0.24f, 0.24f, 0.24f), OMat("led"), PrimitiveType.Sphere);

            var lightGo = new GameObject("AlarmLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = face + outward * 0.7f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.06f);
            light.range = 9f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            ctl.BeaconRenderer = dome.GetComponent<Renderer>();
            ctl.BeaconLight = light;
        }

        // ------------------------------------------------------------------ furniture built from boxes (before the bake)

        /// <summary>A steel double locker: a body, two doors with louvres and handles. It stands against the wall facing the room.</summary>
        GameObject BuildLocker(Piece p, Transform parent, bool collider)
        {
            var def = p.def;
            float w = def.W, d = def.D, h = def.H;
            var root = new GameObject("Locker");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(p.x, p.y, p.z), Quaternion.Euler(0f, p.yaw, 0f));

            Part(root.transform, "Body", new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), OMat("locker"), PrimitiveType.Cube, collider);
            var doorMat = OMat("lockerDoor");
            var dark = OMat("dark");
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                float cx = side * w * 0.25f;
                Part(root.transform, "Door", new Vector3(cx, h * 0.5f, d * 0.5f + 0.008f), new Vector3(w * 0.5f - 0.04f, h - 0.1f, 0.016f), doorMat);
                Part(root.transform, "Vent", new Vector3(cx, h - 0.28f, d * 0.5f + 0.017f), new Vector3(w * 0.5f - 0.2f, 0.05f, 0.004f), dark);
                Part(root.transform, "Vent", new Vector3(cx, 0.22f, d * 0.5f + 0.017f), new Vector3(w * 0.5f - 0.2f, 0.05f, 0.004f), dark);
                Part(root.transform, "Handle", new Vector3(cx - side * (w * 0.25f - 0.09f), h * 0.55f, d * 0.5f + 0.03f), new Vector3(0.03f, 0.12f, 0.025f), dark);
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        /// <summary>The floor safe (see <see cref="FloorSafe"/>): the planner put it against a wall of a numbered room.</summary>
        GameObject BuildFloorSafePiece(Piece p, Transform parent)
        {
            int floor = layoutFloor;
            var mats = new FloorSafe.Mats
            {
                steel = OMat("safeSteel"), dark = OMat("dark"), brass = OMat("brass"), lining = OMat("lining"), led = OMat("led")
            };
            var safe = FloorSafe.Build(parent, p.x, p.z, p.yaw, floor, SafeRules.Code(intelNow, floor), mats);
            floorSafe = safe;
            return safe.gameObject;
        }

        // ------------------------------------------------------------------ what the guards keep (after everything else)

        /// <summary>
        /// Puts the outpost's things on its table and desk, and the cash in the floor's safe. What there is follows from <see cref="OutpostLoot.Roll"/>
        /// (the run's seed and the floor), and this never touches the floor's own random numbers, so nothing else on the floor changes.
        /// </summary>
        void FillOutpost(int floor)
        {
            if (layout == null || !Application.isPlaying) return;
            var op = layout.outpost;
            var parent = current;
            if (op != null)
            {
                var stash = OutpostLoot.Roll(intelNow, floor);
                var slots = op.itemSlots;
                int next = 0;
                Slot NextItemSlot()
                {
                    while (next < slots.Count)
                    {
                        var s = slots[next++];
                        if (s.kind == "item") return s;
                    }
                    return null;
                }

                if (stash.HasNote)
                {
                    var noteSlot = slots.Find(s => s.kind == "note");
                    if (noteSlot != null) MakeNote(noteSlot, stash, floor, parent);
                }
                if (MeleeWeapon.Available)
                {
                    foreach (int w in stash.weapons)
                    {
                        var s = NextItemSlot();
                        if (s == null) break;
                        MeleeWeapon.Create((MeleeWeapon.Kind)w, new Vector3(s.x, s.y + 0.08f, s.z), parent, s.yaw);
                    }
                }
                for (int i = 0; i < stash.valuableKinds.Count; i++)
                {
                    var s = NextItemSlot();
                    if (s == null) break;
                    MakeValuable(stash.valuableKinds[i], stash.valuableValues[i], new Vector3(s.x, s.y + 0.03f, s.z), s.yaw, parent);
                }
            }
            if (floorSafe != null)
            {
                var values = SafeRules.Split(SafeRules.Cash(floor), SafeRules.Bundles(floor));
                floorSafe.FillWithCash(parent, values, OMat("bill"), OMat("band"));
            }
        }

        /// <summary>A sheet of paper on the desk. Point at it and press E to read it (<see cref="NoteProp"/>).</summary>
        void MakeNote(Slot s, OutpostStash stash, int floor, Transform parent)
        {
            var root = new GameObject("Note");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(s.x, s.y + 0.006f, s.z), Quaternion.Euler(0f, s.yaw, 0f));
            var sheet = Part(root.transform, "Sheet", Vector3.zero, new Vector3(0.21f, 0.008f, 0.297f), OMat("paper"));
            var ink = OMat("ink");
            for (int line = 0; line < 5; line++)
                Part(root.transform, "Line", new Vector3(0f, 0.0045f, 0.09f - line * 0.045f), new Vector3(0.15f - (line % 2) * 0.04f, 0.001f, 0.008f), ink);
            Part(root.transform, "Mark", new Vector3(0.07f, 0.0045f, -0.12f), new Vector3(0.04f, 0.001f, 0.04f), OMat("seal"));

            var box = sheet.GetComponent<Renderer>().bounds;
            box.Expand(new Vector3(0.12f, 0.2f, 0.12f));
            root.AddComponent<NoteProp>().Init(box, floor, stash.noteSafeFloor, intelNow, stash.noteGivesCode);
        }

        /// <summary>A small valuable to steal and bank: a cash envelope, a gold watch or a stack of casino chips.</summary>
        void MakeValuable(int kind, int value, Vector3 pos, float yaw, Transform parent)
        {
            var root = new GameObject(OutpostLoot.ValuableName(kind));
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float weight;
            Vector3 colliderCentre, colliderSize;
            if (kind == (int)OutpostValuable.Watch)
            {
                Part(root.transform, "Strap", new Vector3(0f, 0.004f, 0f), new Vector3(0.026f, 0.006f, 0.20f), OMat("leather"));
                Part(root.transform, "Face", new Vector3(0f, 0.01f, 0f), new Vector3(0.07f, 0.005f, 0.07f), OMat("gold"), PrimitiveType.Cylinder);
                colliderCentre = new Vector3(0f, 0.008f, 0f); colliderSize = new Vector3(0.07f, 0.016f, 0.20f);
                weight = 0.15f;
            }
            else if (kind == (int)OutpostValuable.Chips)
            {
                string[] colours = { "chipRed", "chipWhite", "chipBlue", "chipGreen" };
                for (int i = 0; i < 4; i++)
                    Part(root.transform, "Chip", new Vector3(0f, 0.006f + i * 0.0125f, 0f), new Vector3(0.085f, 0.006f, 0.085f), OMat(colours[(i + (int)yaw / 90) & 3]), PrimitiveType.Cylinder);
                colliderCentre = new Vector3(0f, 0.025f, 0f); colliderSize = new Vector3(0.085f, 0.05f, 0.085f);
                weight = 0.35f;
            }
            else
            {
                Part(root.transform, "Envelope", new Vector3(0f, 0.008f, 0f), new Vector3(0.24f, 0.016f, 0.16f), OMat("paper"));
                Part(root.transform, "Seal", new Vector3(0f, 0.0175f, 0f), new Vector3(0.04f, 0.0015f, 0.04f), OMat("seal"), PrimitiveType.Cylinder);
                colliderCentre = new Vector3(0f, 0.008f, 0f); colliderSize = new Vector3(0.24f, 0.016f, 0.16f);
                weight = 0.2f;
            }
            var bc = root.AddComponent<BoxCollider>();
            bc.center = colliderCentre; bc.size = colliderSize;
            root.AddComponent<Rigidbody>().mass = 0.3f;
            var pk = root.AddComponent<Pickup>();             // (registers its network id now, in the same order on every machine)
            pk.weight = weight; pk.label = OutpostLoot.ValuableName(kind); pk.bankValue = value;
            var th = root.AddComponent<Throwable>();
            th.impactNoise = 4f; th.stunSeconds = 0.4f;
        }
    }
}

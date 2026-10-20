using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The guard outpost: a room with a locked steel door and a keypad outside, a long one-way window (a mirror from the corridor, an
    /// ordinary window from inside), desks with monitors, lockers, a table with whatever the guards keep lying about (a note about a safe,
    /// weapons, valuables) and one guard on duty (<see cref="PlaceOutpostGuard"/>). The backup does not arrive here: it comes out of the
    /// guards' own elevator, in the corner cell opposite the players' elevator (<see cref="BuildGuestElevator"/>). The room, the furniture
    /// and the places for the things are planned by <see cref="OfficePlanner"/> (<c>OfficePlanner.Outpost.cs</c>); this file builds them:
    ///  - before the NavMesh bake: the dark walls and the lockers (FloorGenerator.Interior.cs);
    ///  - after it: the lock door (FloorGenerator.Doors.cs), the window, the keypad, the alarm lamp and the controller
    ///    (<see cref="BuildOutpostExtras"/>), and, last of all, the guard on duty, the things lying about and the cash in the floor's safe (<see cref="FillOutpost"/>).
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
        readonly List<FloorSafe> floorSafes = new List<FloorSafe>();          // the floor's safes, as they are built (floors 3 and up have several)
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
            outpostCtl = null; outpostDoor = null; outpostDoorIndex = -1; floorSafes.Clear();
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
        public string SafeRoomLabel(int floor) => SafeRoomLabel(floor, 0);

        /// <summary>The number of the room safe number <paramref name="index"/> (0 first) of floor <paramref name="floor"/> stands in, or null if there is no such safe or it cannot be worked out (see above).</summary>
        public string SafeRoomLabel(int floor, int index)
        {
            int intel = intelNow;
            var tm = TowerManager.Instance;
            if (tm != null) intel = tm.IntelSeed;
            if (index < 0 || index >= SafeRules.SafeCount(intel, floor)) return null;
            if (layout != null && layoutFloor == floor)
            {
                var onShow = layout.safes.Find(sp => sp.index == index);                // the floor on show: its own plan
                return onShow != null ? RoomNumbers.LabelOf(layout, onShow.room, floor) : null;
            }
            if (!haveBase || !furnishRooms || Mathf.Abs(cellSize - 18f) > 0.01f) return null;
            int key = floor * SafeRules.MaxSafes + index;
            string cached;
            if (safeLabels.TryGetValue(key, out cached)) return cached.Length == 0 ? null : cached;

            try
            {
                int seed = baseSeedSeen + floor * 7919;
                SizeOfFloor(out int c, out int r);
                var shape = FloorShape.Maze(new System.Random(seed), c, r, loopChance);
                var dry = OfficePlanner.Build(shape.PlanFor(floor, seed, cellSize, wallHeight, intel));
                int count = SafeRules.SafeCount(intel, floor);
                for (int i = 0; i < count; i++)
                {
                    var spot = dry.safes.Find(sp => sp.index == i);
                    safeLabels[floor * SafeRules.MaxSafes + i] = spot != null ? (RoomNumbers.LabelOf(dry, spot.room, floor) ?? "") : "";
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Outpost] Could not work out where the safes of floor " + floor + " are: " + e.Message);
            }
            if (!safeLabels.TryGetValue(key, out cached)) { safeLabels[key] = ""; return null; }
            return cached.Length == 0 ? null : cached;
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
                case "alarmButton": m = Surface("OpAlarmButton", new Color(0.82f, 0.05f, 0.04f), 0.6f, false, 0.1f); break;
                case "hazard": m = Surface("OpHazard", new Color(0.95f, 0.75f, 0.08f), 0.3f, false); break;
                case "label": m = Surface("OpLabel", new Color(0.88f, 0.88f, 0.84f), 0.2f, false); break;
                case "bill": m = Surface("OpBill", new Color(0.45f, 0.66f, 0.45f), 0.1f, false); break;
                case "band": m = Surface("OpBand", new Color(0.86f, 0.80f, 0.52f), 0.1f, false); break;
                case "postit": m = Surface("OpPostIt", new Color(0.90f, 0.80f, 0.30f), 0.05f, true); break;       // (it glows a little: a post-it is meant to be seen in a dim room)
                case "screen": m = Surface("OpScreen", new Color(0.10f, 0.22f, 0.40f), 0f, true); break;
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

        // ------------------------------------------------------------------ the guard on duty

        /// <summary>
        /// The one guard who works in the outpost: he stands at his post facing the window, and he is blind and deaf to everything outside the
        /// room (<see cref="GuardAI.noticeGate"/>): a player is only noticed once they are inside it, which takes the door open. A player who gets in has
        /// to knock him out (melee, the pew, thrown things) before his two seconds of watching are up (backup) or he catches them (a pin).
        /// Made after the other guards and without the floor's random numbers, so the patrols, the loot and the maze stay what they were.
        /// Returns false when there was nowhere for him to stand.
        /// </summary>
        bool PlaceOutpostGuard(int floor)
        {
            var op = layout != null ? layout.outpost : null;
            if (op == null) return false;
            if (!NavMesh.SamplePosition(new Vector3(op.room.wx, 0f, op.room.wz), out var hit, 3f, NavMesh.AllAreas)) return false;
            var ai = SpawnGuard("Guard_Outpost", new List<Vector3> { hit.position }, floor * 5 + 2);
            var room = op.bounds;
            // blind and deaf outside the room while the door is locked; once it is unlocked (hacked) he notices what a guard notices anywhere and can leave the outpost to chase it, then walks back to his post
            // (the owner, 2026-10-17 night: "The outpost guard should be able to leave the outpost ... players should try to lure the guard out of the outpost after hacking the door")
            ai.noticeGate = p => room.Contains(p.x, p.z) || (outpostCtl != null && outpostCtl.Unlocked);
            ai.StandGuard(new Vector3(op.frontX, 0f, op.frontZ));       // looking out of the window
            return true;
        }

        /// <summary>The inside of the guard outpost on the floor on show (world rectangle), for tests and tools.</summary>
        public bool TryGetOutpostRoom(out Rc room)
        {
            var op = layout != null ? layout.outpost : null;
            room = op != null ? op.bounds : default(Rc);
            return op != null;
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
            if (op == null && floorSafes.Count == 0) return;

            var root = new GameObject("Outpost");
            root.transform.SetParent(current, false);
            var ctl = root.AddComponent<OutpostController>();
            outpostCtl = ctl;
            ctl.Floor = floor;
            ctl.RunSeed = intelNow;
            ctl.Safes.AddRange(floorSafes);

            if (op != null)
            {
                BuildOneWayWindow(op, root.transform);
                BuildDoorTrim(op, root.transform);
                if (outpostDoor != null)
                {
                    BuildKeypad(op, ctl, root.transform);
                    BuildBeacon(op, ctl, root.transform);
                    ctl.Door = outpostDoor;
                    ctl.RoomBounds = op.bounds; ctl.HasRoomBounds = true;
                    ctl.DoorIndex = outpostDoorIndex;
                }
                // (the alarm-off button inside the outpost is gone: the owner asked for it removed, so the guards chasing the players cannot be called off)
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

        /// <summary>
        /// The alarm panel inside the room, on the wall beside the door: a steel box with a big red button (yellow ring), two status lamps and
        /// the words ALARM OFF. E on it switches the siren off and calls the backup off (see <see cref="AlarmPanelProp"/>). Its lamps are green
        /// while all is quiet and flash red while the alarm or the backup is going.
        /// </summary>
        void BuildAlarmPanel(OutpostInfo op, OutpostController ctl, Transform parent)
        {
            var inward = new Vector3(-op.frontX, 0f, -op.frontZ);
            var pos = new Vector3(op.panelX, op.panelY, op.panelZ);
            var root = new GameObject("AlarmPanel");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(inward, Vector3.up));   // (its +z points out of the wall, into the room)

            var housing = Part(root.transform, "Housing", new Vector3(0f, 0f, 0.04f), new Vector3(0.30f, 0.44f, 0.08f), OMat("safeSteel"));
            var lamps = new List<Renderer>();
            foreach (float x in new[] { -0.09f, 0.09f })
                lamps.Add(Part(root.transform, "Lamp", new Vector3(x, 0.175f, 0.083f), new Vector3(0.04f, 0.03f, 0.01f), OMat("led")).GetComponent<Renderer>());

            // the button: a yellow ring, a red dome (cylinders lie along y, so they are turned to point out of the wall)
            var ring = Part(root.transform, "Ring", new Vector3(0f, 0.01f, 0.088f), new Vector3(0.20f, 0.008f, 0.20f), OMat("hazard"), PrimitiveType.Cylinder);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(root.transform, "Button", new Vector3(0f, 0.01f, 0.1f), new Vector3(0.13f, 0.13f, 0.07f), OMat("alarmButton"), PrimitiveType.Sphere);

            DotLabel(root.transform, "Label", "ALARM OFF", 0.0075f, new Vector3(0f, -0.155f, 0.0815f), OMat("label"));

            var box = housing.GetComponent<Renderer>().bounds;
            box.Expand(0.1f);
            root.AddComponent<AlarmPanelProp>().Init(ctl, box);

            ctl.PanelPosition = pos + inward * 0.1f;
            ctl.PanelLamps = lamps.ToArray();
        }

        /// <summary>Words in the dot lettering as one little mesh facing +z of <paramref name="parent"/> (read from the front, which is looking along -z).</summary>
        static void DotLabel(Transform parent, string name, string text, float dot, Vector3 localCentre, Material mat)
        {
            var runs = new List<DotText.Run>();
            DotText.AddRuns(text, 0, 0, runs);
            if (runs.Count == 0) return;
            var verts = new List<Vector3>(runs.Count * 4);
            var normals = new List<Vector3>(runs.Count * 4);
            var tris = new List<int>(runs.Count * 6);
            float left = -DotText.Columns(text) * dot * 0.5f, top = DotText.Rows * dot * 0.5f;
            var right = Vector3.left;                       // (somebody who looks along -z has +x on his left)
            foreach (var run in runs)
            {
                float a = left + run.col * dot, b = left + (run.col + run.len) * dot;
                var c = right * ((a + b) * 0.5f) + Vector3.up * (top - run.row * dot - dot * 0.5f);
                AddQuad(c, right, Vector3.up, (b - a) * 0.5f, dot * 0.5f, Vector3.forward, verts, normals, tris);
            }
            var mesh = new Mesh { name = "DotLabel" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<OwnedMesh>().mesh = mesh;
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
            int index = 0;                                                           // (which of the floor's safes this piece is: the planner numbered them)
            if (layout != null)
                for (int i = 0; i < layout.safes.Count; i++)
                {
                    var spot = layout.safes[i];
                    if (ReferenceEquals(spot.piece, p) || (Mathf.Abs(spot.x - p.x) < 0.01f && Mathf.Abs(spot.z - p.z) < 0.01f)) { index = spot.index; break; }
                }
            var mats = new FloorSafe.Mats
            {
                steel = OMat("safeSteel"), dark = OMat("dark"), brass = OMat("brass"), lining = OMat("lining"), led = OMat("led")
            };
            var safe = FloorSafe.Build(parent, p.x, p.z, p.yaw, floor, index, SafeRules.Code(intelNow, floor, index), mats);
            floorSafes.Add(safe);
            return safe.gameObject;
        }

        // ------------------------------------------------------------------ what the guards keep (after everything else)

        /// <summary>How many loose melee weapons a floor has (the owner: floor 1 always one and sometimes two; floors 2-12 a chance of one, never two).</summary>
        public static int LooseMeleeCount(int runSeed, int floor)
        {
            uint roll = SafeRules.Hash(runSeed, floor, 701) % 100U;
            if (floor <= 1) return roll < 35U ? 2 : 1;
            return roll < 50U ? 1 : 0;
        }

        /// <summary>Lays the floor's loose melee weapons (crowbar, bat, hatchet) on the floor at patrol spots. Uses only the run seed, never the floor's own random numbers.</summary>
        void FillLooseMelee(int floor)
        {
            if (layout == null || !Application.isPlaying || !MeleeWeapon.Available) return;
            int count = 0;                                  // (retired: weapons are only on the outpost's desk now, see OutpostLoot.Roll)
            var spots = new System.Collections.Generic.List<float[]>();
            foreach (var w in layout.waypoints) if (!InOutpost(new Vector3(w[0], 0f, w[1]))) spots.Add(w);
            for (int i = 0; i < count && spots.Count > 0; i++)
            {
                int pick = (int)(SafeRules.Hash(intelNow, floor, 710 + i) % (uint)spots.Count);
                var w = spots[pick];
                spots.RemoveAt(pick);
                var kind = (MeleeWeapon.Kind)(int)(SafeRules.Hash(intelNow, floor, 720 + i) % 3U);
                float yaw = SafeRules.Hash(intelNow, floor, 730 + i) % 360U;
                MeleeWeapon.Create(kind, new Vector3(w[0], 0.08f, w[1]), current, yaw);
            }
        }

        /// <summary>
        /// The gun safe on the outpost's desk (the owner, 2026-10-18: "The pistol should be in a gun safe in the Outpost that you need to hack to get into"): a steel box with a hinged door, a keypad and a lamp. The
        /// pew lies inside on the lining, out of the world until the safe is open (it is made now, so it has its network id like everything else): <see cref="GunSafeProp"/>.
        /// </summary>
        void BuildGunSafe(Vector3 at, float yaw, Transform parent, int floor)
        {
            var root = new GameObject("GunSafe");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var steel = OMat("safeSteel"); var dark = OMat("dark"); var lining = OMat("lining");
            const float w = 0.56f, h = 0.36f, d = 0.40f, t = 0.03f;                        // the outside size and the thickness of the walls
            Part(root.transform, "Bottom", new Vector3(0f, t * 0.5f, 0f), new Vector3(w, t, d), steel, PrimitiveType.Cube, true);
            Part(root.transform, "Top", new Vector3(0f, h - t * 0.5f, 0f), new Vector3(w, t, d), steel);
            Part(root.transform, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f + t * 0.5f), new Vector3(w, h, t), steel, PrimitiveType.Cube, true);
            Part(root.transform, "SideL", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), steel);
            Part(root.transform, "SideR", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), steel);
            Part(root.transform, "Lining", new Vector3(0f, t + 0.005f, 0f), new Vector3(w - 2f * t, 0.01f, d - 2f * t), lining);
            // the door hangs on its left edge (the hinge turns it); the keypad, the lamp and the handle are on its face
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, d * 0.5f - t * 0.5f);
            Part(hinge, "Door", new Vector3((w - t) * 0.5f, 0f, 0f), new Vector3(w - t, h - 0.01f, t), steel, PrimitiveType.Cube, true);
            Part(hinge, "Keypad", new Vector3(w * 0.72f, 0.02f, t * 0.5f + 0.006f), new Vector3(0.11f, 0.15f, 0.012f), dark);
            Part(hinge, "Lamp", new Vector3(w * 0.72f, 0.115f, t * 0.5f + 0.012f), new Vector3(0.03f, 0.03f, 0.012f), OMat("led"));
            Part(hinge, "Handle", new Vector3(w * 0.28f, 0f, t * 0.5f + 0.02f), new Vector3(0.06f, 0.06f, 0.035f), OMat("brass"), PrimitiveType.Sphere);
            // the pistol inside (dormant: it has its id already, like the cash in the floor safe)
            var pew = RangedWeapon.Create(RangedWeapon.Kind.Pew, at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, t + 0.06f, 0.02f), parent, yaw);
            pew.SetActive(false);
            var prop = root.AddComponent<GunSafeProp>();
            prop.Init(outpostCtl, hinge, pew.GetComponent<Pickup>(), (int)(SafeRules.Hash(intelNow, floor, 910) & 0x7FFFFFFF));
            if (outpostCtl != null) outpostCtl.GunSafe = prop;
        }

        /// <summary>The open hard case the floor-3 pew pistol lay in (before the gun safe took its place): a dark shell, red foam, the lid standing open at the back and two brass latches. Decoration, except for one thin box collider under the foam so that the pistol (a rigid body) lies on it.</summary>
        void BuildPewCase(Vector3 at, float yaw, Transform parent)
        {
            var root = new GameObject("PewCase");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            Part(root.transform, "Shell", new Vector3(0f, 0.035f, 0f), new Vector3(0.52f, 0.07f, 0.32f), OMat("dark"));
            Part(root.transform, "Foam", new Vector3(0f, 0.074f, 0f), new Vector3(0.46f, 0.02f, 0.26f), OMat("alarmButton"));
            var lid = Part(root.transform, "Lid", new Vector3(0f, 0.19f, -0.19f), new Vector3(0.52f, 0.28f, 0.03f), OMat("dark"));
            lid.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Part(root.transform, "LidFoam", new Vector3(0f, 0.19f, -0.172f), new Vector3(0.46f, 0.23f, 0.01f), OMat("alarmButton")).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            foreach (float x in new[] { -0.14f, 0.14f }) Part(root.transform, "Latch", new Vector3(x, 0.045f, 0.165f), new Vector3(0.05f, 0.03f, 0.012f), OMat("brass"));
            // (the pistol is a rigid body: it has to land on the foam. Without something to land on it fell through the case onto the desk and lay hidden inside the shell, the open case looked empty)
            var bed = root.AddComponent<BoxCollider>();
            bed.center = new Vector3(0f, 0.05f, 0f);
            bed.size = new Vector3(0.46f, 0.07f, 0.26f);
        }

        /// <summary>
        /// Puts the outpost's things on its table and desk, and the cash (floors 1 and 2) or the pistols (floor 3 and up) in the floor's safes (and sometimes a gold watch in any of them). What there is follows from <see cref="OutpostLoot.Roll"/>
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

                var computerSlot = slots.Find(s => s.kind == "computer");
                if (computerSlot != null) MakeComputer(computerSlot, stash, floor, parent);
                else Debug.LogWarning("[Outpost] The outpost of floor " + floor + " has no place for its computer.");
                foreach (int w in stash.weapons)
                {
                    bool pew = w == OutpostLoot.PewKind;
                    if (pew ? !RangedWeapon.Available : !MeleeWeapon.Available) continue;
                    var s = NextItemSlot();
                    if (s == null) break;
                    var at = new Vector3(s.x, s.y + 0.08f, s.z);
                    if (pew) BuildGunSafe(new Vector3(s.x, s.y, s.z), s.yaw, parent, floor);          // (the pistol is in a gun safe that has to be hacked open: GunSafeProp; a yaw is given, nothing here uses a random number)
                    else MeleeWeapon.Create((MeleeWeapon.Kind)w, at, parent, s.yaw);
                }
                for (int i = 0; i < stash.valuableKinds.Count; i++)
                {
                    var s = NextItemSlot();
                    if (s == null) break;
                    MakeValuable(stash.valuableKinds[i], stash.valuableValues[i], new Vector3(s.x, s.y + 0.03f, s.z), s.yaw, parent);
                }
            }
            floorSafes.Sort((a, b) => a.Index.CompareTo(b.Index));                 // (the same order on every machine: the things made now take their network ids in it)
            foreach (var safe in floorSafes)
            {
                if (SafeRules.IsWeaponFloor(floor))
                {
                    safe.FillWithGuns(parent, SafeRules.WeaponContents(intelNow, floor, safe.Index));          // (floor 3 and up: a pistol in every safe...)
                    safe.FillWithExtras(parent, SafeRules.HasSideCash(intelNow, floor, safe.Index) ? SafeRules.SideCash(floor) : 0, SafeRules.HasSideClip(intelNow, floor, safe.Index),
                                        OMat("bill"), OMat("band"), OMat("dark"), OMat("brass"), SafeRules.ClipKindOf(intelNow, floor, safe.Index));            // (...and sometimes a bit of cash and a spare clip - for the gun in that safe -, each an item of its own)
                }
                else if (safe.Index == 0)
                {
                    var values = SafeRules.Split(SafeRules.Cash(floor), SafeRules.Bundles(floor));
                    safe.FillWithCash(parent, values, OMat("bill"), OMat("band"));
                }
                if (SafeRules.HasSideWatch(intelNow, floor, safe.Index))
                {
                    // (any safe, cash or weapons: sometimes a gold watch - the same thing as the ones on the desks, an item of its own and the last thing made for this safe)
                    var watchAt = safe.WatchSpot(out float watchYaw);
                    safe.AddWatch(BuildValuable(ValuableKind.Watch, SafeRules.SideWatchValue(floor), watchAt, watchYaw, parent));
                }
            }
        }


        /// <summary>
        /// The outpost's computer on its desk (the owner, 2026-10-17: the safes' room and code are no longer on a note, "a player should have to use the computer"): a flat monitor on a stand and a
        /// keyboard, with a post-it stuck to the lower right corner of the monitor that says the user name and the password (dot lettering, a centimetre and a half tall). Point at it and press E
        /// (<see cref="ComputerProp"/>). What it has on record is what the note used to say (<see cref="OutpostLoot.Roll"/>).
        /// </summary>
        void MakeComputer(Slot s, OutpostStash stash, int floor, Transform parent)
        {
            var root = new GameObject("Computer");
            root.transform.SetParent(parent, false);
            float lift = s.y < 0.2f ? 0.75f : 0f;                                          // (a place on the floor, not on a desk: it gets a little table)
            root.transform.SetPositionAndRotation(new Vector3(s.x, s.y + lift + 0.004f, s.z), Quaternion.Euler(0f, s.yaw, 0f));
            var dark = OMat("dark");
            if (lift > 0f) Part(root.transform, "Table", new Vector3(0f, -lift * 0.5f, 0f), new Vector3(0.9f, lift, 0.6f), OMat("locker"), PrimitiveType.Cube, true);
            Part(root.transform, "Base", new Vector3(0f, 0.008f, -0.17f), new Vector3(0.20f, 0.016f, 0.14f), dark);
            Part(root.transform, "Neck", new Vector3(0f, 0.075f, -0.20f), new Vector3(0.045f, 0.12f, 0.03f), dark);
            var frame = Part(root.transform, "Frame", new Vector3(0f, 0.285f, -0.20f), new Vector3(0.44f, 0.30f, 0.035f), dark);
            Part(root.transform, "Screen", new Vector3(0f, 0.285f, -0.1805f), new Vector3(0.40f, 0.26f, 0.004f), OMat("screen"));
            var keyboard = Part(root.transform, "Keyboard", new Vector3(0f, 0.012f, 0.10f), new Vector3(0.34f, 0.024f, 0.12f), dark);
            Part(root.transform, "Keys", new Vector3(0f, 0.026f, 0.10f), new Vector3(0.31f, 0.004f, 0.09f), OMat("keycap"));

            // the post-it on the monitor's frame, turned a little, with four lines of dot lettering (capitals and digits: see ComputerRules)
            var login = ComputerRules.CredentialsFor(intelNow, floor);
            var pivot = new GameObject("PostIt").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0.135f, 0.2125f, -0.1795f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, -4f);
            Part(pivot, "PostItPaper", Vector3.zero, new Vector3(0.135f, 0.125f, 0.003f), OMat("postit"));
            var ink = OMat("ink");
            const float zText = 0.0022f;
            DotLabel(pivot, "PostItUser", "USER", 0.0022f, new Vector3(0f, 0.047f, zText), ink);
            DotLabel(pivot, "PostItUserName", login.user, 0.0031f, new Vector3(0f, 0.027f, zText), ink);
            DotLabel(pivot, "PostItPass", "PASS", 0.0022f, new Vector3(0f, 0.0045f, zText), ink);
            DotLabel(pivot, "PostItPassWord", login.pass, 0.0031f, new Vector3(0f, -0.0155f, zText), ink);

            var box = frame.GetComponent<Renderer>().bounds;
            box.Encapsulate(keyboard.GetComponent<Renderer>().bounds);
            box.Expand(new Vector3(0.12f, 0.18f, 0.12f));
            root.AddComponent<ComputerProp>().Init(box, floor, stash.noteSafeFloor, intelNow, stash.noteGivesCode);
            BuildMicrophone(root.transform);                                               // (the PA's microphone, next to the computer)
        }

        /// <summary>A small valuable of an outpost: a cash envelope, a gold watch or a stack of casino chips (the same things, made by the same code, as the ones on the desks: see <see cref="BuildValuable"/>).</summary>
        void MakeValuable(int kind, int value, Vector3 pos, float yaw, Transform parent) =>
            BuildValuable((ValuableKind)Mathf.Clamp(kind, 0, 2), value, pos, yaw, parent);
    }
}
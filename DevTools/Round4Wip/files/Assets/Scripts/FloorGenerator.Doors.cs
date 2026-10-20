using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The doors of an office floor: a sliding door in every doorway of the room partitions (the doorways have a beam over them, see
    /// OfficePlanner.WallRun). The wide double doors of the cross aisles - the main hallway doors - have windows in them, so they can be told from a
    /// room's door (<see cref="DressHallDoor"/>). Players open and close them with E; officers open the ones they are about to walk through (patrolling, investigating or chasing). They are built after the
    /// NavMesh bake, so the bake sees every doorway open and the guards' routes never depend on a door. The doors themselves live in
    /// <see cref="DoorManager"/>.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Doors")]
        [Tooltip("Sliding doors in the doorways between rooms: a player opens one with E and closes it with E again; officers open the ones they are about to walk through (not the ones they merely walk past). They make door noises.")]
        public bool slidingDoors = true;

        Material doorMat;

        /// <summary>Metal-grey, copied from the wall material so it follows whatever shader the project uses.</summary>
        Material DoorMat()
        {
            if (doorMat) return doorMat;
            if (!wallMat) return null;
            doorMat = new Material(wallMat) { hideFlags = HideFlags.HideAndDontSave };
            if (doorMat.HasProperty("_Color")) doorMat.color = new Color(0.50f, 0.58f, 0.66f);
            return doorMat;
        }

        void DestroyDoorMaterial()
        {
            if (doorMat) Kill(doorMat);
            if (hallGlassMat) Kill(hallGlassMat);
            if (hallBarMat) Kill(hallBarMat);
            if (hallGlassTex) Kill(hallGlassTex);
            if (hallLeafMesh) Kill(hallLeafMesh);
            if (glassLeafMesh) Kill(glassLeafMesh);
            hallLeafMesh = null;
            hallGlassMat = hallBarMat = null; hallGlassTex = null;
        }

        void BuildDoors()
        {
            if (!slidingDoors || layout == null || !Application.isPlaying) return;   // (the editor tools that build floors do not need doors)

            var holder = new GameObject("Doors");
            holder.transform.SetParent(current, false);
            var manager = holder.AddComponent<DoorManager>();
            var mat = DoorMat();
            var byBeam = new Dictionary<WallBox, SlidingDoor>();
            foreach (var w in layout.walls)
            {
                if (w.kind == "header")
                {
                    var door = MakeDoor(holder.transform, w, mat);
                    if (IsDeadDoor(w)) { door.Locked = true; door.Permanent = true; }       // (a restroom: the door never opens)
                    byBeam[w] = door;
                    if (door.Wide) DressHallDoor(door);                    // the main hallway doors get windows
                    else if (InGlassOffice(w)) DressGlassDoor(door);       // the doors of the offices with a glass front are glass
                    manager.Add(door);
                }
                else if (w.kind == "lockheader")
                {
                    // the guard outpost's front door: dark steel with wired-glass panes; the controller (BuildOutpostExtras) keeps it locked
                    outpostDoor = MakeOutpostDoor(holder.transform, w);
                    byBeam[w] = outpostDoor;
                    outpostDoorIndex = manager.Count;
                    manager.Add(outpostDoor);
                }
            }
            if (manager.Count == 0) { Kill(holder); return; }
            try { ApplyDoorSlides(byBeam); }
            catch (System.Exception e) { Debug.LogWarning("[Doors] The doors narrow into their jambs as before (their slide plan failed): " + e.Message); }      // (decoration: never stops a floor)
            manager.OpenDoorsWithPeople();
        }

        /// <summary>Is this doorway one of the restrooms (the planner marked it: Layout.deadDoors)?</summary>
        bool IsDeadDoor(WallBox beam)
        {
            var area = beam.rc.Grow(0.05f);
            for (int i = 0; i < layout.deadDoors.Count; i++) if (layout.deadDoors[i].Overlaps(area)) return true;
            return false;
        }

        /// <summary>How many doors of the floor slide into the wall beside them, and how many swing open instead because their walls have no room for their leaves (see <see cref="DoorSlide"/>).</summary>
        public int DoorSlides { get; private set; }
        public int DoorSwings { get; private set; }

        /// <summary>The doors slide into the wall (the owner, 2026-10-17): each one gets the plan <see cref="DoorSlide"/> works out from the walls beside it.</summary>
        void ApplyDoorSlides(Dictionary<WallBox, SlidingDoor> doorsByBeam)
        {
            var beams = new List<WallBox>();
            var plans = DoorSlide.ForLayout(layout, wallHeight, OverlaySignOf, beams);
            DoorSlides = 0; DoorSwings = 0;
            for (int i = 0; i < beams.Count; i++)
            {
                if (!doorsByBeam.TryGetValue(beams[i], out var door)) continue;
                if (plans[i].slide) { door.SetSlide(plans[i]); DoorSlides++; }
                else { door.SetSwing(plans[i].swing); DoorSwings++; }          // (a door that has no room to slide swings open: the owner, 2026-10-17)
            }
        }

        /// <summary>For a glass front: which side of the wall (+1 / -1 across it) a door leaf may slide in front of it on (the corridor's); 0 for a solid wall.</summary>
        int OverlaySignOf(WallBox w)
        {
            if (!windowedWalls.Contains(w) || !windowedOutward.TryGetValue(w, out var outward)) return 0;
            float across = w.rc.W >= w.rc.D ? outward.z : outward.x;
            return across >= 0f ? 1 : -1;
        }

        /// <summary>One door in the doorway under a beam: the beam says where the doorway is, how wide and how high.</summary>
        SlidingDoor MakeDoor(Transform parent, WallBox beam, Material mat)
        {
            var rc = beam.rc;
            bool alongX = rc.W >= rc.D;
            float gap = alongX ? rc.W : rc.D, depth = alongX ? rc.D : rc.W, height = beam.y0;

            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(rc.Cx, 0f, rc.Cz), alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f));

            // what closes the doorway: only switched on while the door is shut (SlidingDoor)
            var blocker = go.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0f, height * 0.5f, 0f);
            blocker.size = new Vector3(gap, height, depth);

            return new SlidingDoor(go.transform, Leaf("LeafLow", go.transform, mat), Leaf("LeafHigh", go.transform, mat), blocker, alongX, gap, height, depth);
        }

        // ------------------------------------------------------------------ the main hallway doors have windows

        Material hallGlassMat, hallBarMat;
        Texture2D hallGlassTex;

        // (measured in a leaf's own unit cube, -0.5 = the floor, +0.5 = the top of the 2.45 m door; sideways, a fraction of the leaf's width). The windows are REAL openings
        // in the leaf (MakeHallLeafMesh): 1.15 m to 2.2 m up, two to a leaf; the owner, 2026-10-17: "make the windows on the hallway doors see through".
        const float HallWindowY = 0.1837f, HallWindowW = 0.30f, HallWindowH = 0.4286f, HallWindowX = 0.25f;
        const float HallBarY = -0.112f, HallBarW = 0.76f, HallBarH = 0.017f;

        /// <summary>
        /// A main hallway door: the wide double door that carries a cross aisle through a long room (the 2 m doorways of the rooms are not
        /// dressed). Each leaf is a frame with two tall openings, a faint bluish pane with two reflections in each, and a push bar, on both faces, so that
        /// a player can tell a hallway door from a room's door at a glance. The panes are flat pictures hung in the openings (like the
        /// panes of the guard outpost's door): they show and hide with the leaf, and what is behind a shut door stays hidden.
        /// </summary>
        void DressHallDoor(SlidingDoor door)
        {
            try
            {
                var frame = HallLeafMesh();
                door.LowLeaf.GetComponent<MeshFilter>().sharedMesh = frame;           // (the leaves are frames with real openings: a player looks through the windows)
                door.HighLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                var glass = HallGlassMat();
                var bar = HallBarMat();
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = side < 0 ? door.LowLeaf : door.HighLeaf;
                    for (int face = -1; face <= 1; face += 2)
                    {
                        door.AddDecor(side, DoorPicture(leaf, face, -HallWindowX, HallWindowY, HallWindowW, HallWindowH, 0.03f, glass));
                        door.AddDecor(side, DoorPicture(leaf, face, HallWindowX, HallWindowY, HallWindowW, HallWindowH, 0.03f, glass));
                        door.AddDecor(side, DoorPicture(leaf, face, 0f, HallBarY, HallBarW, HallBarH, 0.05f, bar));
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Doors] Could not put the windows in a hallway door (it stays a plain steel one): " + e.Message);      // (decoration: never stops a floor)
            }
        }

        /// <summary>A flat picture on a door leaf (see <see cref="Decal"/>, which is the same but always in the middle of the leaf): <paramref name="cx"/> and <paramref name="w"/> are fractions of the leaf's width.</summary>
        static Renderer DoorPicture(Transform leaf, int face, float cx, float cy, float w, float h, float lift, Material mat)
        {
            var r = Decal(leaf, face, cy, w, h, lift, mat);
            var t = r.transform;
            t.localPosition = new Vector3(cx, t.localPosition.y, t.localPosition.z);
            return r;
        }

        Material HallGlassMat()
        {
            if (hallGlassMat) return hallGlassMat;
            hallGlassTex = MakeHallGlassTexture();
            // an unlit, see-through picture (Sprites/Default blends by the texture's alpha): the glass dims and tints what is behind it a little and shows two reflections
            hallGlassMat = new Material(Shader.Find("Sprites/Default")) { name = "HallGlass", mainTexture = hallGlassTex, hideFlags = HideFlags.HideAndDontSave };
            return hallGlassMat;
        }

        Material HallBarMat() => hallBarMat ? hallBarMat : (hallBarMat = Surface("HallPushBar", new Color(0.78f, 0.80f, 0.84f), 0.7f, false, 0.85f));

        /// <summary>Dark blue-green glass, lighter towards the top, with two soft diagonal reflections, in a dark steel frame.</summary>
        static Texture2D MakeHallGlassTexture()
        {
            const int w = 32, h = 128;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;                      // v = 0 at the bottom
                    float r = Mathf.Lerp(0.10f, 0.30f, v), g = Mathf.Lerp(0.22f, 0.52f, v), b = Mathf.Lerp(0.29f, 0.64f, v);
                    float t1 = Mathf.Abs(u + v * 0.55f - 0.60f), t2 = Mathf.Abs(u + v * 0.55f - 1.08f);
                    float streak = Mathf.Clamp01(1f - t1 / 0.09f) * 0.34f + Mathf.Clamp01(1f - t2 / 0.035f) * 0.26f;
                    r += streak * 0.80f; g += streak * 0.85f; b += streak * 0.90f;
                    float alpha = Mathf.Clamp01(Mathf.Lerp(0.14f, 0.26f, v) + streak * 0.55f);          // mostly clear: a player sees through the window
                    px[y * w + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(alpha * 255f));
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "HallGlass", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        Mesh hallLeafMesh;

        Mesh HallLeafMesh() => hallLeafMesh ? hallLeafMesh : (hallLeafMesh = MakeHallLeafMesh());

        /// <summary>
        /// The frame of a hallway door's leaf, in the leaf's own unit cube (x across, y -0.5 = the floor to +0.5 = the top, z the thickness): a solid lower panel, a top rail and
        /// the stiles between the two openings, which have NOTHING in them. (Public and static so that a test can look through it.)
        /// </summary>
        public static Mesh MakeHallLeafMesh()
        {
            float bottom = HallWindowY - HallWindowH * 0.5f, top = HallWindowY + HallWindowH * 0.5f, half = HallWindowW * 0.5f;
            var b = new MeshBuilder();
            b.Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, bottom, 0.5f));                                    // the solid lower panel
            b.Box(new Vector3(-0.5f, top, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));                                        // the top rail
            b.Box(new Vector3(-0.5f, bottom, -0.5f), new Vector3(-HallWindowX - half, top, 0.5f));                       // the outer stile, the one between the openings, the other outer stile
            b.Box(new Vector3(-HallWindowX + half, bottom, -0.5f), new Vector3(HallWindowX - half, top, 0.5f));
            b.Box(new Vector3(HallWindowX + half, bottom, -0.5f), new Vector3(0.5f, top, 0.5f));
            var m = b.ToMesh("HallLeaf");
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        Transform Leaf(string name, Transform door, Material mat)
        {
            var leaf = Box(name, door.position, Vector3.one, mat, door, collider: false);
            leaf.transform.localRotation = Quaternion.identity;
            leaf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return leaf.transform;
        }
    }
}

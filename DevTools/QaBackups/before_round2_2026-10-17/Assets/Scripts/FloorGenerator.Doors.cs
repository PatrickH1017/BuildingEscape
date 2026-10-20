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
            hallGlassMat = hallBarMat = null; hallGlassTex = null;
        }

        void BuildDoors()
        {
            if (!slidingDoors || layout == null || !Application.isPlaying) return;   // (the editor tools that build floors do not need doors)

            var holder = new GameObject("Doors");
            holder.transform.SetParent(current, false);
            var manager = holder.AddComponent<DoorManager>();
            var mat = DoorMat();
            foreach (var w in layout.walls)
            {
                if (w.kind == "header")
                {
                    var door = MakeDoor(holder.transform, w, mat);
                    if (door.Wide) DressHallDoor(door);                    // the main hallway doors get windows
                    manager.Add(door);
                }
                else if (w.kind == "lockheader")
                {
                    // the guard outpost's front door: dark steel with wired-glass panes; the controller (BuildOutpostExtras) keeps it locked
                    outpostDoor = MakeOutpostDoor(holder.transform, w);
                    outpostDoorIndex = manager.Count;
                    manager.Add(outpostDoor);
                }
            }
            if (manager.Count == 0) { Kill(holder); return; }
            manager.OpenDoorsWithPeople();
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

        // (measured in a leaf's own unit cube, -0.5 = the floor, +0.5 = the top of the 2.45 m door; sideways, a fraction of the leaf's width)
        const float HallWindowY = 0.112f, HallWindowW = 0.28f, HallWindowH = 0.43f, HallWindowX = 0.27f;
        const float HallBarY = -0.092f, HallBarW = 0.76f, HallBarH = 0.017f;

        /// <summary>
        /// A main hallway door: the wide double door that carries a cross aisle through a long room (the 2 m doorways of the rooms are not
        /// dressed). Each leaf gets two tall windows (dark glass with a reflection, in a steel frame) and a push bar, on both faces, so that
        /// a player can tell a hallway door from a room's door at a glance. The windows are flat pictures hung on the leaves (like the
        /// panes of the guard outpost's door): they show and hide with the leaf, and what is behind a shut door stays hidden.
        /// </summary>
        void DressHallDoor(SlidingDoor door)
        {
            try
            {
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
            hallGlassMat = Surface("HallGlass", Color.white, 0.9f, false, 0.1f);
            hallGlassTex = MakeHallGlassTexture();
            hallGlassMat.mainTexture = hallGlassTex;
            hallGlassMat.EnableKeyword("_EMISSION");                          // a faint glow, so the windows read as glass in a dark corridor too
            hallGlassMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (hallGlassMat.HasProperty("_EmissionColor")) hallGlassMat.SetColor("_EmissionColor", new Color(0.05f, 0.10f, 0.13f));
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
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) / 0.07f, Mathf.Min(v, 1f - v) / 0.04f);
                    if (edge < 1f) { float f = Mathf.Clamp01(edge); r = Mathf.Lerp(0.10f, r, f); g = Mathf.Lerp(0.11f, g, f); b = Mathf.Lerp(0.13f, b, f); }
                    px[y * w + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), 255);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "HallGlass", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
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

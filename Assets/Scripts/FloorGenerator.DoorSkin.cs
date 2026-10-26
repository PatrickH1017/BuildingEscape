using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The Floor 7 doors (the owner, 2026-10-23: "update the doors on the 7th floor to match the new assets used around the floor such as the walls"): the leaves of the sliding doors are drawn as the POLYGON Office pack's doors, like the walls
    /// and the ceiling around them (see <see cref="WallSkinRules"/> and FloorGenerator.WallSkin). DoorSkinRules (pure code, see Scripts/Interior) says which look a doorway gets and how a panel of the pack fits a leaf; this part puts them there. It
    /// only DRESSES the doors: each leaf is still the unit cube <see cref="SlidingDoor"/> scales, slides and swings, the blocker, the clearances, the sounds and what the officers do are untouched; the cube's own mesh is swapped for an empty one
    /// and the pack's panels hang on the leaf as children (and show and hide with it, see <see cref="SlidingDoor.AddDecor"/>). If anything goes wrong - the pack's doors are not in the project, one measures wrong, an exception - the door is put back
    /// the way it was and keeps its plain leaves (or the hall / glass-office dressing it had), with ONE warning in the console. The guard outpost's door is not touched: its walls are its own dark slate and keep their look too.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Floor 7 doors")]
        [Tooltip("Draw the sliding doors of floor 7 as the doors of the POLYGON Office pack (solid white or wooden doors, glass double doors in the halls and the glass offices). Off = the plain leaves floor 7 had before.")]
        public bool packDoors = true;

        /// <summary>For tests and pictures: how many doors of the floor on show wear the pack's doors, and how many of each look (white, wood, glass).</summary>
        public int PackDoorCount { get; private set; }
        public int PackDoorsWhite { get; private set; }
        public int PackDoorsWood { get; private set; }
        public int PackDoorsGlass { get; private set; }

        Mesh packLeafBlank;
        bool packDoorsBroken;
        readonly Dictionary<SlidingDoor, DoorLook> packLooks = new Dictionary<SlidingDoor, DoorLook>();

        /// <summary>For tests and pictures: the look of a door of the floor on show, if it wears the pack's door (false for the guard outpost's door, for every door of another floor, and when the pack's doors are off).</summary>
        public bool TryGetPackLook(SlidingDoor door, out DoorLook look) => packLooks.TryGetValue(door, out look);

        /// <summary>Called when the doors of a floor are about to be built.</summary>
        void StartPackDoors()
        {
            PackDoorCount = PackDoorsWhite = PackDoorsWood = PackDoorsGlass = 0;
            packDoorsBroken = false;
            packLooks.Clear();
        }

        void DestroyPackDoorAssets()
        {
            if (packLeafBlank) Kill(packLeafBlank);
            packLeafBlank = null;
        }

        /// <summary>Does this floor get the pack's doors? Only when its walls got the pack's pieces (the doors follow the walls).</summary>
        bool PackDoorsWanted() => packDoors && !packDoorsBroken && WallSkinActive && layout != null && DoorSkinRules.AppliesTo(layoutFloor);

        void WarnPackDoors(string id, string what)
        {
            packDoorsBroken = true;
            if (warned.Add("packdoor:" + id)) Debug.LogWarning("[PackDoors] " + what + ", so floor 7 keeps its plain doors.");
        }

        /// <summary>A prop template of the pack's door pieces, measured, and checked to be the piece the rules expect (size). Null (with one warning) when it is not.</summary>
        Proto PackProtoOf(string key, float w, float h, float d)
        {
            if (!PropTable.TryGet(key, out var def)) { WarnPackDoors("table:" + key, "'" + key + "' is not in the prop table"); return null; }
            var pr = GetProto(def);
            if (pr == null) { WarnPackDoors("proto:" + key, "the pack's door '" + key + "' is not in the catalog (Employees Take Revenge > Setup Office Pack adds it)"); return null; }
            if (Mathf.Abs(pr.size.x - w) > 0.05f || Mathf.Abs(pr.size.y - h) > 0.05f || Mathf.Abs(pr.size.z - d) > 0.05f)
            {
                WarnPackDoors("size:" + key, "'" + key + "' measures " + pr.size.x.ToString("0.000") + " x " + pr.size.y.ToString("0.000") + " x " + pr.size.z.ToString("0.000") + " m, not what the door skin was made for");
                return null;
            }
            return pr;
        }

        /// <summary>The theme of the room a doorway belongs to (the room that lists it among its doors; null for a corridor or an open zone).</summary>
        string DoorwayTheme(WallBox beam)
        {
            if (layout == null) return null;
            var area = beam.rc.Grow(0.05f);
            foreach (var room in layout.rooms)
            {
                if (room.kind != 0) continue;
                foreach (var d in room.doors) if (d.Overlaps(area)) return room.theme;
            }
            return null;
        }

        Mesh PackLeafBlank() => packLeafBlank ? packLeafBlank : (packLeafBlank = new Mesh { name = "PackLeafBlank", hideFlags = HideFlags.HideAndDontSave });

        /// <summary>
        /// Draws one door of floor 7 as the pack's door: both leaves, every panel. True when it stands; false (the door exactly as it was) when it could not be done or the floor does not get them.
        /// </summary>
        bool DressPackDoor(SlidingDoor door, WallBox beam)
        {
            if (!PackDoorsWanted()) return false;
            var made = new List<GameObject>();
            var blanked = new List<KeyValuePair<MeshFilter, Mesh>>();
            try
            {
                var look = DoorSkinRules.LookFor(DoorwayTheme(beam), InGlassOffice(beam), door.Wide);
                Proto single = null, left = null, right = null;
                if (look == DoorLook.Glass)
                {
                    left = PackProtoOf(DoorSkinRules.KeyGlassL, DoorSkinRules.LargeW, DoorSkinRules.LargeH, DoorSkinRules.LargeD);
                    right = PackProtoOf(DoorSkinRules.KeyGlassR, DoorSkinRules.LargeW, DoorSkinRules.LargeH, DoorSkinRules.LargeD);
                    if (left == null || right == null) return false;
                }
                else
                {
                    single = PackProtoOf(look == DoorLook.Wood ? DoorSkinRules.KeyWood : DoorSkinRules.KeyWhite, DoorSkinRules.SingleW, DoorSkinRules.SingleH, DoorSkinRules.SingleD);
                    if (single == null) return false;
                }

                float leafW = door.HalfGap - DoorGeometry.Seam;                       // (the width of a leaf of the shut door: SlidingDoor uses the same)
                int n = DoorSkinRules.Panels(leafW, DoorSkinRules.PanelWidth(look));
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = side < 0 ? door.LowLeaf : door.HighLeaf;
                    var mf = leaf.GetComponent<MeshFilter>();
                    blanked.Add(new KeyValuePair<MeshFilter, Mesh>(mf, mf.sharedMesh));
                    mf.sharedMesh = PackLeafBlank();                                      // (the old slab is not drawn any more; its renderer still tells SlidingDoor when the leaf shows)
                    for (int i = 0; i < n; i++)
                    {
                        var pr = look == DoorLook.Glass ? (DoorSkinRules.UseRight(side, i, n) ? right : left) : single;
                        var fit = DoorSkinRules.PanelFit(i, n, DoorSkinRules.Mirrored(look, side), pr.size.x, pr.size.y, pr.size.z, pr.centre.x, pr.centre.y, pr.centre.z);
                        var go = Instantiate(pr.go, leaf);
                        go.name = "PackPanel";
                        made.Add(go);
                        var t = go.transform;
                        t.localPosition = new Vector3(fit.px, fit.py, fit.pz);
                        t.localRotation = Quaternion.identity;
                        t.localScale = Vector3.Scale(t.localScale, new Vector3(fit.sx, fit.sy, fit.sz));
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        {
                            r.shadowCastingMode = ShadowCastingMode.Off;
                            r.receiveShadows = false;
                            door.AddDecor(side, r);                                       // (it shows and hides with the leaf)
                        }
                    }
                }
                PackDoorCount++;
                if (look == DoorLook.White) PackDoorsWhite++; else if (look == DoorLook.Wood) PackDoorsWood++; else PackDoorsGlass++;
                packLooks[door] = look;
                if (look == DoorLook.Glass && !door.Wide) GlassDoors++;                  // (an office with a glass front has a glass door, as before: the count the glass-front checks read)
                return true;
            }
            catch (System.Exception e)
            {
                foreach (var g in made) if (g) Kill(g);
                foreach (var kv in blanked) if (kv.Key) kv.Key.sharedMesh = kv.Value;
                WarnPackDoors("exception", "The pack's door could not be put on a door (" + e.GetType().Name + ": " + e.Message + ")");
                return false;
            }
        }

        /// <summary>One line in the console when a floor got the pack's doors (called when the doors of a floor are built).</summary>
        void LogPackDoors(int doors)
        {
            if (PackDoorCount > 0)
                Debug.Log($"[PackDoors] Floor {layoutFloor}: {PackDoorCount} of {doors} doors are the pack's ({PackDoorsWhite} white, {PackDoorsWood} wood, {PackDoorsGlass} glass)");
        }
    }
}

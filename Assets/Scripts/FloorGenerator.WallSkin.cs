using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The Floor 7 walls and ceiling (the owner, 2026-10-23: "try replacing the walls with the walls from our asset as well as the ceiling. right now the walls seem a little blank"): the wall pieces and the ceiling
    /// tiles of the POLYGON Office pack laid over the floor. WallSkinRules (pure code, see Scripts/Interior) says where every piece goes; this part puts them there. It only DRESSES the floor: the plain boxes of the walls stay
    /// (they are the colliders and what the NavMesh is baked from), the old flat ceiling stays (its colliders keep the camera and thrown things in), and nothing the layout planned changes. If anything goes wrong - the pack is
    /// not in the project, a piece measures wrong, an exception - the pieces are taken down again and the floor has the walls and the ceiling it had before.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Floor 7 walls and ceiling")]
        [Tooltip("Cover the walls and the ceiling of floor 7 with the wall pieces and ceiling tiles of the POLYGON Office pack. Off = the plain painted walls and flat ceiling floor 7 had before.")]
        public bool wallSkin = true;

        /// <summary>For tests and pictures: was the pack skin built on the floor on show, how many wall pieces and ceiling tiles, and the plan it was built from (null when it was not).</summary>
        public bool WallSkinActive { get; private set; }
        public int SkinPieces { get; private set; }
        public int SkinTilesBuilt { get; private set; }
        public SkinPlan SkinPlanOnShow => skinPlan;
        public Transform SkinRoot => skinHolder;

        SkinPlan skinPlan;
        Transform skinHolder;
        readonly Dictionary<string, Material> skinMats = new Dictionary<string, Material>();
        Material skinFlatMat;
        // the boxes of the walls between the cells (BuildShell): with a skin on, the strip of one that shows at a doorway gets the plain paint of the halls
        readonly List<GameObject> shellBoxes = new List<GameObject>();

        const string SkinBaseboard = "skin_wall_baseboard", SkinTrim = "skin_wall_trim", SkinBlank = "skin_wall_blank", SkinBrick = "skin_wall_brick", SkinCeiling = "skin_ceiling_panel";

        void ResetWallSkin()
        {
            WallSkinActive = false; SkinPieces = 0; SkinTilesBuilt = 0;
            skinPlan = null; skinHolder = null;
            shellBoxes.Clear();
        }

        void DestroySkinMaterials()
        {
            foreach (var m in skinMats.Values) if (m) Kill(m);
            skinMats.Clear();
            if (skinFlatMat) Kill(skinFlatMat);
            skinFlatMat = null;
        }

        static string KeyOf(SkinStyle s)
        {
            switch (s)
            {
                case SkinStyle.Baseboard: return SkinBaseboard;
                case SkinStyle.Trim: return SkinTrim;
                case SkinStyle.Blank: return SkinBlank;
                default: return SkinBrick;
            }
        }

        /// <summary>The plain paint of the halls, for what is left of the old walls (a sliver beside a piece, the end of a wall between two cells, the beams over the doors).</summary>
        Material SkinFlatMat()
        {
            if (skinFlatMat) return skinFlatMat;
            if (!wallMat) return null;
            var p = WallSkinRules.PaintOf("hall");
            skinFlatMat = new Material(wallMat) { hideFlags = HideFlags.HideAndDontSave };
            if (skinFlatMat.HasProperty("_Color")) skinFlatMat.color = new Color(p.r, p.g, p.b, 1f);
            return skinFlatMat;
        }

        /// <summary>A copy of the pack's material with a tint, one per colour (the pieces share them).</summary>
        Material SkinMat(Material baseMat, float r, float g, float b)
        {
            string key = baseMat.GetInstanceID() + "|" + r.ToString("0.000") + "," + g.ToString("0.000") + "," + b.ToString("0.000");
            if (skinMats.TryGetValue(key, out var m) && m) return m;
            m = new Material(baseMat) { hideFlags = HideFlags.HideAndDontSave };
            var c = new Color(r, g, b, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            skinMats[key] = m;
            return m;
        }

        /// <summary>A prop template of the pack, measured, and checked to be the piece the plan expects (size and which way it faces). Null (with one warning) when it is not.</summary>
        Proto SkinProtoOf(string key, float length, float height, float depth, bool wallPiece)
        {
            if (!PropTable.TryGet(key, out var def)) { WarnSkinOnce("table:" + key, "'" + key + "' is not in the prop table"); return null; }
            var pr = GetProto(def);
            if (pr == null) { WarnSkinOnce("proto:" + key, "the pack's piece '" + key + "' is not in the catalog (Employees Take Revenge > Setup Office Pack adds it)"); return null; }
            bool sizeOk = Mathf.Abs(pr.size.x - length) < 0.15f && Mathf.Abs(pr.size.y - height) < 0.15f && Mathf.Abs(pr.size.z - depth) < 0.1f;
            bool faceOk = !wallPiece || pr.centre.z > 0.02f;                // (the decorated side of a wall piece is +z of the template: the pack's own way round)
            if (!sizeOk || !faceOk)
            {
                WarnSkinOnce("size:" + key, "'" + key + "' measures " + pr.size.x.ToString("0.00") + " x " + pr.size.y.ToString("0.00") + " x " + pr.size.z.ToString("0.00") + " m and its middle is at z " + pr.centre.z.ToString("0.00") + ", not what the skin was made for");
                return null;
            }
            return pr;
        }

        void WarnSkinOnce(string id, string what)
        {
            if (warned.Add("skin:" + id)) Debug.LogWarning("[WallSkin] " + what + ", so floor 7 keeps its plain walls and ceiling.");
        }

        Renderer FirstRenderer(Proto pr) => pr.go.GetComponentInChildren<Renderer>(true);

        /// <summary>Does this floor get the pack walls and ceiling?</summary>
        bool WallSkinWanted() => wallSkin && layout != null && layout.input != null && WallSkinRules.AppliesTo(layoutFloor);

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// Lays the pack's wall pieces over the walls (after the glass fronts: those keep their own look). True when the skin stands; false (and nothing left behind) when it could not be built.
        /// </summary>
        bool BuildWallSkin(Transform interior, Layout L)
        {
            WallSkinActive = false; SkinPieces = 0; skinPlan = null;
            if (!WallSkinWanted()) return false;
            Transform holder = null;
            try
            {
                var protos4 = new Proto[4];
                var mats = new Material[4];
                for (int i = 0; i < 4; i++)
                {
                    var style = (SkinStyle)i;
                    bool plain = style == SkinStyle.Blank || style == SkinStyle.Brick;
                    protos4[i] = SkinProtoOf(KeyOf(style), WallSkinRules.ModuleLength, WallSkinRules.ModuleHeight, plain ? 0.150f : 0.164f, true);
                    if (protos4[i] == null) return false;
                    var rend = FirstRenderer(protos4[i]);
                    if (!rend || !rend.sharedMaterial) { WarnSkinOnce("mat:" + KeyOf(style), "the pack's piece '" + KeyOf(style) + "' has no material"); return false; }
                    mats[i] = rend.sharedMaterial;
                }

                var plan = WallSkinRules.Plan(L, windowedWalls, doorWidth);
                if (plan.modules.Count == 0) return false;

                holder = new GameObject("WallSkin").transform;
                holder.SetParent(interior, false);
                var cells = new Dictionary<int, Transform>();
                int made = 0;
                foreach (var m in plan.modules)
                {
                    int ci = SkinCellIndex(m.alongX ? (m.a + m.b) * 0.5f : m.line, m.alongX ? m.line : (m.a + m.b) * 0.5f);
                    if (!cells.TryGetValue(ci, out var parent))
                    {
                        parent = new GameObject("Skin_" + (ci % cols) + "_" + (ci / cols)).transform;
                        parent.SetParent(holder, false);
                        cells[ci] = parent;
                    }
                    int si = (int)m.style;
                    PlaceSkinModule(m, protos4[si], mats[si], parent);
                    made++;
                }

                // the old boxes under the pieces and the ends of the walls between the cells take the plain paint of the halls: where a sliver of one shows it matches
                var flat = SkinFlatMat();
                if (flat)
                {
                    foreach (var w in plan.boxes)
                        if (wallObjects.TryGetValue(w, out var go) && go) { var rr = go.GetComponent<Renderer>(); if (rr && rr.enabled) rr.sharedMaterial = flat; }
                    foreach (var go in shellBoxes) { if (!go) continue; var rr = go.GetComponent<Renderer>(); if (rr && rr.enabled) rr.sharedMaterial = flat; }
                    var ow = interior.Find("OfficeWindows/WindowWalls");           // (the piers, sills and heads of the glass fronts)
                    if (ow) { var rr = ow.GetComponent<Renderer>(); if (rr) rr.sharedMaterial = flat; }
                }

                if (Application.isPlaying && propDrawDistance > 0f)
                {
                    var culler = holder.gameObject.AddComponent<PropCuller>();           // (the walls and ceiling are drawn farther out than the furniture: they show at the end of a corridor)
                    culler.Setup(new List<Transform>(cells.Values), propDrawDistance + 30f);
                }

                skinPlan = plan; skinHolder = holder; SkinPieces = made; AccentWalls = plan.accentFaces;
                WallSkinActive = true;
                Debug.Log($"[WallSkin] Floor {layoutFloor}: {made} wall pieces ({plan.perStyle.GetValueOrDefault(SkinStyle.Baseboard)} dado, {plan.perStyle.GetValueOrDefault(SkinStyle.Trim)} skirting, " +
                          $"{plan.perStyle.GetValueOrDefault(SkinStyle.Blank)} plain, {plan.perStyle.GetValueOrDefault(SkinStyle.Brick)} brick), {plan.accentFaces} accent walls");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WallSkin] The pack walls could not be built, so floor 7 keeps its plain walls: " + e);
                if (holder) { holder.gameObject.SetActive(false); Kill(holder.gameObject); }
                WallSkinActive = false; SkinPieces = 0; skinPlan = null; skinHolder = null;
                return false;
            }
        }

        int SkinCellIndex(float x, float z)
        {
            int cx = Mathf.Clamp((int)(x / cellSize), 0, cols - 1), cz = Mathf.Clamp((int)(z / cellSize), 0, rows - 1);
            return cz * cols + cx;
        }

        /// <summary>One piece of wall: the template, stretched along the wall to fit, turned so its decorated face looks the way the wall face does and moved so its flat face stands <see cref="WallSkinRules.Lift"/> off the wall.</summary>
        GameObject PlaceSkinModule(SkinModule m, Proto pr, Material baseMat, Transform parent)
        {
            float st = m.a - (m.capA ? WallSkinRules.EndOver : 0f);
            float en = m.b + (m.capB ? WallSkinRules.EndOver : (m.joinB ? WallSkinRules.JoinOver : 0f));
            float centre = (st + en) * 0.5f;
            float kx = (en - st) / pr.size.x, ky = (m.y1 - m.y0) / pr.size.y;
            bool moulded = m.style == SkinStyle.Baseboard || m.style == SkinStyle.Trim;
            float flat = pr.centre.z + pr.size.z * 0.5f - (moulded ? WallSkinRules.Bump : 0f);          // the flat face of the piece, from the template's pivot
            float across = m.line + m.nrm * WallSkinRules.Lift;
            var anchor = m.alongX ? new Vector3(centre, m.y0, across) : new Vector3(across, m.y0, centre);
            var rot = Quaternion.Euler(0f, m.Yaw, 0f);
            var local = new Vector3(kx * pr.centre.x, ky * pr.minY, flat);
            var go = Instantiate(pr.go, anchor - rot * local, rot, parent);
            go.name = "Wall_" + m.style;
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(kx, ky, 1f));
            var mat = SkinMat(baseMat, m.tr, m.tg, m.tb);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return go;
        }

        // ------------------------------------------------------------------ ceiling

        /// <summary>
        /// The ceiling tiles of the pack under the old flat ceiling (which stays: its colliders keep the camera and thrown things in, and it shows where the tiles are not drawn). Only when the walls got their skin.
        /// A failure takes the tiles down again and leaves the old ceiling as it is.
        /// </summary>
        void BuildCeilingTiles(Transform parent)
        {
            SkinTilesBuilt = 0;
            if (!WallSkinActive || skinPlan == null || skinPlan.tiles.Count == 0) return;
            Transform holder = null;
            try
            {
                var pr = SkinProtoOf(SkinCeiling, 2.5f, 0.039f, 2.5f, false);
                if (pr == null) return;
                var rend = FirstRenderer(pr);
                if (!rend || !rend.sharedMaterial) { WarnSkinOnce("mat:" + SkinCeiling, "the pack's ceiling tile has no material"); return; }
                var baseMat = rend.sharedMaterial;
                holder = new GameObject("CeilingTiles").transform;
                holder.SetParent(parent, false);
                var cells = new Dictionary<int, Transform>();
                float top = wallHeight - CeilingSink;                    // the underside of the old ceiling: the tiles hang from it
                int made = 0;
                foreach (var t in skinPlan.tiles)
                {
                    float cx = (t.x0 + t.x1) * 0.5f, cz = (t.z0 + t.z1) * 0.5f;
                    int ci = SkinCellIndex(cx, cz);
                    if (!cells.TryGetValue(ci, out var cell))
                    {
                        cell = new GameObject("Tiles_" + (ci % cols) + "_" + (ci / cols)).transform;
                        cell.SetParent(holder, false);
                        cells[ci] = cell;
                    }
                    float kx = (t.x1 - t.x0) / pr.size.x, kz = (t.z1 - t.z0) / pr.size.z;
                    var pos = new Vector3(cx - kx * pr.centre.x, top - (pr.minY + pr.size.y), cz - kz * pr.centre.z);
                    var go = Instantiate(pr.go, pos, Quaternion.identity, cell);
                    go.name = "Tile";
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(kx, 1f, kz));
                    var mat = SkinMat(baseMat, t.tr, t.tg, t.tb);
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                        r.receiveShadows = false;
                    }
                    made++;
                }
                if (propDrawDistance > 0f)
                {
                    var culler = holder.gameObject.AddComponent<PropCuller>();
                    culler.Setup(new List<Transform>(cells.Values), propDrawDistance + 10f);
                }
                SkinTilesBuilt = made;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[WallSkin] The ceiling tiles could not be built, so floor 7 keeps its flat ceiling: " + e);
                if (holder) { holder.gameObject.SetActive(false); Kill(holder.gameObject); }
                SkinTilesBuilt = 0;
            }
        }
    }
}

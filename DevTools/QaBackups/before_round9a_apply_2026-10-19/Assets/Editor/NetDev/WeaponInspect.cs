using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Developer tool (the owner, 2026-10-18: "I added more Synty assets for the weapons, please utilize and implement them"): prints how the weapon models of the Synty Battle Royale pack lie - the size and place of every part,
    /// which way the long axis runs and where the thick end is - so that the weapon catalog can turn each one the way the game holds it. Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.WeaponInspect.Run
    /// (the log lines start with [WeaponInspect]).
    /// </summary>
    public static class WeaponInspect
    {
        const string Pack = "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/";

        public static void Run()
        {
            foreach (var n in new[]
            {
                "SM_Wep_Bat_01", "SM_Wep_Crowbar_01", "SM_Wep_Fireaxe_01", "SM_Wep_Machete_01", "SM_Wep_Pickaxe_01", "SM_Wep_Pan_01",
                "SM_Wep_Pistol_Heavy_01", "SM_Wep_Pistol_Revolver_01", "SM_Wep_Revolver_Snub_01", "SM_Wep_Attachment_Silencer_01",
                "SM_Wep_Pistol_Ammo_01", "SM_Wep_Pistol_Ammo_Box_01", "SM_Wep_Pistol_Bullet_01", "SM_Wep_Pistol_Ammo_Pile_01", "SM_Wep_Bullet_Ammo_01"
            })
                Inspect(n);
        }

        /// <summary>
        /// Where the pistol's barrel leaves the slide and what the silencer's two ends look like: the vertices at the front of the heavy pistol (the body and the slide) by depth, and the silencer's rings (radius and
        /// middle by depth). The silencer must sit on the barrel's own line, so this prints the line and the mouth the silencer has to cover.
        /// </summary>
        public static void Muzzle()
        {
            try { MuzzleReport(); }
            finally { EditorApplication.Exit(0); }
        }

        static void MuzzleReport()
        {
            var sb = new StringBuilder();
            // the hierarchies (are there sockets, are the parts turned or scaled?) and the handles of the melee weapons (where is the shaft's own axis, level by level)
            foreach (var n in new[] { "SM_Wep_Pistol_Heavy_01", "SM_Wep_Attachment_Silencer_01", "SM_Wep_Bat_01", "SM_Wep_Crowbar_01", "SM_Wep_Fireaxe_01", "SM_Wep_Pistol_Ammo_01" })
            {
                var g = Spawn(n);
                sb.AppendLine("[WeaponInspect] tree " + n);
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                    sb.AppendLine($"[WeaponInspect]   {(t == g.transform ? "(root)" : t.name)}: local pos ({t.localPosition.x:0.0000}, {t.localPosition.y:0.0000}, {t.localPosition.z:0.0000}) euler ({t.localEulerAngles.x:0.0}, {t.localEulerAngles.y:0.0}, {t.localEulerAngles.z:0.0}) scale ({t.localScale.x:0.000}, {t.localScale.y:0.000}, {t.localScale.z:0.000}) renderer {(t.GetComponent<Renderer>() ? "yes" : "no")}");
                if (n.Contains("Bat") || n.Contains("Crowbar") || n.Contains("Fireaxe"))
                {
                    var mf = g.GetComponent<MeshFilter>();
                    var rows = new System.Collections.Generic.SortedDictionary<int, Bounds>();
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        int key = Mathf.RoundToInt(v.y * 50f);              // every 2 cm along the handle
                        rows[key] = rows.TryGetValue(key, out var bb) ? Grow(bb, v) : new Bounds(v, Vector3.zero);
                    }
                    foreach (var kv in rows)
                        sb.AppendLine($"[WeaponInspect]     y {kv.Key / 50f:0.00}: x {kv.Value.min.x:0.000}..{kv.Value.max.x:0.000}, z {kv.Value.min.z:0.000}..{kv.Value.max.z:0.000}");
                }
                Object.DestroyImmediate(g);
            }
            var pistol = Spawn("SM_Wep_Pistol_Heavy_01");
            float slideFront = -1f;
            foreach (var mf in pistol.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.Contains("Slide")) slideFront = mf.GetComponent<Renderer>().bounds.max.z;
            sb.AppendLine($"[WeaponInspect] pistol: slide front at z {slideFront:0.0000}");
            foreach (var mf in pistol.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !(mf.name.EndsWith("Heavy_01") || mf.name.Contains("Slide"))) continue;
                sb.AppendLine($"[WeaponInspect]   mesh {mf.name} (readable {mf.sharedMesh.isReadable}), vertices in the front {0.03f * 1000f:0} mm before the slide front and beyond, by depth:");
                var levels = new System.Collections.Generic.SortedDictionary<int, Vector4>();     // z in half millimetres -> (count, min/max packed below)
                var box = new System.Collections.Generic.Dictionary<int, Bounds>();
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    if (w.z < slideFront - 0.03f) continue;
                    int key = Mathf.RoundToInt(w.z * 2000f);
                    if (!box.TryGetValue(key, out var bb)) bb = new Bounds(w, Vector3.zero); else bb.Encapsulate(w);
                    box[key] = bb;
                    levels[key] = new Vector4(levels.TryGetValue(key, out var old) ? old.x + 1 : 1, 0, 0, 0);
                }
                foreach (var kv in levels)
                {
                    var bb = box[kv.Key];
                    sb.AppendLine($"[WeaponInspect]     z {kv.Key / 2000f:0.0000}: {kv.Value.x:0} vertices, x {bb.min.x:0.0000}..{bb.max.x:0.0000}, y {bb.min.y:0.0000}..{bb.max.y:0.0000} (middle y {bb.center.y:0.0000})");
                }
            }
            Object.DestroyImmediate(pistol);

            var sil = Spawn("SM_Wep_Attachment_Silencer_01");
            foreach (var mf in sil.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                sb.AppendLine($"[WeaponInspect] silencer mesh {mf.name} (readable {mf.sharedMesh.isReadable}): rings by depth (radius around its own axis)");
                var rings = new System.Collections.Generic.SortedDictionary<int, (int n, float rmin, float rmax, Vector2 sum)>();
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = mf.transform.TransformPoint(v);
                    int key = Mathf.RoundToInt(w.z * 2000f);
                    float r = new Vector2(w.x, w.y).magnitude;
                    var e = rings.TryGetValue(key, out var o) ? o : (0, float.MaxValue, 0f, Vector2.zero);
                    rings[key] = (e.Item1 + 1, Mathf.Min(e.Item2, r), Mathf.Max(e.Item3, r), e.Item4 + new Vector2(w.x, w.y));
                }
                foreach (var kv in rings)
                    sb.AppendLine($"[WeaponInspect]     z {kv.Key / 2000f:0.0000}: {kv.Value.n} vertices, radius {kv.Value.rmin:0.0000}..{kv.Value.rmax:0.0000}, middle ({kv.Value.sum.x / kv.Value.n:0.0000}, {kv.Value.sum.y / kv.Value.n:0.0000})");
            }
            Object.DestroyImmediate(sil);
            Debug.Log(sb.ToString());
        }

        static Bounds Grow(Bounds b, Vector3 p) { b.Encapsulate(p); return b; }

        static GameObject Spawn(string n)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Pack + n + ".prefab"));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go;
        }

        static void Inspect(string n)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + n + ".prefab");
            if (!prefab) { Debug.Log("[WeaponInspect] " + n + ": NOT FOUND"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var sb = new StringBuilder();
            bool any = false; Bounds all = default;
            sb.AppendLine("[WeaponInspect] == " + n + "  (root scale " + prefab.transform.localScale + ")");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                var mf = r.GetComponent<MeshFilter>();
                var mats = new StringBuilder();
                foreach (var m in r.sharedMaterials) mats.Append(m ? m.name + "[" + (m.shader ? m.shader.name : "no shader") + "] " : "null ");
                sb.AppendLine($"[WeaponInspect]    part {r.name}: centre ({b.center.x:0.000}, {b.center.y:0.000}, {b.center.z:0.000}) size ({b.size.x:0.000}, {b.size.y:0.000}, {b.size.z:0.000}) min ({b.min.x:0.000}, {b.min.y:0.000}, {b.min.z:0.000}) max ({b.max.x:0.000}, {b.max.y:0.000}, {b.max.z:0.000}) verts {(mf && mf.sharedMesh ? mf.sharedMesh.vertexCount : 0)} mats {mats}");
                if (!any) { all = b; any = true; } else all.Encapsulate(b);
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) sb.AppendLine("[WeaponInspect]    collider " + c.GetType().Name + " on " + c.name);
            if (any)
            {
                sb.AppendLine($"[WeaponInspect]    ALL: centre ({all.center.x:0.000}, {all.center.y:0.000}, {all.center.z:0.000}) size ({all.size.x:0.000}, {all.size.y:0.000}, {all.size.z:0.000})");
                // which way does it run, and which end is thick: the widest cross-section in each tenth of the long axis
                int axis = all.size.x >= all.size.y && all.size.x >= all.size.z ? 0 : all.size.y >= all.size.z ? 1 : 2;
                var profile = new float[10];
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var w = mf.transform.TransformPoint(v);
                        float along = axis == 0 ? w.x : axis == 1 ? w.y : w.z;
                        int slot = Mathf.Clamp((int)((along - (axis == 0 ? all.min.x : axis == 1 ? all.min.y : all.min.z)) / Mathf.Max(1e-5f, all.size[axis]) * 10f), 0, 9);
                        float a = axis == 0 ? w.y - all.center.y : w.x - all.center.x, c = axis == 2 ? w.y - all.center.y : w.z - all.center.z;
                        if (axis == 1) { a = w.x - all.center.x; c = w.z - all.center.z; }
                        profile[slot] = Mathf.Max(profile[slot], Mathf.Sqrt(a * a + c * c));
                    }
                }
                var line = new StringBuilder();
                foreach (var p in profile) line.Append((p * 100f).ToString("0.0")).Append(' ');
                sb.AppendLine($"[WeaponInspect]    long axis {"xyz"[axis]}; widest radius (cm) in each tenth from the minimum end: {line}");
            }
            Debug.Log(sb.ToString());
            Object.DestroyImmediate(go);
        }
    }
}

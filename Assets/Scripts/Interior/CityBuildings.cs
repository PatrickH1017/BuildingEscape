using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The owner's buildings (the SimplePoly City pack, added 2026-10-19: "use them to make the city outside the windows look more realistic" and "have the city building assets in the background of the
    /// cinematic intro") as pieces of the scenery. A building is placed on the ground in a plot of the street grid: turned, scaled to fit the plot (it is never made bigger than asked, only smaller),
    /// with no colliders, no shadows and no probes.
    /// </summary>
    public static class CityBuildings
    {
        /// <summary>A building that has to be shrunk to less than this share of its size to fit is not placed (the caller puts a box tower there).</summary>
        const float MinFit = 0.72f;

        static GameObject[] low, towers;
        static CityCatalog sortedFrom;

        static void Sort(CityCatalog catalog)
        {
            if (low != null && sortedFrom == catalog) return;
            var l = new List<GameObject>(); var t = new List<GameObject>();
            if (catalog != null && catalog.buildings != null)
                foreach (var b in catalog.buildings)
                {
                    if (b == null) continue;
                    string n = b.name;
                    if (n.Contains("Stadium") || n.Contains("Factory") || n.Contains("Super Market")) continue;      // (wider than a plot of the street grid)
                    l.Add(b);
                    if (n.StartsWith("Building Sky_") || n.StartsWith("Building_Residential")) t.Add(b);          // (the pack's towers)
                }
            low = l.ToArray(); towers = t.ToArray(); sortedFrom = catalog;
        }

        /// <summary>The buildings that suit a plot of the street grid (the pack's wide ones are left out). Empty with no catalog.</summary>
        public static GameObject[] Pool(CityCatalog catalog) { Sort(catalog); return low; }

        /// <summary>The pack's towers (the Sky and Residential buildings), the ones tall enough to be seen from far. Empty with no catalog.</summary>
        public static GameObject[] Towers(CityCatalog catalog) { Sort(catalog); return towers; }

        static Bounds BoundsOf(Renderer[] rs)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static void Reject(GameObject go)
        {
            go.SetActive(false);
            Object.Destroy(go);
        }

        /// <summary>
        /// Stands <paramref name="prefab"/> on the ground at <paramref name="foot"/> (the middle of its base, in world coordinates), turned by <paramref name="yaw"/> degrees and first scaled by
        /// <paramref name="scale"/>; if it is then wider than the plot or taller than <paramref name="maxHeight"/> it is shrunk to fit. Returns null (nothing left behind) when it would have to
        /// shrink too far. <paramref name="skin"/> (may be null) turns each of the pack's materials into the one to draw with; <paramref name="layer"/> -1 leaves the layer alone.
        /// The parent is taken to be unrotated and unscaled.
        /// </summary>
        public static GameObject Place(Transform parent, GameObject prefab, Vector3 foot, float yaw, float plotW, float plotD, float maxHeight, float scale, System.Func<Material, Material> skin, int layer)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = "Building " + prefab.name;
            CityProps.Strip(go);
            var t = go.transform;
            t.localPosition = Vector3.zero; t.localRotation = Quaternion.Euler(0f, yaw, 0f); t.localScale = Vector3.one * scale;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Reject(go); return null; }
            var b = BoundsOf(rs);
            float fit = Mathf.Min(1f, plotW / Mathf.Max(0.1f, b.size.x), plotD / Mathf.Max(0.1f, b.size.z), maxHeight / Mathf.Max(0.1f, b.size.y));
            if (fit < MinFit) { Reject(go); return null; }
            if (fit < 1f) { t.localScale = Vector3.one * (scale * fit); b = BoundsOf(rs); }
            t.position += foot - new Vector3(b.center.x, b.min.y, b.center.z);
            foreach (var r in rs)
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (skin != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = skin(mats[i]);
                    r.sharedMaterials = mats;
                }
            }
            if (layer >= 0) foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
            return go;
        }
    }
}

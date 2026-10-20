using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>Frees what a floor's scenery made at run time (meshes, materials) when the floor goes: a mesh or a material is not destroyed with the object it hangs on.</summary>
    public sealed class OwnedObjects : MonoBehaviour
    {
        public readonly List<Object> items = new List<Object>();

        void OnDestroy()
        {
            foreach (var o in items) if (o) Destroy(o);
            items.Clear();
        }
    }

    /// <summary>
    /// The city outside the windows of an office floor: the same city as the one behind the boss in the run-start cinematic (the owner, 2026-10-17: "use a city similar to the one behind
    /// the boss in the cinematic outside the windows on the floors") - towers, windows, streets, haze and a sky - but all the way round the building, because the floors have windows on every
    /// side and a player walks past all of them, and at NIGHT ("it should be nighttime outside"): dark towers with lit windows, black streets, a starry sky with a moon and a glow of city light
    /// at the horizon. It is real geometry (about a thousand towers in nine meshes: a ring of near, middle and far towers, streets below and a sky dome over all), drawn unlit (an emissive
    /// Standard material with black albedo: depth-sorted from any side, and the dim night ambient changes nothing of it) and with no colliders, so it costs the NavMesh and the physics nothing.
    /// The street lies as far below the floor as a floor of that height does. What it makes is freed with the floor (<see cref="OwnedObjects"/>). Cosmetic only: no network ids, no random
    /// numbers of the floor's.
    /// </summary>
    public static class ExteriorCity
    {
        /// <summary>The sky dome, and so the farthest a tower stands, from the middle of the building (the camera's far plane is 1000 m).</summary>
        public const float Radius = 840f;
        /// <summary>No tower is closer to the building than this (a street's width and more).</summary>
        public const float KeepClear = 38f;

        public sealed class Stats { public int towers, meshes, quads; public float maxReach; }

        /// <summary>How bright a band of towers is (near, middle, far) and how bright a face is (top, north / south, east / west).</summary>
        static readonly float[] GainOfBand = { 1.00f, 0.85f, 0.70f };
        static readonly float[] ShadeOfFace = { 0.55f, 1.00f, 0.82f };
        /// <summary>The glow of city light at the horizon: the sky and the far streets fade to it.</summary>
        static readonly Color NightHaze = new Color(0.075f, 0.09f, 0.17f);

        static Texture2D nightSky;
        static readonly Texture2D[] nightWindows = new Texture2D[3];

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        // ------------------------------------------------------------------ the night's pictures (made once, shared by every floor)

        /// <summary>The night sky, 2048 x 512, for a dome that reaches from the horizon (row 77) to the zenith: a glow at the horizon, deep blue overhead, stars, and a moon to the right.</summary>
        public static Texture2D NightSky()
        {
            if (nightSky) return nightSky;
            const int W = 2048, H = 512, horizon = 77, moonX = 1446, moonY = 283;
            var px = new Color32[W * H];
            var zenith = new Color(0.008f, 0.014f, 0.045f);
            var glow = new Color(0.20f, 0.14f, 0.15f);                       // the city's light on the low sky
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = NightHaze;
                    if (y >= horizon)
                    {
                        float t = (y - horizon) / (float)(H - horizon);                 // 0 at the horizon, 1 at the top
                        c = Color.Lerp(NightHaze, zenith, Mathf.Pow(t, 0.42f));
                        c = Color.Lerp(c, glow, 0.55f * Mathf.Pow(1f - t, 5f));
                        float star = Hash(x * 7 + 13, y * 13 + 5);
                        if (star < 0.0014f + 0.0035f * t)                                // sparse near the glow, thicker overhead
                        {
                            float b = 0.35f + 0.65f * Hash(x + 91, y + 17);
                            c = Color.Lerp(c, new Color(0.95f, 0.97f, 1f), b);
                        }
                        float dx = x - moonX, dy = y - moonY, d2 = dx * dx + dy * dy;
                        c = Color.Lerp(c, new Color(0.35f, 0.42f, 0.62f), 0.55f * Mathf.Exp(-d2 / (2f * 90f * 90f)));        // the halo
                        if (d2 < 26f * 26f) c = Color.Lerp(new Color(0.88f, 0.90f, 0.82f), new Color(1f, 1f, 0.96f), Hash(x / 5, y / 5));    // the moon itself
                        else if (d2 < 30f * 30f) c = Color.Lerp(c, new Color(0.9f, 0.92f, 0.95f), 0.5f);
                    }
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            nightSky = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "NightSky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            nightSky.SetPixels32(px);
            nightSky.Apply(false, false);
            return nightSky;
        }

        /// <summary>
        /// A tile of 8 x 8 windows (a 32 x 28 m piece of a tower) with the lights of an office tower at night: some rows are lit all along (a floor that works late), the others have a few windows
        /// on, in warm or cool white. Level 0 is for the near towers, 1 for the middle and 2 for the far ones (fewer and fainter, on a façade that has taken on the glow of the horizon).
        /// </summary>
        public static Texture2D NightWindows(int level)
        {
            level = Mathf.Clamp(level, 0, 2);
            if (nightWindows[level]) return nightWindows[level];
            const int T = 128, cell = 16;
            var facade = level == 0 ? new Color(0.040f, 0.048f, 0.072f) : level == 1 ? new Color(0.052f, 0.064f, 0.105f) : new Color(0.066f, 0.082f, 0.140f);
            var frame = Color.Lerp(facade, Color.white, 0.04f);
            var dark = level == 0 ? new Color(0.018f, 0.028f, 0.050f) : level == 1 ? new Color(0.030f, 0.042f, 0.075f) : new Color(0.048f, 0.062f, 0.110f);
            float gain = level == 0 ? 1f : level == 1 ? 0.78f : 0.55f;
            float rowChance = level == 0 ? 0.28f : 0.22f, litInBusyRow = level == 0 ? 0.85f : 0.70f, litElsewhere = level == 0 ? 0.20f : 0.16f;
            var px = new Color32[T * T];
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    int cx = x / cell, cy = y / cell, lx = x % cell, ly = y % cell;
                    Color c = (lx < 2 || ly < 2) ? frame : facade;
                    bool glass = lx >= 3 && lx < 13 && ly >= 3 && ly < 12;
                    if (glass)
                    {
                        bool busy = Hash(cy * 13 + 5, 99 + level) < rowChance;
                        bool lit = Hash(cx * 31 + 7 + level * 5, cy * 17 + 3) < (busy ? litInBusyRow : litElsewhere);
                        if (lit)
                        {
                            bool warm = Hash(cx * 7 + 1, cy * 11 + 2) < 0.72f;
                            float jitter = 0.62f + 0.38f * Hash(cx * 3 + 19, cy * 5 + 23);
                            var lamp = warm ? new Color(1.00f, 0.80f, 0.46f) : new Color(0.76f, 0.86f, 1.00f);
                            c = lamp * (jitter * gain);
                            if (ly >= 9) c *= 0.82f;                                               // the lower edge of the pane is a little dimmer
                        }
                        else c = dark;
                    }
                    c.a = 1f;
                    px[y * T + x] = c;
                }
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = "NightWindows" + level, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return nightWindows[level] = tex;
        }

        // ------------------------------------------------------------------ the city

        /// <param name="centre">The middle of the building's footprint (x, z; y unused).</param>
        /// <param name="groundY">Where the streets are (below the floor).</param>
        public static GameObject Build(Transform parent, Vector3 centre, float halfX, float halfZ, float groundY, int seed, out Stats stats)
        {
            stats = new Stats();
            var root = new GameObject("City");
            root.transform.SetParent(parent, false);
            var owned = root.AddComponent<OwnedObjects>();
            var groups = new Dictionary<int, MeshBuilder>();
            float floorAbove = -groundY;                                     // how high our floor is over the street

            // ---- the towers: one per grid cell of the band they are in, some cells empty (streets, parks)
            float[] cell = { 26f, 40f, 64f }, bandEnd = { 260f, 540f, Radius };
            for (int band = 0; band < 3; band++)
            {
                float step = cell[band], lo = band == 0 ? 0f : bandEnd[band - 1], hi = bandEnd[band];
                int n = Mathf.CeilToInt(hi / step);
                for (int ix = -n; ix <= n; ix++)
                    for (int iz = -n; iz <= n; iz++)
                    {
                        float cx = ix * step, cz = iz * step, r = Mathf.Sqrt(cx * cx + cz * cz);
                        if (r < lo || r >= hi) continue;
                        float outsideX = Mathf.Abs(cx) - halfX, outsideZ = Mathf.Abs(cz) - halfZ;
                        if (Mathf.Max(outsideX, outsideZ) < KeepClear + step * 0.5f) continue;      // too near the building: a street
                        int hx = ix + band * 1000 + seed * 7, hz = iz + band * 77;
                        if (Hash(hx, hz) < 0.12f) continue;
                        float w = step * (0.50f + 0.35f * Hash(hx + 11, hz)), d = step * (0.50f + 0.35f * Hash(hx, hz + 23));
                        float tx = centre.x + cx + (Hash(hx + 3, hz + 5) - 0.5f) * (step - w), tz = centre.z + cz + (Hash(hx + 7, hz + 9) - 0.5f) * (step - d);
                        float roll = Hash(hx + 31, hz + 41);
                        float h = (10f + 14f * band) + (30f + 18f * band) * roll * roll;
                        if (Hash(hx + 53, hz + 59) < 0.08f) h += 30f + 60f * Hash(hx + 61, hz + 67);                     // an odd tower
                        if (r > 300f && Hash(hx + 97, hz + 101) < 0.35f) h *= 1.4f;                                      // downtown
                        float edge = Mathf.Max(outsideX, outsideZ);
                        h = Mathf.Min(h, floorAbove + 6f + 0.35f * edge + 40f * Hash(hx + 103, hz + 107));               // near towers do not wall the windows in on a high floor
                        AddTower(groups, band, tx, tz, w, d, groundY, groundY + h, Hash(hx + 79, hz + 83), Hash(hx + 43, hz + 47), stats);
                        stats.maxReach = Mathf.Max(stats.maxReach, Mathf.Sqrt((tx - centre.x) * (tx - centre.x) + (tz - centre.z) * (tz - centre.z)));
                    }
            }

            var roof = new Color(0.016f, 0.019f, 0.030f);
            foreach (var kv in groups)
            {
                int band = kv.Key / 3, face = kv.Key % 3;
                var mesh = kv.Value.ToMesh("City_" + kv.Key);
                if (mesh == null) continue;
                var tex = face == 0 ? (Texture)Texture2D.whiteTexture : NightWindows(band);
                Color emit = face == 0 ? roof : Color.white * (GainOfBand[band] * ShadeOfFace[face]);
                emit.a = 1f;
                AddMesh(root.transform, owned, mesh, UnlitMaterial(tex, emit, owned), "City" + kv.Key);
                stats.meshes++;
            }

            // ---- the streets below: nearly black, lit a little by the city, fading to the glow of the horizon with distance (unlit: it only ever has to lie under things)
            var ground = new MeshBuilder();
            const int cells = 36; float size = Radius * 2f / cells;
            var colours = new List<Color32>();
            var asphalt = new Color(0.030f, 0.034f, 0.048f);
            var lampGlow = new Color(0.090f, 0.072f, 0.060f);
            for (int ix = 0; ix < cells; ix++)
                for (int iz = 0; iz < cells; iz++)
                {
                    float x0 = -Radius + ix * size, z0 = -Radius + iz * size;
                    ground.Quad(new Vector3(centre.x + x0, groundY, centre.z + z0), new Vector3(centre.x + x0, groundY, centre.z + z0 + size), new Vector3(centre.x + x0 + size, groundY, centre.z + z0 + size), new Vector3(centre.x + x0 + size, groundY, centre.z + z0), Vector3.up);
                    foreach (var corner in new[] { new Vector2(x0, z0), new Vector2(x0, z0 + size), new Vector2(x0 + size, z0 + size), new Vector2(x0 + size, z0) })
                    {
                        float dist = corner.magnitude;
                        var street = Color.Lerp(asphalt, lampGlow, Mathf.Exp(-dist / 140f));
                        colours.Add(Color.Lerp(street, NightHaze, Mathf.Min(0.97f, 1f - Mathf.Exp(-dist / 420f))));
                    }
                }
            var gmesh = ground.ToMesh("CityStreets");
            gmesh.SetColors(colours);
            var gm = new Material(Shader.Find("Sprites/Default")) { name = "CityStreets", mainTexture = Texture2D.whiteTexture, hideFlags = HideFlags.HideAndDontSave };
            owned.items.Add(gm);
            AddMesh(root.transform, owned, gmesh, gm, "Streets");

            // ---- the sky: the night laid round a dome whose rim is the street level
            var sky = Dome(centre + new Vector3(0f, groundY, 0f), Radius);
            AddMesh(root.transform, owned, sky, UnlitMaterial(NightSky(), Color.white, owned), "Sky");
            stats.quads = 0;
            return root;
        }

        static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)
        {
            owned.items.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>A matt black Standard material that only glows: its colour is the emission (a picture times a tint), so lights, shadows and the dim night ambient change nothing of it.</summary>
        static Material UnlitMaterial(Texture tex, Color emission, OwnedObjects owned)
        {
            var m = new Material(Shader.Find("Standard")) { name = "CityUnlit", color = Color.black, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetTexture("_EmissionMap", tex);
            m.SetColor("_EmissionColor", emission);
            owned.items.Add(m);
            return m;
        }

        static void AddTower(Dictionary<int, MeshBuilder> groups, int band, float cx, float cz, float w, float d, float y0, float y1, float u, float v, Stats stats)
        {
            float x0 = cx - w * 0.5f, x1 = cx + w * 0.5f, z0 = cz - d * 0.5f, z1 = cz + d * 0.5f;
            float vTop = v + (y1 - y0) / 28f;
            MeshBuilder B(int face) { int key = band * 3 + face; if (!groups.TryGetValue(key, out var b)) groups[key] = b = new MeshBuilder(); return b; }
            var ns = B(1); var ew = B(2); var top = B(0);
            float uw = u + w / 32f, ud = u + d / 32f;
            ns.QuadUV(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back, new Vector2(u, v), new Vector2(u, vTop), new Vector2(uw, vTop), new Vector2(uw, v));
            ns.QuadUV(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward, new Vector2(u, v), new Vector2(u, vTop), new Vector2(uw, vTop), new Vector2(uw, v));
            ew.QuadUV(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left, new Vector2(u, v), new Vector2(u, vTop), new Vector2(ud, vTop), new Vector2(ud, v));
            ew.QuadUV(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, new Vector2(u, v), new Vector2(u, vTop), new Vector2(ud, vTop), new Vector2(ud, v));
            top.QuadUV(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            stats.towers++;
        }

        /// <summary>The top half of a sphere (radius <paramref name="radius"/>, its rim at <paramref name="centre"/>'s height), made to be seen from inside (both windings, so no face is lost to culling), the sky picture laid round it: u once round, v from the horizon row up to the zenith.</summary>
        static Mesh Dome(Vector3 centre, float radius)
        {
            const int S = 32, R = 8;
            const float vHorizon = 77f / 512f;                      // the horizon is row 77 of the sky picture
            var mb = new MeshBuilder();
            for (int r = 0; r < R; r++)
                for (int s = 0; s < S; s++)
                {
                    Vector3 P(int rr, int ss)
                    {
                        float elev = rr / (float)R * Mathf.PI * 0.5f, az = ss / (float)S * Mathf.PI * 2f;
                        return centre + radius * new Vector3(Mathf.Cos(elev) * Mathf.Cos(az), Mathf.Sin(elev), Mathf.Cos(elev) * Mathf.Sin(az));
                    }
                    Vector2 UV(int rr, int ss) => new Vector2(ss / (float)S, Mathf.Lerp(vHorizon, 1f, rr / (float)R));
                    var a = P(r, s); var b = P(r + 1, s); var c = P(r + 1, s + 1); var d = P(r, s + 1);
                    var nrm = -((a + b + c + d) * 0.25f - centre).normalized;
                    mb.QuadUV(a, b, c, d, nrm, UV(r, s), UV(r + 1, s), UV(r + 1, s + 1), UV(r, s + 1));
                    mb.QuadUV(d, c, b, a, nrm, UV(r, s + 1), UV(r + 1, s + 1), UV(r + 1, s), UV(r, s));
                }
            return mb.ToMesh("CitySky");
        }
    }
}

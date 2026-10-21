using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The streets of the night city outside the windows and the few low-poly cars that drive round the blocks with their headlights on (the owner, 2026-10-17: "There should be streets in the
    /// outside city with a few low res cars with headlights driving around and as you go up in floors the street should look seem further away"). The streets are the gaps between the towers
    /// (<see cref="ExteriorCity"/> keeps them clear): wide dark roads with a dashed centre line. Each car is a few boxes in one mesh (body, cabin, wheels, two headlights, two tail lights) and a pale
    /// pool of light on the road ahead of it; it follows a loop round one to four blocks, driving on the right, and never stops. Everything is cosmetic: no colliders, no network ids, and the
    /// floor's own random numbers are never used. The street lies as far below the floor as a floor of that height does (<c>groundY</c>), so a higher floor looks down on smaller cars on a thinner street.
    /// </summary>
    public static class CityStreets
    {
        /// <summary>The share of a tower cell (its width) that a street between two towers takes, and the distance from the building the streets start at.</summary>
        public const float StreetShare = 0.34f;

        static readonly Color[] BodyColours =
        {
            new Color(0.50f, 0.06f, 0.06f), new Color(0.05f, 0.14f, 0.55f), new Color(0.80f, 0.80f, 0.76f),
            new Color(0.10f, 0.10f, 0.12f), new Color(0.85f, 0.66f, 0.08f), new Color(0.12f, 0.42f, 0.20f),
        };

        /// <summary>The pack's cars (2026-10-19) have their noses along +z, like the box car.</summary>
        const float CarYaw = 0f;
        /// <summary>A car's length (the pack's cars are 4.4 to 6.5 m long: all are fitted to about this) and where the bottom of its wheels is against its origin (the origin is 0.55 m above the ground, the road's top 0.15 m).</summary>
        const float CarLength = 4.7f, CarBottom = -0.40f;

        static Texture2D beamTexture;

        /// <summary>A pale pool of light, brightest next to the car and fading along the road: 16 x 64, alpha only matters.</summary>
        static Texture2D BeamTexture()
        {
            if (beamTexture) return beamTexture;
            const int W = 16, H = 64;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;                // v 0 at the car, 1 at the far end
                    float across = Mathf.Clamp01(1f - Mathf.Abs(u));
                    float a = Mathf.Pow(across, 1.2f) * Mathf.Pow(1f - v, 1.3f) * 1.0f;
                    px[y * W + x] = new Color32(255, 240, 190, (byte)(Mathf.Clamp01(a) * 255f));
                }
            beamTexture = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "CarBeam", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            beamTexture.SetPixels32(px);
            beamTexture.Apply(false, true);
            return beamTexture;
        }

        static Texture2D glowTexture;

        /// <summary>A soft round glow (white, brightest in the middle, nothing at the rim): the lamps of a car seen from far above are blobs of light.</summary>
        static Texture2D GlowTexture()
        {
            if (glowTexture) return glowTexture;
            const int N = 32;
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            glowTexture = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "CarGlow", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            glowTexture.SetPixels32(px);
            glowTexture.Apply(false, true);
            return glowTexture;
        }

        static float Hash(int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + salt * 2246822519u);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Roads along the cell borders of the near band, dashed centre lines, and the cars. Returns how many cars were made.</summary>
        /// <param name="step">The cell of the near band of towers (26 m): streets run on the borders between cells, out to <paramref name="reach"/> metres from the building's middle.</param>
        public static int Build(Transform root, OwnedObjects owned, Vector3 centre, float halfX, float halfZ, float groundY, int seed, float step, float reach, out int roads)
        {
            roads = 0;
            var asphalt = new MeshBuilder();
            var dashes = new MeshBuilder();
            float roadHalf = step * StreetShare * 0.5f, y = groundY + 0.15f;
            int lines = Mathf.FloorToInt(reach / step);
            for (int k = -lines; k <= lines; k++)
            {
                float off = (k + 0.5f) * step;
                // a road along z at x = centre.x + off, and one along x at z = centre.z + off, as long as the near band
                float x = centre.x + off, z = centre.z + off;
                asphalt.Quad(new Vector3(x - roadHalf, y, centre.z - reach), new Vector3(x - roadHalf, y, centre.z + reach), new Vector3(x + roadHalf, y, centre.z + reach), new Vector3(x + roadHalf, y, centre.z - reach), Vector3.up);
                asphalt.Quad(new Vector3(centre.x - reach, y, z - roadHalf), new Vector3(centre.x - reach, y, z + roadHalf), new Vector3(centre.x + reach, y, z + roadHalf), new Vector3(centre.x + reach, y, z - roadHalf), Vector3.up);
                roads += 2;
                const float dash = 3.2f, pitch = 7.5f, half = 0.14f, dy = 0.12f;
                for (float t = -reach; t < reach; t += pitch)
                {
                    dashes.Quad(new Vector3(x - half, y + dy, centre.z + t), new Vector3(x - half, y + dy, centre.z + t + dash), new Vector3(x + half, y + dy, centre.z + t + dash), new Vector3(x + half, y + dy, centre.z + t), Vector3.up);
                    dashes.Quad(new Vector3(centre.x + t, y + dy, z - half), new Vector3(centre.x + t, y + dy, z + half), new Vector3(centre.x + t + dash, y + dy, z + half), new Vector3(centre.x + t + dash, y + dy, z - half), Vector3.up);
                }
            }
            AddMesh(root, owned, asphalt.ToMesh("CityRoads"), Unlit(owned, new Color(0.072f, 0.078f, 0.098f)), "Roads");
            AddMesh(root, owned, dashes.ToMesh("CityRoadDashes"), Unlit(owned, new Color(0.34f, 0.31f, 0.20f)), "RoadDashes");

            // ---- the cars: a few loops round the blocks, outside the building
            var holder = new GameObject("Traffic");
            holder.transform.SetParent(root, false);
            var traffic = holder.AddComponent<CityTraffic>();
            var catalog = CityCatalog.Instance;
            var models = catalog && catalog.cars != null ? System.Array.FindAll(catalog.cars, g => g != null) : new GameObject[0];       // (the owner's cars; with none the cars are the plain boxes)
            bool useModels = models.Length > 0;
            var night = new Dictionary<Material, Material>();
            var bodyMats = new Material[BodyColours.Length];
            var carMeshes = new Mesh[BodyColours.Length];
            var darkMat = Unlit(owned, new Color(0.02f, 0.02f, 0.03f));
            var headMat = Unlit(owned, new Color(1f, 0.93f, 0.72f) * 2.6f);
            var tailMat = Unlit(owned, new Color(1f, 0.10f, 0.06f) * 1.5f);
            var beamMat = new Material(Shader.Find("Sprites/Default")) { name = "CarBeam", mainTexture = BeamTexture(), hideFlags = HideFlags.HideAndDontSave };
            owned.items.Add(beamMat);
            for (int i = 0; i < BodyColours.Length && !useModels; i++)
            {
                bodyMats[i] = Unlit(owned, BodyColours[i] * 0.55f);
                carMeshes[i] = CarMesh(i);
                owned.items.Add(carMeshes[i]);
            }
            var beamMesh = BeamMesh();
            owned.items.Add(beamMesh);
            var glowMesh = GlowMesh();
            owned.items.Add(glowMesh);
            var glowWarm = new Material(Shader.Find("Sprites/Default")) { name = "CarGlowWarm", mainTexture = GlowTexture(), color = new Color(1f, 0.93f, 0.70f, 0.95f), hideFlags = HideFlags.HideAndDontSave };
            var glowRed = new Material(Shader.Find("Sprites/Default")) { name = "CarGlowRed", mainTexture = GlowTexture(), color = new Color(1f, 0.12f, 0.08f, 0.8f), hideFlags = HideFlags.HideAndDontSave };
            owned.items.Add(glowWarm); owned.items.Add(glowRed);

            int cars = 0;
            var sideCars = new int[4];
            int loopsWanted = 7;
            float lane = roadHalf * 0.5f;
            for (int l = 0; l < loopsWanted * 16 && cars < 16; l++)
            {
                // a rectangle of one to three blocks on the street grid, somewhere in the near band and clear of the building
                int w = 1 + (int)(Hash(seed, l, 1) * 3f), d = 1 + (int)(Hash(seed, l, 2) * 3f);
                float ux = Hash(seed, l, 3) * 2f - 1f, uz = Hash(seed, l, 4) * 2f - 1f;                       // (loops are likelier close to the building, where the windows see them)
                int kx = Mathf.FloorToInt(Mathf.Sign(ux) * Mathf.Pow(Mathf.Abs(ux), 1.6f) * (lines - 1)), kz = Mathf.FloorToInt(Mathf.Sign(uz) * Mathf.Pow(Mathf.Abs(uz), 1.6f) * (lines - 1));
                float x0 = centre.x + (kx + 0.5f) * step, z0 = centre.z + (kz + 0.5f) * step, x1 = x0 + w * step, z1 = z0 + d * step;
                if (Mathf.Abs(x1 - centre.x) > reach - roadHalf || Mathf.Abs(z1 - centre.z) > reach - roadHalf) continue;
                // the loops go round the building, one side after the other (south, north, west, east), close to it where the windows look: no more than 4 cars to a side
                int side = l % 4;
                float ex0 = halfX + roadHalf + 6f, ez0 = halfZ + roadHalf + 6f;
                bool onSide = side == 0 ? z1 <= centre.z - ez0 : side == 1 ? z0 >= centre.z + ez0 : side == 2 ? x1 <= centre.x - ex0 : x0 >= centre.x + ex0;
                float nearest = side < 2 ? (side == 0 ? centre.z - z1 : z0 - centre.z) - halfZ : (side == 2 ? centre.x - x1 : x0 - centre.x) - halfX;
                if (!onSide || nearest > 140f || sideCars[side] >= 4) continue;
                // not through the building (its footprint and a margin)
                float ex = halfX + roadHalf + 6f, ez = halfZ + roadHalf + 6f;
                bool hitsBuilding = x0 < centre.x + ex && x1 > centre.x - ex && z0 < centre.z + ez && z1 > centre.z - ez;
                if (hitsBuilding) continue;
                bool clockwise = Hash(seed, l, 5) < 0.5f;
                float inset = clockwise ? lane : -lane;                                  // driving on the right: inside the loop for a clockwise one, outside for the other
                var corners = new List<Vector3>
                {
                    new Vector3(x0 + inset, groundY + 0.55f, z0 + inset), new Vector3(x0 + inset, groundY + 0.55f, z1 - inset),
                    new Vector3(x1 - inset, groundY + 0.55f, z1 - inset), new Vector3(x1 - inset, groundY + 0.55f, z0 + inset),
                };
                if (!clockwise) corners.Reverse();
                int onLoop = 1 + (int)(Hash(seed, l, 6) * 2.99f);
                for (int c = 0; c < onLoop && cars < 16 && sideCars[side] < 4; c++)
                {
                    float pick = Hash(seed, l * 7 + c, 7);
                    var go = new GameObject("Car");
                    go.transform.SetParent(holder.transform, false);
                    if (useModels) DressCar(go.transform, models[(int)(pick * models.Length) % models.Length], night, owned);
                    else
                    {
                        int variant = (int)(pick * BodyColours.Length) % BodyColours.Length;
                        go.AddComponent<MeshFilter>().sharedMesh = carMeshes[variant];
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = new[] { bodyMats[variant], darkMat, headMat, tailMat };
                        ConfigureRenderer(mr);
                    }
                    var beam = new GameObject("Beam");
                    beam.transform.SetParent(go.transform, false);
                    beam.transform.localPosition = new Vector3(0f, -0.38f, 2.3f);
                    beam.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                    var br = beam.AddComponent<MeshRenderer>();
                    br.sharedMaterial = beamMat;
                    ConfigureRenderer(br);
                    Glow(go.transform, glowMesh, glowWarm, new Vector3(0f, -0.36f, 2.9f), 5.5f);          // the lamps, seen from far above: a pale blob ahead of the car and a small red one behind it
                    Glow(go.transform, glowMesh, glowRed, new Vector3(0f, -0.36f, -2.6f), 2.6f);
                    float speed = 7f + 5f * Hash(seed, l * 11 + c, 8);
                    traffic.Add(go.transform, corners, speed, (c + Hash(seed, l, 9)) / onLoop);
                    cars++; sideCars[side]++;
                }
            }
            return cars;
        }

        /// <summary>One of the owner's cars as a child of a car: turned to the car's frame, fitted to about 4.7 m, its wheels' bottom at the road, its materials made to glow by themselves (like the towers).</summary>
        static void DressCar(Transform car, GameObject prefab, Dictionary<Material, Material> night, OwnedObjects owned)
        {
            var model = Object.Instantiate(prefab, car);
            model.name = "Model " + prefab.name;
            CityProps.Strip(model);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0f, CarYaw, 0f); model.transform.localScale = Vector3.one;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float s = Mathf.Clamp(CarLength / Mathf.Max(0.5f, b.size.z), 0.7f, 1.15f);
            var at = car.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));            // (the car has just been made: it lies at its parent's frame, unscaled)
            model.transform.localScale = Vector3.one * s;
            model.transform.localPosition = new Vector3(-at.x * s, CarBottom - at.y * s, -at.z * s);
            foreach (var r in rs)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = NightOf(mats[i], night, owned);
                r.sharedMaterials = mats;
                ConfigureRenderer(r);
            }
        }

        /// <summary>The glowing copy of a pack material (one per material for the whole street; its lamps glow brighter than its paint).</summary>
        static Material NightOf(Material src, Dictionary<Material, Material> night, OwnedObjects owned)
        {
            if (!src) return null;
            if (!night.TryGetValue(src, out var m))
            {
                m = CityProps.NightCopy(src, CityProps.Glows(src) ? 1.6f : 0.55f);
                owned.items.Add(m);
                night[src] = m;
            }
            return m;
        }

        static void Glow(Transform car, Mesh mesh, Material mat, Vector3 at, float size)
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(car, false);
            g.transform.localPosition = at;
            g.transform.localScale = new Vector3(size, 1f, size);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            ConfigureRenderer(r);
        }

        /// <summary>A flat unit quad lying in the car's x-z plane (facing up), centred on its origin.</summary>
        static Mesh GlowMesh()
        {
            var b = new MeshBuilder();
            b.QuadUV(new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f), Vector3.up, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            return b.ToMesh("CarGlow");
        }

        static void ConfigureRenderer(Renderer r)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)
        {
            if (mesh == null) return;
            owned.items.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            ConfigureRenderer(mr);
        }

        /// <summary>A matt black Standard material that only glows in <paramref name="emission"/> (like the towers: light, shadow and the dim night ambient change nothing of it).</summary>
        static Material Unlit(OwnedObjects owned, Color emission)
        {
            var m = new Material(Shader.Find("Standard")) { name = "CityGlow", color = Color.black, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", emission);
            owned.items.Add(m);
            return m;
        }

        /// <summary>
        /// One car, 1.8 m wide and 4.2 m long, facing +z, its wheels' bottom at y = -0.55 (the car is placed 0.55 m above the road): four submeshes - the body and cabin in the car's colour,
        /// the dark parts (windows, wheels), two headlights, two tail lights. Plain boxes: the owner asked for "low res cars".
        /// </summary>
        static Mesh CarMesh(int variant)
        {
            var body = new MeshBuilder(); var dark = new MeshBuilder(); var head = new MeshBuilder(); var tail = new MeshBuilder();
            body.Box(new Vector3(-0.9f, -0.30f, -2.1f), new Vector3(0.9f, 0.30f, 2.1f));                      // the body
            body.Box(new Vector3(-0.78f, 0.30f, -1.0f), new Vector3(0.78f, 0.78f, 0.95f));                    // the cabin
            dark.Box(new Vector3(-0.80f, 0.36f, -0.88f), new Vector3(0.80f, 0.70f, 0.85f));                   // the windows, a hair proud of the cabin's sides
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { -1.3f, 1.3f })
                    dark.Box(new Vector3(sx * 0.80f - 0.10f, -0.55f, z - 0.35f), new Vector3(sx * 0.80f + 0.10f, -0.18f, z + 0.35f));       // the wheels
            foreach (float sx in new[] { -1f, 1f })
            {
                head.Box(new Vector3(sx * 0.58f - 0.17f, -0.10f, 2.10f), new Vector3(sx * 0.58f + 0.17f, 0.12f, 2.16f));
                tail.Box(new Vector3(sx * 0.62f - 0.15f, -0.08f, -2.16f), new Vector3(sx * 0.62f + 0.15f, 0.10f, -2.10f));
            }
            var meshes = new[] { body.ToMesh("c"), dark.ToMesh("c"), head.ToMesh("c"), tail.ToMesh("c") };
            var combine = new CombineInstance[meshes.Length];
            for (int i = 0; i < meshes.Length; i++) combine[i] = new CombineInstance { mesh = meshes[i], transform = Matrix4x4.identity, subMeshIndex = 0 };
            var car = new Mesh { name = "Car" + variant };
            car.CombineMeshes(combine, false, false);                                                         // (one submesh per part, so one material each)
            foreach (var m in meshes) { if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m); }
            car.RecalculateBounds();
            return car;
        }

        /// <summary>The pool of light on the road ahead of a car: a flat quad 5 m wide and 11 m long along +z.</summary>
        static Mesh BeamMesh()
        {
            var b = new MeshBuilder();
            b.QuadUV(new Vector3(-1.0f, 0f, 0f), new Vector3(-3.4f, 0f, 14f), new Vector3(3.4f, 0f, 14f), new Vector3(1.0f, 0f, 0f), Vector3.up, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            return b.ToMesh("CarBeam");
        }
    }

    /// <summary>Moves the city's cars along their loops (see <see cref="CityStreets"/>). It never stops and costs a few transforms a frame.</summary>
    public sealed class CityTraffic : MonoBehaviour
    {
        sealed class Car
        {
            public Transform t;
            public Vector3[] pts;
            public float[] cum;
            public float length, s, speed;
        }

        readonly List<Car> cars = new List<Car>();

        /// <summary>How many cars drive.</summary>
        public int Count => cars.Count;

        /// <summary>A car on a closed loop through the corners; <paramref name="phase"/> (0..1) is how far round it starts.</summary>
        public void Add(Transform car, List<Vector3> corners, float speed, float phase)
        {
            var c = new Car { t = car, pts = corners.ToArray(), speed = speed };
            c.cum = new float[c.pts.Length + 1];
            for (int i = 0; i < c.pts.Length; i++) c.cum[i + 1] = c.cum[i] + Vector3.Distance(c.pts[i], c.pts[(i + 1) % c.pts.Length]);
            c.length = c.cum[c.pts.Length];
            c.s = Mathf.Repeat(phase, 1f) * c.length;
            Place(c, 1f);
            cars.Add(c);
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float k = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (!c.t) continue;
                c.s = Mathf.Repeat(c.s + c.speed * dt, c.length);
                Place(c, k);
            }
        }

        static void Place(Car c, float turn)
        {
            int n = c.pts.Length, seg = 0;
            while (seg < n - 1 && c.s >= c.cum[seg + 1]) seg++;
            var a = c.pts[seg]; var b = c.pts[(seg + 1) % n];
            float len = Mathf.Max(0.001f, c.cum[seg + 1] - c.cum[seg]);
            float f = (c.s - c.cum[seg]) / len;
            var dir = (b - a) / len;
            c.t.position = Vector3.Lerp(a, b, f);
            var want = Quaternion.LookRotation(dir, Vector3.up);
            c.t.rotation = turn >= 1f ? want : Quaternion.Slerp(c.t.rotation, want, turn);
        }
    }
}

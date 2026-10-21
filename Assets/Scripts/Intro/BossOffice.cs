using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The boss's office, built from code for the run-start cinematic (floor 13's real boss office can reuse it later: <see cref="Build"/> only needs an
    /// origin). The look follows the owner's reference picture (2026-10-16): a wide glass-topped desk, a tall black ladder-back chair, floor-to-ceiling
    /// windows with thin dark mullions over a vast city under clouds, a Tiffany-style lamp and leather document boxes at the left, a big black monitor at
    /// the right, papers and small metal pyramids on the desk. It lives on its own layer (<see cref="Layer"/>) with its own lights, far from the map, so
    /// only the cinematic's camera sees it and it lights nothing else. Local axes: the boss sits at +z facing -z, the camera comes from -z, the windows are
    /// at +z. Everything it makes (materials, meshes) is freed by <see cref="Destroy"/> (the pictures it paints - sky, windows, lamp glass - are kept for the next run).
    /// Art assets are only used (the boss is a Tiny Toon person), never edited.
    ///
    /// The city is real geometry (about 2500 boxes in three meshes: near, middle, far) so the camera's small moves give parallax. It is drawn with the
    /// unlit Sprites/Default shader, which neither writes depth nor culls, so the faces that can be seen are written far rows first (a painter's order)
    /// and every corner is faded towards <see cref="Haze"/> by its own distance (aerial perspective); the meshes carry explicit sorting orders so they
    /// are drawn before anything else that is transparent (the lamp's glass).
    /// </summary>
    public sealed class BossOffice
    {
        /// <summary>An unnamed user layer: the office and its lights are on it, the cinematic camera sees only it.</summary>
        public const int Layer = 31;
        public static readonly Vector3 DefaultOrigin = new Vector3(2500f, 0f, 2500f);
        /// <summary>The colour of the far distance: the city and the ground fade into it, the lowest band of the sky is it.</summary>
        public static readonly Color Haze = new Color(0.80f, 0.87f, 0.95f);
        /// <summary>Tests: another boss model (a CharacterCatalog id) than <see cref="IntroContent.BossModelId"/>.</summary>
        public static string ModelOverride;
        /// <summary>Where the boss sits. His origin is the middle of the 2 m capsule he is dressed on, so his feet are a metre lower: he stands 0.08 m under the floor, which
        /// puts his chest above the desk (the desk hides the rest of him).</summary>
        public static readonly Vector3 BossSeat = new Vector3(0f, 0.92f, 1.3f);

        const float PlaneScale = 5.0f;                            // the white plane (3.1 m long as the pack made it) flies a little bigger than the old plain jet (12.6 m): 15.5 m, so that it reads on the small TV
        const float GroundY = -96f;                               // the street level, far below the windows
        static readonly Vector3 Eye = new Vector3(-1f, 1.5f, 0f); // about where the camera is: what the haze measures from

        /// <summary>The private jet's path outside the windows (<paramref name="progress"/> 0 = far right, 1 = far left); the window shot's camera follows it.</summary>
        public static Vector3 JetPoint(float progress) => new Vector3(Mathf.Lerp(52f, -52f, progress), 15f, 64f);

        public readonly Transform root;
        public Transform head { get; private set; }
        public Transform spine { get; private set; }
        Transform jet;

        readonly List<Object> owned = new();
        static Texture2D skyTexture, lampTexture;
        static readonly Texture2D[] windowTextures = new Texture2D[2];

        public static BossOffice Build(Vector3 origin) => new BossOffice(origin);

        /// <summary>Paints the pictures now (the sky, the tower windows, the lamp's glass; about 0.3 s the first time), so the first Intro of a session does not stall the
        /// frame it starts in: the director calls this while the run's scene is still loading.</summary>
        public static void Prewarm() { SkyTexture(); WindowTexture(0); WindowTexture(1); LampTexture(); _ = CityCatalog.Instance; }

        BossOffice(Vector3 origin)
        {
            root = new GameObject("BossOffice").transform;
            root.position = origin;

            var wood = Mat(new Color(0.20f, 0.12f, 0.07f), 0.55f);
            var plaster = Mat(new Color(0.68f, 0.65f, 0.60f), 0.1f);
            var ceiling = Mat(new Color(0.85f, 0.85f, 0.83f), 0.05f);
            var steel = Mat(new Color(0.06f, 0.06f, 0.07f), 0.4f, 0.55f);
            var black = Mat(new Color(0.035f, 0.035f, 0.04f), 0.5f);
            var glass = Mat(new Color(0.05f, 0.08f, 0.10f), 0.93f);
            var rug = Mat(new Color(0.11f, 0.11f, 0.14f), 0.05f);

            // ---- the room (the windows are the +z wall: its frame and thin mullions only, nothing between them)
            Box("Floor", new Vector3(0f, -0.1f, -0.85f), new Vector3(12f, 0.2f, 10.7f), wood);
            Box("Rug", new Vector3(0f, 0.01f, 0.2f), new Vector3(4.4f, 0.02f, 3.6f), rug);
            Box("Ceiling", new Vector3(0f, 3.5f, -0.85f), new Vector3(12f, 0.2f, 10.7f), ceiling);
            Box("WallLeft", new Vector3(-6.1f, 1.75f, -0.85f), new Vector3(0.2f, 3.5f, 10.7f), plaster);
            Box("WallRight", new Vector3(6.1f, 1.75f, -0.85f), new Vector3(0.2f, 3.5f, 10.7f), plaster);
            Box("WallBack", new Vector3(0f, 1.75f, -6.3f), new Vector3(12.4f, 3.5f, 0.2f), plaster);
            Box("Sill", new Vector3(0f, 0.08f, 4.4f), new Vector3(12.2f, 0.16f, 0.30f), steel);
            Box("Head", new Vector3(0f, 3.35f, 4.4f), new Vector3(12.2f, 0.30f, 0.30f), steel);
            for (int i = -4; i <= 4; i++) Box("Mullion" + i, new Vector3(i * 1.5f, 1.75f, 4.4f), new Vector3(0.07f, 3.5f, 0.12f), steel);

            // ---- outside: the sky (one big picture far away) and the city far below
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "Sky";
            Object.Destroy(sky.GetComponent<Collider>());
            sky.transform.SetParent(root, false);
            sky.transform.localPosition = new Vector3(0f, 1030f, 2500f);        // from 10 degrees under the horizon to 45 over it, 80 degrees across
            sky.transform.localScale = new Vector3(4400f, 2940f, 1f);
            var skyMat = Unlit(SkyTexture());
            sky.GetComponent<Renderer>().sharedMaterial = skyMat;
            Flat(sky.GetComponent<Renderer>(), -10);
            BuildCity();

            // ---- the desk (a wide glass top on a dark steel frame)
            Box("DeskTop", new Vector3(0f, 0.74f, 0f), new Vector3(3.2f, 0.05f, 1.4f), glass);
            Box("DeskRailBack", new Vector3(0f, 0.69f, 0.66f), new Vector3(3.2f, 0.06f, 0.05f), steel);
            Box("DeskRailFront", new Vector3(0f, 0.69f, -0.66f), new Vector3(3.2f, 0.06f, 0.05f), steel);
            foreach (float x in new[] { -1.52f, 1.52f })
                foreach (float z in new[] { -0.62f, 0.62f })
                    Box("DeskLeg", new Vector3(x, 0.35f, z), new Vector3(0.06f, 0.7f, 0.06f), steel);
            Box("DeskShelf", new Vector3(0f, 0.22f, 0f), new Vector3(3.0f, 0.025f, 1.2f), steel);
            Box("DeskPanel", new Vector3(0f, 0.43f, -0.62f), new Vector3(2.96f, 0.55f, 0.03f), steel);      // (a modesty panel: the boss's legs are not for the camera)

            // ---- the chair (black, ladder back, on a swivel base); the boss sits in it
            Box("ChairSeat", new Vector3(0f, 0.50f, 1.45f), new Vector3(0.62f, 0.10f, 0.62f), black);
            for (int i = 0; i < 6; i++) Box("ChairSlat" + i, new Vector3(0f, 0.68f + i * 0.21f, 1.76f), new Vector3(0.58f, 0.10f, 0.07f), black);
            Box("ChairSpine", new Vector3(0f, 1.25f, 1.79f), new Vector3(0.09f, 1.40f, 0.06f), black);
            Box("ChairHeadrest", new Vector3(0f, 1.95f, 1.80f), new Vector3(0.50f, 0.26f, 0.09f), black);
            foreach (float x in new[] { -0.37f, 0.37f })
            {
                Box("ChairArm", new Vector3(x, 0.74f, 1.45f), new Vector3(0.07f, 0.04f, 0.52f), black);
                Box("ChairArmPost", new Vector3(x, 0.62f, 1.28f), new Vector3(0.05f, 0.24f, 0.05f), steel);
            }
            Prim(PrimitiveType.Cylinder, "ChairPole", new Vector3(0f, 0.26f, 1.45f), new Vector3(0.08f, 0.24f, 0.08f), steel);
            Prim(PrimitiveType.Cylinder, "ChairBase", new Vector3(0f, 0.04f, 1.45f), new Vector3(0.62f, 0.02f, 0.62f), steel);

            // ---- on the desk: the Tiffany lamp and leather document boxes (left), monitor (right), papers, pyramids
            var brass = Mat(new Color(0.35f, 0.25f, 0.08f), 0.7f, 0.6f);
            Prim(PrimitiveType.Cylinder, "LampBase", new Vector3(-1.30f, 0.785f, 0.15f), new Vector3(0.18f, 0.012f, 0.18f), brass);
            Prim(PrimitiveType.Cylinder, "LampStem", new Vector3(-1.30f, 0.89f, 0.15f), new Vector3(0.03f, 0.10f, 0.03f), brass);
            var shade = new GameObject("LampShade");                    // an open dome of stained glass (unlit, so it glows)
            shade.transform.SetParent(root, false);
            shade.transform.localPosition = new Vector3(-1.30f, 0.97f, 0.15f);
            shade.transform.localScale = new Vector3(0.24f, 0.21f, 0.24f);
            shade.AddComponent<MeshFilter>().sharedMesh = Dome();
            var shadeRenderer = shade.AddComponent<MeshRenderer>();
            shadeRenderer.sharedMaterial = Unlit(LampTexture());
            Flat(shadeRenderer, 0);
            var lampLight = new GameObject("LampLight").AddComponent<Light>();
            lampLight.transform.SetParent(root, false);
            lampLight.transform.localPosition = new Vector3(-1.30f, 1.05f, 0.0f);
            lampLight.type = LightType.Point; lampLight.range = 3.4f; lampLight.intensity = 1.1f; lampLight.color = new Color(1f, 0.78f, 0.45f);
            lampLight.shadows = LightShadows.None; lampLight.cullingMask = 1 << Layer;

            var leather = Mat(new Color(0.27f, 0.15f, 0.08f), 0.4f);
            Box("DocBox0", new Vector3(-1.45f, 0.83f, -0.35f), new Vector3(0.48f, 0.12f, 0.34f), leather, new Vector3(0f, 6f, 0f));
            Box("DocBox1", new Vector3(-1.45f, 0.95f, -0.35f), new Vector3(0.44f, 0.12f, 0.32f), leather, new Vector3(0f, -4f, 0f));
            Box("DocBox2", new Vector3(-1.44f, 1.07f, -0.35f), new Vector3(0.40f, 0.12f, 0.30f), leather, new Vector3(0f, 9f, 0f));

            var monitor = Mat(new Color(0.02f, 0.02f, 0.025f), 0.6f, 0.3f);
            var housing = Mat(new Color(0.09f, 0.09f, 0.10f), 0.35f, 0.4f);
            Box("MonitorScreen", new Vector3(1.25f, 1.18f, 0.30f), new Vector3(0.98f, 0.58f, 0.035f), monitor, new Vector3(0f, -12f, 0f));
            Box("MonitorBack", new Vector3(1.25f, 1.15f, 0.24f), new Vector3(0.52f, 0.32f, 0.07f), housing, new Vector3(0f, -12f, 0f));
            Box("MonitorNeck", new Vector3(1.25f, 0.96f, 0.30f), new Vector3(0.08f, 0.34f, 0.05f), housing, new Vector3(0f, -12f, 0f));
            Box("MonitorFoot", new Vector3(1.25f, 0.775f, 0.30f), new Vector3(0.38f, 0.02f, 0.24f), housing, new Vector3(0f, -12f, 0f));

            var paper = Mat(new Color(0.92f, 0.91f, 0.86f), 0.1f);
            Box("Paper0", new Vector3(-0.35f, 0.768f, -0.20f), new Vector3(0.30f, 0.006f, 0.22f), paper, new Vector3(0f, 14f, 0f));
            Box("Paper1", new Vector3(-0.20f, 0.772f, -0.12f), new Vector3(0.30f, 0.006f, 0.22f), paper, new Vector3(0f, -9f, 0f));
            Box("Folder", new Vector3(0.05f, 0.776f, -0.14f), new Vector3(0.34f, 0.014f, 0.25f), Mat(new Color(0.12f, 0.18f, 0.30f), 0.3f), new Vector3(0f, 4f, 0f));
            Box("Pen", new Vector3(0.28f, 0.778f, -0.10f), new Vector3(0.14f, 0.012f, 0.012f), steel, new Vector3(0f, 25f, 0f));
            var silver = Mat(new Color(0.80f, 0.81f, 0.85f), 0.65f, 0.35f);
            var pyramid = Pyramid();
            float[,] pyr = { { 0.75f, -0.50f, 0.09f }, { 0.97f, -0.36f, 0.07f }, { 1.12f, -0.56f, 0.10f } };
            for (int i = 0; i < 3; i++)
            {
                var g = new GameObject("Pyramid" + i);
                g.transform.SetParent(root, false);
                g.transform.localPosition = new Vector3(pyr[i, 0], 0.765f, pyr[i, 1]);
                g.transform.localScale = new Vector3(pyr[i, 2], pyr[i, 2] * 1.25f, pyr[i, 2]);
                g.transform.localRotation = Quaternion.Euler(0f, 20f + i * 25f, 0f);
                g.AddComponent<MeshFilter>().sharedMesh = pyramid;
                g.AddComponent<MeshRenderer>().sharedMaterial = silver;
            }

            // ---- the boss: a Tiny Toon person in the chair behind the desk (the desk hides all of him but his head and chest)
            var host = new GameObject("Boss");
            host.transform.SetParent(root, false);
            host.transform.localPosition = BossSeat;
            host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            PartyCharacterFactory.Attach(host, new CharacterLook("Boss", new Color(0.113f, 0.113f, 0.113f), 2, null, 1f, 1f, ModelOverride ?? IntroContent.BossModelId));
            var animator = host.GetComponentInChildren<Animator>();
            if (animator && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            // a tie (a boss wears one): the visible strip of his chest is between his chin (y 0.97) and the desk top (0.77); the pieces stand just in front of him
            var tie = Mat(new Color(0.55f, 0.05f, 0.08f), 0.4f);
            Box("TieKnot", new Vector3(0f, 0.935f, BossSeat.z - 0.34f), new Vector3(0.06f, 0.06f, 0.04f), tie);
            Box("TieBlade", new Vector3(0f, 0.78f, BossSeat.z - 0.34f), new Vector3(0.075f, 0.28f, 0.02f), tie);

            // ---- the private jet (parked out of sight until the window shot), its nose toward -x; big, because it flies far outside
            jet = new GameObject("Jet").transform;
            jet.SetParent(root, false);
            if (DressPlane(jet)) jet.localScale = Vector3.one * PlaneScale;
            else
            {
                jet.localScale = Vector3.one * 1.7f;
                var white = Mat(new Color(0.94f, 0.95f, 0.97f), 0.6f);
                var gold = Mat(new Color(0.85f, 0.65f, 0.18f), 0.6f, 0.5f);
                Part(PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(1.5f, 5.0f, 1.5f), white, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cube, "Wings", new Vector3(0.6f, -0.2f, 0f), new Vector3(2.3f, 0.14f, 11f), white, new Vector3(0f, 14f, 0f));
                Part(PrimitiveType.Cube, "Fin", new Vector3(4.3f, 1.35f, 0f), new Vector3(1.9f, 2.3f, 0.14f), gold, new Vector3(0f, 0f, -12f));
                Part(PrimitiveType.Cube, "Tailplane", new Vector3(4.4f, 0.55f, 0f), new Vector3(1.2f, 0.1f, 4.2f), white, Vector3.zero);
                Part(PrimitiveType.Cylinder, "EngineL", new Vector3(3.5f, 0.45f, -1.1f), new Vector3(0.8f, 1.2f, 0.8f), steel, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cylinder, "EngineR", new Vector3(3.5f, 0.45f, 1.1f), new Vector3(0.8f, 1.2f, 0.8f), steel, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.05f, 0f), new Vector3(8.6f, 0.2f, 1.53f), gold, Vector3.zero);
            }
            SetJet(-1f);

            // ---- lights: a soft key from the camera side, a warm sun through the windows, a little fill; they light this layer only
            Sun("Key", new Vector3(32f, 12f, 0f), 0.95f, new Color(1f, 0.97f, 0.92f), LightShadows.Soft);
            Sun("Sun", new Vector3(24f, 200f, 0f), 0.75f, new Color(1f, 0.93f, 0.80f), LightShadows.None);
            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.transform.localPosition = new Vector3(0f, 2.8f, -2.0f);
            fill.type = LightType.Point; fill.range = 10f; fill.intensity = 0.55f; fill.shadows = LightShadows.None; fill.cullingMask = 1 << Layer;

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        /// <summary>The owner's white plane (2026-10-19: the pack's ComPlaneV1; its nose is +z) as the jet: turned to fly toward -x, its landing gear up, centred on the jet's origin. False (nothing made) when the catalog has no plane.</summary>
        bool DressPlane(Transform parent)
        {
            var catalog = CityCatalog.Instance;
            var prefab = catalog ? catalog.plane : null;
            if (!prefab) return false;
            var model = Object.Instantiate(prefab, parent);
            model.name = "Plane";
            CityProps.Strip(model);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t != model.transform && t.name.Contains("LandingGear")) t.gameObject.SetActive(false);            // (it is in the air)
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localPosition = Vector3.zero;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.Destroy(model); return false; }
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            model.transform.localPosition = -parent.InverseTransformPoint(b.center);
            return true;
        }

        /// <summary>Where the jet is: <paramref name="progress"/> 0 = far right outside the windows, 1 = far left; outside 0..1 it is parked out of sight.</summary>
        public void SetJet(float progress)
        {
            if (!jet) return;
            bool flying = progress >= 0f && progress <= 1f;
            jet.gameObject.SetActive(flying);
            if (flying) jet.localPosition = JetPoint(progress);
        }

        public void Destroy()
        {
            foreach (var o in owned) if (o) Object.Destroy(o);
            owned.Clear();
            if (root) Object.Destroy(root.gameObject);
        }

        // ------------------------------------------------------------------ builders

        // ---- the owner's towers (2026-10-19): the pack's tall buildings, made big enough to be seen from far, as the skyline the windows look at

        const float PackCell = 64f, PackZ0 = 288f;                // the plots: 64 m squares in rows from z = 288 m outward

        /// <summary>The cone of the view the windows give: the plots outside it would never be seen.</summary>
        static bool PackCone(float x, float z) => Mathf.Abs(x) < 0.65f * z + 40f;

        /// <summary>The plot of the pack's grid a spot lies in (column, row).</summary>
        static Vector2Int PackIndex(float x, float z) => new Vector2Int(Mathf.FloorToInt((x + PackCell * 0.5f) / PackCell), Mathf.FloorToInt((z - PackZ0) / PackCell));

        /// <summary>Stands the pack's towers four times their size, with the aerial perspective of the other towers, on the plots of the cone: 62 % of the nearest row, 8 % of the seventh, none farther (the owner, 2026-10-19: keep some of the old tall towers sporadically, they make good skyscrapers in the distance); returns the plots that got one (the box towers there stay out).</summary>
        HashSet<Vector2Int> PackTowers()
        {
            var placed = new HashSet<Vector2Int>();
            var pool = CityBuildings.Towers(CityCatalog.Instance);
            if (pool.Length == 0) return placed;
            for (int jp = 0; jp < 7; jp++)
                for (int ip = -14; ip <= 14; ip++)
                {
                    float x = ip * PackCell, z = PackZ0 + (jp + 0.5f) * PackCell;
                    if (!PackCone(x, z) || Hash(ip + 700, jp + 700) >= 0.62f - 0.085f * jp) continue;                       // (fewer the farther: the old glass skyscrapers keep the distance)
                    float haze = 0.7f * Mathf.Min(0.97f, 1f - Mathf.Exp(-Mathf.Sqrt(x * x + z * z) / 560f));
                    var go = CityBuildings.Place(root, pool[(int)(Hash(ip + 911, jp + 913) * pool.Length) % pool.Length], root.TransformPoint(new Vector3(x, GroundY, z)),
                        90f * Mathf.Floor(Hash(ip + 917, jp + 919) * 4f), PackCell - 12f, PackCell - 12f, 150f, 4f, src => Hazed(src, haze), -1);
                    if (go != null) placed.Add(new Vector2Int(ip, jp));
                }
            return placed;
        }

        /// <summary>A copy of a pack material for the daytime city: its picture dimmed by <paramref name="haze"/> with the haze's colour added (the aerial perspective the other towers have in their vertex colours).</summary>
        Material Hazed(Material src, float haze)
        {
            float shade = (1f - haze) * 0.78f;                            // (the pack's pale walls are white in this much light: a little darker)
            var m = new Material(Shader.Find("Standard")) { color = new Color(shade, shade, shade, 1f), mainTexture = src ? src.mainTexture : null };
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Haze * haze);
            owned.Add(m);
            return m;
        }

        Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f)
        {
            var m = new Material(Shader.Find("Standard")) { color = color };
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_Metallic", metallic);
            owned.Add(m);
            return m;
        }

        /// <summary>An unlit picture material (the sky, the lamp's glass, the city).</summary>
        Material Unlit(Texture tex)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
            owned.Add(m);
            return m;
        }

        /// <summary>A picture that casts and takes no shadow, is not lit by probes, and is drawn in this order among the transparent things (lower = earlier).</summary>
        static void Flat(Renderer r, int order)
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortingOrder = order;
        }

        GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent ? parent : root, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        void Box(string name, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null) => Prim(PrimitiveType.Cube, name, pos, size, mat, euler);

        void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler) => Prim(type, name, pos, scale, mat, euler, jet);

        void Sun(string name, Vector3 euler, float intensity, Color color, LightShadows shadows)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localRotation = Quaternion.Euler(euler);
            l.type = LightType.Directional; l.intensity = intensity; l.color = color; l.shadows = shadows; l.cullingMask = 1 << Layer;
        }

        Mesh Pyramid()
        {
            const float b = 0.5f, h = 1f;
            var a = new Vector3(-b, 0, -b); var c1 = new Vector3(b, 0, -b); var c2 = new Vector3(b, 0, b); var d = new Vector3(-b, 0, b); var top = new Vector3(0, h, 0);
            var v = new List<Vector3>(); var tri = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { int i = v.Count; v.Add(p); v.Add(q); v.Add(r); tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); }
            Tri(a, top, c1); Tri(c1, top, c2); Tri(c2, top, d); Tri(d, top, a); Tri(a, c1, c2); Tri(a, c2, d);
            var m = new Mesh { name = "IntroPyramid" };
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            owned.Add(m);
            return m;
        }

        /// <summary>A unit open dome (the top of a sphere, radius 1, its rim at y = 0) with the texture laid round it: u once round, v from the rim up to the top.</summary>
        Mesh Dome()
        {
            const int S = 24, R = 6;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int r = 0; r <= R; r++)
                for (int s = 0; s <= S; s++)
                {
                    float phi = r / (float)R * Mathf.PI * 0.5f, th = s / (float)S * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)));
                    uv.Add(new Vector2(s / (float)S, 1f - r / (float)R));
                }
            for (int r = 0; r < R; r++)
                for (int s = 0; s < S; s++)
                {
                    int a = r * (S + 1) + s, b = a + S + 1;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1); tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }
            var m = new Mesh { name = "IntroDome" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
            owned.Add(m);
            return m;
        }

        // ------------------------------------------------------------------ the city far below the windows

        /// <summary>Quads with a colour at each corner, collected for one mesh.</summary>
        sealed class CityMesh
        {
            public readonly List<Vector3> v = new();
            public readonly List<Vector2> uv = new();
            public readonly List<Color32> col = new();
            public readonly List<int> tri = new();

            /// <summary>One quad (corners a, b, c, d in order round it; the texture runs from (u0, v0) at a to (u1, v1) at c). Every corner fades to the haze by its own distance.</summary>
            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float u0, float u1, float v0, float v1)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
                col.Add(Fade(color, a)); col.Add(Fade(color, b)); col.Add(Fade(color, c)); col.Add(Fade(color, d));
                tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
            }
        }

        struct Tower { public float x, z, w, d, h, key, shade, u; public int tint; }

        /// <summary>Aerial perspective: the colour a corner has at this spot, seen from the office (about 17 % haze at 100 m, 60 % at 500 m, 85 % at 1 km).</summary>
        static Color Fade(Color c, Vector3 p)
        {
            float f = Mathf.Min(0.97f, 1f - Mathf.Exp(-(p - Eye).magnitude / 560f));
            return Color.Lerp(c, Haze, f);
        }

        void BuildCity()
        {
            // near (windows clear), middle (windows faint), far (no windows): z ranges, grid cell sizes, and the ground's quad sizes
            var packed = PackTowers();                                  // (the owner's towers first: the box towers of their plots stay out)
            float[] zNear = { 14f, 260f, 640f }, zFar = { 260f, 640f, 1500f }, cell = { 24f, 32f, 46f };
            float[] groundEnd = { 260f, 640f, 5000f }, groundStep = { 40f, 80f, 800f }, groundCell = { 60f, 120f, 1200f };
            var palette = new[] { new Color(0.66f, 0.70f, 0.76f), new Color(0.44f, 0.54f, 0.68f), new Color(0.80f, 0.75f, 0.67f), new Color(0.55f, 0.58f, 0.62f) };
            var street = new Color(0.30f, 0.33f, 0.38f);

            for (int band = 0; band < 3; band++)
            {
                var m = new CityMesh();

                // the ground (the streets): a grid of quads so each corner fades with its own distance
                for (float z = zNear[band]; z < groundEnd[band]; z += groundStep[band])
                {
                    float z2 = Mathf.Min(z + groundStep[band], groundEnd[band]);
                    float reach = 1.9f * z2 + 120f;
                    for (float x = -reach; x < reach; x += groundCell[band])
                        m.Face(new Vector3(x, GroundY, z), new Vector3(x + groundCell[band], GroundY, z), new Vector3(x + groundCell[band], GroundY, z2), new Vector3(x, GroundY, z2), street, 0.02f, 0.02f, 0.02f, 0.02f);
                }

                // the towers: one per grid cell (some cells stay empty: streets, parks), tall ones downtown; sorted far to near
                var towers = new List<Tower>();
                float step = cell[band];
                int rows = Mathf.FloorToInt((zFar[band] - zNear[band]) / step);
                for (int r = 0; r < rows; r++)
                {
                    float zc = zNear[band] + (r + 0.5f) * step;
                    int cols = Mathf.CeilToInt((1.9f * zc + 100f) / step);
                    for (int ci = -cols; ci <= cols; ci++)
                    {
                        int hx = ci + band * 1000, hz = r + band * 77;
                        if (Hash(hx, hz) < 0.12f) continue;
                        float w = step * (0.50f + 0.35f * Hash(hx + 11, hz)), d = step * (0.50f + 0.35f * Hash(hx, hz + 23));
                        float cx = ci * step + (Hash(hx + 3, hz + 5) - 0.5f) * (step - w), cz = zc + (Hash(hx + 7, hz + 9) - 0.5f) * (step - d);
                        if (band >= 1 && packed.Contains(PackIndex(cx, cz))) continue;                                              // (one of the owner's towers stands there)
                        float roll = Hash(hx + 31, hz + 41);
                        float h = 12f + 34f * roll * roll;
                        if (Hash(hx + 53, hz + 59) < 0.08f) h += 40f + 80f * Hash(hx + 61, hz + 67);                                     // an odd tower
                        if (zc > 150f && Mathf.Abs(cx) < 320f) h *= 1.5f;                                                             // downtown
                        if (band == 0) h = Mathf.Min(h, 60f);                                                                          // (nothing near reaches the jet)
                        float dx = cx - Eye.x, dz = cz - Eye.z;
                        towers.Add(new Tower { x = cx, z = cz, w = w, d = d, h = h, key = dx * dx + dz * dz, shade = 0.88f + 0.24f * Hash(hx + 71, hz + 73), u = Hash(hx + 79, hz + 83), tint = Mathf.FloorToInt(Hash(hx + 89, hz + 97) * palette.Length) % palette.Length });
                    }
                }
                towers.Sort((p, q) => q.key.CompareTo(p.key));                                                                      // farthest first
                foreach (var t in towers)
                {
                    float x0 = t.x - t.w * 0.5f, x1 = t.x + t.w * 0.5f, z0 = t.z - t.d * 0.5f, z1 = t.z + t.d * 0.5f, top = GroundY + t.h;
                    var wall = palette[t.tint] * t.shade; wall.a = 1f;
                    bool right = t.x > Eye.x;                                                                                      // which side face the camera can see
                    float xs = right ? x0 : x1;
                    var side = wall * (right ? 0.64f : 0.92f); side.a = 1f;
                    var front = wall * 0.80f; front.a = 1f;
                    float vTop = t.h / 14f;
                    m.Face(new Vector3(xs, GroundY, z0), new Vector3(xs, GroundY, z1), new Vector3(xs, top, z1), new Vector3(xs, top, z0), side, t.u, t.u + t.d / 16f, 0f, vTop);
                    m.Face(new Vector3(x0, GroundY, z0), new Vector3(x1, GroundY, z0), new Vector3(x1, top, z0), new Vector3(x0, top, z0), front, t.u, t.u + t.w / 16f, 0f, vTop);
                    m.Face(new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x0, top, z1), wall, 0.02f, 0.02f, 0.02f, 0.02f);
                }

                var mesh = new Mesh { name = "IntroCity" + band, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(m.v); mesh.SetUVs(0, m.uv); mesh.SetColors(m.col); mesh.SetTriangles(m.tri, 0); mesh.RecalculateBounds();
                owned.Add(mesh);
                var go = new GameObject("City" + band);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Unlit(band == 2 ? (Texture)Texture2D.whiteTexture : WindowTexture(band));
                Flat(mr, -7 - band);                                                                                               // far meshes first, the sky (-10) before them all
            }
        }

        // ------------------------------------------------------------------ the pictures (painted once, kept for the next run)

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Sky and clouds, 2048 x 512, for a quad that reaches from 10 degrees under the horizon (row 77) to 45 over it; a soft sun to the right.</summary>
        static Texture2D SkyTexture()
        {
            if (skyTexture) return skyTexture;
            const int W = 2048, H = 512, horizon = 77, sunX = 1446, sunY = 283;
            var px = new Color32[W * H];
            var top = new Color(0.28f, 0.50f, 0.92f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c = Haze;
                    if (y >= horizon)
                    {
                        float t = (y - horizon) / (float)(H - horizon);                       // 0 at the horizon, 1 at the top
                        c = Color.Lerp(Haze, top, Mathf.Pow(t, 0.6f));
                        float n = Mathf.PerlinNoise(x * 0.004f + 3f, y * 0.012f + 9f) * 0.65f + Mathf.PerlinNoise(x * 0.013f, y * 0.032f) * 0.35f;
                        float cloud = Mathf.SmoothStep(0.44f, 0.76f, n) * (1f - 0.35f * t);
                        c = Color.Lerp(c, new Color(0.99f, 0.99f, 1f), cloud);
                        float dx = x - sunX, dy = y - sunY, glow = Mathf.Exp(-(dx * dx + dy * dy) / (2f * 85f * 85f));
                        c = Color.Lerp(c, new Color(1f, 0.96f, 0.82f), glow * 0.85f);
                    }
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            skyTexture = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "IntroSky", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            skyTexture.SetPixels32(px);
            skyTexture.Apply(false, false);
            return skyTexture;
        }

        /// <summary>A tile of 4 x 4 windows (a 16 x 14 m piece of a tower). Level 0: clear dark glass in a pale wall (near towers); 1: faint (middle distance).</summary>
        static Texture2D WindowTexture(int level)
        {
            if (windowTextures[level]) return windowTextures[level];
            float contrast = level == 0 ? 1f : 0.4f;
            const int T = 64;
            var px = new Color32[T * T];
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16;
                    Color c = Color.white;
                    if (lx >= 4 && lx < 12 && ly >= 5 && ly < 13)
                    {
                        var glass = Color.Lerp(new Color(0.40f, 0.52f, 0.70f), new Color(0.66f, 0.78f, 0.92f), Hash(cx + 5, cy + 9));
                        c = Color.Lerp(Color.white, glass, contrast);
                    }
                    c.a = 1f;
                    px[y * T + x] = c;
                }
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, true) { name = "IntroWindows" + level, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            windowTextures[level] = tex;
            return tex;
        }

        /// <summary>The lamp's stained glass: 12 panels round, 4 rows up, dark lead lines between (192 x 64).</summary>
        static Texture2D LampTexture()
        {
            if (lampTexture) return lampTexture;
            const int W = 192, H = 64;
            var palette = new[] { new Color(1.00f, 0.66f, 0.20f), new Color(0.90f, 0.40f, 0.14f), new Color(0.40f, 0.68f, 0.30f), new Color(1.00f, 0.84f, 0.38f) };
            var lead = new Color(0.13f, 0.08f, 0.04f);
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16;
                    Color c = lx < 2 || ly < 2 ? lead : palette[Mathf.FloorToInt(Hash(cx * 3 + 1, cy * 5 + 2) * palette.Length) % palette.Length] * (0.9f + 0.2f * Hash(cx, cy + 40));
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            lampTexture = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "IntroLampGlass", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            lampTexture.SetPixels32(px);
            lampTexture.Apply(true, false);
            return lampTexture;
        }
    }
}

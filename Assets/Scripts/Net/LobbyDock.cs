using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The lobby's background: the Loading Docks (the owner, 2026-10-17: "make the lobby feel more alive, have the background be in the loading dock"). A concrete floor, the back wall
    /// with three roller doors - two shut, the third rolled up onto the white trailer backed in, its inside lit - hazard paint, bollards, pallets and boxes, a forklift, steel
    /// shelving, drums, pipes, a clock; warm lamps hang from the ceiling and an orange beacon turns by the open bay. Built entirely from primitives in the same flat matt colours the
    /// game's own dock uses, so the lobby looks like the place the run starts in. <see cref="LobbyDockLife"/> keeps it moving.
    /// </summary>
    public static class LobbyDock
    {
        static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();

        internal static Material Mat(Color c, float gloss = 0.12f, float metal = 0f)
        {
            if (mats.TryGetValue(c, out var m) && m) return m;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            m = new Material(sh) { name = "LobbyDock", color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            mats[c] = m;
            return m;
        }

        /// <summary>A lit surface (a lamp, a tube, the trailer's ceiling strip): the colour glows.</summary>
        internal static Material Glow(Color c, float strength)
        {
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { name = "LobbyDockGlow", color = c * 0.4f };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * strength);
            return m;
        }

        static GameObject Make(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            var col = g.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col); }
            return g;
        }

        internal static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, Vector3? euler = null) => Make(PrimitiveType.Cube, name, pos, size, mat, parent, euler);
        internal static GameObject Cyl(string name, Vector3 pos, float radius, float height, Material mat, Transform parent, Vector3? euler = null) => Make(PrimitiveType.Cylinder, name, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, parent, euler);

        internal static Light MakeLight(string name, LightType type, Vector3 pos, Color colour, float intensity, float range, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = type; l.color = colour; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        /// <summary>Builds the loading dock under <paramref name="parent"/> and returns the component that animates it.</summary>
        public static LobbyDockLife Build(Transform parent)
        {
            var root = new GameObject("LoadingDock").transform;
            root.SetParent(parent, false);
            var life = root.gameObject.AddComponent<LobbyDockLife>();

            // the colours (the same family as the game's dock: matt, a little worn)
            var concrete = Mat(new Color(0.33f, 0.34f, 0.36f), 0.22f);
            var wall = Mat(new Color(0.56f, 0.55f, 0.51f), 0.05f);
            var baseBand = Mat(new Color(0.20f, 0.22f, 0.25f), 0.1f);
            var steelDoor = Mat(new Color(0.62f, 0.65f, 0.70f), 0.3f, 0.3f);
            var rib = Mat(new Color(0.40f, 0.43f, 0.47f), 0.3f, 0.3f);
            var dark = Mat(new Color(0.11f, 0.11f, 0.12f), 0.1f);
            var hazard = Mat(new Color(0.93f, 0.74f, 0.10f), 0.15f);
            var white = Mat(new Color(0.88f, 0.89f, 0.91f), 0.3f);
            var cardboard = Mat(new Color(0.62f, 0.46f, 0.30f), 0.02f);
            var cardboard2 = Mat(new Color(0.55f, 0.40f, 0.26f), 0.02f);
            var tape = Mat(new Color(0.78f, 0.66f, 0.45f), 0.1f);
            var wood = Mat(new Color(0.48f, 0.36f, 0.23f), 0.03f);
            var steel = Mat(new Color(0.50f, 0.53f, 0.57f), 0.35f, 0.5f);
            var steelDark = Mat(new Color(0.24f, 0.27f, 0.31f), 0.3f, 0.4f);
            var red = Mat(new Color(0.68f, 0.12f, 0.10f), 0.25f);
            var blue = Mat(new Color(0.15f, 0.30f, 0.58f), 0.25f);
            var yellowMachine = Mat(new Color(0.90f, 0.66f, 0.10f), 0.25f);
            var rubber = Mat(new Color(0.07f, 0.07f, 0.08f), 0.05f);

            // ---- the floor: polished concrete, a long way back
            Box("Floor", new Vector3(0f, -0.15f, 12f), new Vector3(70f, 0.3f, 44f), concrete, root);

            // ---- the back wall with three doors (centres gx-5.8, gx, gx+5.8; the third is the open bay)
            const float zWall = 8.0f, doorW = 5.0f, doorH = 3.1f, wallH = 8.0f;
            float gx = -0.8f;
            float[] doorX = { gx - 5.8f, gx, gx + 5.8f };
            float prevEnd = -26f;
            for (int i = 0; i < 3; i++)
            {
                float left = doorX[i] - doorW * 0.5f, right = doorX[i] + doorW * 0.5f;
                if (left > prevEnd) Box("WallPier", new Vector3((prevEnd + left) * 0.5f, wallH * 0.5f, zWall + 0.25f), new Vector3(left - prevEnd, wallH, 0.5f), wall, root);
                Box("Lintel", new Vector3(doorX[i], doorH + (wallH - doorH) * 0.5f, zWall + 0.25f), new Vector3(doorW, wallH - doorH, 0.5f), wall, root);
                Box("FrameL", new Vector3(left + 0.07f, doorH * 0.5f, zWall - 0.05f), new Vector3(0.14f, doorH, 0.18f), steelDark, root);
                Box("FrameR", new Vector3(right - 0.07f, doorH * 0.5f, zWall - 0.05f), new Vector3(0.14f, doorH, 0.18f), steelDark, root);
                Box("FrameTop", new Vector3(doorX[i], doorH - 0.07f, zWall - 0.05f), new Vector3(doorW, 0.14f, 0.18f), steelDark, root);
                prevEnd = right;
                // the paint in front of every door
                Box("HazardLine", new Vector3(doorX[i], 0.012f, zWall - 1.7f), new Vector3(doorW, 0.025f, 0.28f), hazard, root);
                if (i < 2)
                {
                    // a shut roller door: slats, ribs and a handle
                    Box("RollerDoor", new Vector3(doorX[i], doorH * 0.5f - 0.05f, zWall + 0.02f), new Vector3(doorW - 0.3f, doorH - 0.15f, 0.1f), steelDoor, root);
                    for (int k = 0; k < 7; k++) Box("Slat", new Vector3(doorX[i], 0.35f + k * 0.42f, zWall - 0.07f), new Vector3(doorW - 0.32f, 0.07f, 0.06f), rib, root);
                    Box("Handle", new Vector3(doorX[i], 1.0f, zWall - 0.14f), new Vector3(0.7f, 0.1f, 0.08f), dark, root);
                    Box("RollerBox", new Vector3(doorX[i], doorH + 0.12f, zWall - 0.2f), new Vector3(doorW + 0.2f, 0.26f, 0.4f), steelDark, root);
                }
            }
            Box("WallPier", new Vector3((prevEnd + 26f) * 0.5f, wallH * 0.5f, zWall + 0.25f), new Vector3(26f - prevEnd, wallH, 0.5f), wall, root);
            // a darker painted band along the foot of the wall
            Box("WallBase", new Vector3(0f, 0.55f, zWall - 0.03f), new Vector3(60f, 1.1f, 0.06f), baseBand, root);

            // ---- the open bay: the roller door rolled up, the trailer backed in, its inside lit and stocked
            float bx = doorX[2];
            Box("BayRoll", new Vector3(bx, doorH + 0.12f, zWall - 0.2f), new Vector3(doorW + 0.2f, 0.34f, 0.46f), steelDark, root);
            float tw = 3.7f, tl = 9.0f, tz0 = zWall + 0.35f, tz1 = tz0 + tl;
            Box("TrailerFloor", new Vector3(bx, 0.02f, (tz0 + tz1) * 0.5f), new Vector3(tw, 0.06f, tl), Mat(new Color(0.42f, 0.33f, 0.22f), 0.05f), root);
            Box("TrailerWallL", new Vector3(bx - tw * 0.5f - 0.08f, doorH * 0.5f, (tz0 + tz1) * 0.5f), new Vector3(0.16f, doorH, tl), white, root);
            Box("TrailerWallR", new Vector3(bx + tw * 0.5f + 0.08f, doorH * 0.5f, (tz0 + tz1) * 0.5f), new Vector3(0.16f, doorH, tl), white, root);
            Box("TrailerRoof", new Vector3(bx, doorH + 0.08f, (tz0 + tz1) * 0.5f), new Vector3(tw + 0.32f, 0.16f, tl), white, root);
            Box("TrailerBack", new Vector3(bx, doorH * 0.5f, tz1), new Vector3(tw + 0.32f, doorH, 0.16f), Mat(new Color(0.78f, 0.79f, 0.82f), 0.2f), root);
            Box("DockSealL", new Vector3(bx - tw * 0.5f - 0.35f, doorH * 0.5f, zWall + 0.28f), new Vector3(0.5f, doorH, 0.3f), rubber, root);
            Box("DockSealR", new Vector3(bx + tw * 0.5f + 0.35f, doorH * 0.5f, zWall + 0.28f), new Vector3(0.5f, doorH, 0.3f), rubber, root);
            var strip = Box("TrailerLamp", new Vector3(bx, doorH - 0.06f, (tz0 + tz1) * 0.5f - 1.5f), new Vector3(0.3f, 0.05f, 3.4f), Glow(new Color(1f, 0.93f, 0.78f), 2.4f), root);
            life.trailerLamp = MakeLight("TrailerLight", LightType.Point, new Vector3(bx, doorH - 0.4f, tz0 + 2.2f), new Color(1f, 0.90f, 0.74f), 1.7f, 8.5f, root);
            // what is in the trailer: boxes along both walls
            for (int k = 0; k < 6; k++)
            {
                float z = tz0 + 1.2f + k * 1.2f;
                int stack = 1 + (k * 7 % 3);
                for (int h = 0; h < stack; h++)
                {
                    Box("TrailerBoxL", new Vector3(bx - tw * 0.5f + 0.55f, 0.35f + h * 0.62f, z), new Vector3(0.85f, 0.6f, 0.9f), (k + h) % 2 == 0 ? cardboard : cardboard2, root, new Vector3(0f, (k * 13 + h * 7) % 9 - 4f, 0f));
                    if ((k + h) % 3 != 0) Box("TrailerBoxR", new Vector3(bx + tw * 0.5f - 0.55f, 0.35f + h * 0.62f, z + 0.3f), new Vector3(0.85f, 0.6f, 0.9f), (k + h) % 2 == 0 ? cardboard2 : cardboard, root, new Vector3(0f, (k * 5 + h * 11) % 9 - 4f, 0f));
                }
            }
            // bollards by the bay
            foreach (float off in new[] { -doorW * 0.5f - 0.5f, doorW * 0.5f + 0.5f })
            {
                Cyl("Bollard", new Vector3(bx + off, 0.6f, zWall - 1.2f), 0.13f, 1.2f, hazard, root);
                Cyl("BollardCap", new Vector3(bx + off, 1.22f, zWall - 1.2f), 0.14f, 0.06f, dark, root);
            }

            // ---- the pedestrian lane: white dashes across the floor
            for (int d = -14; d <= 10; d++) Box("Dash", new Vector3(d * 1.2f, 0.01f, 3.2f), new Vector3(0.55f, 0.012f, 0.12f), white, root);

            // ---- pallets and boxes on the right, a forklift, drums
            Pallet(root, new Vector3(5.2f, 0f, 4.6f), 8f, wood);
            Stack(root, new Vector3(5.2f, 0.14f, 4.6f), 3, cardboard, cardboard2, tape, 11);
            Pallet(root, new Vector3(6.6f, 0f, 5.9f), -12f, wood);
            Stack(root, new Vector3(6.6f, 0.14f, 5.9f), 2, cardboard2, cardboard, tape, 5);
            Pallet(root, new Vector3(3.1f, 0f, 6.1f), 4f, wood);
            Stack(root, new Vector3(3.1f, 0.14f, 6.1f), 4, cardboard, cardboard2, tape, 3);
            Forklift(root, new Vector3(4.9f, 0f, 1.9f), -128f, yellowMachine, steelDark, dark, steel, rubber);
            Cyl("DrumRed", new Vector3(7.4f, 0.45f, 3.4f), 0.29f, 0.9f, red, root);
            Cyl("DrumRedLid", new Vector3(7.4f, 0.91f, 3.4f), 0.3f, 0.04f, steelDark, root);
            Cyl("DrumBlue", new Vector3(8.05f, 0.45f, 4.0f), 0.29f, 0.9f, blue, root);
            Cyl("DrumBlueLid", new Vector3(8.05f, 0.91f, 4.0f), 0.3f, 0.04f, steelDark, root);
            Cyl("DrumRed2", new Vector3(7.0f, 0.45f, 4.1f), 0.29f, 0.9f, red, root);

            // ---- left of the stage: steel shelving with boxes, a stack of crates
            ShelfUnit(root, new Vector3(-5.6f, 0f, 6.7f), steel, steelDark, cardboard, cardboard2);
            ShelfUnit(root, new Vector3(-3.7f, 0f, 6.8f), steel, steelDark, cardboard2, cardboard);
            Stack(root, new Vector3(-4.7f, 0f, 3.4f), 3, cardboard, cardboard2, tape, 17);
            Stack(root, new Vector3(-3.7f, 0f, 3.8f), 2, cardboard2, cardboard, tape, 23);
            Pallet(root, new Vector3(-2.6f, 0f, 5.0f), 25f, wood);

            // ---- on the wall: extinguisher, pipes, a clock, a notice board
            Cyl("Extinguisher", new Vector3(-9.6f, 1.25f, zWall - 0.2f), 0.11f, 0.5f, red, root);
            Cyl("ExtinguisherTop", new Vector3(-9.6f, 1.55f, zWall - 0.2f), 0.05f, 0.1f, dark, root);
            Box("ExtBracket", new Vector3(-9.6f, 1.15f, zWall - 0.1f), new Vector3(0.26f, 0.08f, 0.1f), steelDark, root);
            Cyl("Pipe", new Vector3(0f, 5.2f, zWall - 0.2f), 0.09f, 60f, steelDark, root, new Vector3(0f, 0f, 90f));
            Cyl("Pipe2", new Vector3(0f, 5.55f, zWall - 0.2f), 0.06f, 60f, Mat(new Color(0.72f, 0.20f, 0.16f), 0.25f), root, new Vector3(0f, 0f, 90f));
            for (int p = -6; p <= 5; p++) Box("PipeBracket", new Vector3(p * 3.4f, 5.37f, zWall - 0.12f), new Vector3(0.1f, 0.6f, 0.12f), steelDark, root);
            var clock = new GameObject("Clock").transform;
            clock.SetParent(root, false);
            clock.localPosition = new Vector3(gx, 4.5f, zWall - 0.12f);
            Cyl("ClockFace", Vector3.zero, 0.42f, 0.06f, white, clock, new Vector3(90f, 0f, 0f));
            Cyl("ClockRim", new Vector3(0f, 0f, 0.015f), 0.46f, 0.05f, dark, clock, new Vector3(90f, 0f, 0f));
            life.clockHour = ClockHand(clock, "Hour", 0.24f, 0.035f, dark);
            life.clockMinute = ClockHand(clock, "Minute", 0.34f, 0.025f, dark);
            life.clockSecond = ClockHand(clock, "Second", 0.36f, 0.012f, red);
            Box("NoticeBoard", new Vector3(gx - 5.8f - 2.9f, 2.0f, zWall - 0.08f), new Vector3(1.4f, 1.0f, 0.06f), Mat(new Color(0.45f, 0.33f, 0.2f), 0.02f), root);
            var paper = Mat(new Color(0.9f, 0.9f, 0.85f), 0.05f);
            Box("Note1", new Vector3(gx - 5.8f - 3.2f, 2.1f, zWall - 0.12f), new Vector3(0.38f, 0.5f, 0.01f), paper, root, new Vector3(0f, 0f, 3f));
            Box("Note2", new Vector3(gx - 5.8f - 2.7f, 2.0f, zWall - 0.12f), new Vector3(0.42f, 0.34f, 0.01f), Mat(new Color(0.95f, 0.85f, 0.3f), 0.05f), root, new Vector3(0f, 0f, -4f));
            Box("Note3", new Vector3(gx - 5.8f - 2.45f, 2.25f, zWall - 0.12f), new Vector3(0.3f, 0.4f, 0.01f), paper, root, new Vector3(0f, 0f, 6f));

            // ---- the ceiling and its lamps
            var ceiling = Box("Ceiling", new Vector3(0f, 7.6f, 6f), new Vector3(60f, 0.3f, 30f), Mat(new Color(0.16f, 0.17f, 0.19f), 0.05f), root);
            ceiling.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;       // (a roof that cast a shadow would darken the floor from a sharp line on)
            life.lamps = new Transform[2];
            life.lampLights = new Light[2];
            Vector3[] lampAt = { new Vector3(-2.6f, 0f, 2.2f), new Vector3(2.8f, 0f, 5.4f) };
            for (int i = 0; i < 2; i++)
            {
                var pivot = new GameObject("LampPivot" + i).transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(lampAt[i].x, 7.4f, lampAt[i].z);
                Cyl("Cord", new Vector3(0f, -1.45f, 0f), 0.015f, 2.9f, dark, pivot);
                Cyl("Shade", new Vector3(0f, -2.95f, 0f), 0.42f, 0.22f, steelDark, pivot);
                Box("Tube", new Vector3(0f, -3.08f, 0f), new Vector3(0.5f, 0.04f, 0.5f), Glow(new Color(1f, 0.92f, 0.76f), 2.2f), pivot);
                life.lampLights[i] = MakeLight("LampLight" + i, LightType.Point, new Vector3(0f, -3.3f, 0f), new Color(1f, 0.88f, 0.70f), 1.5f, 11f, pivot);
                life.lamps[i] = pivot;
            }
            // a fluorescent tube that flickers, over the first door
            life.tubeRenderer = Box("FlickerTube", new Vector3(gx - 5.8f, 4.8f, zWall - 0.35f), new Vector3(1.6f, 0.08f, 0.12f), Glow(new Color(0.85f, 0.95f, 1f), 2.6f), root).GetComponent<Renderer>();
            life.tubeLight = MakeLight("FlickerLight", LightType.Point, new Vector3(gx - 5.8f, 4.4f, zWall - 1.2f), new Color(0.85f, 0.95f, 1f), 0.9f, 6f, root);

            // ---- the beacon by the open bay: an orange lamp that turns and throws a spot of light across the floor
            var beacon = new GameObject("Beacon").transform;
            beacon.SetParent(root, false);
            beacon.localPosition = new Vector3(bx - doorW * 0.5f - 1.0f, 3.6f, zWall - 0.35f);
            Cyl("BeaconBase", Vector3.zero, 0.16f, 0.12f, dark, beacon);
            Cyl("BeaconGlass", new Vector3(0f, 0.16f, 0f), 0.13f, 0.2f, Glow(new Color(1f, 0.42f, 0.05f), 1.1f), beacon);
            var spin = new GameObject("BeaconSpin").transform;
            spin.SetParent(beacon, false);
            spin.localPosition = new Vector3(0f, 0.16f, 0f);
            var spot = MakeLight("BeaconSpot", LightType.Spot, Vector3.zero, new Color(1f, 0.55f, 0.15f), 3.5f, 11f, spin);
            spot.spotAngle = 48f;
            spot.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            life.beaconSpin = spin;

            // ---- a cool glow from outside, a soft fill
            MakeLight("SkyFill", LightType.Point, new Vector3(bx + 1f, 2.4f, zWall - 3.5f), new Color(0.55f, 0.68f, 1f), 0.55f, 9f, root);

            // ---- dust drifting in the light
            life.dust = MakeDust(root);
            return life;
        }

        internal static void Pallet(Transform parent, Vector3 pos, float yaw, Material wood)
        {
            var p = new GameObject("Pallet").transform;
            p.SetParent(parent, false);
            p.localPosition = pos; p.localEulerAngles = new Vector3(0f, yaw, 0f);
            for (int i = -1; i <= 1; i++) Box("Runner", new Vector3(i * 0.5f, 0.05f, 0f), new Vector3(0.1f, 0.1f, 1.0f), wood, p);
            for (int i = 0; i < 5; i++) Box("Board", new Vector3(0f, 0.115f, -0.4f + i * 0.2f), new Vector3(1.2f, 0.03f, 0.16f), wood, p);
        }

        static void Stack(Transform parent, Vector3 pos, int boxes, Material a, Material b, Material tape, int seed)
        {
            for (int i = 0; i < boxes; i++)
            {
                float s = 0.62f + ((seed * (i + 3)) % 5) * 0.04f;
                float yaw = ((seed * (i + 5)) % 15) - 7f;
                Vector3 at = pos + new Vector3(((seed + i) % 3 - 1) * 0.03f, 0.06f + s * 0.5f + i * 0.66f, ((seed * i) % 3 - 1) * 0.03f);
                Box("Box", at, new Vector3(s, 0.6f, s * 0.9f), (i + seed) % 2 == 0 ? a : b, parent, new Vector3(0f, yaw, 0f));
                Box("BoxTape", at + new Vector3(0f, 0.305f, 0f), new Vector3(0.1f, 0.01f, s * 0.9f + 0.01f), tape, parent, new Vector3(0f, yaw, 0f));
            }
        }

        internal static void ShelfUnit(Transform parent, Vector3 pos, Material steel, Material dark, Material boxA, Material boxB)
        {
            var s = new GameObject("Shelf").transform;
            s.SetParent(parent, false);
            s.localPosition = pos;
            foreach (float x in new[] { -0.85f, 0.85f })
                foreach (float z in new[] { -0.3f, 0.3f })
                    Box("Upright", new Vector3(x, 1.2f, z), new Vector3(0.06f, 2.4f, 0.06f), dark, s);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.15f + i * 0.75f;
                Box("ShelfBoard", new Vector3(0f, y, 0f), new Vector3(1.8f, 0.04f, 0.66f), steel, s);
                if (i < 3)
                {
                    Box("ShelfBox", new Vector3(-0.45f, y + 0.3f, 0f), new Vector3(0.7f, 0.5f, 0.5f), (i % 2 == 0) ? boxA : boxB, s, new Vector3(0f, i * 3f - 3f, 0f));
                    if (i != 1) Box("ShelfBox", new Vector3(0.4f, y + 0.25f, 0f), new Vector3(0.6f, 0.4f, 0.5f), (i % 2 == 0) ? boxB : boxA, s, new Vector3(0f, 4f - i * 2f, 0f));
                }
            }
        }

        internal static void Forklift(Transform parent, Vector3 pos, float yaw, Material body, Material darkSteel, Material dark, Material steel, Material rubber)
        {
            var f = new GameObject("Forklift").transform;
            f.SetParent(parent, false);
            f.localPosition = pos; f.localEulerAngles = new Vector3(0f, yaw, 0f);
            // (the forks point along +z)
            Box("Chassis", new Vector3(0f, 0.55f, -0.25f), new Vector3(1.05f, 0.65f, 1.7f), body, f);
            Box("Counterweight", new Vector3(0f, 0.7f, -1.15f), new Vector3(1.05f, 0.8f, 0.4f), darkSteel, f);
            Box("Seat", new Vector3(0f, 1.05f, -0.4f), new Vector3(0.5f, 0.12f, 0.5f), dark, f);
            Box("SeatBack", new Vector3(0f, 1.3f, -0.62f), new Vector3(0.5f, 0.45f, 0.1f), dark, f);
            foreach (float x in new[] { -0.45f, 0.45f })
            {
                Box("GuardFront", new Vector3(x, 1.55f, 0.3f), new Vector3(0.06f, 1.3f, 0.06f), darkSteel, f);
                Box("GuardRear", new Vector3(x, 1.55f, -0.95f), new Vector3(0.06f, 1.3f, 0.06f), darkSteel, f);
                Cyl("Wheel", new Vector3(x * 1.1f, 0.3f, 0.2f), 0.3f, 0.22f, rubber, f, new Vector3(0f, 0f, 90f));
                Cyl("Wheel", new Vector3(x * 1.05f, 0.26f, -0.85f), 0.26f, 0.2f, rubber, f, new Vector3(0f, 0f, 90f));
                Box("Mast", new Vector3(x * 0.55f, 1.15f, 0.72f), new Vector3(0.08f, 2.1f, 0.1f), darkSteel, f);
                Box("Fork", new Vector3(x * 0.6f, 0.08f, 1.3f), new Vector3(0.12f, 0.05f, 1.15f), steel, f);
            }
            Box("GuardRoof", new Vector3(0f, 2.2f, -0.32f), new Vector3(1.05f, 0.05f, 1.35f), darkSteel, f);
            Box("Carriage", new Vector3(0f, 0.35f, 0.78f), new Vector3(1.0f, 0.5f, 0.06f), darkSteel, f);
            Cyl("SteeringColumn", new Vector3(0f, 1.0f, 0.1f), 0.025f, 0.5f, dark, f, new Vector3(25f, 0f, 0f));
        }

        internal static Transform ClockHand(Transform clock, string name, float length, float width, Material mat)
        {
            var pivot = new GameObject(name + "Pivot").transform;
            pivot.SetParent(clock, false);
            pivot.localPosition = new Vector3(0f, 0f, -0.05f);
            Box(name, new Vector3(0f, length * 0.5f, 0f), new Vector3(width, length, 0.012f), mat, pivot);
            return pivot;
        }

        static ParticleSystem MakeDust(Transform parent)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 3f, 2f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new Color(1f, 0.96f, 0.85f, 0.35f);
            main.maxParticles = 220; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 20f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22f, 6f, 14f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f); vel.y = new ParticleSystem.MinMaxCurve(-0.015f, 0.04f); vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            var sh = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh) { name = "LobbyDust", mainTexture = SoftDot() };
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(1f, 1f, 1f, 0.5f));
            rend.sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        static Texture2D SoftDot()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f)) / (n * 0.5f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            t.Apply();
            return t;
        }
    }

    /// <summary>Keeps the lobby's loading dock moving: the lamps sway, one tube flickers, the beacon turns, the clock tells the time, the trailer's light breathes and dust drifts.</summary>
    public sealed class LobbyDockLife : MonoBehaviour
    {
        public Transform[] lamps;
        public Light[] lampLights;
        public Light trailerLamp, tubeLight;
        public Renderer tubeRenderer;
        public Transform beaconSpin, clockHour, clockMinute, clockSecond;
        public ParticleSystem dust;

        float trailerBase, tubeBase, nextFlicker, flickerUntil;
        float[] lampBase;

        void Start()
        {
            if (trailerLamp) trailerBase = trailerLamp.intensity;
            if (tubeLight) tubeBase = tubeLight.intensity;
            if (lampLights != null) { lampBase = new float[lampLights.Length]; for (int i = 0; i < lampLights.Length; i++) lampBase[i] = lampLights[i] ? lampLights[i].intensity : 0f; }
            nextFlicker = Time.time + 2f;
        }

        void Update()
        {
            float t = Time.time;
            if (lamps != null)
                for (int i = 0; i < lamps.Length; i++)
                    if (lamps[i]) lamps[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + i * 2.1f) * 2.2f, 0f, Mathf.Sin(t * 0.53f + i * 1.3f) * 2.6f);
            if (beaconSpin) beaconSpin.Rotate(0f, 150f * Time.deltaTime, 0f, Space.Self);
            if (trailerLamp) trailerLamp.intensity = trailerBase * (0.94f + 0.06f * Mathf.Sin(t * 1.7f));

            // the tube: a steady glow, now and then a stutter
            if (t >= nextFlicker) { flickerUntil = t + Random.Range(0.12f, 0.45f); nextFlicker = t + Random.Range(3.5f, 9f); }
            bool off = t < flickerUntil && Mathf.Sin(t * 70f) > -0.2f;
            if (tubeLight) tubeLight.intensity = off ? tubeBase * 0.15f : tubeBase;
            if (tubeRenderer) tubeRenderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.85f, 0.95f, 1f) * (off ? 0.3f : 2.6f));

            // the clock: the real time
            var now = System.DateTime.Now;
            if (clockSecond) clockSecond.localRotation = Quaternion.Euler(0f, 0f, -(now.Second + now.Millisecond / 1000f) * 6f);
            if (clockMinute) clockMinute.localRotation = Quaternion.Euler(0f, 0f, -(now.Minute + now.Second / 60f) * 6f);
            if (clockHour) clockHour.localRotation = Quaternion.Euler(0f, 0f, -((now.Hour % 12) + now.Minute / 60f) * 30f);
        }
    }
}
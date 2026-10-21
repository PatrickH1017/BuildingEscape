using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The TV on the wall in the corner of the Loading Docks. The owner, 2026-10-17: the run-start cinematic has the workers "watching the boss's monologue on a tv mounted on the
    /// wall in the corner of the loading docks (this is to give the players more of a feel that they are role-playing as the [dock workers])". A flat screen on a bracket, high on
    /// the south wall near the south-east corner, facing into the room; the space in front of it is kept clear of storage (<c>FloorGenerator.Dock</c>). Most of the time it
    /// sits on standby (colour bars, a faint glow); during the cinematic <see cref="IntroCinematic"/> puts the boss's office camera on it (<see cref="ShowProgram"/>), and when the
    /// broadcast ends it goes to static (<see cref="ShowStatic"/>). Its own light makes the workers' faces glow. Local axes: the TV hangs on the wall at z = 0 and looks along +z.
    /// </summary>
    public sealed class DockTv : MonoBehaviour
    {
        /// <summary>The picture is 1.5 m wide and 0.84 m tall (16:9), its middle 2.15 m above the floor.</summary>
        public const float ScreenWidth = 1.5f, ScreenHeight = 0.84f, CentreHeight = 2.15f;

        public static DockTv Current { get; private set; }

        enum Mode { Standby, Program, Static, Off }

        Mode mode = Mode.Standby;
        Renderer screen;
        Material screenMat;
        Light glow;
        Texture2D standby, noise;
        Color32[] noisePixels;
        float nextNoise;
        float flicker;

        /// <summary>The floor point under the middle of the screen, and the way out of the TV into the room, to the TV's right-hand side seen from the room (the room's +x), and up.</summary>
        public Vector3 Origin => transform.position;
        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;

        /// <summary>A point in the TV's own frame: <paramref name="side"/> metres along <see cref="Right"/>, <paramref name="up"/> above the floor, <paramref name="outward"/> out of the wall into the room.</summary>
        public Vector3 Point(float side, float up, float outward) => transform.position + transform.right * side + Vector3.up * up + transform.forward * outward;

        public bool OnProgram => mode == Mode.Program;

        public static DockTv Build(Transform parent, Vector3 floorPoint)
        {
            var root = new GameObject("DockTv");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floorPoint, Quaternion.identity);
            var tv = root.AddComponent<DockTv>();
            tv.Make();
            return tv;
        }

        readonly System.Collections.Generic.List<Object> owned = new System.Collections.Generic.List<Object>();      // (freed with the TV: it is built again every time the docks are)

        Material Lit(Color c, float gloss, float metallic)
        {
            var m = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse")) { color = c, hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            owned.Add(m);
            return m;
        }

        GameObject Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var c = g.GetComponent<Collider>(); if (c) Destroy(c);
            g.transform.SetParent(transform, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        void Make()
        {
            var dark = Lit(new Color(0.035f, 0.036f, 0.04f), 0.55f, 0.3f);
            var steel = Lit(new Color(0.20f, 0.21f, 0.23f), 0.5f, 0.6f);
            var led = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.15f, 0.1f), hideFlags = HideFlags.HideAndDontSave };
            owned.Add(led);
            // the bracket on the wall, the body, the picture
            Part("TvBracket", PrimitiveType.Cube, new Vector3(0f, CentreHeight, 0.045f), new Vector3(0.26f, 0.24f, 0.09f), steel);
            Part("TvArm", PrimitiveType.Cube, new Vector3(0f, CentreHeight + 0.14f, 0.07f), new Vector3(0.6f, 0.04f, 0.06f), steel);
            Part("TvBody", PrimitiveType.Cube, new Vector3(0f, CentreHeight, 0.115f), new Vector3(ScreenWidth + 0.08f, ScreenHeight + 0.08f, 0.05f), dark);
            screenMat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            var quad = Part("TvScreen", PrimitiveType.Quad, new Vector3(0f, CentreHeight, 0.142f), new Vector3(ScreenWidth, ScreenHeight, 1f), screenMat);
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);            // (a Quad is seen from its back: this one faces the room)
            screen = quad.GetComponent<Renderer>();
            Part("TvLed", PrimitiveType.Cube, new Vector3(ScreenWidth * 0.5f - 0.05f, CentreHeight - ScreenHeight * 0.5f - 0.015f, 0.142f), new Vector3(0.018f, 0.008f, 0.004f), led);

            glow = new GameObject("TvGlow").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, CentreHeight - 0.1f, 1.1f);
            glow.type = LightType.Point; glow.range = 9f; glow.intensity = 0.3f; glow.color = new Color(0.62f, 0.74f, 1f); glow.shadows = LightShadows.None;

            standby = MakeBars();
            noise = new Texture2D(64, 36, TextureFormat.RGBA32, false) { name = "TvStatic", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            noisePixels = new Color32[64 * 36];
            ShowStandby();
        }

        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (screenMat) Destroy(screenMat);
            if (standby) Destroy(standby);
            if (noise) Destroy(noise);
            foreach (var o in owned) if (o) Destroy(o);
            owned.Clear();
        }

        /// <summary>The picture a camera makes (the boss's office): full brightness, the glow follows it.</summary>
        public void ShowProgram(Texture t)
        {
            mode = Mode.Program;
            screenMat.mainTexture = t;
            screenMat.color = Color.white;
            glow.intensity = 1.25f;
        }

        /// <summary>Static: the broadcast is over.</summary>
        public void ShowStatic()
        {
            if (mode == Mode.Static) return;
            mode = Mode.Static;
            screenMat.mainTexture = noise;
            screenMat.color = new Color(0.9f, 0.92f, 0.95f);
            glow.intensity = 0.95f;
            Shuffle();
        }

        /// <summary>Dark: the TV is off (the opening of the Intro, before the announcement); only its LED glows.</summary>
        public void ShowOff()
        {
            if (mode == Mode.Off) return;
            mode = Mode.Off;
            screenMat.mainTexture = Texture2D.blackTexture;
            screenMat.color = new Color(0.03f, 0.03f, 0.035f);
            glow.intensity = 0.02f;
        }

        /// <summary>Colour bars, dimmed, and a faint glow: nobody is watching.</summary>
        public void ShowStandby()
        {
            mode = Mode.Standby;
            screenMat.mainTexture = standby;
            screenMat.color = new Color(0.42f, 0.42f, 0.46f);
            glow.intensity = 0.3f;
        }

        void Update()
        {
            if (mode == Mode.Static && Time.unscaledTime >= nextNoise) Shuffle();
            if (mode != Mode.Standby && mode != Mode.Off)
            {
                flicker = Mathf.Lerp(flicker, Mathf.PerlinNoise(Time.unscaledTime * 9f, 3.7f), 0.5f);
                float basis = mode == Mode.Program ? 1.25f : 0.95f;
                glow.intensity = basis * (0.88f + 0.24f * flicker);
            }
        }

        void Shuffle()
        {
            nextNoise = Time.unscaledTime + 0.05f;
            for (int i = 0; i < noisePixels.Length; i++)
            {
                byte v = (byte)Random.Range(30, 225);
                noisePixels[i] = new Color32(v, v, v, 255);
            }
            // a few rolling bars
            int bar = Random.Range(0, 36);
            for (int x = 0; x < 64; x++) { var c = noisePixels[bar * 64 + x]; noisePixels[bar * 64 + x] = new Color32((byte)(c.r / 2), (byte)(c.g / 2), (byte)(c.b / 2), 255); }
            noise.SetPixels32(noisePixels);
            noise.Apply(false);
        }

        /// <summary>SMPTE-style colour bars.</summary>
        static Texture2D MakeBars()
        {
            const int w = 70, h = 40;
            var px = new Color32[w * h];
            Color32[] bars = { new Color32(235, 235, 235, 255), new Color32(235, 235, 20, 255), new Color32(20, 235, 235, 255), new Color32(20, 235, 20, 255), new Color32(235, 20, 235, 255), new Color32(235, 20, 20, 255), new Color32(20, 20, 235, 255) };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c;
                    if (y >= h * 2 / 3 + 3) c = new Color32(18, 18, 24, 255);                                      // the black strip at the bottom
                    else if (y >= h * 2 / 3) c = bars[6 - x * 7 / w] ;                                              // the reverse strip
                    else c = bars[x * 7 / w];
                    px[y * w + x] = c;
                }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TvBars", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            t.SetPixels32(px);
            t.Apply(false);
            return t;
        }
    }
}

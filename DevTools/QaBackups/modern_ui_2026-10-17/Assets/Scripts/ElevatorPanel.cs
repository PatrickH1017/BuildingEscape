using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The service elevator's wall panel: a lit button for every floor (the Loading Docks "Ld", then 1..13) and a
    /// separate GO button beside it.
    ///   - The floor the car is on is lit bright yellow, floors it can reach from here glow amber, the others are dark.
    ///   - Stand next to the panel and press F to use it: the camera moves in front of the panel and the cursor is freed.
    ///   - Click a floor button to choose it (it turns green), then click GO. Everyone must be inside the car.
    ///   - Esc, F or walking away closes the panel again.
    /// The panel only draws and sends clicks; ElevatorExtraction (on the host) decides what is allowed.
    /// </summary>
    public class ElevatorPanel : MonoBehaviour
    {
        /// <summary>The local player is using the panel: cursor free, camera in front of it, character hidden.</summary>
        public static bool Open { get; private set; }
        public static ElevatorPanel Active { get; private set; }

        public KeyCode useKey = KeyCode.F;
        [Tooltip("How close the player must be to the panel to use it.")]
        public float useReach = 3.4f;

        // ---------------------------------------------------------------- layout (local to the panel, which faces INTO the wall)
        const int Columns = 3;
        const float Pitch = 0.4f, ButtonSize = 0.32f;
        const float FaceZ = -0.085f;       // buttons stand out of the plate toward the viewer (viewer is on -z)
        const float GoOffset = 1.1f;

        sealed class Btn
        {
            public int floor;               // 0..13 (0 = Loading Docks); -1 for GO
            public bool isGo;
            public Transform pivot;
            public Material body, glyph;
            public float pressUntil;
        }

        readonly List<Btn> buttons = new();
        readonly Dictionary<Collider, Btn> byCollider = new();
        readonly List<Material> materials = new();
        Btn go;
        Btn hovered;
        Transform headerGlyphs;
        int headerFloor = -999;
        Shader shader;
        bool nearLocal;
        readonly List<Renderer> hidden = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Open = false; Active = null; }

        // ================================================================== building

        /// <summary>
        /// Builds the panel on a wall. <paramref name="viewDir"/> is the direction a person looks to face it (into the
        /// wall); <paramref name="goSide"/> is -1 / +1: which side (in panel space) the GO button goes.
        /// </summary>
        public static ElevatorPanel Build(Transform parent, Vector3 worldPos, Vector3 viewDir, int goSide)
        {
            var g = new GameObject("ElevatorPanel");
            g.transform.SetParent(parent, true);
            g.transform.SetPositionAndRotation(worldPos, Quaternion.LookRotation(viewDir, Vector3.up));
            var p = g.AddComponent<ElevatorPanel>();
            p.Construct(goSide);
            return p;
        }

        void Construct(int goSide)
        {
            shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (!shader) { Debug.LogWarning("ElevatorPanel: no shader found, the panel is not built."); return; }

            // backing plate + the floor readout screen at the top
            var plate = NewMat(new Color(0.09f, 0.09f, 0.11f));
            Cube("Plate", transform, new Vector3(0f, 0f, -0.03f), new Vector3(1.4f, 2.6f, 0.06f), plate, false);
            var screen = NewMat(new Color(0.02f, 0.03f, 0.02f));
            Cube("Screen", transform, new Vector3(0f, 1.05f, -0.065f), new Vector3(1.0f, 0.42f, 0.02f), screen, false);
            headerGlyphs = new GameObject("Readout").transform;
            headerGlyphs.SetParent(transform, false);

            // floor buttons: 13 at the top left down to 1, then the docks
            for (int k = 0; k < 14; k++)
            {
                int floor = k < 13 ? 13 - k : 0;
                int row = k / Columns, col = k % Columns;
                var pos = new Vector3((col - 1) * Pitch, 0.5f - row * Pitch, FaceZ);
                var b = MakeButton(floor.ToString(), pos, ButtonSize, floor == 0 ? "Ld" : floor.ToString(), 0.16f);
                b.floor = floor;
                buttons.Add(b);
            }

            // the GO button on its own backing, beside the panel
            var goPlate = NewMat(new Color(0.09f, 0.09f, 0.11f));
            var gp = new Vector3(goSide * GoOffset, -0.2f, 0f);
            Cube("GoPlate", transform, gp + new Vector3(0f, 0f, -0.03f), new Vector3(0.66f, 0.66f, 0.06f), goPlate, false);
            go = MakeButton("Go", gp + new Vector3(0f, 0f, FaceZ), 0.5f, "GO", 0.2f);
            go.isGo = true; go.floor = -1;
        }

        Material NewMat(Color albedo)
        {
            var m = new Material(shader) { color = albedo };
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            materials.Add(m);
            return m;
        }

        static void SetLook(Material m, Color albedo, Color emission)
        {
            m.color = albedo;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        GameObject Cube(string n, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool keepCollider)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.name = n;
            c.transform.SetParent(parent, false);
            c.transform.localPosition = localPos;
            c.transform.localScale = size;
            var r = c.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var col = c.GetComponent<Collider>();
            if (keepCollider) { if (col) col.isTrigger = true; } // only used for mouse clicks, never in the way
            else if (col) Destroy(col);
            return c;
        }

        Btn MakeButton(string n, Vector3 localPos, float size, string label, float glyphHeight)
        {
            var b = new Btn();
            var pivot = new GameObject("Button_" + n).transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = localPos;
            b.pivot = pivot;
            b.body = NewMat(new Color(0.13f, 0.13f, 0.15f));
            b.glyph = NewMat(new Color(0.38f, 0.38f, 0.42f));
            var body = Cube("Body", pivot, Vector3.zero, new Vector3(size, size, 0.05f), b.body, true);
            byCollider[body.GetComponent<Collider>()] = b;
            var glyphRoot = new GameObject("Label").transform;
            glyphRoot.SetParent(pivot, false);
            glyphRoot.localPosition = new Vector3(0f, 0f, -0.032f); // just in front of the button face
            DrawText(glyphRoot, label, glyphHeight, b.glyph);
            return b;
        }

        // ---------------------------------------------------------------- seven-segment lettering (no fonts needed)

        static readonly string[] Digits = { "abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg" };

        static string Segments(char ch)
        {
            if (ch >= '0' && ch <= '9') return Digits[ch - '0'];
            switch (ch)
            {
                case 'L': return "def";
                case 'd': return "bcdeg";
                case 'G': return "acdef";
                case 'O': return "abcdef";
            }
            return "";
        }

        /// <summary>Draws text as glowing seven-segment characters, centred on the parent.</summary>
        void DrawText(Transform parent, string text, float h, Material mat)
        {
            float w = h * 0.5f, spacing = h * 0.72f;
            float x0 = -spacing * (text.Length - 1) / 2f;
            for (int i = 0; i < text.Length; i++) Glyph(parent, text[i], new Vector3(x0 + i * spacing, 0f, 0f), h, w, mat);
        }

        void Glyph(Transform parent, char ch, Vector3 c, float h, float w, Material mat)
        {
            float t = h * 0.14f, d = 0.012f;
            foreach (char s in Segments(ch))
            {
                Vector3 pos, size;
                switch (s)
                {
                    case 'a': pos = new Vector3(0f, h / 2f, 0f); size = new Vector3(w, t, d); break;
                    case 'd': pos = new Vector3(0f, -h / 2f, 0f); size = new Vector3(w, t, d); break;
                    case 'g': pos = Vector3.zero; size = new Vector3(w, t, d); break;
                    case 'b': pos = new Vector3(w / 2f, h / 4f, 0f); size = new Vector3(t, h / 2f, d); break;
                    case 'c': pos = new Vector3(w / 2f, -h / 4f, 0f); size = new Vector3(t, h / 2f, d); break;
                    case 'f': pos = new Vector3(-w / 2f, h / 4f, 0f); size = new Vector3(t, h / 2f, d); break;
                    default:  pos = new Vector3(-w / 2f, -h / 4f, 0f); size = new Vector3(t, h / 2f, d); break; // 'e'
                }
                Cube("seg", parent, c + pos, size, mat, false);
            }
        }

        /// <summary>The big readout at the top shows the floor the car is on.</summary>
        void RebuildReadout(int floor)
        {
            headerFloor = floor;
            if (!headerGlyphs) return;
            for (int i = headerGlyphs.childCount - 1; i >= 0; i--) Destroy(headerGlyphs.GetChild(i).gameObject);
            if (readoutMat == null) readoutMat = NewMat(new Color(0.4f, 1f, 0.5f));
            SetLook(readoutMat, new Color(0.4f, 1f, 0.5f), new Color(0.25f, 1f, 0.4f) * 2.2f);
            var root = new GameObject("Digits").transform;
            root.SetParent(headerGlyphs, false);
            root.localPosition = new Vector3(0f, 1.05f, -0.085f);
            DrawText(root, floor <= 0 ? "Ld" : floor.ToString(), 0.3f, readoutMat);
        }
        Material readoutMat;

        // ================================================================== using it

        static Transform LocalPlayer()
        {
            var np = Net.NetPlayer.Local;
            if (np) return np.transform;
            return WobblyPlayerRegistry.All.Count > 0 && WobblyPlayerRegistry.All[0] ? WobblyPlayerRegistry.All[0].transform : null;
        }

        static bool Blocked => Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || OutpostUi.Open;

        static bool MovementPressed() =>
            Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)
            || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftArrow)
            || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Space);

        /// <summary>Where the camera goes while the panel is open (it looks straight at the panel).</summary>
        public bool TryGetView(out Vector3 pos, out Quaternion rot)
        {
            var cam = Camera.main;
            float fov = cam ? Mathf.Max(20f, cam.fieldOfView) : 60f;
            float dist = Mathf.Clamp(1.6f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), 2.2f, 3.6f); // far enough to fit the whole panel
            pos = transform.position - transform.forward * dist;
            rot = Quaternion.LookRotation(transform.forward, Vector3.up);
            return true;
        }

        void OpenPanel(Transform me)
        {
            Open = true; Active = this;
            hidden.Clear();
            // the character would stand between the camera and the panel, so it is hidden while you use it
            foreach (var r in me.GetComponentsInChildren<Renderer>())
                if (r && r.enabled) { r.enabled = false; hidden.Add(r); }
        }

        void ClosePanel()
        {
            if (Active == this) Open = false;
            foreach (var r in hidden) if (r) r.enabled = true;
            hidden.Clear();
            hovered = null;
        }

        void OnDestroy()
        {
            ClosePanel();
            if (Active == this) Active = null;
            foreach (var m in materials) if (m) Destroy(m);
        }

        void Update()
        {
            var hm = HeistManager.Instance;
            if (!hm || buttons.Count == 0) return;
            var tm = TowerManager.Instance;
            int cur = hm.Floor;
            int up = tm ? TowerRules.UpFrom(cur, hm.Phase == HeistPhase.FloorComplete, tm.ResumeLevel) : -1;
            int down = TowerRules.DownFrom(cur, hm.Phase == HeistPhase.FloorComplete);

            var me = LocalPlayer();
            nearLocal = me && (me.position - transform.position).sqrMagnitude <= useReach * useReach;
            bool sealing = ElevatorExtraction.Sealing;

            if (!Open)
            {
                if (nearLocal && !Blocked && !sealing && Input.GetKeyDown(useKey)) OpenPanel(me);
            }
            else if (!nearLocal || sealing || Blocked || Input.GetKeyDown(useKey) || Input.GetKeyDown(KeyCode.Escape) || MovementPressed())
            {
                ClosePanel();
            }
            else HandleMouse();

            if (headerFloor != cur) RebuildReadout(cur);
            UpdateLights(cur, up, down, sealing);
        }

        // The ray passes through the trigger buttons, the plate's air gap and everything behind them; the buffer is generous because
        // RaycastNonAlloc keeps an arbitrary subset (not the nearest) once it is full.
        readonly RaycastHit[] rayHits = new RaycastHit[128];

        void HandleMouse()
        {
            hovered = null;
            var cam = Camera.main;
            if (!cam) return;
            int n = Physics.RaycastNonAlloc(cam.ScreenPointToRay(Input.mousePosition), rayHits, 12f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var h = rayHits[i];
                if (h.distance < best && byCollider.TryGetValue(h.collider, out var b)) { best = h.distance; hovered = b; }
            }

            if (hovered != null && Input.GetMouseButtonDown(0))
            {
                hovered.pressUntil = Time.time + 0.12f;
                var car = ElevatorExtraction.Current;
                if (car) car.LocalPress(hovered.isGo ? 1 : ElevatorExtraction.SelectBase + hovered.floor);
            }
        }

        // ================================================================== lights

        void UpdateLights(int cur, int up, int down, bool sealing)
        {
            int dest = ElevatorExtraction.Destination;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);

            foreach (var b in buttons)
            {
                bool hov = ReferenceEquals(b, hovered) && Open;
                float boost = hov ? 1.5f : 1f;
                if (b.floor == cur)            // where the car is: bright yellow
                {
                    SetLook(b.body, new Color(0.30f, 0.24f, 0.08f), new Color(0.55f, 0.42f, 0.10f) * boost);
                    SetLook(b.glyph, new Color(1f, 0.92f, 0.55f), new Color(1f, 0.85f, 0.35f) * 2.4f * boost);
                }
                else if (b.floor == dest)      // chosen: green
                {
                    SetLook(b.body, new Color(0.08f, 0.30f, 0.10f), new Color(0.10f, 0.60f, 0.20f) * pulse * boost);
                    SetLook(b.glyph, new Color(0.5f, 1f, 0.55f), new Color(0.3f, 1f, 0.4f) * 2.6f * pulse * boost);
                }
                else if (b.floor == up || b.floor == down) // the elevator can go there: amber
                {
                    SetLook(b.body, new Color(0.22f, 0.14f, 0.04f), new Color(0.35f, 0.2f, 0.05f) * boost);
                    SetLook(b.glyph, new Color(1f, 0.75f, 0.3f), new Color(1f, 0.6f, 0.15f) * 1.5f * boost);
                }
                else                           // unavailable: dark
                {
                    SetLook(b.body, new Color(0.13f, 0.13f, 0.15f) * (hov ? 1.5f : 1f), Color.black);
                    SetLook(b.glyph, new Color(0.38f, 0.38f, 0.42f), Color.black);
                }
                Pose(b, hov);
            }

            // GO: green and pulsing when it will work, amber while waiting for the team, dark with nothing chosen
            bool hovGo = ReferenceEquals(go, hovered) && Open;
            float gb = hovGo ? 1.4f : 1f;
            if (sealing)
                { SetLook(go.body, new Color(0.30f, 0.08f, 0.06f), new Color(0.6f, 0.1f, 0.05f)); SetLook(go.glyph, new Color(1f, 0.4f, 0.3f), new Color(1f, 0.3f, 0.2f) * 2f); }
            else if (dest >= 0 && ElevatorExtraction.EveryoneAboard)
                { SetLook(go.body, new Color(0.06f, 0.30f, 0.10f), new Color(0.1f, 0.7f, 0.2f) * pulse * gb); SetLook(go.glyph, new Color(0.5f, 1f, 0.55f), new Color(0.3f, 1f, 0.4f) * 2.8f * pulse * gb); }
            else if (dest >= 0)
                { SetLook(go.body, new Color(0.22f, 0.14f, 0.04f), new Color(0.35f, 0.2f, 0.05f) * gb); SetLook(go.glyph, new Color(1f, 0.75f, 0.3f), new Color(1f, 0.6f, 0.15f) * 1.5f * gb); }
            else
                { SetLook(go.body, new Color(0.08f, 0.14f, 0.09f) * gb, Color.black); SetLook(go.glyph, new Color(0.3f, 0.42f, 0.32f), Color.black); }
            Pose(go, hovGo);
        }

        static void Pose(Btn b, bool hover)
        {
            float s = hover ? 1.08f : 1f;
            if (Time.time < b.pressUntil) s = 0.92f;
            b.pivot.localScale = Vector3.one * s;
        }

        // ================================================================== on screen

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;      // everything here is drawing only (the clicks are read in Update)
            if (!this || buttons.Count == 0) return;
            var hm = HeistManager.Instance;
            if (!hm) return;

            if (!Open)
            {
                if (nearLocal && !Blocked && !ElevatorExtraction.Sealing)
                    HudWidgets.Prompt($"[{useKey}] Use the elevator panel", Screen.height * 0.72f);
                return;
            }

            float s = HudTheme.S;
            int dest = ElevatorExtraction.Destination;
            string title = "ELEVATOR PANEL  -  click a floor, then click GO";
            float tw = HudTheme.Width(title, 24f) + title.Length * 2.6f * s + 64f * s;
            var tr = new Rect((Screen.width - tw) * 0.5f, 22f * s, tw, 46f * s);
            HudTheme.Panel(tr, 14f);
            HudTheme.TextTracked(new Rect(tr.x, tr.y + 3f * s, tr.width, tr.height - 3f * s), title, 22f, HudTheme.Bright, 2.6f, TextAnchor.MiddleCenter);
            string status = dest >= 0 ? $"Going to: {TowerRules.Label(dest)}" : "No floor chosen yet";
            if (dest >= 0 && !ElevatorExtraction.EveryoneAboard) status += "   (everyone must be inside the elevator first)";
            float sw = HudTheme.Width(status, 18f) + 44f * s;
            HudTheme.Chip(new Rect((Screen.width - sw) * 0.5f, tr.yMax + 10f * s, sw, 32f * s), status, dest >= 0 ? (ElevatorExtraction.EveryoneAboard ? HudTheme.Good : HudTheme.Warning) : HudTheme.Dim, 18f);
            HudTheme.Text(new Rect(0f, Screen.height - 52f * s, Screen.width, 30f * s), $"[{useKey}] or Esc to step back", 18f, HudTheme.Muted, TextAnchor.MiddleCenter);

            // what the button under the cursor does
            if (hovered != null)
            {
                string tip;
                var tm = TowerManager.Instance;
                int cur = hm.Floor;
                int up = tm ? TowerRules.UpFrom(cur, hm.Phase == HeistPhase.FloorComplete, tm.ResumeLevel) : -1;
                int down = TowerRules.DownFrom(cur, hm.Phase == HeistPhase.FloorComplete);
                if (hovered.isGo) tip = dest < 0 ? "GO - choose a floor first" : $"GO to {TowerRules.Label(dest)}";
                else if (hovered.floor == cur) tip = $"{TowerRules.Label(hovered.floor)} - you are here";
                else if (hovered.floor == up || hovered.floor == down) tip = $"{TowerRules.Label(hovered.floor)} - click to choose";
                else tip = $"{TowerRules.Label(hovered.floor)} - " + ElevatorExtraction.WhyNot(hovered.floor, cur, up, down);
                var m = Event.current.mousePosition;
                float w = HudTheme.Width(tip, 18f) + 34f * s, th = 36f * s;
                var tipRect = new Rect(Mathf.Clamp(m.x + 18f * s, 4f, Mathf.Max(4f, Screen.width - w - 4f)), Mathf.Clamp(m.y + 18f * s, 4f, Mathf.Max(4f, Screen.height - th - 4f)), w, th);
                HudTheme.Body(tipRect, HudTheme.Card, 8f, 2f, 2f);
                HudTheme.Text(tipRect, tip, 18f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
            }
        }
    }
}
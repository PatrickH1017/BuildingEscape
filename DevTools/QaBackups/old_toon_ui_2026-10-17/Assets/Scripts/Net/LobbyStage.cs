using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The 3D lobby: a small stage where your character and your friends' characters stand on pads,
    /// idling. Nobody can move; it exists to look at the group and to customize your own character.
    /// Built entirely in code and driven by NetSession.Roster, so the Menu scene only needs a camera.
    /// </summary>
    public class LobbyStage : MonoBehaviour
    {
        public static LobbyStage Instance { get; private set; }

        /// <summary>True while the Customize page is open: the camera moves in and you can spin your character.</summary>
        public bool Customizing;
        /// <summary>Set by the menu: width of its panel in screen pixels (the stage is framed to the right of it).</summary>
        public static float PanelPixels;

        public int CharacterCount => slots.Count;

        const float Spacing = 1.9f;
        // Pulled back and shifted left so the characters sit in the part of the screen the menu panel leaves free (five need to be pulled back further).
        static readonly Vector3 OverviewCam = new(-1.8f, 2.2f, -9.5f), OverviewCamFive = new(-1.8f, 2.3f, -10.8f);
        const float TightSpacing = 1.7f;   // five characters stand a little closer together

        sealed class Slot
        {
            public GameObject root, holder, marker;
            public CharacterAnimator anim;
            public string look = "", name = "";
            public bool isLocal;
            public float yaw = 180f;
            public Vector3 target;
            public float height = 1f;
            public bool placed;
        }

        readonly List<Slot> slots = new();
        readonly List<(string name, string look, bool isLocal)> entries = new();
        PlayerAppearance local;
        Camera cam;
        Material padMat, markerMat;
        Quaternion camRot;

        // ------------------------------------------------------------------ lifecycle

        void Awake() { Instance = this; }

        void Start()
        {
            cam = Camera.main;
            local = PlayerAppearance.LoadOrDefault();
            BuildStage();
            if (NetSession.Instance != null) NetSession.Instance.RosterChanged += Refresh;
            Refresh();
            if (cam)
            {
                cam.transform.position = OverviewCam;
                cam.transform.rotation = Quaternion.identity;
                camRot = cam.transform.rotation;
            }
        }

        void OnDestroy()
        {
            if (NetSession.Instance != null) NetSession.Instance.RosterChanged -= Refresh;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ stage

        void BuildStage()
        {
            if (cam)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
                cam.fieldOfView = 38f;
            }
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.0f;
            key.color = new Color(1f, 0.95f, 0.88f);
            key.transform.SetParent(transform);
            key.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 14f; fill.intensity = 0.8f;
            fill.color = new Color(0.55f, 0.65f, 1f);
            fill.transform.SetParent(transform);
            fill.transform.position = new Vector3(-3f, 3.5f, -3f);

            padMat = NewMat(new Color(0.22f, 0.24f, 0.3f));
            markerMat = NewMat(new Color(1f, 0.82f, 0.2f));
            Prop(PrimitiveType.Cube, "Floor", new Vector3(0, -0.15f, 0), new Vector3(40f, 0.3f, 24f), NewMat(new Color(0.12f, 0.13f, 0.17f)));
            Prop(PrimitiveType.Cube, "Backdrop", new Vector3(0, 4f, 4.2f), new Vector3(40f, 10f, 0.3f), NewMat(new Color(0.14f, 0.17f, 0.26f)));
        }

        GameObject Prop(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent ? parent : transform, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(g.GetComponent<Collider>());
            return g;
        }

        static Material NewMat(Color c)
        {
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            return m;
        }

        // ------------------------------------------------------------------ occupants

        /// <summary>Your own character, updated live while customizing (also saved by the menu on Done).</summary>
        public void SetLocalAppearance(PlayerAppearance a)
        {
            local = a;
            Refresh();
        }

        /// <summary>Rebuilds who is on stage from the roster; with no session, just you.</summary>
        void Refresh()
        {
            entries.Clear();
            var net = NetSession.Instance;
            if (net != null && net.Roster.Count > 0)
                foreach (var r in net.Roster) entries.Add((r.Name, r.IsLocal ? local.Serialize() : r.Look, r.IsLocal));
            else
                entries.Add((LocalName(), local.Serialize(), true));
            ApplyEntries();
        }

        static string LocalName()
        {
            var steam = SteamService.Instance;
            return steam != null && steam.Ready ? steam.LocalName : "You";
        }

#if UNITY_EDITOR
        /// <summary>Editor/test only: show n characters without a lobby, to preview the stage.</summary>
        public void DebugShow(int n)
        {
            entries.Clear();
            for (int i = 0; i < n; i++)
                entries.Add(($"Friend {i + 1}", i == 0 ? local.Serialize() : PlayerAppearance.ForSlot(i).Serialize(), i == 0));
            ApplyEntries();
        }

        /// <summary>Editor/test only: show exactly these look strings (the first one is "you").</summary>
        public void DebugShowLooks(System.Collections.Generic.IList<string> looks)
        {
            entries.Clear();
            for (int i = 0; i < looks.Count; i++) entries.Add(($"Friend {i + 1}", looks[i], i == 0));
            ApplyEntries();
        }
#endif

        void ApplyEntries()
        {
            while (slots.Count < entries.Count) slots.Add(NewSlot());
            while (slots.Count > entries.Count)
            {
                Destroy(slots[slots.Count - 1].root);
                slots.RemoveAt(slots.Count - 1);
            }
            for (int i = 0; i < entries.Count; i++)
            {
                var s = slots[i];
                var e = entries[i];
                s.name = e.name;
                s.isLocal = e.isLocal;
                s.marker.SetActive(e.isLocal && entries.Count > 1);
                s.target = new Vector3((i - (entries.Count - 1) / 2f) * (entries.Count > 4 ? TightSpacing : Spacing), 0f, 0f);
                if (!s.placed) { s.root.transform.position = s.target; s.placed = true; } // arrivals appear in place

                // Unknown or invalid looks fall back to the slot's default so nobody is ever missing.
                string look = e.look;
                if (!PlayerAppearance.TryParse(look, out _)) look = PlayerAppearance.ForSlot(i).Serialize();
                if (look != s.look) Dress(s, look);
            }
        }

        Slot NewSlot()
        {
            var s = new Slot { root = new GameObject("Slot") };
            s.root.transform.SetParent(transform, false);
            Prop(PrimitiveType.Cylinder, "Pad", new Vector3(0, 0.05f, 0), new Vector3(1.6f, 0.05f, 1.6f), padMat, s.root.transform);
            s.marker = Prop(PrimitiveType.Cylinder, "YouMarker", new Vector3(0, 0.02f, 0), new Vector3(1.85f, 0.03f, 1.85f), markerMat, s.root.transform);

            // A capsule stands in until the pack's model is attached (and stays if the pack is missing).
            s.holder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            s.holder.name = "Character";
            s.holder.transform.SetParent(s.root.transform, false);
            s.holder.transform.localPosition = new Vector3(0, 1.1f, 0);
            Destroy(s.holder.GetComponent<Collider>());
            return s;
        }

        void Dress(Slot s, string look)
        {
            s.look = look;
            PlayerAppearance.TryParse(look, out var a);
            s.height = a.height;
            if (s.anim) Destroy(s.anim.gameObject);
            s.anim = PartyCharacterFactory.Attach(s.holder, a.ToLook(s.name));
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            float follow = 1f - Mathf.Exp(-8f * Time.deltaTime);
            Slot mine = null;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                s.root.transform.position = Vector3.Lerp(s.root.transform.position, s.target, follow);
                float rest = 180f - s.target.x * 4f; // everyone turns slightly towards the middle
                if (s.isLocal && Customizing)
                {
                    if (Input.GetMouseButton(0) && Input.mousePosition.x > PanelPixels) s.yaw -= Input.GetAxis("Mouse X") * 5f;
                }
                else s.yaw = Mathf.LerpAngle(s.yaw, rest, follow);
                s.root.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                if (s.isLocal) mine = s;
            }
            if (cam) MoveCamera(mine);
        }

        void MoveCamera(Slot mine)
        {
            Vector3 pos = entries.Count > 4 ? OverviewCamFive : OverviewCam;
            if (Customizing && mine != null)
            {
                // close-up: slightly left of the character so it sits to the right of the menu panel
                pos = new Vector3(mine.target.x - 1.35f, 1.6f, -6.4f);
            }
            float k = 1f - Mathf.Exp(-5f * Time.deltaTime);
            cam.transform.position = Vector3.Lerp(cam.transform.position, pos, k);
            camRot = Quaternion.Slerp(camRot, Quaternion.Euler(Customizing ? 1f : 6f, 0f, 0f), k);
            cam.transform.rotation = camRot;
        }

        void OnGUI()
        {
            if (!cam || Event.current.type != EventType.Repaint) return;     // name tags are only drawn, never clicked
            float k = Mathf.Max(0.75f, Screen.height / 720f);
            int size = Mathf.RoundToInt(16 * k);
            var style = HudStyles.Label(size, TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            var shadow = HudStyles.Label(size, TextAnchor.MiddleCenter, FontStyle.Bold, Color.black);
            foreach (var s in slots)
            {
                var p = cam.WorldToScreenPoint(s.root.transform.position + Vector3.up * (2.15f * s.height + 0.15f));
                if (p.z <= 0f) continue;
                var rect = new Rect(p.x - 110f, Screen.height - p.y - 14f, 220f, 28f);
                string text = s.isLocal ? s.name + "  (you)" : s.name;
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, shadow);
                GUI.Label(rect, text, style);
            }
        }
    }
}

using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The dock radio's screen (the owner, 2026-10-27: "they should get a screen where they can click the little buttons to change the station. Right now there is only 3 stations so only 3 buttons should be clickable. If they click
    /// the button it should play a different station"). Opened by E on the radio (<see cref="DockRadio.Use"/>), shut by Esc, by walking away or by LEAVE. The radio itself fills the screen - a camera in front of it, lit by a lamp
    /// beside the camera - and its eight buttons are where they are on the model: the first three (reading order) are the stations (<see cref="RadioRules.StationOfButton"/>), lit, the one on air brightest; the fourth is the power
/// (OFF / ON), the fifth and the sixth the volume down and up (the owner, 2026-10-27: "add a volume control to the radio as well as an off button": <see cref="RadioRules.FunctionOf"/>); the last two do nothing.
    /// The green display shows the station; under the picture what is playing. While it is up it counts as a screen over the game (<see cref="TestMode.PanelOpen"/>: the game keys are blocked and the cursor is free).
    /// </summary>
    public sealed class RadioScreen : MonoBehaviour
    {
        static RadioScreen instance;

        /// <summary>True while the radio's screen is up.</summary>
        public static bool Open { get; private set; }
        /// <summary>The radio whose screen is up.</summary>
        public static DockRadio Radio => instance != null ? instance.radio : null;

        DockRadio radio;
        Camera cam;
        Light lamp;
        RenderTexture rt;
        readonly Rect[] buttonRects = new Rect[RadioRules.ButtonCount];
        readonly int[] readingOrder = new int[RadioRules.ButtonCount];       // reading order (left to right, top to bottom, as seen in the picture) -> the button of DockRadio.ButtonCentres
        Rect displayRect;

        static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color Lcd = new Color(0.66f, 1f, 0.45f);

        const int TexW = 1280, TexH = 800;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Open = false; }

        /// <summary>Opens the screen of the radio (E on it).</summary>
        public static void Show(DockRadio r)
        {
            if (!r) return;
            if (instance == null)
            {
                var go = new GameObject("RadioScreen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<RadioScreen>();
            }
            instance.radio = r;
            instance.StartView();
            Open = true;
        }

        public static void Close()
        {
            Open = false;
            if (instance != null) instance.StopView();
        }

        /// <summary>Tests: presses a button of the pad (reading order) as a click does. False when the button does nothing.</summary>
        public static bool TestPress(int button) => instance != null && Open && instance.Press(button);

        /// <summary>Tests and pictures: where a button (reading order) is on the screen, in GUI pixels.</summary>
        public static Rect ButtonRect(int button) => instance != null && button >= 0 && button < RadioRules.ButtonCount ? instance.buttonRects[button] : default;

        void OnDestroy() { if (instance == this) { instance = null; Open = false; } StopView(); }

        static WobblyPlayer LocalPlayer()
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.TryGetComponent(out Backpack b) && b.Viewed) return p;
            return null;
        }

        // ---- the picture of the radio: a camera a little in front of its pad, looking at it
        void StartView()
        {
            StopView();
            if (!radio) return;
            rt = new RenderTexture(TexW, TexH, 24) { name = "RadioScreen", antiAliasing = 4 };
            cam = new GameObject("RadioScreenCamera").AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 12f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.1f, 0.16f);
            var aim = radio.Point(new Vector3(0f, 0.27f, ButtonFront()));
            cam.transform.position = aim + radio.Forward * 0.46f + radio.Up * 0.05f;          // (close: the pad and the display large, the speakers at the edges)
            cam.transform.LookAt(aim + radio.Up * 0.01f, radio.Up);
            lamp = new GameObject("RadioScreenLamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 2.2f;
            lamp.intensity = 1.6f;
            lamp.color = new Color(1f, 0.96f, 0.88f);
            lamp.shadows = LightShadows.None;
            lamp.transform.position = cam.transform.position + radio.Up * 0.3f;
        }

        static float ButtonFront() => DockRadio.ButtonFront;

        void StopView()
        {
            if (cam) { cam.targetTexture = null; Destroy(cam.gameObject); }
            if (lamp) Destroy(lamp.gameObject);
            if (rt) { rt.Release(); Destroy(rt); }
            cam = null; lamp = null; rt = null;
        }

        void Update()
        {
            if (!Open) return;
            var me = LocalPlayer();
            if (!radio || me == null || me.InCustody || Input.GetKeyDown(KeyCode.Escape) || radio.DistanceFrom(me.transform.position) > radio.reach + 1.2f) Close();
        }

        bool Press(int button)
        {
            if (button < 0 || button >= RadioRules.ButtonCount) return false;
            int action = RadioRules.ActionOf(button);
            if (action < 0) return false;
            RadioState.Request(action);
            if (radio) GameAudio.PlaySound(SoundId.KeypadBeep, radio.transform.position + Vector3.up * 0.3f, 0.5f, 0.8f, true, 8f, 2f);          // (the click of the button)
            return true;
        }

        /// <summary>Where a point of the radio is in the picture drawn at <paramref name="pic"/> (GUI pixels).</summary>
        Vector2 OnPicture(Vector3 world, Rect pic)
        {
            var vp = cam.WorldToViewportPoint(world);
            return new Vector2(pic.x + vp.x * pic.width, pic.y + (1f - vp.y) * pic.height);
        }

        Rect FaceRect(Vector3 centre, Vector2 size, Rect pic)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var c = centre + new Vector3((i & 1) == 0 ? -size.x * 0.5f : size.x * 0.5f, (i & 2) == 0 ? -size.y * 0.5f : size.y * 0.5f, 0f);
                var p = OnPicture(radio.Point(c), pic);
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The buttons and the display where they are in the picture, the buttons in reading order.</summary>
        void Layout(Rect pic)
        {
            var raw = new Rect[RadioRules.ButtonCount];
            for (int i = 0; i < raw.Length; i++)
            {
                var c = DockRadio.ButtonCentres[i];
                raw[i] = FaceRect(new Vector3(c.x, c.y, DockRadio.ButtonFront), DockRadio.ButtonSize, pic);
                readingOrder[i] = i;
            }
            System.Array.Sort(readingOrder, (a, b) =>
            {
                float dy = raw[a].center.y - raw[b].center.y;
                if (Mathf.Abs(dy) > raw[a].height * 0.5f) return dy < 0f ? -1 : 1;
                return raw[a].center.x.CompareTo(raw[b].center.x);
            });
            for (int k = 0; k < raw.Length; k++) buttonRects[k] = raw[readingOrder[k]];
            displayRect = FaceRect(DockRadio.DisplayCentre, DockRadio.DisplaySize, pic);
        }

        void OnGUI()
        {
            if (!Open || !radio || !rt || !cam) return;
            var ev = Event.current;
            GUI.depth = 5;
            MenuKit.Begin();
            float s = HudTheme.S;
            float pad = 22f * s, headH = 70f * s, footH = 150f * s;
            float pw = Mathf.Min(900f * s, Screen.width - 40f * s);
            float picW = pw - pad * 2f, picH = picW * TexH / TexW;
            float maxPicH = Screen.height - 40f * s - headH - footH;
            if (picH > maxPicH) { picH = Mathf.Max(120f * s, maxPicH); picW = picH * TexW / TexH; pw = picW + pad * 2f; }
            float ph = headH + picH + footH;
            var panel = new Rect((Screen.width - pw) * 0.5f, Mathf.Max(16f * s, (Screen.height - ph) * 0.5f), pw, ph);
            var pic = new Rect(panel.x + pad, panel.y + headH, picW, picH);
            Layout(pic);
            bool power = RadioState.On;
            int on = power ? RadioState.Station : -1;                                // (switched off: no station is on air)
            int hovered = -1;
            for (int k = 0; k < RadioRules.ButtonCount; k++)
                if (RadioRules.FunctionOf(k) != RadioRules.Button.None && Grow(buttonRects[k], 4f * s).Contains(ev.mousePosition)) hovered = k;

            if (ev.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = Dim;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
                HudTheme.Panel(panel);
                const string tag = "RADIO";
                float tagW = HudTheme.Width(tag, 15f) + 28f * s;
                HudTheme.Chip(new Rect(panel.xMax - pad - tagW, panel.y + 20f * s, tagW, 30f * s), tag, HudTheme.Warning, 15f);
                string head = !power ? "THE RADIO   OFF" : RadioRules.ValidStation(on) ? RadioRules.Frequencies[on] + " FM   " + RadioRules.Names[on] : "THE RADIO";
                HudTheme.Text(new Rect(panel.x + pad, panel.y + 12f * s, pw - pad * 2f - tagW - 10f * s, 46f * s), head, 26f, HudTheme.Bright, TextAnchor.MiddleLeft);
                HudTheme.Rounded(Grow(pic, 4f * s), HudTheme.Ink, 10f * s);
                GUI.DrawTexture(pic, rt, ScaleMode.StretchToFill, false);

                // the display: the station, in green
                if (!power && displayRect.width > 4f) HudTheme.Rounded(displayRect, new Color(0.04f, 0.06f, 0.05f, 0.97f), 3f * s);          // (off: the display is dark)
                if (RadioRules.ValidStation(on) && displayRect.width > 4f)
                {
                    var d = displayRect;
                    HudTheme.Rounded(d, new Color(0.03f, 0.12f, 0.02f, 0.92f), 3f * s);
                    HudTheme.TextPx(new Rect(d.x, d.y, d.width, d.height * 0.55f), RadioRules.Frequencies[on] + " FM", d.height * 0.42f, Lcd, TextAnchor.MiddleCenter, false);
                    HudTheme.TextPx(new Rect(d.x, d.y + d.height * 0.45f, d.width, d.height * 0.5f), RadioRules.Names[on], d.height * 0.3f, Lcd, TextAnchor.MiddleCenter, false);
                }
                // the buttons: the stations' lit (the one on air brightest), the power red while it is on and green while it is off, the volume blue; the one under the mouse brighter; the last two dark
                for (int k = 0; k < RadioRules.ButtonCount; k++)
                {
                    var fn = RadioRules.FunctionOf(k);
                    if (fn == RadioRules.Button.None) continue;
                    var r = buttonRects[k];
                    float rad = Mathf.Min(r.width, r.height) * 0.25f;
                    float a = k == hovered ? 0.8f : 0.5f;
                    Color fill; string label; float size = 0.7f;
                    switch (fn)
                    {
                        case RadioRules.Button.Station:
                            int st = RadioRules.StationOfButton(k);
                            fill = st == on ? HudTheme.Alpha(HudTheme.Cash, 0.75f) : HudTheme.Alpha(HudTheme.Quota, k == hovered ? 0.7f : 0.38f);
                            label = (st + 1).ToString();
                            break;
                        case RadioRules.Button.Power:
                            fill = HudTheme.Alpha(power ? HudTheme.Danger : HudTheme.Cash, a);
                            label = power ? "OFF" : "ON"; size = 0.5f;
                            break;
                        default:
                            fill = HudTheme.Alpha(HudTheme.Info, a);
                            label = fn == RadioRules.Button.VolumeDown ? "VOL -" : "VOL +"; size = 0.45f;
                            break;
                    }
                    if (k == hovered) HudTheme.Rounded(Grow(r, 3f * s), HudTheme.Alpha(HudTheme.Bright, 0.85f), rad + 3f * s);
                    HudTheme.Rounded(r, fill, rad);
                    HudTheme.TextPx(r, label, r.height * size, HudTheme.Ink, TextAnchor.MiddleCenter, false);
                }
            }

            // a click on a button that does something
            if (ev.type == EventType.MouseDown && ev.button == 0 && hovered >= 0) { ev.Use(); Press(hovered); }

            // under the picture: what is playing, the stations, what the button under the mouse is
            float y = pic.yMax + 12f * s;
            if (ev.type == EventType.Repaint)
            {
                string playing = !power ? "OFF   (the stations play on - the ON button brings it back)" : radio.TitleNow.Length > 0 ? "NOW PLAYING   " + radio.TitleNow : RadioRules.ValidStation(on) ? "TUNING..." : "";
                HudTheme.Text(new Rect(panel.x + pad, y, pw * 0.62f, 30f * s), playing, 18f, power ? HudTheme.Cash : HudTheme.Muted, TextAnchor.MiddleLeft);
                // the volume: the radio's own steps (0-10), as a bar
                float vw = 200f * s, vx = panel.xMax - pad - vw;
                HudTheme.Text(new Rect(vx - 96f * s, y, 90f * s, 30f * s), "VOLUME", 15f, HudTheme.Muted, TextAnchor.MiddleRight);
                HudTheme.Bar(new Rect(vx, y + 8f * s, vw - 44f * s, 14f * s), RadioState.Volume / (float)RadioRules.VolumeSteps, power ? HudTheme.Info : HudTheme.Dim, 2.5f);
                HudTheme.Text(new Rect(panel.xMax - pad - 40f * s, y, 40f * s, 30f * s), RadioState.Volume.ToString(), 17f, HudTheme.Bright, TextAnchor.MiddleRight);
                float x = panel.x + pad;
                for (int st = 0; st < RadioRules.StationCount; st++)
                {
                    string t = (st + 1) + "  " + RadioRules.Names[st];
                    float w = HudTheme.Width(t, 14f) + 26f * s;
                    HudTheme.Chip(new Rect(x, y + 38f * s, w, 26f * s), t, st == on ? HudTheme.Cash : HudTheme.Tag, 14f);
                    x += w + 8f * s;
                }
                var hf = hovered >= 0 ? RadioRules.FunctionOf(hovered) : RadioRules.Button.None;
                string hint = hf == RadioRules.Button.Station ? "Tune to " + RadioRules.Names[RadioRules.StationOfButton(hovered)]
                            : hf == RadioRules.Button.Power ? (power ? "Switch the radio off" : "Switch the radio on")
                            : hf == RadioRules.Button.VolumeDown ? "Quieter" : hf == RadioRules.Button.VolumeUp ? "Louder"
                            : "Click a lit button: a station, OFF / ON, the volume";
                HudTheme.Text(new Rect(x + 6f * s, y + 36f * s, panel.xMax - pad - x - 6f * s, 30f * s), hint, 15f, HudTheme.Muted, TextAnchor.MiddleRight);
            }
            var leave = new Rect(panel.x + pad, panel.yMax - 62f * s, pw - pad * 2f, 46f * s);
            if (MenuKit.Button(leave, "LEAVE", HudTheme.Quota, 20f)) Close();
        }

        static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
    }
}

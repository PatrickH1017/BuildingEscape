using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// A window that draws one of each <see cref="MenuKit"/> widget at fixed places (a scroll area with six buttons, a text box, a slider, ...)
    /// and remembers what they returned. <see cref="NetDevMenuClicks"/> sends it real IMGUI mouse and key events (EditorWindow.SendEvent), so the
    /// widgets' own event handling (presses, drags, the wheel, clipping, typing) is tested, not only how they look.
    /// </summary>
    public class MenuKitProbe : EditorWindow
    {
        public int clicks, disabledClicks, pickClicks, swatchClicks, scaledClicks, paints;
        public Rect rScaled;
        public Vector2 scaledPivot = new Vector2(20f, 20f);
        public float slider = 0.25f;
        public bool toggle;
        public string text = "";
        public readonly List<int> itemClicks = new List<int>();
        public Rect rButton, rDisabled, rSlider, rToggle, rPick, rSwatch, rText, rView;
        public readonly Rect[] rItems = new Rect[6];      // in window coordinates, as laid out by the last event
        public readonly MenuKit.Scroll scroll = new MenuKit.Scroll();
        readonly MenuKit.Flow flow = new MenuKit.Flow();

        void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) paints++;
            MenuKit.Begin();
            float s = HudTheme.S;
            rButton = new Rect(20f, 20f, 200f * s, 50f * s);
            if (MenuKit.Button(rButton, "CLICK", HudTheme.Cash)) clicks++;
            rDisabled = new Rect(20f, rButton.yMax + 10f, 200f * s, 50f * s);
            if (MenuKit.Button(rDisabled, "DISABLED", HudTheme.Info, 24f, false)) disabledClicks++;
            rSlider = new Rect(20f, rDisabled.yMax + 10f, 300f * s, 34f * s);
            slider = MenuKit.Slider(rSlider, slider, 0f, 1f, HudTheme.Cash);
            rToggle = new Rect(20f, rSlider.yMax + 10f, 300f * s, 50f * s);
            toggle = MenuKit.ToggleRow(rToggle, "SWITCH", toggle);
            rPick = new Rect(20f, rToggle.yMax + 10f, 150f * s, 44f * s);
            if (MenuKit.Pick(rPick, "PICK", false, HudTheme.Quota)) pickClicks++;
            rSwatch = new Rect(rPick.xMax + 20f, rPick.y, 44f * s, 44f * s);
            if (MenuKit.Swatch(rSwatch, Color.red, false)) swatchClicks++;
            rText = new Rect(20f, rPick.yMax + 10f, 300f * s, 44f * s);
            text = MenuKit.TextBox(rText, text, 12, "probe", "TYPE");

            // a scroll area with six buttons, only about two of them in view
            rView = new Rect(20f, rText.yMax + 12f, 300f * s, 150f * s);
            float cw = scroll.Begin(rView, 4f * s);
            flow.At(4f * s, scroll.Top, cw);
            for (int i = 0; i < rItems.Length; i++)
            {
                var r = flow.Row(60f, 10f);
                rItems[i] = new Rect(rView.x + r.x, rView.y + r.y, r.width, r.height);
                if (MenuKit.Button(r, "ITEM " + i, HudTheme.Info)) itemClicks.Add(i);
            }
            scroll.End(rView, flow.y);

            // a button inside a scaled GUI matrix (the menus pop in this way): it is clicked where it is drawn, not where it would be unscaled
            rScaled = new Rect(rView.x, rView.yMax + 20f, 160f * s, 44f * s);
            var mat = HudAnim.Begin(1.5f, scaledPivot);
            if (MenuKit.Button(rScaled, "SCALED", HudTheme.Warning)) scaledClicks++;
            HudAnim.End(mat);
        }
    }

    /// <summary>
    /// Run: <c>Unity.exe -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.NetDevMenuClicks.Run</c> (no -batchmode: a window is needed;
    /// on the hidden desktop, see DEVELOPER_NOTES 8.6). Prints <c>[MenuClick] PASS / FAIL ...</c> and exits with 0 when everything passed.
    /// </summary>
    public static class NetDevMenuClicks
    {
        static MenuKitProbe w;
        static IEnumerator steps;
        static int fails, checks;
        static double started;

        static void Check(string name, bool ok, string detail = "")
        {
            checks++;
            if (!ok) fails++;
            Debug.Log($"[MenuClick] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        public static void Run()
        {
            w = EditorWindow.GetWindow<MenuKitProbe>(true, "MenuKitProbe", true);
            w.position = new Rect(60f, 60f, 760f, 1000f);
            w.Show();
            started = EditorApplication.timeSinceStartup;
            steps = Steps();
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (steps == null) return;
            if (EditorApplication.timeSinceStartup - started > 120.0) { Debug.Log("[MenuClick] FAIL timeout"); EditorApplication.Exit(1); steps = null; return; }
            bool more;
            try { more = steps.MoveNext(); }
            catch (System.Exception e) { Debug.Log("[MenuClick] FAIL exception " + e); EditorApplication.Exit(1); steps = null; return; }
            if (!more)
            {
                steps = null;
                Debug.Log($"[MenuClick] DONE checks={checks} fails={fails}");
                EditorApplication.Exit(fails == 0 ? 0 : 1);
            }
        }

        // ---- sending events (window coordinates)
        static void Send(Event e) { w.SendEvent(e); }
        static void Down(Vector2 p, int button = 0) => Send(new Event { type = EventType.MouseDown, mousePosition = p, button = button, clickCount = 1 });
        static void Up(Vector2 p, int button = 0) => Send(new Event { type = EventType.MouseUp, mousePosition = p, button = button, clickCount = 1 });
        static void Drag(Vector2 p) => Send(new Event { type = EventType.MouseDrag, mousePosition = p, button = 0 });
        static void Click(Vector2 p) { Down(p); Up(p); }
        static void Wheel(Vector2 p, float dy) => Send(new Event { type = EventType.ScrollWheel, mousePosition = p, delta = new Vector2(0f, dy) });
        static void Key(char c, KeyCode k) { Send(new Event { type = EventType.KeyDown, keyCode = k, character = c }); Send(new Event { type = EventType.KeyUp, keyCode = k }); }

        static IEnumerator Steps()
        {
            // let the window draw a few times first
            for (int i = 0; i < 20 && w.paints < 3; i++) { w.Repaint(); yield return null; }
            Check("the probe window has drawn", w.paints >= 3, $"(paints {w.paints})");
            yield return null;

            // ---- a button: press and release over it
            Click(w.rButton.center);
            Check("a click on a button is returned once", w.clicks == 1, $"(clicks {w.clicks})");
            Down(w.rButton.center); Up(w.rButton.center + new Vector2(400f, 0f));
            Check("pressed over a button but released elsewhere is no click", w.clicks == 1, $"(clicks {w.clicks})");
            Down(w.rButton.center + new Vector2(400f, 0f)); Up(w.rButton.center);
            Check("pressed elsewhere and released over a button is no click", w.clicks == 1, $"(clicks {w.clicks})");
            Click(w.rButton.center);
            Check("and the next click counts again", w.clicks == 2, $"(clicks {w.clicks})");
            Click(w.rDisabled.center);
            Check("a disabled button is never returned", w.disabledClicks == 0, $"(clicks {w.disabledClicks})");
            Click(w.rButton.position + new Vector2(-8f, -8f));
            Check("a click just outside a button misses it", w.clicks == 2, $"(clicks {w.clicks})");

            // ---- the other pressables
            Click(w.rPick.center);
            Check("a pick pill is returned on a click", w.pickClicks == 1, $"({w.pickClicks})");
            Click(w.rSwatch.center);
            Check("a colour swatch is returned on a click", w.swatchClicks == 1, $"({w.swatchClicks})");
            Click(w.rToggle.center);
            Check("a click on a switch row turns it on", w.toggle, $"({w.toggle})");
            Click(w.rToggle.center + new Vector2(60f, 0f));
            Check("and another click turns it off", !w.toggle, $"({w.toggle})");

            // ---- the slider: press sets, drag follows, release lets go
            float kd = Mathf.Min(w.rSlider.height, 32f * HudTheme.S), x0 = w.rSlider.x + kd * 0.5f, tw = w.rSlider.width - kd;
            Vector2 At(float t) => new Vector2(x0 + t * tw, w.rSlider.center.y);
            Down(At(0.5f));
            Check("pressing on a slider sets the value there", Mathf.Abs(w.slider - 0.5f) < 0.04f, $"({w.slider:0.00})");
            Drag(At(0.9f));
            Check("dragging follows the mouse", Mathf.Abs(w.slider - 0.9f) < 0.04f, $"({w.slider:0.00})");
            Drag(At(2.0f));
            Check("dragging past the end stops at the end", w.slider > 0.99f, $"({w.slider:0.00})");
            Drag(new Vector2(x0 + 0.2f * tw, w.rSlider.center.y + 300f));
            Check("the drag goes on while the mouse is away from the track", Mathf.Abs(w.slider - 0.2f) < 0.04f, $"({w.slider:0.00})");
            Up(At(0.2f));
            Drag(At(0.7f));
            Check("after the release a drag changes nothing", Mathf.Abs(w.slider - 0.2f) < 0.04f, $"({w.slider:0.00})");

            // ---- the text box: a click gives it the keyboard, typing fills it
            Click(w.rText.center);
            yield return null;
            Key('A', KeyCode.A); Key('b', KeyCode.B); Key('C', KeyCode.C);
            Check("typing into a clicked text box", w.text == "AbC", $"(\"{w.text}\")");
            // (keys that type nothing, backspace and the arrows, are not delivered to a text field by EditorWindow.SendEvent, so they are not tested here;
            //  they are Unity's own text editing, which the box does not touch)
            Key('d', KeyCode.D);
            Check("typing goes on after the first letters", w.text == "AbCd", $"(\"{w.text}\")");
            for (int i = 0; i < 12; i++) Key('x', KeyCode.X);
            Check("the box stops at its maximum length (12)", w.text.Length == 12, $"(\"{w.text}\")");
            Click(w.rButton.center);        // (leave the text box)

            // ---- the scroll area: the wheel scrolls, what is out of view cannot be clicked
            var inView = w.rView.center;
            Check("six buttons of 60 + 10 are taller than the view", w.scroll.ContentHeight > w.rView.height, $"(content {w.scroll.ContentHeight:0}, view {w.rView.height:0})");
            Click(w.rItems[0].center);
            Check("the first item is clicked where it is drawn", w.itemClicks.Count == 1 && w.itemClicks[0] == 0, $"({string.Join(",", w.itemClicks)})");
            Check("the sixth item is below the fold: a click where it would be misses it", w.rItems[5].y > w.rView.yMax, "");
            Click(new Vector2(w.rItems[5].center.x, w.rView.yMax + 5f));
            Check("a click just under the view hits nothing", w.itemClicks.Count == 1, $"({string.Join(",", w.itemClicks)})");
            Wheel(inView, 3f);
            Check("the wheel over the view scrolls it", w.scroll.pos > 10f, $"(pos {w.scroll.pos:0})");
            float afterOne = w.scroll.pos;
            Wheel(new Vector2(inView.x, w.rText.center.y), 3f);
            Check("the wheel away from the view does not", Mathf.Approximately(w.scroll.pos, afterOne), $"(pos {w.scroll.pos:0})");
            for (int i = 0; i < 30; i++) Wheel(inView, 3f);
            float max = w.scroll.ContentHeight - w.rView.height;
            Check("the wheel stops at the end", Mathf.Abs(w.scroll.pos - max) < 1.5f, $"(pos {w.scroll.pos:0}, end {max:0})");
            Click(w.rItems[5].center);
            Check("scrolled to the end, the sixth item is clicked where it is drawn", w.itemClicks.Count == 2 && w.itemClicks[1] == 5, $"({string.Join(",", w.itemClicks)})");
            Check("the first item is now above the view", w.rItems[0].yMax < w.rView.y, "");
            Click(w.rItems[0].center);
            Check("a click where the scrolled-away first item is drawn does not reach it", w.itemClicks.Count == 2, $"({string.Join(",", w.itemClicks)})");

            // ---- the bar: drag its thumb
            float s = HudTheme.S;
            var track = new Rect(w.rView.xMax - 11f * s, w.rView.y + 4f * s, 9f * s, w.rView.height - 8f * s);
            float thumbH = Mathf.Max(40f * s, track.height * w.rView.height / w.scroll.ContentHeight);
            var thumbAtEnd = new Vector2(track.center.x, track.yMax - thumbH * 0.5f);
            Down(thumbAtEnd); Drag(new Vector2(track.center.x, track.y)); Up(new Vector2(track.center.x, track.y));
            Check("dragging the bar's thumb to the top scrolls back to the top", w.scroll.pos < 2f, $"(pos {w.scroll.pos:0})");

            // ---- a button inside a scaled GUI matrix
            Vector2 vis = w.scaledPivot + (w.rScaled.center - w.scaledPivot) * 1.5f;
            Click(vis);
            Check("a button in a scaled matrix is clicked where it is drawn", w.scaledClicks == 1, $"({w.scaledClicks})");
            Click(w.rScaled.center);
            Check("and not where it would be without the scaling", w.scaledClicks == 1, $"({w.scaledClicks})");

            yield return null;
        }
    }
}

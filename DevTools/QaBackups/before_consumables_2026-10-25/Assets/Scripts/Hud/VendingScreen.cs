using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The screen of a vending machine (the owner, 2026-10-23: "the UI should be a screen like the reference image attached (the green one)": a green terminal, a header and one wide button for each thing to buy). Opened by E on a machine
    /// (<see cref="VendingMachine.Use"/>), shut by Esc, by walking away from the machine or by its LEAVE button. While it is up it counts as a screen over the game (<see cref="TestMode.PanelOpen"/> asks: the game keys are blocked and the cursor is free).
    /// A click buys one: offline and on the host the dealer's purchase runs at once (<see cref="Vendor.BuyAtMachine"/>), a client asks the host (<see cref="Net.RunSync.RequestMachineBuy"/>), who checks the player is at the machine and answers with the usual notice.
    /// What it sells is <see cref="Vendor.MachineProducts"/>, with the dealer's own names, prices and words (the line under the buttons is the blurb of the one the mouse is on). The look is the owner's reference picture: dark green, thin bright green edges.
    /// </summary>
    public sealed class VendingScreen : MonoBehaviour
    {
        static VendingScreen instance;

        /// <summary>True while a machine's screen is up.</summary>
        public static bool Open { get; private set; }

        VendingMachine machine;
        string status = "";
        float statusUntil;

        static readonly Color Dim = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color PanelFill = new Color(0.02f, 0.09f, 0.07f, 0.97f);
        static readonly Color Edge = new Color(0.30f, 0.95f, 0.60f, 1f);
        static readonly Color EdgeDim = new Color(0.16f, 0.45f, 0.32f, 1f);
        static readonly Color Button = new Color(0.04f, 0.18f, 0.13f, 1f);
        static readonly Color ButtonHover = new Color(0.07f, 0.30f, 0.21f, 1f);
        static readonly Color Ink = new Color(0.55f, 1f, 0.75f, 1f);
        static readonly Color InkDim = new Color(0.30f, 0.55f, 0.42f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Open = false; }

        /// <summary>Opens the screen of a machine (E on it).</summary>
        public static void Show(VendingMachine m)
        {
            if (!m) return;
            if (instance == null)
            {
                var go = new GameObject("VendingScreen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VendingScreen>();
            }
            instance.machine = m;
            instance.status = "";
            Open = true;
        }

        /// <summary>Shuts the screen (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void Close() { Open = false; }

        /// <summary>Tests: presses the button of this product (an entry of the dealer's catalog) as a click does.</summary>
        public static void TestBuy(int product) { if (instance != null && Open) instance.Buy(product); }

        /// <summary>Tests: what the screen last said.</summary>
        public static string Status => instance != null ? instance.status : "";

        void OnDestroy() { if (instance == this) { instance = null; Open = false; } }

        static WobblyPlayer LocalPlayer()
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.TryGetComponent(out Backpack b) && b.Viewed) return p;
            return null;
        }

        void Update()
        {
            if (!Open) return;
            var me = LocalPlayer();
            if (!machine || me == null || me.InCustody || Input.GetKeyDown(KeyCode.Escape)
                || machine.DistanceFrom(me.transform.position) > machine.reach + 1.2f) Open = false;
        }

        void Say(string text) { status = text ?? ""; statusUntil = Time.time + 3.5f; }

        void Buy(int product)
        {
            var hm = HeistManager.Instance;
            var me = LocalPlayer();
            var pack = me ? me.GetComponent<Backpack>() : null;
            if (hm == null || pack == null || !machine) return;
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestMachineBuy(machine.Number, product); return; }
            var vendor = Vendor.Current;
            if (vendor == null) return;
            Say(vendor.BuyAtMachine(hm, pack, product, machine.TrayPoint));
        }

        static Rect Grow(Rect r, float d) => new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f);

        void OnGUI()
        {
            if (!Open || !machine) return;
            var ev = Event.current;
            GUI.depth = 5;                                              // (behind the HUD: its notices - the answer a client gets - show over the screen)
            float s = HudTheme.S;
            var products = Vendor.MachineProducts;
            float pw = Mathf.Min(560f * s, Screen.width - 40f * s), pad = 22f * s, rowH = 58f * s, gap = 9f * s, headH = 112f * s, footH = 112f * s;
            float ph = headH + products.Length * (rowH + gap) + footH + pad;
            var panel = new Rect((Screen.width - pw) * 0.5f, Mathf.Max(16f * s, (Screen.height - ph) * 0.5f), pw, ph);
            var hm = HeistManager.Instance;
            int cash = hm ? hm.Cash : 0;
            string hint = "";

            if (ev.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = Dim;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
                HudTheme.Rounded(Grow(panel, 3f * s), Edge, 16f * s);
                HudTheme.Rounded(panel, PanelFill, 14f * s);
                HudTheme.Text(new Rect(panel.x + pad, panel.y + 14f * s, pw - pad * 2f, 22f * s), "CASH", 15f, InkDim, TextAnchor.MiddleLeft);
                HudTheme.Text(new Rect(panel.x + pad, panel.y + 36f * s, pw - pad * 2f, 50f * s), HudTheme.Money(cash), 34f, Ink, TextAnchor.MiddleLeft);
                HudTheme.Rounded(new Rect(panel.x + pad, panel.y + headH - 12f * s, pw - pad * 2f, 2f * s), EdgeDim, 1f * s);
            }

            float y = panel.y + headH;
            for (int k = 0; k < products.Length; k++)
            {
                int index = products[k];
                var p = Vendor.Catalog[index];
                var r = new Rect(panel.x + pad, y, pw - pad * 2f, rowH);
                bool can = cash >= p.price;
                bool hover = r.Contains(ev.mousePosition);
                if (hover) hint = p.blurb;
                if (ev.type == EventType.Repaint)
                {
                    HudTheme.Rounded(r, can ? Edge : EdgeDim, 10f * s);
                    HudTheme.Rounded(Grow(r, -2f * s), hover && can ? ButtonHover : Button, 8f * s);
                    HudTheme.Text(new Rect(r.x + 20f * s, r.y, r.width * 0.62f, r.height), p.name.ToUpperInvariant(), 22f, can ? Ink : InkDim, TextAnchor.MiddleLeft);
                    HudTheme.Text(new Rect(r.xMax - 170f * s, r.y, 150f * s, r.height), HudTheme.Money(p.price), 22f, can ? Ink : InkDim, TextAnchor.MiddleRight);
                }
                if (ev.type == EventType.MouseDown && ev.button == 0 && hover) { ev.Use(); Buy(index); }
                y += rowH + gap;
            }

            // the blurb of the one under the mouse, or what the last purchase said
            string line = Time.time < statusUntil && status.Length > 0 ? status : hint;
            if (ev.type == EventType.Repaint && line.Length > 0)
                HudTheme.Text(new Rect(panel.x + pad, y + 2f * s, pw - pad * 2f, 26f * s), line, 16f, Ink, TextAnchor.MiddleLeft);
            y += 36f * s;

            var leave = new Rect(panel.x + pad, y, pw - pad * 2f, 52f * s);
            bool overLeave = leave.Contains(ev.mousePosition);
            if (ev.type == EventType.Repaint)
            {
                HudTheme.Rounded(leave, Edge, 10f * s);
                HudTheme.Rounded(Grow(leave, -2f * s), overLeave ? ButtonHover : Button, 8f * s);
                HudTheme.Text(leave, "LEAVE", 20f, Ink, TextAnchor.MiddleCenter);
            }
            if (ev.type == EventType.MouseDown && ev.button == 0 && overLeave) { ev.Use(); Open = false; }
        }
    }
}

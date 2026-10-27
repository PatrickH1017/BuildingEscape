using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The screen of a vending machine (the owner, 2026-10-23: "the UI should be a screen like the reference image attached (the green one)": a green terminal, a header and one wide button for each thing to buy). Opened by E on a machine
    /// (<see cref="VendingMachine.Use"/>), shut by Esc, by walking away from the machine or by its LEAVE button. While it is up it counts as a screen over the game (<see cref="TestMode.PanelOpen"/> asks: the game keys are blocked and the cursor is free).
    /// A click buys one: offline and on the host the dealer's purchase runs at once (<see cref="Vendor.BuyAtMachine"/>), a client asks the host (<see cref="Net.RunSync.RequestMachineBuy"/>), who checks the player is at the machine and answers with the usual notice.
    /// What it sells is <see cref="Vendor.MachineProducts"/>, with the dealer's own names, prices and words (the line under the buttons is the blurb of the one the mouse is on). The look is the HUD's (the owner, 2026-10-27: "make the vending machine match the colors of" the cash card): the blue card with its ink outline, the gold coin and the green cash, an orange tag, dark blue rows with green prices, a yellow LEAVE pill.
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

        void OnGUI()
        {
            if (!Open || !machine) return;
            var ev = Event.current;
            GUI.depth = 5;                                              // (behind the HUD: its notices - the answer a client gets - show over the screen)
            MenuKit.Begin();
            float s = HudTheme.S;
            var products = Vendor.MachineProducts;
            float pw = Mathf.Min(560f * s, Screen.width - 40f * s), pad = 22f * s, gap = 10f * s, headH = 96f * s, footH = 112f * s;
            float rowH = Mathf.Clamp((Screen.height - 32f * s - headH - footH - pad) / Mathf.Max(1, products.Length) - gap, 30f * s, 58f * s);          // (the rows shrink to fit a short window)
            var minePack = LocalPlayer() ? LocalPlayer().GetComponent<Backpack>() : null;
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
                HudTheme.Panel(panel);
                // the head: the coin and the cash, as on the HUD's cash card, and the machine's tag
                const string tag = "VENDING";
                float tagW = HudTheme.Width(tag, 15f) + 28f * s;
                HudTheme.Chip(new Rect(panel.xMax - pad - tagW, panel.y + 24f * s, tagW, 30f * s), tag, HudTheme.Warning, 15f);
                HudIcons.Coin(new Rect(panel.x + pad, panel.y + 20f * s, 38f * s, 38f * s));
                HudTheme.Text(new Rect(panel.x + pad + 46f * s, panel.y + 14f * s, pw - pad * 2f - 46f * s - tagW, 50f * s), HudTheme.Money(cash), 36f, HudTheme.Cash, TextAnchor.MiddleLeft);
                HudTheme.Rounded(new Rect(panel.x + pad, panel.y + headH - 16f * s, pw - pad * 2f, 3f * s), HudTheme.Alpha(HudTheme.Ink, 0.45f), 1.5f * s);
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
                    HudTheme.Body(r, hover && can ? HudTheme.Light(HudTheme.Track, 0.16f) : HudTheme.Track, 12f, 2.5f, 3f, true);
                    HudTheme.Text(new Rect(r.x + 20f * s, r.y, r.width * 0.58f, r.height), p.name.ToUpperInvariant(), 21f, can ? HudTheme.Bright : HudTheme.Dim, TextAnchor.MiddleLeft);
                    HudTheme.Text(new Rect(r.xMax - 150f * s, r.y, 132f * s, r.height), HudTheme.Money(p.price), 22f, can ? HudTheme.Cash : HudTheme.Dim, TextAnchor.MiddleRight);
                    if (p.isConsumable && minePack)
                    {
                        string have = minePack.CountConsumable(p.consumable) + "/" + ConsumableRules.Of(p.consumable).limit;          // (what the pack holds / the limit)
                        float cw = HudTheme.Width(have, 14f) + 22f * s;
                        HudTheme.Chip(new Rect(r.xMax - 160f * s - cw, r.center.y - 12f * s, cw, 24f * s), have, HudTheme.Info, 14f);
                    }
                }
                if (ev.type == EventType.MouseDown && ev.button == 0 && hover) { ev.Use(); Buy(index); }
                y += rowH + gap;
            }

            // the blurb of the one under the mouse, or what the last purchase said
            string line = Time.time < statusUntil && status.Length > 0 ? status : hint;
            if (ev.type == EventType.Repaint && line.Length > 0)
                HudTheme.Text(new Rect(panel.x + pad, y + 2f * s, pw - pad * 2f, 26f * s), line, 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
            y += 36f * s;

            var leave = new Rect(panel.x + pad, y, pw - pad * 2f, 50f * s);
            if (MenuKit.Button(leave, "LEAVE", HudTheme.Quota, 20f)) Open = false;
        }
    }
}

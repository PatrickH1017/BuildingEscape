using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The IMGUI front end over the 3D lobby: Play / Customize / How to play / Settings / Quit, the lobby browser, the friends list and the
    /// pre-run lobby, in the toon look of the in-run HUD (<see cref="MenuKit"/>, drawn with <see cref="HudTheme"/>). It only calls
    /// NetSession and LobbyStage and reads their state, so it can be replaced without touching any networking code.
    /// One tall card on the left (the 3D lobby shows on the right of it); the card's page scrolls when it does not fit, and the button that
    /// leaves the page (BACK, DONE, LEAVE LOBBY, QUIT) is pinned to its bottom edge.
    /// </summary>
    public class LobbyMenu : MonoBehaviour
    {
        enum Page { Main, Play, Browse, Customize, Settings, HowTo, Invite, Keybinds }

        const float PanelW = 470f;   // reference pixels (see HudTheme.S)
        const string VolumeKey = "OvertimeEscape.MasterVolume";   // shared with the in-run Esc menu
        const string SensitivityKey = "OvertimeEscape.MouseSensitivity";

        static readonly Color Violet = new Color32(0xA9, 0x8B, 0xE8, 255);

        static readonly Color[] Swatches =
        {
            new Color(0.25f, 0.90f, 0.70f), new Color(1.00f, 1.00f, 0.00f), new Color(1.00f, 0.40f, 0.70f), new Color(0.00f, 1.00f, 1.00f),
            new Color(1.00f, 0.45f, 0.10f), new Color(0.90f, 0.15f, 0.15f), new Color(0.60f, 0.35f, 1.00f), new Color(0.20f, 0.45f, 1.00f),
            new Color(0.45f, 0.85f, 0.20f), new Color(0.95f, 0.95f, 0.95f), new Color(0.55f, 0.55f, 0.60f), new Color(0.12f, 0.12f, 0.14f),
        };

        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
        static readonly string[] FaceNames = Upper(PlayerAppearance.FaceLabels);
        static readonly string[] HatNames = Upper(PlayerAppearance.HatLabels);

        static string[] Upper(string[] labels)
        {
            var u = new string[labels.Length];
            for (int i = 0; i < labels.Length; i++) u[i] = labels[i].ToUpperInvariant();
            return u;
        }

        Page page;
        string codeInput = "";
        string ipInput = "127.0.0.1";
        readonly List<LobbyListing> listings = new();
        bool searching;
        PlayerAppearance edit;
        string[] toonNames;          // "PARTY" and the Tiny Toon people, for the character pills
        float hue, sat = 1f, val = 1f;

        // the page on show: when it changes the card gives a small bounce and the page starts again at the top; the card's height follows the page
        int shownId = -1;
        float shownAt, cardH;
        readonly MenuKit.Flow flow = new MenuKit.Flow();
        readonly MenuKit.Scroll scroll = new MenuKit.Scroll();

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (!GetComponent<LobbyStage>()) gameObject.AddComponent<LobbyStage>();
            // Opened the Menu scene directly in the editor: go through Boot so the Net object exists.
            if (NetSession.Instance == null) SceneManager.LoadScene(NetConst.BootScene);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            // Esc steps back out of a page (the Keybinds screen and the text boxes deal with Esc themselves; Customize needs DONE to keep the look)
            switch (page)
            {
                case Page.Play: page = Page.Main; break;
                case Page.Browse: page = Page.Play; break;
                case Page.Settings: PlayerPrefs.Save(); page = Page.Main; break;
                case Page.HowTo:
                case Page.Invite: page = Page.Main; break;
            }
        }

        // ------------------------------------------------------------------ frame

        void OnGUI()
        {
            var net = NetSession.Instance;
            if (net == null) return;

            // the Keybinds screen takes the whole window (and does its own scaling), so it is drawn instead of this menu
            if (page == Page.Keybinds)
            {
                if (KeybindScreen.Draw()) page = Page.Settings;
                return;
            }

            // A lobby / connection takes over the panel; pages that make no sense online fall back to Main.
            if (net.State != NetSessionState.Offline && (page == Page.Play || page == Page.Browse)) page = Page.Main;
            if (net.State == NetSessionState.Offline && page == Page.Invite) page = Page.Main;

            MenuKit.Begin();
            float s = HudTheme.S, pad = 24f * s, inset = 8f * s;
            float width = Mathf.Min(PanelW * s, Screen.width - 44f * s), left = 22f * s, y0 = 46f * s, maxH = Screen.height - 70f * s;
            bool logo = page == Page.Main && net.State == NetSessionState.Offline;
            LobbyStage.PanelPixels = left + width + 22f * s;

            int id = (int)page * 16 + (int)net.State;
            if (id != shownId) { shownId = id; shownAt = Time.unscaledTime; scroll.pos = 0f; GUI.FocusControl(null); }

            // what the session says ("Creating lobby...", why a join failed) sits under the title
            float statusH = 0f;
            if (!string.IsNullOrEmpty(net.Status)) statusH = MenuKit.WrappedHeight(net.Status, 15f * s, width - pad * 2f - 24f * s) + 14f * s + 8f * s;

            // the card is as tall as its page (up to the whole screen): the height the page had on its last frame, eased so it grows and shrinks smoothly
            float footerH = FooterHeight(net) * s, titleH = (logo ? 136f : 44f) * s;
            float want = Mathf.Clamp(titleH + statusH + scroll.ContentHeight + footerH + 10f * s + pad + 8f * s, 300f * s, maxH);
            if (cardH <= 0f) cardH = want;
            else if (Event.current.type == EventType.Repaint) cardH = Mathf.Lerp(cardH, want, 1f - Mathf.Exp(-14f * Mathf.Min(0.1f, Time.unscaledDeltaTime)));
            var panel = new Rect(left, y0, width, Mathf.Min(cardH, maxH));

            var mat = HudAnim.Begin(0.97f + 0.03f * HudAnim.Spring(Time.unscaledTime - shownAt), panel.center);
            MenuKit.Sheet(panel);
            float top;
            if (logo) { DrawLogo(panel); top = panel.y + titleH; }
            else { MenuKit.Ribbon(panel, TitleOf(net), RibbonOf(net)); top = panel.y + titleH; }

            if (statusH > 0f)
            {
                float ww = panel.width - pad * 2f - 24f * s, hh = statusH - 8f * s;
                var plate = new Rect(panel.x + pad, top, panel.width - pad * 2f, hh);
                MenuKit.Plate(plate, 0.22f);
                MenuKit.Wrapped(new Rect(plate.x + 12f * s, plate.y + 7f * s, ww, hh), net.Status, 15f * s, HudTheme.Muted);
                top += statusH;
            }

            float bottom = panel.yMax - pad - footerH - 10f * s;
            var view = new Rect(panel.x + pad - inset, top, panel.width - pad * 2f + inset * 2f, Mathf.Max(10f, bottom - top));

            float cw = scroll.Begin(view, inset);
            flow.At(inset, scroll.Top, cw);
            DrawPage(net, flow);
            scroll.End(view, flow.y);

            DrawFooter(net, new Rect(panel.x + pad, panel.yMax - pad - footerH, panel.width - pad * 2f, footerH));
            HudAnim.End(mat);
        }

        string TitleOf(NetSession net)
        {
            switch (page)
            {
                case Page.Play: return "PLAY";
                case Page.Browse: return "PUBLIC LOBBIES";
                case Page.Customize: return "CUSTOMIZE";
                case Page.Settings: return "SETTINGS";
                case Page.HowTo: return "HOW TO PLAY";
                case Page.Invite: return "INVITE FRIENDS";
                default: return net.State == NetSessionState.Connecting ? "CONNECTING" : "LOBBY";
            }
        }

        Color RibbonOf(NetSession net)
        {
            switch (page)
            {
                case Page.Play: return HudTheme.Cash;
                case Page.Customize: return Violet;
                case Page.HowTo: return HudTheme.Warning;
                case Page.Main: return net.State == NetSessionState.Connecting ? HudTheme.Info : HudTheme.Cash;
                default: return HudTheme.Info;
            }
        }

        /// <summary>The name of the game at the top of the card: a small spaced caption, the big word, a short amber line and the build number.</summary>
        void DrawLogo(Rect panel)
        {
            if (Event.current.type != EventType.Repaint) return;
            float s = HudTheme.S;
            float fitW = panel.width - 56f * s;
            var cap = new Rect(panel.x + 28f * s, panel.y + 18f * s, fitW, 26f * s);
            HudTheme.TextTracked(cap, "EMPLOYEES TAKE", 19f, HudTheme.Muted, 4.4f, TextAnchor.MiddleLeft);
            HudTheme.TextTracked(new Rect(panel.xMax - 130f * s, panel.y + 12f * s, 104f * s, 20f * s), "BUILD " + NetConst.ProtocolVersion, 12f, HudTheme.Dim, 2f, TextAnchor.MiddleRight);
            const string word = "REVENGE";
            float size = 64f;
            while (size > 30f && HudTheme.Width(word, size) + 1.5f * word.Length > fitW / s * s) size -= 2f;
            HudTheme.TextTracked(new Rect(cap.x - 2f * s, panel.y + 38f * s, fitW, 70f * s), word, size, HudTheme.Bright, 1.5f, TextAnchor.MiddleLeft);
            HudTheme.Rounded(new Rect(cap.x + 2f * s, panel.y + 114f * s, 64f * s, Mathf.Max(3f, 4.5f * s)), HudTheme.Quota, 1f);
        }
        // ------------------------------------------------------------------ the page and its footer

        void DrawPage(NetSession net, MenuKit.Flow f)
        {
            if (page == Page.Customize) DrawCustomize(net, f);
            else if (page == Page.Settings) DrawSettings(f);
            else if (page == Page.HowTo) DrawHowTo(f);
            else if (page == Page.Invite) DrawInvite(net, f);
            else switch (net.State)
            {
                case NetSessionState.Offline:
                    if (page == Page.Play) DrawPlay(net, f);
                    else if (page == Page.Browse) DrawBrowse(net, f);
                    else DrawMain(f);
                    break;
                case NetSessionState.Connecting:
                    f.Gap(30f);
                    MenuKit.Label(f.Row(40f, 0f), "PLEASE WAIT" + new string('.', 1 + (int)(Time.unscaledTime * 2.5f) % 3), 26f, HudTheme.Bright, TextAnchor.MiddleCenter, true);
                    break;
                default:
                    DrawLobby(net, f);
                    break;
            }
        }

        /// <summary>How tall (reference pixels) the button pinned to the card's bottom edge is.</summary>
        float FooterHeight(NetSession net) => page == Page.Main && net.State == NetSessionState.Offline ? 60f : 62f;

        void DrawFooter(NetSession net, Rect r)
        {
            bool offline = net.State == NetSessionState.Offline;
            switch (page)
            {
                case Page.Main:
                    if (offline) { if (MenuKit.Button(r, "QUIT GAME", HudTheme.Danger, 26f)) Quit(); }
                    else if (net.State == NetSessionState.Connecting) { if (MenuKit.Button(r, "CANCEL", HudTheme.Warning, 26f)) net.Leave("Cancelled."); }
                    else if (net.State != NetSessionState.StartingRun && MenuKit.Button(r, "LEAVE LOBBY", HudTheme.Danger, 26f)) net.Leave();
                    break;
                case Page.Customize:
                    if (MenuKit.Button(r, "DONE", HudTheme.Cash, 30f)) FinishCustomize(net);
                    break;
                case Page.Settings:
                    if (MenuKit.Button(r, "BACK", HudTheme.Info, 26f)) { PlayerPrefs.Save(); page = Page.Main; }
                    break;
                case Page.Browse:
                    if (MenuKit.Button(r, "BACK", HudTheme.Info, 26f)) page = Page.Play;
                    break;
                default:
                    if (MenuKit.Button(r, "BACK", HudTheme.Info, 26f)) page = Page.Main;
                    break;
            }
        }

        // ------------------------------------------------------------------ main

        void DrawMain(MenuKit.Flow f)
        {
            var steam = SteamService.Instance;
            bool ready = steam != null && steam.Ready;
            MenuKit.StatusPill(f.Row(34f, 14f), ready ? $"STEAM  signed in as {steam.LocalName}" : "STEAM  not available (" + (steam?.Error ?? "?") + ")",
                ready ? HudTheme.Good : HudTheme.Warning);


            if (MenuKit.Button(f.Row(76f, 14f), "PLAY", HudTheme.Cash, 36f)) page = Page.Play;
            if (MenuKit.Button(f.Row(62f, 14f), "CUSTOMIZE", HudTheme.Info, 28f)) OpenCustomize();
            if (MenuKit.Button(f.Row(62f, 14f), "HOW TO PLAY", HudTheme.Warning, 28f)) page = Page.HowTo;
            if (MenuKit.Button(f.Row(62f, 14f), "SETTINGS", Violet, 28f)) page = Page.Settings;
        }

        static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ play

        void DrawPlay(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            var steam = net.Steam;
            bool ready = steam != null && steam.Ready;
            if (!ready)
            {
                string why = "Steam is not available: " + (steam?.Error ?? "?");
                float hh = MenuKit.WrappedHeight(why, 15f * s, f.w - 24f * s) + 14f * s;
                var plate = f.Row(hh / s, 8f);
                HudTheme.Rounded(plate, HudTheme.Alpha(HudTheme.Warning, 0.3f), 12f * s);
                MenuKit.Wrapped(new Rect(plate.x + 12f * s, plate.y + 7f * s, plate.width - 24f * s, plate.height), why, 15f * s, HudTheme.Bright);
                if (MenuKit.Button(f.Row(44f, 14f), "RETRY STEAM", HudTheme.Warning, 20f)) steam?.Retry();
            }

            MenuKit.Header(f.Row(28f, 8f), "HOST A LOBBY", HudTheme.Cash);
            MenuKit.Label(f.Row(22f, 6f), $"up to {NetConst.MaxPlayers} players", 15f, HudTheme.Muted);
            var hostRow = f.Row(56f, 14f);
            if (MenuKit.Button(MenuKit.Cell(hostRow, 0, 3, 8f), "PUBLIC", HudTheme.Cash, 18f, ready)) net.HostSteam(LobbyVisibility.Public);
            if (MenuKit.Button(MenuKit.Cell(hostRow, 1, 3, 8f), "CODE ONLY", HudTheme.Info, 18f, ready)) net.HostSteam(LobbyVisibility.CodeOnly);
            if (MenuKit.Button(MenuKit.Cell(hostRow, 2, 3, 8f), "FRIENDS ONLY", Violet, 18f, ready)) net.HostSteam(LobbyVisibility.FriendsOnly);

            MenuKit.Header(f.Row(28f, 8f), "JOIN", HudTheme.Info);
            if (MenuKit.Button(f.Row(52f, 10f), "BROWSE PUBLIC LOBBIES", HudTheme.Info, 21f, ready)) { page = Page.Browse; Refresh(net); }
            var codeRow = f.Row(52f, 14f);
            var codeBox = MenuKit.Cell(codeRow, 0, 2, 10f);
            codeInput = MenuKit.TextBox(new Rect(codeBox.x, codeBox.y, codeBox.width, codeBox.height), codeInput, NetConst.CodeLength, "lobbycode", "LOBBY CODE").ToUpperInvariant();
            if (MenuKit.Button(MenuKit.Cell(codeRow, 1, 2, 10f), "JOIN BY CODE", HudTheme.Cash, 19f, ready)) net.JoinByCode(codeInput);

            MenuKit.Header(f.Row(28f, 8f), "SOLO", HudTheme.Quota);
            if (MenuKit.Button(f.Row(52f, 14f), "PLAY SOLO", HudTheme.Quota, 24f)) SceneManager.LoadScene(NetConst.RunScene);

            MenuKit.Header(f.Row(28f, 8f), "LOCAL TEST  (NO STEAM)", HudTheme.Dim);
            if (MenuKit.Button(f.Row(46f, 10f), "HOST LOCAL", Violet, 19f)) net.HostLocal();
            var ipRow = f.Row(46f, 10f);
            var ipBox = MenuKit.Cell(ipRow, 0, 2, 10f);
            ipInput = MenuKit.TextBox(ipBox, ipInput, 45, "localip", "127.0.0.1");
            if (MenuKit.Button(MenuKit.Cell(ipRow, 1, 2, 10f), "JOIN LOCAL IP", Violet, 18f)) net.JoinLocal(ipInput);
        }

        void DrawBrowse(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            var head = f.Row(48f, 12f);
            if (MenuKit.Button(MenuKit.Cell(head, 0, 2, 10f), "REFRESH", HudTheme.Info, 20f)) Refresh(net);
            var count = MenuKit.Cell(head, 1, 2, 10f);
            MenuKit.Label(count, searching ? "SEARCHING..." : listings.Count == 1 ? "1 OPEN LOBBY" : $"{listings.Count} OPEN LOBBIES", 17f, HudTheme.Muted, TextAnchor.MiddleCenter);
            if (!searching && listings.Count == 0)
                MenuKit.Label(f.Row(40f, 0f), "Nobody is hosting right now.", 17f, HudTheme.Muted, TextAnchor.MiddleCenter);

            foreach (var l in listings)
            {
                var row = f.Row(64f, 8f);
                MenuKit.Plate(row);
                var name = new Rect(row.x + 14f * s, row.y + 6f * s, row.width - 150f * s, 28f * s);
                MenuKit.Label(name, MenuKit.Fit(l.Name, 19f * s, name.width), 19f, HudTheme.Bright, TextAnchor.MiddleLeft, true);
                float cx = row.x + 14f * s;
                float cwid = HudTheme.Width($"{l.Members}/{l.Max}", 14f) + 22f * s;
                HudTheme.Chip(new Rect(cx, row.y + 35f * s, cwid, 22f * s), $"{l.Members}/{l.Max}", l.Members >= l.Max ? HudTheme.Danger : HudTheme.Info, 14f);
                if (!l.VersionOk) HudTheme.Chip(new Rect(cx + cwid + 8f * s, row.y + 35f * s, HudTheme.Width("OTHER VERSION", 13f) + 22f * s, 22f * s), "OTHER VERSION", HudTheme.Warning, 13f);
                var join = new Rect(row.xMax - 116f * s, row.y + 9f * s, 104f * s, 46f * s);
                if (MenuKit.Button(join, "JOIN", HudTheme.Cash, 20f, l.VersionOk && l.Members < l.Max)) { page = Page.Main; net.JoinSteam(l.Id); }
            }
        }

        void Refresh(NetSession net)
        {
            searching = true;
            net.Browse(list =>
            {
                listings.Clear();
                listings.AddRange(list);
                searching = false;
            });
        }

        // ------------------------------------------------------------------ lobby

        void DrawLobby(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            if (net.UsingSteam && !string.IsNullOrEmpty(net.LobbyCode))
            {
                var row = f.Row(66f, 14f);
                MenuKit.Plate(row, 0.22f);
                MenuKit.Label(new Rect(row.x + 16f * s, row.y + 6f * s, 160f * s, 22f * s), "LOBBY CODE", 14f, HudTheme.Muted);
                MenuKit.Label(new Rect(row.x + 16f * s, row.y + 24f * s, row.width * 0.6f, 40f * s), net.LobbyCode, 36f, HudTheme.Quota, TextAnchor.MiddleLeft, true);
                if (MenuKit.Button(new Rect(row.xMax - 110f * s, row.y + 11f * s, 98f * s, 44f * s), "COPY", HudTheme.Info, 18f)) GUIUtility.systemCopyBuffer = net.LobbyCode;
            }

            MenuKit.Header(f.Row(28f, 8f), $"PLAYERS  {net.Roster.Count}/{NetConst.MaxPlayers}", HudTheme.Info);
            foreach (var r in net.Roster)
            {
                var row = f.Row(46f, 7f);
                MenuKit.Plate(row);
                float right = row.xMax - 10f * s;
                if (net.UsingSteam && !r.IsHost)
                {
                    string state = r.Ready ? "READY" : "NOT READY";
                    float w = HudTheme.Width(state, 13f) + 22f * s;
                    HudTheme.Chip(new Rect(right - w, row.y + 11f * s, w, 24f * s), state, r.Ready ? HudTheme.Good : HudTheme.Dim, 13f);
                    right -= w + 6f * s;
                }
                if (r.IsLocal)
                {
                    float w = HudTheme.Width("YOU", 13f) + 22f * s;
                    HudTheme.Chip(new Rect(right - w, row.y + 11f * s, w, 24f * s), "YOU", HudTheme.Info, 13f);
                    right -= w + 6f * s;
                }
                if (r.IsHost)
                {
                    float w = HudTheme.Width("HOST", 13f) + 22f * s;
                    HudTheme.Chip(new Rect(right - w, row.y + 11f * s, w, 24f * s), "HOST", HudTheme.Quota, 13f);
                    right -= w + 6f * s;
                }
                MenuKit.Label(new Rect(row.x + 14f * s, row.y, right - row.x - 20f * s, row.height), MenuKit.Fit(r.Name, 19f * s, right - row.x - 20f * s), 19f, HudTheme.Bright, TextAnchor.MiddleLeft, true);
            }

            f.Gap(8f);
            if (net.UsingSteam && MenuKit.Button(f.Row(54f, 12f), "INVITE FRIENDS", HudTheme.Info, 22f)) { page = Page.Invite; RefreshFriends(); }
            if (MenuKit.Button(f.Row(54f, 14f), "CUSTOMIZE YOUR CHARACTER", Violet, 20f)) OpenCustomize();

            if (net.State == NetSessionState.StartingRun) { MenuKit.Notice(f.Row(40f, 0f), "LOADING THE RUN...", HudTheme.Quota, 20f); return; }

            if (net.IsHost)
            {
                bool can = net.CanStart;
                if (MenuKit.Button(f.Row(72f, 0f), can ? "START RUN" : "WAITING FOR EVERYONE TO BE READY...", HudTheme.Cash, can ? 36f : 17f, can)) net.StartRun();
            }
            else if (net.UsingSteam)
            {
                if (MenuKit.Button(f.Row(64f, 12f), net.LocalReady ? "READY!   (TAP TO CANCEL)" : "I AM READY", net.LocalReady ? HudTheme.Cash : HudTheme.Warning, 26f)) net.SetReady(!net.LocalReady);
                MenuKit.Notice(f.Row(34f, 0f), "WAITING FOR THE HOST TO START...", HudTheme.Shade(HudTheme.Card, 0.3f), 16f);
            }
            else MenuKit.Notice(f.Row(34f, 0f), "WAITING FOR THE HOST TO START...", HudTheme.Shade(HudTheme.Card, 0.3f), 16f);
        }

        // ------------------------------------------------------------------ customize

        void OpenCustomize()
        {
            edit = PlayerAppearance.LoadOrDefault();
            Color.RGBToHSV(edit.body, out hue, out sat, out val);
            var catalog = CharacterCatalog.Instance;
            var toons = catalog ? catalog.Players() : null;
            toonNames = null;
            if (toons != null && toons.Count > 0)
            {
                toonNames = new string[toons.Count];          // (no PARTY choice any more: every player is a dock worker; the Party character is only the fallback of a machine without the pack)
                for (int i = 0; i < toons.Count; i++) toonNames[i] = catalog.DockLabel(toons[i]).ToUpperInvariant();
            }
            page = Page.Customize;
            if (LobbyStage.Instance) LobbyStage.Instance.Customizing = true;
        }

        void FinishCustomize(NetSession net)
        {
            edit.Save();
            net.SetLook(edit.Serialize()); // friends in the lobby see it
            if (LobbyStage.Instance) { LobbyStage.Instance.SetLocalAppearance(edit); LobbyStage.Instance.Customizing = false; }
            page = Page.Main;
        }

        void DrawCustomize(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            bool changed = false;
            MenuKit.Notice(f.Row(30f, 12f), "DRAG ON THE CHARACTER TO TURN IT", HudTheme.Shade(HudTheme.Card, 0.3f), 15f);

            // Which character: the Party character (colour, face, hat) or one of the Tiny Toon people.
            var catalog = CharacterCatalog.Instance;
            var toons = catalog ? catalog.Players() : null;
            if (toonNames != null && toons != null && toons.Count > 0 && toonNames.Length == toons.Count)
            {
                MenuKit.Header(f.Row(28f, 8f), "CHARACTER", HudTheme.Info);
                int current = 0;
                for (int i = 0; i < toons.Count; i++) if (edit.model == toons[i].id) current = i;
                int pick = MenuKit.Choice(f, toonNames, current, 3, HudTheme.Quota, 42f, 16f);
                if (pick != current || edit.model != toons[pick].id) { edit.model = toons[pick].id; changed = true; }
                f.Gap(6f);
            }

            if (!edit.IsToon) // colour, face and hat belong to the Party character
            {
                MenuKit.Header(f.Row(28f, 8f), "COLOUR", HudTheme.Cash);
                const int cols = 6;
                float g = 8f * s, cell = (f.w - g * (cols - 1)) / cols;
                for (int i = 0; i < Swatches.Length; i++)
                {
                    var r = new Rect(f.x + (i % cols) * (cell + g), f.y + (i / cols) * (cell + g), cell, cell);
                    if (MenuKit.Swatch(r, Swatches[i], SameColour(edit.body, Swatches[i])))
                    {
                        edit.body = Swatches[i];
                        Color.RGBToHSV(Swatches[i], out hue, out sat, out val);
                        changed = true;
                    }
                }
                f.y += ((Swatches.Length + cols - 1) / cols) * (cell + g) + 6f * s;

                float h = MenuKit.SliderRow(f, "HUE", Mathf.RoundToInt(hue * 360f) + "°", hue, 0f, 1f, HudTheme.Quota);
                float sa = MenuKit.SliderRow(f, "SATURATION", Mathf.RoundToInt(sat * 100f) + "%", sat, 0f, 1f, HudTheme.Quota);
                float v = MenuKit.SliderRow(f, "BRIGHTNESS", Mathf.RoundToInt(val * 100f) + "%", val, 0.15f, 1f, HudTheme.Quota);
                if (h != hue || sa != sat || v != val)
                {
                    hue = h; sat = sa; val = v;
                    edit.body = Color.HSVToRGB(hue, sat, val);
                    changed = true;
                }

                f.Gap(4f);
                MenuKit.Header(f.Row(28f, 8f), "FACE", HudTheme.Warning);
                int face = MenuKit.Choice(f, FaceNames, edit.face - 1, 3, HudTheme.Quota, 42f, 17f) + 1;
                if (face != edit.face) { edit.face = face; changed = true; }

                f.Gap(4f);
                MenuKit.Header(f.Row(28f, 8f), "HAT", HudTheme.Danger, HudTheme.Bright);
                int hat = MenuKit.Choice(f, HatNames, edit.hat, 2, HudTheme.Quota, 42f, 17f);
                if (hat != edit.hat) { edit.hat = hat; changed = true; }
            }

            f.Gap(4f);
            MenuKit.Header(f.Row(28f, 8f), "BUILD", HudTheme.Info);
            float height = MenuKit.SliderRow(f, "HEIGHT", Mathf.RoundToInt(edit.height * 100f) + "%", edit.height, PlayerAppearance.MinBuild, PlayerAppearance.MaxBuild, HudTheme.Info);
            float width = MenuKit.SliderRow(f, "WIDTH", Mathf.RoundToInt(edit.width * 100f) + "%", edit.width, PlayerAppearance.MinBuild, PlayerAppearance.MaxBuild, HudTheme.Info);
            if (height != edit.height || width != edit.width) { edit.height = height; edit.width = width; changed = true; }

            f.Gap(6f);
            var row = f.Row(50f, 8f);
            if (MenuKit.Button(MenuKit.Cell(row, 0, 2, 10f), "RANDOMIZE", Violet, 19f))
            {
                string keepModel = edit.model; // randomize the look, not which character it is
                edit = PlayerAppearance.Random(new System.Random());
                edit.model = keepModel;
                Color.RGBToHSV(edit.body, out hue, out sat, out val);
                changed = true;
            }
            if (MenuKit.Button(MenuKit.Cell(row, 1, 2, 10f), "RESET", HudTheme.Warning, 19f))
            {
                edit = PlayerAppearance.ForSlot(0);
                Color.RGBToHSV(edit.body, out hue, out sat, out val);
                changed = true;
            }

            if (changed && LobbyStage.Instance) LobbyStage.Instance.SetLocalAppearance(edit);
        }

        static bool SameColour(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;

        // ------------------------------------------------------------------ invite friends

        readonly List<FriendEntry> friends = new();
        readonly Dictionary<ulong, float> invitedAt = new();
        float nextFriendRefresh;

        void RefreshFriends()
        {
            FriendsService.Refresh(friends);
            nextFriendRefresh = Time.unscaledTime + 2f;
        }

        void DrawInvite(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            if (Event.current.type == EventType.Layout && Time.unscaledTime >= nextFriendRefresh) RefreshFriends();

            int online = 0;
            foreach (var fr in friends) if (fr.Online) online++;
            MenuKit.Label(f.Row(28f, 8f), friends.Count == 0 ? "No friends found on Steam." : $"{online} of {friends.Count} friends online", 17f, HudTheme.Muted, TextAnchor.MiddleCenter);

            foreach (var fr in friends)
            {
                var row = f.Row(56f, 7f);
                MenuKit.Plate(row, fr.Online ? 0.18f : 0.1f);
                HudIcons.Dot(new Vector2(row.x + 22f * s, row.center.y), 15f * s, fr.Online ? HudTheme.Good : HudTheme.Dim);
                MenuKit.Label(new Rect(row.x + 40f * s, row.y + 4f * s, row.width - 170f * s, 28f * s), MenuKit.Fit(fr.Name, 18f * s, row.width - 170f * s), 18f, fr.Online ? HudTheme.Bright : HudTheme.Dim, TextAnchor.MiddleLeft, fr.Online);
                MenuKit.Label(new Rect(row.x + 40f * s, row.y + 30f * s, row.width - 170f * s, 20f * s), fr.Status, 13f, HudTheme.Muted);

                bool recent = invitedAt.TryGetValue(fr.Id.m_SteamID, out float at) && Time.unscaledTime - at < 8f;
                var btn = new Rect(row.xMax - 112f * s, row.y + 8f * s, 102f * s, 40f * s);
                if (MenuKit.Button(btn, recent ? "INVITED" : "INVITE", recent ? HudTheme.Good : HudTheme.Cash, 17f, fr.Online && !recent))
                {
                    if (net.InviteFriend(fr.Id)) invitedAt[fr.Id.m_SteamID] = Time.unscaledTime;
                }
            }

            f.Gap(6f);
            var bottom = f.Row(48f, 6f);
            if (MenuKit.Button(MenuKit.Cell(bottom, 0, 2, 10f), "REFRESH", HudTheme.Info, 18f)) RefreshFriends();
            if (MenuKit.Button(MenuKit.Cell(bottom, 1, 2, 10f), "STEAM DIALOG", Violet, 18f)) net.InviteFriends();
        }

        // ------------------------------------------------------------------ how to play

        void DrawHowTo(MenuKit.Flow f)
        {
            float s = HudTheme.S;
            foreach (var (title, body) in HowToPlayContent.Sections)
            {
                MenuKit.Header(f.Row(28f, 6f), title.ToUpperInvariant(), HudTheme.Quota);
                float h = MenuKit.WrappedHeight(body, 16f * s, f.w - 8f * s);
                MenuKit.Wrapped(new Rect(f.x + 4f * s, f.y, f.w - 8f * s, h), body, 16f * s, HudTheme.Bright);
                f.y += h + 16f * s;
            }

            MenuKit.Header(f.Row(28f, 10f), "CONTROLS", HudTheme.Info);
            float keyW = 128f * s, textX = keyW + 14f * s;
            foreach (var (key, action) in HowToPlayContent.Controls)
            {
                float textH = MenuKit.WrappedHeight(action, 15f * s, f.w - textX - 6f * s), h = Mathf.Max(34f * s, textH + 4f * s);
                if (Event.current.type == EventType.Repaint)
                {
                    float kh = 30f * s, size = 14f;
                    float need = HudTheme.TextWidthPx(key, size * s) + kh * 0.55f;
                    if (need > keyW) size = Mathf.Max(9f, size * (keyW - kh * 0.55f) / Mathf.Max(1f, HudTheme.TextWidthPx(key, size * s)));
                    HudTheme.KeyCap(f.x + 2f * s, f.y + (h - kh) * 0.5f, key, kh, size);
                }
                MenuKit.Wrapped(new Rect(f.x + textX, f.y + (h - textH) * 0.5f, f.w - textX - 6f * s, textH + 2f * s), action, 15f * s, HudTheme.Bright);
                f.y += h + 8f * s;
            }

            f.Gap(6f);
            if (MenuKit.Button(f.Row(50f, 6f), "KEYBINDS", HudTheme.Info, 21f)) page = Page.Keybinds;
        }

        // ------------------------------------------------------------------ settings

        void DrawSettings(MenuKit.Flow f)
        {
            float vol = MenuKit.SliderRow(f, "VOLUME", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", AudioListener.volume, 0f, 1f, HudTheme.Cash);
            if (!Mathf.Approximately(vol, AudioListener.volume))
            {
                AudioListener.volume = vol;
                PlayerPrefs.SetFloat(VolumeKey, vol);
            }

            float sens = PlayerPrefs.GetFloat(SensitivityKey, 2.4f);
            float ns = MenuKit.SliderRow(f, "MOUSE SENSITIVITY", sens.ToString("0.0"), sens, 0.5f, 6f, HudTheme.Info);
            if (!Mathf.Approximately(ns, sens)) PlayerPrefs.SetFloat(SensitivityKey, ns);

            f.Gap(6f);
            MenuKit.Header(f.Row(28f, 8f), "VOICE CHAT", HudTheme.Warning);
            int vm = MenuKit.Choice(f, VoiceModes, (int)VoiceChat.CurrentMode, 1, HudTheme.Quota, 44f, 18f);
            if (vm != (int)VoiceChat.CurrentMode) VoiceChat.CurrentMode = (VoiceChat.Mode)vm;
            f.Gap(4f);
            float vv = MenuKit.SliderRow(f, "VOICES", Mathf.RoundToInt(VoiceChat.Volume * 100f) + "%", VoiceChat.Volume, 0f, 1f, HudTheme.Warning);
            if (!Mathf.Approximately(vv, VoiceChat.Volume)) VoiceChat.Volume = vv;

            f.Gap(6f);
            bool fs = MenuKit.ToggleRow(f.Row(52f, 14f), "FULLSCREEN", Screen.fullScreen);
            if (fs != Screen.fullScreen) Screen.fullScreen = fs;

            if (MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f)) page = Page.Keybinds;
        }
    }
}

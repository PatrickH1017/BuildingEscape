using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Esc menu: master and music volume, a Settings button (the same page as the lobby's: volumes, mouse sensitivity, voice chat, fullscreen, keybinds - the owner, 2026-10-18: "There should be a Settings button on escape
    /// menu"), the Keybinds screen, leaving the run and Exit Game, in the toon look of the in-run HUD (<see cref="MenuKit"/>). It does NOT pause the game; it only frees the cursor. Created automatically.
    /// </summary>
    public class EscapeMenu : MonoBehaviour
    {
        const string VolumeKey = SettingsPanel.VolumeKey;

        public static bool Open { get; private set; }
        static EscapeMenu instance;

        bool shopWasOpen, panelWasOpen, outpostWasOpen, terminalWasOpen;
        /// <summary>The Keybinds screen is showing instead of the menu (Esc closes it first).</summary>
        bool controlsOpen;
        /// <summary>The Settings page is showing instead of the menu (Esc closes it; the Keybinds screen opened from it comes back to it).</summary>
        bool settingsOpen;
        readonly MenuKit.Flow flow = new MenuKit.Flow();
        readonly MenuKit.Flow settingsFlow = new MenuKit.Flow();
        readonly MenuKit.Scroll settingsScroll = new MenuKit.Scroll();

        /// <summary>The Settings page is up (for the tests).</summary>
        public static bool SettingsShowing => Open && instance != null && instance.settingsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (FindFirstObjectByType<EscapeMenu>()) return;
            var go = new GameObject("EscapeMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<EscapeMenu>();
        }

        void Awake() { instance = this; }

        void OnDestroy()
        {
            Open = false;
            if (instance == this) instance = null;
        }

        /// <summary>Closes the menu and every page of it (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void CloseAll()
        {
            Open = false;
            if (instance != null) { instance.controlsOpen = false; instance.settingsOpen = false; }
        }

        /// <summary>For the tests: the menu is up, on its Settings page or on its main page, as if the player had pressed Esc (and the button).</summary>
        public static void OpenForTests(bool settings)
        {
            Open = true;
            if (instance != null) { instance.controlsOpen = false; instance.settingsOpen = settings; }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (controlsOpen) controlsOpen = false;    // Esc closes the keybinds screen first
                else if (settingsOpen) { settingsOpen = false; PlayerPrefs.Save(); }    // then the settings page
                else if (Backpack.UiOpen) Backpack.CloseUi();   // then the inventory
                else if (!shopWasOpen && !panelWasOpen && !outpostWasOpen && !terminalWasOpen) Open = !Open;   // (the shop, the elevator panel and the outpost's screens close themselves on Esc)
            }
        }

        void LateUpdate() { shopWasOpen = Vendor.ShopOpen; panelWasOpen = ElevatorPanel.Open; outpostWasOpen = OutpostUi.Open; terminalWasOpen = DevTerminal.Open; }

        void OnGUI()
        {
            if (!Open) { controlsOpen = false; settingsOpen = false; return; }
            if (controlsOpen) { if (KeybindScreen.Draw()) controlsOpen = false; return; }
            if (settingsOpen) { DrawSettings(); return; }

            MenuKit.Begin();
            float s = HudTheme.S, pad = 30f * s;
            bool online = Net.NetSession.Online;

            // the card is as tall as what is on it (reference pixels): room for the ribbon, two sliders, the buttons and a note
            float need = 70f + 2f * 72f + 8f + (64f + 12f) + (56f + 12f) + (56f + 12f) + (online ? 56f + 12f : 0f) + (56f + 16f) + 28f + 26f;
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s), ph = Mathf.Min(need * s, Screen.height - 40f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);

            MenuKit.Dim(0.5f);
            var mat = HudAnim.Begin(HudAnim.Pop(HudAnim.Id.Esc, true), panel.center);
            MenuKit.Sheet(panel);
            MenuKit.Ribbon(panel, "MENU", HudTheme.Info);
            var f = flow.At(panel.x + pad, panel.y + 70f * s, panel.width - pad * 2f);

            float vol = MenuKit.SliderRow(f, "MASTER VOLUME", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", AudioListener.volume, 0f, 1f, HudTheme.Cash);
            if (!Mathf.Approximately(vol, AudioListener.volume))
            {
                AudioListener.volume = vol;
                PlayerPrefs.SetFloat(VolumeKey, vol);
            }
            float music = MenuKit.SliderRow(f, "MUSIC VOLUME", Mathf.RoundToInt(MusicDirector.Volume * 100f) + "%", MusicDirector.Volume, 0f, 1f, HudTheme.Info);
            if (!Mathf.Approximately(music, MusicDirector.Volume)) MusicDirector.Volume = music;
            f.Gap(8f);

            if (MenuKit.Button(f.Row(64f, 12f), "RESUME", HudTheme.Cash, 30f)) Open = false;
            if (MenuKit.Button(f.Row(56f, 12f), "SETTINGS", SettingsPanel.Violet, 26f)) { settingsOpen = true; settingsScroll.pos = 0f; }
            if (MenuKit.Button(f.Row(56f, 12f), "KEYBINDS", HudTheme.Info, 26f)) controlsOpen = true;
            if (online && MenuKit.Button(f.Row(56f, 12f), "LEAVE RUN", HudTheme.Warning, 26f))
            {
                Open = false;
                Net.NetSession.Instance.Leave();
            }
            if (MenuKit.Button(f.Row(56f, 16f), "EXIT GAME", HudTheme.Danger, 26f)) Quit();
            MenuKit.Notice(f.Row(28f, 0f), "THE GAME KEEPS RUNNING", HudTheme.Shade(HudTheme.Card, 0.3f), 13f);
            HudAnim.End(mat);
        }

        /// <summary>The Settings page: the same controls as the lobby's (<see cref="SettingsPanel"/>) in a card of its own, with BACK pinned at the bottom; it scrolls when the window is too short for it.</summary>
        void DrawSettings()
        {
            MenuKit.Begin();
            float s = HudTheme.S, pad = 30f * s, inset = 8f * s;
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s), ph = Mathf.Min(880f * s, Screen.height - 40f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);

            MenuKit.Dim(0.5f);
            var mat = HudAnim.Begin(HudAnim.Pop(HudAnim.Id.Esc, true), panel.center);
            MenuKit.Sheet(panel);
            MenuKit.Ribbon(panel, "SETTINGS", HudTheme.Info);

            float footerH = 62f * s;
            var view = new Rect(panel.x + pad - inset, panel.y + 70f * s, panel.width - pad * 2f + inset * 2f, Mathf.Max(10f, panel.height - 70f * s - pad - footerH - 10f * s));
            float cw = settingsScroll.Begin(view, inset);
            settingsFlow.At(inset, settingsScroll.Top, cw);
            bool keys = SettingsPanel.DrawControls(settingsFlow);
            settingsScroll.End(view, settingsFlow.y);

            if (MenuKit.Button(new Rect(panel.x + pad, panel.yMax - pad - footerH, panel.width - pad * 2f, footerH), "BACK", HudTheme.Info, 26f)) { settingsOpen = false; PlayerPrefs.Save(); }
            if (keys) controlsOpen = true;
            HudAnim.End(mat);
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
    }
}

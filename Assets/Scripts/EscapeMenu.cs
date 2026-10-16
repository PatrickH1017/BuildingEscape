using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Esc menu: master and music volume, the Keybinds screen, leaving the run and Exit Game, in the toon look of the in-run HUD
    /// (<see cref="MenuKit"/>). It does NOT pause the game; it only frees the cursor. Created automatically.
    /// </summary>
    public class EscapeMenu : MonoBehaviour
    {
        const string VolumeKey = "OvertimeEscape.MasterVolume";

        public static bool Open { get; private set; }

        bool shopWasOpen, panelWasOpen, outpostWasOpen;
        /// <summary>The Keybinds screen is showing instead of the menu (Esc closes it first).</summary>
        bool controlsOpen;
        readonly MenuKit.Flow flow = new MenuKit.Flow();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (FindFirstObjectByType<EscapeMenu>()) return;
            var go = new GameObject("EscapeMenu");
            DontDestroyOnLoad(go);
            go.AddComponent<EscapeMenu>();
        }

        void OnDestroy() { Open = false; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (controlsOpen) controlsOpen = false;    // Esc closes the keybinds screen first
                else if (Backpack.UiOpen) Backpack.CloseUi();   // then the inventory
                else if (!shopWasOpen && !panelWasOpen && !outpostWasOpen) Open = !Open;   // (the shop, the elevator panel and the outpost's screens close themselves on Esc)
            }
        }

        void LateUpdate() { shopWasOpen = Vendor.ShopOpen; panelWasOpen = ElevatorPanel.Open; outpostWasOpen = OutpostUi.Open; }

        void OnGUI()
        {
            if (!Open) { controlsOpen = false; return; }
            if (controlsOpen) { if (KeybindScreen.Draw()) controlsOpen = false; return; }

            MenuKit.Begin();
            float s = HudTheme.S, pad = 30f * s;
            bool online = Net.NetSession.Online;

            // the card is as tall as what is on it (reference pixels): room for the ribbon, two sliders, the buttons and a note
            float need = 70f + 2f * 72f + 8f + (64f + 12f) + (56f + 12f) + (online ? 56f + 12f : 0f) + (56f + 16f) + 28f + 26f;
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

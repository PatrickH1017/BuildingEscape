# Round 6, batch A2 (the owner's task list of 2026-10-18): (5) the intro's skip vote is saved once the bar has filled, (8) a Settings button on the Esc menu (the lobby's Settings page, shared), (1) every menu closes when an
# invite is accepted (MenuRegistry.CloseAll). The new files (SettingsPanel.cs, MenuRegistry.cs, the rewritten EscapeMenu.cs) are copied in by r6_a2_files.ps1. BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---------------------------------------------------------------- (5) the intro: the bar saves the vote
Open-Src (Join-Path $Dir 'Scripts\Intro\IntroRules.cs')
Rep '        public static bool HoldDone(float held) => held >= HoldSeconds - 0.0001f;' '        public static bool HoldDone(float held) => held >= HoldSeconds - 0.0001f;

        /// <summary>
        /// Is this player''s vote to skip in? Once the bar has filled the vote is SAVED: letting go of Space does not take it back (the owner, 2026-10-18: "if the bar fills up for a player it saves their skip vote, not keep
        /// holding for it to count"). <paramref name="saved"/> is whether it already was.
        /// </summary>
        public static bool VoteSaved(bool saved, float held) => saved || HoldDone(held);'
Rep 'HOLDING SPACE skips it; in multiplayer it is skipped, for everybody, as soon as at least 50 % of the party members are holding it.' 'HOLDING SPACE skips it; in multiplayer it is skipped, for everybody, as soon as at least 50 % of the party members have filled their bar (the vote is saved: they need not keep holding).'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Intro\IntroContent.cs')
Rep '        public const string SkipHint = "HOLD SPACE TO SKIP";' '        public const string SkipHint = "HOLD SPACE TO SKIP";
        /// <summary>Shown instead of the hint once this player''s bar has filled (the vote is saved, they can let go).</summary>
        public const string SavedHint = "SKIP VOTE SAVED";'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Intro\IntroCinematic.cs')
Rep '            bool vote = IntroRules.HoldDone(held);
            if (vote != voting) { voting = vote; sync.RequestIntroHold(vote); }' '            if (!voting && IntroRules.VoteSaved(voting, held)) { voting = true; sync.RequestIntroHold(true); }      // (the bar filled: the vote is saved, letting go of Space does not take it back)'
Rep '            string line = party > 1 && sync != null ? $"{IntroContent.SkipHint}   {Mathf.Min(sync.IntroHolders, needed)} / {needed}" : IntroContent.SkipHint;' '            string hintText = voting ? IntroContent.SavedHint : IntroContent.SkipHint;
            string line = party > 1 && sync != null ? $"{hintText}   {Mathf.Min(sync.IntroHolders, needed)} / {needed}" : hintText;'
Rep '            GUI.color = HudTheme.Quota; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(held / IntroRules.HoldSeconds), bar.height), Texture2D.whiteTexture);' '            GUI.color = voting ? HudTheme.Cash : HudTheme.Quota; GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * (voting ? 1f : Mathf.Clamp01(held / IntroRules.HoldSeconds)), bar.height), Texture2D.whiteTexture);'
Save-Src

# ---------------------------------------------------------------- (8) + (1) the lobby menu: the shared settings page, and the close hook
Open-Src (Join-Path $Dir 'Scripts\Net\LobbyMenu.cs')
Rep '        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
' ''
Rep '        void Start()
        {
            Cursor.lockState = CursorLockMode.None;' '        static LobbyMenu instance;

        /// <summary>Closes the page the lobby menu is on: back to the main page, which shows the lobby or the connection (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void CloseAll()
        {
            if (instance == null) return;
            instance.page = Page.Main;
            GUI.FocusControl(null);
        }

        /// <summary>Is the lobby menu on a page other than its main one?</summary>
        public static bool PageOpen => instance != null && instance.page != Page.Main;

        void OnDestroy() { if (instance == this) instance = null; }

        void Start()
        {
            instance = this;
            Cursor.lockState = CursorLockMode.None;'
Rep '        void DrawSettings(MenuKit.Flow f)
        {
            float vol = MenuKit.SliderRow(f, "MASTER VOLUME", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", AudioListener.volume, 0f, 1f, HudTheme.Cash);
            if (!Mathf.Approximately(vol, AudioListener.volume))
            {
                AudioListener.volume = vol;
                PlayerPrefs.SetFloat(VolumeKey, vol);
            }
            float music = MenuKit.SliderRow(f, "MUSIC VOLUME", Mathf.RoundToInt(MusicDirector.Volume * 100f) + "%", MusicDirector.Volume, 0f, 1f, HudTheme.Info);
            if (!Mathf.Approximately(music, MusicDirector.Volume)) MusicDirector.Volume = music;

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
        }' '        /// <summary>The Settings page: the controls are <see cref="SettingsPanel"/>''s, the same as the Esc menu''s Settings page.</summary>
        void DrawSettings(MenuKit.Flow f)
        {
            if (SettingsPanel.DrawControls(f)) page = Page.Keybinds;
        }'
Save-Src

# ---------------------------------------------------------------- (1) the overlays learn to close, and the invite closes them
Open-Src (Join-Path $Dir 'Scripts\Vendor.cs')
Rep '        public static bool ShopOpen { get; private set; }' '        public static bool ShopOpen { get; private set; }

        /// <summary>Closes the shop window (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void CloseShop() { ShopOpen = false; }'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\ElevatorPanel.cs')
Rep '        public static bool Open { get; private set; }' '        public static bool Open { get; private set; }

        /// <summary>Closes the panel if it is up (an accepted invite closes every menu, see <see cref="MenuRegistry"/>).</summary>
        public static void CloseAll() { if (Active != null) Active.ClosePanel(); else Open = false; }'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Outpost\OutpostUi.cs')
Rep '        /// <summary>A screen went away (ignored if another one has taken its place).</summary>' '        /// <summary>Every screen of the outpost goes away (an accepted invite closes every menu, see <see cref="MenuRegistry"/>); the run-start cinematic is not one of the menus and stays.</summary>
        public static void HideAll()
        {
            if (Current != Kind.Intro) Current = Kind.None;
        }

        /// <summary>A screen went away (ignored if another one has taken its place).</summary>'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Net\Social\InviteInbox.cs')
Rep '            showing = false;
            Accepted++;
            var net = NetSession.Instance;' '            showing = false;
            Accepted++;
            MenuRegistry.CloseAll();            // (the owner, 2026-10-18: "All open menus should close when accepting an invite")
            var net = NetSession.Instance;'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Net\NetSession.cs')
Rep 'lobby.JoinRequested += id => { if (State == NetSessionState.Offline) JoinSteam(id); };' 'lobby.JoinRequested += id => { if (State == NetSessionState.Offline) { MenuRegistry.CloseAll(); JoinSteam(id); } };'
Save-Src

# ---------------------------------------------------------------- the checks
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6Test.cs')
Rep '            Check("...with no armed floor (the test menu''s never) the first-floor values apply", Mathf.Approximately(an, 0.8f));
        }' '            Check("...with no armed floor (the test menu''s never) the first-floor values apply", Mathf.Approximately(an, 0.8f));

            // ---- the intro: the filled bar saves the vote
            float held = 0f; bool saved = false, droppedWhenReleased = false;
            for (int i = 0; i < 90; i++) { held = IntroRules.Hold(held, true, 1f / 60f); saved = IntroRules.VoteSaved(saved, held); }          // 1.5 s of holding: the bar fills at 1 s
            bool savedAtEnd = saved;
            for (int i = 0; i < 600; i++) { held = IntroRules.Hold(held, false, 1f / 60f); saved = IntroRules.VoteSaved(saved, held); }       // ten seconds with Space let go: the bar is empty
            droppedWhenReleased = !saved;
            Check("intro: the bar fills in a second of holding and the vote is saved", savedAtEnd && !IntroRules.VoteSaved(false, 0.9f) && IntroRules.VoteSaved(false, IntroRules.HoldSeconds));
            Check("intro: letting go of Space afterwards does not take the saved vote back (the bar itself empties)", !droppedWhenReleased && held <= 0.0001f);
            Check("intro: a short hold that does not fill the bar saves nothing", !IntroRules.VoteSaved(false, IntroRules.Hold(0f, true, 0.6f)));
            Check("intro: the vote still takes half of the party (2 of 3, 2 of 4, 3 of 5; 1 of 1)", IntroRules.SkipPassed(2, 3) && IntroRules.SkipPassed(2, 4) && !IntroRules.SkipPassed(1, 3) && IntroRules.SkipPassed(3, 5) && IntroRules.SkipPassed(1, 1));
        }'
Save-Src
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6.cs')
Rep '            Log($"[R6] DONE fails={r6Fails}");' '            // ---- (1) accepting an invite closes every open menu
            {
                var inbox = BuildingEscape.Net.InviteInbox.Instance;
                R6Check("the invite inbox is there", inbox != null);
                EscapeMenu.OpenForTests(true);
                OutpostUi.Show(OutpostUi.Kind.Journal);
                R6Check("the Esc menu (on its Settings page) and an outpost screen are up", EscapeMenu.Open && EscapeMenu.SettingsShowing && OutpostUi.Open && MenuRegistry.AnyOpen);
                if (inbox != null)
                {
                    int closed0 = MenuRegistry.Closed;
                    inbox.Enqueue(new Steamworks.CSteamID(76561198000000123UL), "A friend");
                    yield return new WaitUntil(() => inbox.Showing);
                    R6Check("the invite is showing", inbox.Showing);
                    inbox.Accept();
                    yield return null;
                    R6Check("accepting it closed every menu: the Esc menu with its settings page and the outpost screen are gone", !EscapeMenu.Open && !EscapeMenu.SettingsShowing && !OutpostUi.Open && !MenuRegistry.AnyOpen && MenuRegistry.Closed == closed0 + 1);
                }
                OutpostUi.Hide(OutpostUi.Kind.Journal);
                R6Check("the intro cinematic is not one of the menus: HideAll leaves it", ShowThenHideAll());
                EscapeMenu.OpenForTests(false);
                R6Check("the Esc menu opens on its main page for the tests and closes with CloseAll", EscapeMenu.Open && !EscapeMenu.SettingsShowing);
                MenuRegistry.CloseAll();
                R6Check("...closed", !EscapeMenu.Open);
            }

            Log($"[R6] DONE fails={r6Fails}");'
Rep '        IEnumerator R6Scenario()
        {' '        static bool ShowThenHideAll()
        {
            OutpostUi.Show(OutpostUi.Kind.Intro);
            OutpostUi.HideAll();
            bool stays = OutpostUi.Current == OutpostUi.Kind.Intro;
            OutpostUi.Hide(OutpostUi.Kind.Intro);
            return stays && !OutpostUi.Open;
        }

        IEnumerator R6Scenario()
        {'
Save-Src
Write-Output 'round 6 batch A2 patched'

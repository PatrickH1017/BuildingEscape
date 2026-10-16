using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Placeholder IMGUI front end over the 3D lobby: Play / Customize / Settings / Quit, the lobby
    /// browser and the pre-run lobby. It only calls NetSession and LobbyStage and reads their state,
    /// so it can be replaced by real UI without touching any networking code.
    /// </summary>
    public class LobbyMenu : MonoBehaviour
    {
        enum Page { Main, Play, Browse, Customize, Settings, HowTo, Invite }

        const float PanelW = 380f;
        const string VolumeKey = "OvertimeEscape.MasterVolume";   // shared with the in-run Esc menu
        const string SensitivityKey = "OvertimeEscape.MouseSensitivity";

        static readonly Color[] Swatches =
        {
            new Color(0.25f, 0.90f, 0.70f), new Color(1.00f, 1.00f, 0.00f), new Color(1.00f, 0.40f, 0.70f), new Color(0.00f, 1.00f, 1.00f),
            new Color(1.00f, 0.45f, 0.10f), new Color(0.90f, 0.15f, 0.15f), new Color(0.60f, 0.35f, 1.00f), new Color(0.20f, 0.45f, 1.00f),
            new Color(0.45f, 0.85f, 0.20f), new Color(0.95f, 0.95f, 0.95f), new Color(0.55f, 0.55f, 0.60f), new Color(0.12f, 0.12f, 0.14f),
        };

        Page page;
        string codeInput = "";
        string ipInput = "127.0.0.1";
        readonly List<LobbyListing> listings = new();
        bool searching;
        Vector2 scroll, howToScroll;
        PlayerAppearance edit;
        Vector2 charScroll;
        float hue, sat = 1f, val = 1f;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (!GetComponent<LobbyStage>()) gameObject.AddComponent<LobbyStage>();
            // Opened the Menu scene directly in the editor: go through Boot so the Net object exists.
            if (NetSession.Instance == null) SceneManager.LoadScene(NetConst.BootScene);
        }

        // ------------------------------------------------------------------ frame

        void OnGUI()
        {
            var net = NetSession.Instance;
            if (net == null) return;

            float k = Mathf.Max(0.75f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;                 // GUI state is shared by every OnGUI of the frame: put it back when done
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            LobbyStage.PanelPixels = (PanelW + 20f) * k;

            // A lobby / connection takes over the panel; pages that make no sense online fall back to Main.
            if (net.State != NetSessionState.Offline && (page == Page.Play || page == Page.Browse)) page = Page.Main;
            if (net.State == NetSessionState.Offline && page == Page.Invite) page = Page.Main; // only inside a lobby

            GUILayout.BeginArea(new Rect(10f, 10f, PanelW, Screen.height / k - 20f), GUI.skin.box);
            GUILayout.Label("<size=26><b>OVERTIME ESCAPE</b></size>", Rich());
            if (!string.IsNullOrEmpty(net.Status)) GUILayout.Label(net.Status);
            GUILayout.Space(6f);

            if (page == Page.Customize) DrawCustomize(net);
            else if (page == Page.Settings) DrawSettings();
            else if (page == Page.HowTo) DrawHowTo();
            else if (page == Page.Invite) DrawInvite(net);
            else switch (net.State)
            {
                case NetSessionState.Offline:
                    if (page == Page.Play) DrawPlay(net);
                    else if (page == Page.Browse) DrawBrowse(net);
                    else DrawMain();
                    break;
                case NetSessionState.Connecting:
                    GUILayout.Label("Please wait...");
                    if (GUILayout.Button("Cancel", GUILayout.Height(30f))) net.Leave("Cancelled.");
                    break;
                default:
                    DrawLobby(net);
                    break;
            }
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        // ------------------------------------------------------------------ main

        void DrawMain()
        {
            var steam = SteamService.Instance;
            bool ready = steam != null && steam.Ready;
            GUILayout.Label(ready ? $"Steam: signed in as {steam.LocalName}" : "Steam: not available (" + (steam?.Error ?? "?") + ")");
            GUILayout.Space(14f);

            if (GUILayout.Button("PLAY", GUILayout.Height(52f))) page = Page.Play;
            GUILayout.Space(6f);
            if (GUILayout.Button("CUSTOMIZE", GUILayout.Height(52f))) OpenCustomize();
            GUILayout.Space(6f);
            if (GUILayout.Button("HOW TO PLAY", GUILayout.Height(52f))) { page = Page.HowTo; howToScroll = Vector2.zero; }
            GUILayout.Space(6f);
            if (GUILayout.Button("SETTINGS", GUILayout.Height(52f))) page = Page.Settings;
            GUILayout.Space(6f);
            if (GUILayout.Button("QUIT", GUILayout.Height(52f))) Quit();
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

        void DrawPlay(NetSession net)
        {
            var steam = net.Steam;
            bool ready = steam != null && steam.Ready;
            if (!ready)
            {
                GUILayout.Label("Steam is not available: " + (steam?.Error ?? "?"));
                if (GUILayout.Button("Retry Steam", GUILayout.Height(28f))) steam?.Retry();
            }

            GUI.enabled = ready;
            GUILayout.Label($"Host a lobby (up to {NetConst.MaxPlayers} players)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Public", GUILayout.Height(34f))) net.HostSteam(LobbyVisibility.Public);
            if (GUILayout.Button("Code only", GUILayout.Height(34f))) net.HostSteam(LobbyVisibility.CodeOnly);
            if (GUILayout.Button("Friends only", GUILayout.Height(34f))) net.HostSteam(LobbyVisibility.FriendsOnly);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Join");
            if (GUILayout.Button("Browse public lobbies", GUILayout.Height(34f))) { page = Page.Browse; Refresh(net); }
            GUILayout.BeginHorizontal();
            codeInput = GUILayout.TextField(codeInput, NetConst.CodeLength, GUILayout.Height(28f), GUILayout.Width(140f)).ToUpperInvariant();
            if (GUILayout.Button("Join by code", GUILayout.Height(28f))) net.JoinByCode(codeInput);
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            GUILayout.Space(10f);
            GUILayout.Label("Solo");
            if (GUILayout.Button("Play solo", GUILayout.Height(34f))) SceneManager.LoadScene(NetConst.RunScene);

            GUILayout.Space(8f);
            GUILayout.Label("Local test (no Steam)");
            if (GUILayout.Button("Host local", GUILayout.Height(28f))) net.HostLocal();
            GUILayout.BeginHorizontal();
            ipInput = GUILayout.TextField(ipInput, GUILayout.Width(150f), GUILayout.Height(28f));
            if (GUILayout.Button("Join local IP", GUILayout.Height(28f))) net.JoinLocal(ipInput);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("< Back", GUILayout.Height(30f))) page = Page.Main;
        }

        void DrawBrowse(NetSession net)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Back", GUILayout.Width(80f))) page = Page.Play;
            if (GUILayout.Button("Refresh", GUILayout.Width(80f))) Refresh(net);
            GUILayout.EndHorizontal();
            GUILayout.Label(searching ? "Searching..." : $"{listings.Count} open lobby(ies)");
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var l in listings)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{l.Name}   {l.Members}/{l.Max}" + (l.VersionOk ? "" : "   (other version)"));
                GUI.enabled = l.VersionOk && l.Members < l.Max;
                if (GUILayout.Button("Join", GUILayout.Width(60f))) { page = Page.Main; net.JoinSteam(l.Id); }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
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

        void DrawLobby(NetSession net)
        {
            if (net.UsingSteam && !string.IsNullOrEmpty(net.LobbyCode))
                GUILayout.Label($"<size=20>Lobby code:  <b>{net.LobbyCode}</b></size>", Rich());
            GUILayout.Label($"Players {net.Roster.Count}/{NetConst.MaxPlayers}");
            foreach (var r in net.Roster)
                GUILayout.Label($"   {(r.IsHost ? "[host] " : "")}{r.Name}{(r.IsLocal ? " (you)" : "")}" +
                                (net.UsingSteam && !r.IsHost ? (r.Ready ? "   - ready" : "   - not ready") : ""));

            GUILayout.Space(10f);
            if (net.UsingSteam && GUILayout.Button("Invite friends", GUILayout.Height(30f))) { page = Page.Invite; RefreshFriends(); }
            if (GUILayout.Button("Customize your character", GUILayout.Height(30f))) OpenCustomize();

            if (net.State == NetSessionState.StartingRun) { GUILayout.Label("Loading the run..."); return; }

            GUILayout.Space(10f);
            if (net.IsHost)
            {
                GUI.enabled = net.CanStart;
                if (GUILayout.Button(net.CanStart ? "START RUN" : "Waiting for everyone to be ready...", GUILayout.Height(42f)))
                    net.StartRun();
                GUI.enabled = true;
            }
            else if (net.UsingSteam)
            {
                bool want = GUILayout.Toggle(net.LocalReady, " Ready");
                if (want != net.LocalReady) net.SetReady(want);
                GUILayout.Label("Waiting for the host to start...");
            }
            else GUILayout.Label("Waiting for the host to start...");

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Leave lobby", GUILayout.Height(30f))) net.Leave();
        }

        // ------------------------------------------------------------------ customize

        void OpenCustomize()
        {
            edit = PlayerAppearance.LoadOrDefault();
            Color.RGBToHSV(edit.body, out hue, out sat, out val);
            page = Page.Customize;
            if (LobbyStage.Instance) LobbyStage.Instance.Customizing = true;
        }

        void DrawCustomize(NetSession net)
        {
            GUILayout.Label("<size=18><b>Customize</b></size>   (drag on the character to turn it)", Rich());
            bool changed = false;

            // Which character: the Party character (colour, face, hat) or one of the Tiny Toon people.
            var catalog = CharacterCatalog.Instance;
            var toons = catalog ? catalog.Players() : null;
            if (toons != null && toons.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Character");
                var names = new string[toons.Count + 1];
                names[0] = "Party";
                int current = 0;
                for (int i = 0; i < toons.Count; i++)
                {
                    names[i + 1] = toons[i].label;
                    if (edit.model == toons[i].id) current = i + 1;
                }
                // one list of everybody; with a big roster (the Citizens Megapack) the list scrolls instead of growing the page
                const int columns = 3;
                int rows = (names.Length + columns - 1) / columns;
                float gridHeight = 28f * rows, shown = Mathf.Min(gridHeight, 28f * 4f);
                charScroll = GUILayout.BeginScrollView(charScroll, GUILayout.Height(shown + (gridHeight > shown ? 4f : 0f)));
                int pick = GUILayout.SelectionGrid(current, names, columns, GUILayout.Height(gridHeight));
                GUILayout.EndScrollView();
                if (pick != current) { edit.model = pick == 0 ? null : toons[pick - 1].id; changed = true; }
            }

            if (!edit.IsToon) // colour, face and hat belong to the Party character
            {
            GUILayout.Space(6f);
            GUILayout.Label("Colour");
            for (int row = 0; row < Swatches.Length / 4; row++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 4; c++)
                {
                    var sw = Swatches[row * 4 + c];
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = sw;
                    if (GUILayout.Button("", GUILayout.Height(28f)))
                    {
                        edit.body = sw;
                        Color.RGBToHSV(sw, out hue, out sat, out val);
                        changed = true;
                    }
                    GUI.backgroundColor = old;
                }
                GUILayout.EndHorizontal();
            }
            float h = Slider("Hue", hue, 0f, 1f), sa = Slider("Saturation", sat, 0f, 1f), v = Slider("Brightness", val, 0.15f, 1f);
            if (h != hue || sa != sat || v != val)
            {
                hue = h; sat = sa; val = v;
                edit.body = Color.HSVToRGB(hue, sat, val);
                changed = true;
            }

            GUILayout.Space(6f);
            GUILayout.Label("Face");
            int face = GUILayout.SelectionGrid(edit.face - 1, PlayerAppearance.FaceLabels, 3, GUILayout.Height(28f)) + 1;
            if (face != edit.face) { edit.face = face; changed = true; }

            GUILayout.Label("Hat");
            int hat = GUILayout.SelectionGrid(edit.hat, PlayerAppearance.HatLabels, 4, GUILayout.Height(28f));
            if (hat != edit.hat) { edit.hat = hat; changed = true; }
            }

            GUILayout.Space(4f);
            float height = Slider("Height", edit.height, PlayerAppearance.MinBuild, PlayerAppearance.MaxBuild);
            float width = Slider("Width", edit.width, PlayerAppearance.MinBuild, PlayerAppearance.MaxBuild);
            if (height != edit.height || width != edit.width) { edit.height = height; edit.width = width; changed = true; }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Randomize", GUILayout.Height(30f)))
            {
                string keepModel = edit.model; // randomize the look, not which character it is
                edit = PlayerAppearance.Random(new System.Random());
                edit.model = keepModel;
                Color.RGBToHSV(edit.body, out hue, out sat, out val);
                changed = true;
            }
            if (GUILayout.Button("Reset", GUILayout.Height(30f)))
            {
                edit = PlayerAppearance.ForSlot(0);
                Color.RGBToHSV(edit.body, out hue, out sat, out val);
                changed = true;
            }
            GUILayout.EndHorizontal();

            if (changed && LobbyStage.Instance) LobbyStage.Instance.SetLocalAppearance(edit);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("DONE", GUILayout.Height(42f)))
            {
                edit.Save();
                net.SetLook(edit.Serialize()); // friends in the lobby see it
                if (LobbyStage.Instance) { LobbyStage.Instance.SetLocalAppearance(edit); LobbyStage.Instance.Customizing = false; }
                page = Page.Main;
            }
        }

        static float Slider(string label, float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(90f));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return v;
        }

        // ------------------------------------------------------------------ invite friends

        readonly List<FriendEntry> friends = new();
        readonly Dictionary<ulong, float> invitedAt = new();
        Vector2 friendsScroll;
        float nextFriendRefresh;
        Texture2D dotTexture;

        void RefreshFriends()
        {
            FriendsService.Refresh(friends);
            nextFriendRefresh = Time.unscaledTime + 2f;
        }

        Texture2D Dot()
        {
            if (dotTexture) return dotTexture;
            const int n = 16;
            dotTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - (n - 1) / 2f, dy = y - (n - 1) / 2f;
                    float a = Mathf.Clamp01((n / 2f - 0.5f) - Mathf.Sqrt(dx * dx + dy * dy));
                    dotTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            dotTexture.Apply();
            return dotTexture;
        }

        void DrawInvite(NetSession net)
        {
            GUILayout.Label("<size=18><b>Invite friends</b></size>", Rich());
            if (Time.unscaledTime >= nextFriendRefresh) RefreshFriends();

            int online = 0;
            foreach (var f in friends) if (f.Online) online++;
            GUILayout.Label(friends.Count == 0 ? "No friends found on Steam." : $"{online} of {friends.Count} friends online");

            friendsScroll = GUILayout.BeginScrollView(friendsScroll);
            foreach (var f in friends)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);

                // status dot: green when online, grey when not
                var r = GUILayoutUtility.GetRect(16f, 24f, GUILayout.Width(16f), GUILayout.Height(24f));
                var old = GUI.color;
                GUI.color = f.Online ? new Color(0.25f, 0.85f, 0.4f) : new Color(0.45f, 0.45f, 0.5f);
                GUI.DrawTexture(new Rect(r.x + 2f, r.y + 6f, 12f, 12f), Dot());
                GUI.color = old;

                GUILayout.Label(f.Name, f.Online ? GUI.skin.label : OfflineFriendStyle());
                GUILayout.Label(f.Status, FriendStatusStyle(), GUILayout.Width(110f));

                bool recent = invitedAt.TryGetValue(f.Id.m_SteamID, out float at) && Time.unscaledTime - at < 8f;
                GUI.enabled = f.Online && !recent;
                if (GUILayout.Button(recent ? "Invited" : "Invite", GUILayout.Width(74f)))
                {
                    if (net.InviteFriend(f.Id)) invitedAt[f.Id.m_SteamID] = Time.unscaledTime;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(30f))) RefreshFriends();
            if (GUILayout.Button("Steam invite dialog", GUILayout.Height(30f))) net.InviteFriends();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("< Back", GUILayout.Height(34f))) page = Page.Main;
        }

        // ------------------------------------------------------------------ how to play

        void DrawHowTo()
        {
            GUILayout.Label("<size=18><b>How to play</b></size>", Rich());
            howToScroll = GUILayout.BeginScrollView(howToScroll);

            foreach (var (title, body) in HowToPlayContent.Sections)
            {
                GUILayout.Label($"<b>{title}</b>", Rich());
                GUILayout.Label(body, Wrap());
                GUILayout.Space(8f);
            }

            GUILayout.Label("<b>Controls</b>", Rich());
            foreach (var (key, action) in HowToPlayContent.Controls)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{key}</b>", Rich(), GUILayout.Width(120f));
                GUILayout.Label(action, Wrap());
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            if (GUILayout.Button("< Back", GUILayout.Height(36f))) page = Page.Main;
        }

        // ------------------------------------------------------------------ settings

        void DrawSettings()
        {
            GUILayout.Label("<size=18><b>Settings</b></size>", Rich());
            GUILayout.Space(8f);

            float vol = Slider($"Volume {Mathf.RoundToInt(AudioListener.volume * 100f)}%", AudioListener.volume, 0f, 1f);
            if (!Mathf.Approximately(vol, AudioListener.volume))
            {
                AudioListener.volume = vol;
                PlayerPrefs.SetFloat(VolumeKey, vol);
            }

            float sens = PlayerPrefs.GetFloat(SensitivityKey, 2.4f);
            float ns = Slider($"Mouse {sens:0.0}", sens, 0.5f, 6f);
            if (!Mathf.Approximately(ns, sens)) PlayerPrefs.SetFloat(SensitivityKey, ns);

            GUILayout.Space(8f);
            GUILayout.Label("Voice chat");
            int vm = GUILayout.SelectionGrid((int)VoiceChat.CurrentMode, new[] { "Open mic", "Push to talk (T)", "Off" }, 3, GUILayout.Height(28f));
            if (vm != (int)VoiceChat.CurrentMode) VoiceChat.CurrentMode = (VoiceChat.Mode)vm;
            float vv = Slider($"Voices {Mathf.RoundToInt(VoiceChat.Volume * 100f)}%", VoiceChat.Volume, 0f, 1f);
            if (!Mathf.Approximately(vv, VoiceChat.Volume)) VoiceChat.Volume = vv;

            GUILayout.Space(6f);
            bool fs = GUILayout.Toggle(Screen.fullScreen, " Fullscreen");
            if (fs != Screen.fullScreen) Screen.fullScreen = fs;

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("< Back", GUILayout.Height(36f)))
            {
                PlayerPrefs.Save();
                page = Page.Main;
            }
        }

        // The styles are made once (a GUIStyle is a native object: making them on every GUI event is a steady stream of garbage).
        static GUIStyle rich, wrap, offlineFriend, friendStatus;
        static GUIStyle Rich() => rich ??= new GUIStyle(GUI.skin.label) { richText = true };
        static GUIStyle Wrap() => wrap ??= new GUIStyle(GUI.skin.label) { wordWrap = true };

        static GUIStyle OfflineFriendStyle()
        {
            if (offlineFriend == null)
            {
                offlineFriend = new GUIStyle(GUI.skin.label);
                offlineFriend.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
            }
            return offlineFriend;
        }

        static GUIStyle FriendStatusStyle()
        {
            if (friendStatus == null)
            {
                friendStatus = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                friendStatus.normal.textColor = new Color(0.65f, 0.7f, 0.8f);
            }
            return friendStatus;
        }
    }
}

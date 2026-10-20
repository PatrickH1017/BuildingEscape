using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BuildingEscape.Net;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pictures of the menus (real screen captures of the Game view, so this needs a real, non-batch Editor window: IMGUI is not drawn in
    /// batch mode; run it on the hidden desktop, see DEVELOPER_NOTES 8.6). <c>Menus</c> opens the Esc menu and the Keybinds screen in a run;
    /// <c>MenusLobby</c> (Boot, then the Menu scene) walks the lobby menu's pages. Pictures: %TEMP%\be_menu_*.png.
    /// Run: <c>Unity.exe -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.NetDevHarness.Menus</c> (no -batchmode); the same with MenusLobby.
    /// </summary>
    public partial class NetDevRunner
    {
        int menuFails;

        void MenuCheck(string name, bool ok, string detail = "")
        {
            if (!ok) menuFails++;
            Log($"[Menu] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator SnapMenu(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_menu_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Log($"[Menu] saved {path} ({tex.width}x{tex.height}, screen {Screen.width}x{Screen.height})");
            Object.Destroy(tex);
        }

        static void SetStaticProp(System.Type type, string prop, object value)
        {
            var p = type.GetProperty(prop, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var set = p != null ? p.GetSetMethod(true) : null;
            if (set != null) set.Invoke(null, new[] { value });
            else Log($"[Menu] cannot set {type.Name}.{prop}");
        }

        IEnumerator MenusScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            Log($"[Menu] screen {Screen.width}x{Screen.height}");
            yield return new WaitForSeconds(1.0f);

            // ---- the Esc menu
            var esc = FindFirstObjectByType<EscapeMenu>();
            MenuCheck("the Esc menu exists", esc != null);
            if (esc == null) { Log($"[Menu] DONE fails={menuFails}"); Quit(1); yield break; }
            SetStaticProp(typeof(EscapeMenu), "Open", true);
            yield return new WaitForSeconds(0.9f);
            MenuCheck("the Esc menu is open", EscapeMenu.Open);
            yield return SnapMenu("esc");

            // ---- the Keybinds screen (from the Esc menu's button)
            typeof(EscapeMenu).GetField("controlsOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(esc, true);
            yield return new WaitForSeconds(0.7f);
            yield return SnapMenu("keybinds");

            // ---- and closed again
            typeof(EscapeMenu).GetField("controlsOpen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(esc, false);
            SetStaticProp(typeof(EscapeMenu), "Open", false);
            yield return new WaitForSeconds(0.3f);
            MenuCheck("the menu is closed again", !EscapeMenu.Open);

            Log($"[Menu] DONE fails={menuFails}");
            Quit(menuFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ the lobby menu, page by page

        static void SetMember(object target, string name, object value)
        {
            var t = target.GetType();
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) { f.SetValue(target, value); return; }
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) p.GetSetMethod(true).Invoke(target, new[] { value });
        }

        IEnumerator MenusLobbyScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            yield return WaitFor(() => NetSession.Instance != null && FindFirstObjectByType<LobbyMenu>() != null, 40, "the lobby menu");
            var menu = FindFirstObjectByType<LobbyMenu>();
            var net = NetSession.Instance;
            const BindingFlags inst = BindingFlags.NonPublic | BindingFlags.Instance;
            var lt = typeof(LobbyMenu);
            var pageField = lt.GetField("page", inst);
            void Go(string page) => pageField.SetValue(menu, System.Enum.Parse(pageField.FieldType, page));
            var scrollObj = lt.GetField("scroll", inst).GetValue(menu);
            void ScrollTo(float y) => scrollObj.GetType().GetField("pos").SetValue(scrollObj, y);

            yield return new WaitForSeconds(2.0f);
            Log($"[Menu] screen {Screen.width}x{Screen.height}");
            MenuCheck("the lobby menu is on the Main page offline", net.State == NetSessionState.Offline && pageField.GetValue(menu).ToString() == "Main");
            MenuCheck("the lobby has its loading dock behind the characters", FindFirstObjectByType<BuildingEscape.Net.LobbyDockLife>() != null);
            MenuCheck("the lobby song is loaded and plays", MusicDirector.Instance != null && MusicDirector.Instance.HasLobbySong && MusicDirector.Instance.LobbyPlaying);
            yield return SnapMenu("lobby_main");

            Go("Play"); yield return new WaitForSeconds(0.8f); yield return SnapMenu("lobby_play");

            // a few made-up lobbies for the browser
            var list = (List<LobbyListing>)lt.GetField("listings", inst).GetValue(menu);
            list.Clear();
            list.Add(new LobbyListing { Name = "Pat's lobby", Members = 2, Max = 5, VersionOk = true });
            list.Add(new LobbyListing { Name = "Heist Night", Members = 5, Max = 5, VersionOk = true });
            list.Add(new LobbyListing { Name = "A very long lobby name that will not fit on one line", Members = 1, Max = 5, VersionOk = false });
            list.Add(new LobbyListing { Name = "Overtime Crew", Members = 3, Max = 5, VersionOk = true });
            Go("Browse"); yield return new WaitForSeconds(0.8f); yield return SnapMenu("lobby_browse");
            list.Clear();

            lt.GetMethod("OpenCustomize", inst).Invoke(menu, null);
            yield return new WaitForSeconds(1.0f);
            yield return SnapMenu("lobby_customize");
            ScrollTo(100000f); yield return new WaitForSeconds(0.3f);
            yield return SnapMenu("lobby_customize_end");
            ScrollTo(0f);
            // the first Tiny Toon person instead of the Party character
            var cat = CharacterCatalog.Instance;
            var toons = cat ? cat.Players() : null;
            if (toons != null && toons.Count > 0)
            {
                var edit = lt.GetField("edit", inst).GetValue(menu);
                SetMember(edit, "model", toons[0].id);
                lt.GetField("edit", inst).SetValue(menu, edit);
                yield return new WaitForSeconds(0.6f);
                yield return SnapMenu("lobby_customize_toon");
            }
            // (leave without saving: the picture run must not change the saved look)
            if (LobbyStage.Instance) LobbyStage.Instance.Customizing = false;

            Go("Settings"); yield return new WaitForSeconds(0.8f); yield return SnapMenu("lobby_settings");
            Go("HowTo"); yield return new WaitForSeconds(0.8f); yield return SnapMenu("lobby_howto");
            ScrollTo(900f); yield return new WaitForSeconds(0.3f); yield return SnapMenu("lobby_howto_mid");
            ScrollTo(100000f); yield return new WaitForSeconds(0.3f); yield return SnapMenu("lobby_howto_end");
            ScrollTo(0f);

            // ---- a lobby of our own (local test mode: no Steam needed), then the friends page over it
            Go("Main");
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "a local lobby");
            yield return new WaitForSeconds(1.0f);
            yield return SnapMenu("lobby_lobby");

            var friends = (List<FriendEntry>)lt.GetField("friends", inst).GetValue(menu);
            var fieldNext = lt.GetField("nextFriendRefresh", inst);
            friends.Clear();
            friends.Add(new FriendEntry { Name = "Alex", Online = true, InThisGame = true, Status = "playing this game" });
            friends.Add(new FriendEntry { Name = "Sam the Safecracker", Online = true, Status = "online" });
            friends.Add(new FriendEntry { Name = "Robin", Online = true, Status = "away" });
            friends.Add(new FriendEntry { Name = "Kim", Online = false, Status = "offline" });
            Go("Invite");
            fieldNext.SetValue(menu, Time.unscaledTime + 1000f);
            yield return new WaitForSeconds(0.8f);
            fieldNext.SetValue(menu, Time.unscaledTime + 1000f);
            yield return SnapMenu("lobby_invite");
            friends.Clear();

            // ---- the message line under the title
            SetMember(net, "Status", "Could not reach the host (timed out). Check the code and try again, or ask your friend to host again.");
            Go("Main");
            yield return new WaitForSeconds(0.6f);
            yield return SnapMenu("lobby_status");
            SetMember(net, "Status", "");
            net.Leave();
            yield return WaitFor(() => net.State == NetSessionState.Offline, 20, "back offline");

            // ---- a small window (the card must still fit or scroll) and a big one
            foreach (var (w, h) in new[] { (1280, 720), (960, 540), (3840, 2160) })
            {
                if (!SetGameViewSize(w, h)) { Log("[Menu] SKIP other window sizes: the Game view cannot be resized from here"); break; }
                yield return new WaitForSeconds(0.8f);
                Log($"[Menu] screen now {Screen.width}x{Screen.height}");
                Go("Main"); yield return new WaitForSeconds(0.5f); yield return SnapMenu($"lobby_main_{w}");
                lt.GetMethod("OpenCustomize", inst).Invoke(menu, null); yield return new WaitForSeconds(0.8f); yield return SnapMenu($"lobby_customize_{w}");
                if (LobbyStage.Instance) LobbyStage.Instance.Customizing = false;
                Go("Main");
            }
            ResetGameViewSize();

            Log($"[Menu] DONE fails={menuFails}");
            Quit(menuFails == 0 ? 0 : 1);
        }

        /// <summary>Back to "Free Aspect": a custom size set by an earlier run stays in the Game view's saved sizes and would otherwise carry over to every later picture.</summary>
        static void ResetGameViewSize()
        {
            var gvt = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            var p = gvt != null ? gvt.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) : null;
            if (p != null) p.SetValue(EditorWindow.GetWindow(gvt), 0);
        }

        static bool SetGameViewSize(int w, int h)
        {
            var gvt = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            var m = gvt != null ? gvt.GetMethod("SetCustomResolution", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            if (m == null) return false;
            m.Invoke(EditorWindow.GetWindow(gvt), new object[] { new Vector2(w, h), "menu test" });
            return true;
        }

        /// <summary>The Esc menu in a networked run (it has the LEAVE RUN button): host a local lobby alone, start the run, open the menu.</summary>
        IEnumerator MenusRunScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;
            yield return WaitFor(() => NetSession.Instance != null, 40, "the net session");
            var net = NetSession.Instance;
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "a local lobby");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "the run");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1.5f);
            MenuCheck("the run is networked", NetSession.Online);
            SetStaticProp(typeof(EscapeMenu), "Open", true);
            yield return new WaitForSeconds(0.9f);
            yield return SnapMenu("esc_online");
            SetStaticProp(typeof(EscapeMenu), "Open", false);
            Log($"[Menu] DONE fails={menuFails}");
            Quit(menuFails == 0 ? 0 : 1);
        }
    }
}

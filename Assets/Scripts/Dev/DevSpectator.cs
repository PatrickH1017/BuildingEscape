#if ETR_DEV_TOOLS || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What a developer sees and can do on the machine that watches a match (the owner, 2026-10-23: "a developer joins somebody else's match as an invisible spectator to watch how it goes", and the dev menu works on that match). The machine has no player: the camera follows
    /// one of the players (<see cref="CameraFollow.spectate"/>; A / D or the mouse buttons step to the next / the one before, as for a busted player who watches, <see cref="Spectator"/>), a small chip says who it is, and F1 opens the spectator's dev menu once the terminal's
    /// code was typed (<see cref="DevAccess"/>). The menu's commands are sent to the host (<c>RunSync.RequestDevCommand</c>), which runs them only for a developer it let in as a spectator. Nothing here is ever shown to, or heard by, the party.
    /// Compiled into the Editor and the friends' builds only (see the stub at the bottom of this file).
    /// </summary>
    public sealed class DevSpectator : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DevSpectator>()) return;
            var go = new GameObject("DevSpectator");
            DontDestroyOnLoad(go);
            go.AddComponent<DevSpectator>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; Watching = null; WatchingNumber = 0; MenuOpen = false; TestStep = 0; TestMenuToggle = false; LastCommand = ""; FreeCam = false; TestFreeToggle = false; TestMove = Vector3.zero; }

        /// <summary>Is the camera on a player right now (this machine is a spectator in a run)?</summary>
        public static bool Active { get; private set; }
        /// <summary>The player being watched, and the number shown for him ("PLAYER n", the same as on the Nav map).</summary>
        public static WobblyPlayer Watching { get; private set; }
        public static int WatchingNumber { get; private set; }
        /// <summary>The spectator's dev menu is up (the camera frees the cursor: <see cref="TestMode.PanelOpen"/>).</summary>
        public static bool MenuOpen { get; private set; }
        /// <summary>The last command the menu sent (the tests read it).</summary>
        public static string LastCommand { get; private set; } = "";

        /// <summary>Tests: +1 / -1 steps to the next / the previous player, as D / A do (taken on the next frame); the menu opens or closes on the next frame, as F1 does.</summary>
        public static int TestStep;
        public static bool TestMenuToggle;
        /// <summary>The free camera is on (F): the camera has left the player it followed and flies where the keys (W A S D, E up, Q down, Shift fast, Ctrl slow) and the mouse say.</summary>
        public static bool FreeCam { get; private set; }
        /// <summary>Tests: F is pressed on the next frame; the free camera's keys held as (right, up, forward).</summary>
        public static bool TestFreeToggle;
        public static Vector3 TestMove;

        CameraFollow rig;
        float nextLookup;
        int index;
        Vector2 scroll;
        Vector3 freePos;
        float savedMinPitch, savedMaxPitch;
        readonly List<WobblyPlayer> watchable = new List<WobblyPlayer>();

        static int SlotOf(WobblyPlayer p)
        {
            if (p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned) return Mathf.Max(0, np.Slot);
            return WobblyPlayerRegistry.All.IndexOf(p);
        }

        static readonly System.Comparison<WobblyPlayer> BySlot = (a, b) => SlotOf(a).CompareTo(SlotOf(b));

        static bool Spectating
        {
            get { var net = Net.NetSession.Instance; return net != null && net.IsSpectator && Net.NetSession.Online; }
        }

        void Update()
        {
            if (!Spectating) { Release(); rig = null; MenuOpen = false; return; }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (TestMenuToggle) { TestMenuToggle = false; if (DevAccess.Unlocked) MenuOpen = !MenuOpen; }
            else if (DevTerminalRules.MenuKeyOpens(Input.GetKeyDown(KeyCode.F1), ctrl, DevAccess.Unlocked, DevTerminal.Open)) MenuOpen = !MenuOpen;
            if (MenuOpen && !DevAccess.Unlocked) MenuOpen = false;               // (locked again: the menu goes)

            if (Net.RunSync.Instance == null) { Release(); return; }               // (the lobby: nothing to watch yet)
            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }
            if (!rig) return;

            bool keys = !EscapeMenu.Open && !DevTerminal.Open && !MenuOpen;
            if (keys && Input.GetKeyDown(KeyCode.O)) DevSpectatorView.OverlayOn = !DevSpectatorView.OverlayOn;
            if (keys && Input.GetKeyDown(KeyCode.N)) DevSpectatorView.NetPanelOn = !DevSpectatorView.NetPanelOn;
            if (TestFreeToggle || (keys && Input.GetKeyDown(KeyCode.F))) SetFree(!FreeCam);
            TestFreeToggle = false;
            if (FreeCam) { Active = false; Watching = null; WatchingNumber = 0; if (rig.spectate) rig.spectate = null; return; }          // (the free camera: no player is followed, LateUpdate flies it)

            watchable.Clear();
            foreach (var p in WobblyPlayerRegistry.All) if (p) watchable.Add(p);
            if (watchable.Count == 0) { Release(); return; }
            watchable.Sort(BySlot);

            int at = Watching ? watchable.IndexOf(Watching) : -1;                   // (stay with the one watched while he is there)
            if (at < 0) at = Mathf.Min(index, watchable.Count - 1);
            int dir = TestStep; TestStep = 0;
            if (!EscapeMenu.Open && !DevTerminal.Open && !MenuOpen)
            {
                if (Input.GetKeyDown(KeyCode.D) || Input.GetMouseButtonDown(0)) dir = 1;
                if (Input.GetKeyDown(KeyCode.A) || Input.GetMouseButtonDown(1)) dir = -1;
            }
            if (dir != 0) at = SpectateRules.Step(at, watchable.Count, dir);
            index = at;
            Watching = watchable[at];
            WatchingNumber = SlotOf(Watching) + 1;
            Active = true;
            rig.spectate = Watching.transform;
        }

        /// <summary>F: the camera leaves the player (and flies from where it was), or goes back to the one it followed. While it flies the view may tilt almost straight up and down.</summary>
        void SetFree(bool on)
        {
            if (on == FreeCam) return;
            if (on)
            {
                if (!rig) return;
                FreeCam = true;
                freePos = rig.transform.position;
                savedMinPitch = rig.minPitch; savedMaxPitch = rig.maxPitch;
                rig.minPitch = DevOverlayRules.FreeMinPitch; rig.maxPitch = DevOverlayRules.FreeMaxPitch;
                rig.spectate = null;
            }
            else
            {
                FreeCam = false;
                if (rig) { rig.minPitch = savedMinPitch; rig.maxPitch = savedMaxPitch; }
            }
        }

        void LateUpdate()
        {
            if (!FreeCam || !rig || !Spectating) return;
            Vector3 move = TestMove;
            bool fast = false, slow = false;
            if (move == Vector3.zero && Application.isFocused && !MenuOpen && !DevTerminal.Open && !EscapeMenu.Open)
            {
                if (Input.GetKey(KeyCode.W)) move.z += 1f;
                if (Input.GetKey(KeyCode.S)) move.z -= 1f;
                if (Input.GetKey(KeyCode.D)) move.x += 1f;
                if (Input.GetKey(KeyCode.A)) move.x -= 1f;
                if (Input.GetKey(KeyCode.E)) move.y += 1f;
                if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
                fast = Input.GetKey(KeyCode.LeftShift); slow = Input.GetKey(KeyCode.LeftControl);
            }
            var rot = Quaternion.Euler(rig.AimPitch, rig.AimYaw, 0f);
            move = Vector3.ClampMagnitude(move, 1f);
            freePos += (rot * new Vector3(move.x, 0f, move.z) + Vector3.up * move.y) * DevOverlayRules.FreeStep(fast, slow, Time.unscaledDeltaTime);
            rig.transform.SetPositionAndRotation(freePos, rot);
        }

        void Release()
        {
            if (FreeCam) SetFree(false);
            if (rig && rig.spectate) rig.spectate = null;
            Active = false; Watching = null; WatchingNumber = 0; index = 0;
        }

        void OnDisable() { Release(); MenuOpen = false; }

        // ------------------------------------------------------------------ the screen

        void OnGUI()
        {
            if (!Spectating) return;
            if (Event.current.type == EventType.Repaint && !IntroCinematic.Playing)
            {
                if (Net.RunSync.Instance != null && DevSpectatorView.OverlayOn) DevSpectatorView.DrawOverlay(Camera.main);
                if (DevSpectatorView.NetPanelOn) DevSpectatorView.DrawNetPanel(WobblyPlayerRegistry.All.Count);
                DrawChip();
            }
            if (MenuOpen && DevAccess.Unlocked) DrawMenu();
        }

        void DrawChip()
        {
            float s = HudTheme.S;
            string text = FreeCam ? "DEV SPECTATOR   FREE CAMERA" : Active ? "DEV SPECTATOR   PLAYER " + WatchingNumber : "DEV SPECTATOR   waiting for the run";
            float w = Mathf.Max(HudTheme.Width(text, 22f) + 56f * s, 360f * s), h = 42f * s;
            var r = new Rect((Screen.width - w) * 0.5f, 92f * s, w, h);
            HudTheme.Chip(r, text, FreeCam ? HudTheme.Info : Active ? HudTheme.PlayerColour(WatchingNumber) : HudTheme.Dim, 22f, HudTheme.Ink);
            string hint = (FreeCam ? "W A S D fly, E up, Q down, Shift fast, Ctrl slow.   " : Active ? "A / D or the mouse buttons: another player.   " : "")
                          + "F: free camera.   O: overlay.   N: network.   " + (DevAccess.Unlocked ? "F1: dev menu." : "CTRL + F1: terminal.");
            float hw = Mathf.Max(r.width, HudTheme.Width(hint, 15f) + 24f * s);
            HudTheme.Text(new Rect((Screen.width - hw) * 0.5f, r.yMax + 4f * s, hw, 24f * s), hint, 15f, HudTheme.Muted, TextAnchor.MiddleCenter);
        }

        static void Send(DevCommand cmd, int arg)
        {
            var sync = Net.RunSync.Instance;
            if (sync == null) return;
            sync.RequestDevCommand(cmd, arg);
            LastCommand = DevCommandRules.Label(cmd, arg);
        }

        void DrawMenu()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            float w = Mathf.Min(Screen.width - 24f, 560f);
            var r = new Rect(12, 60, w, Mathf.Max(200f, Mathf.Min(Screen.height - 140f, 640f)));
            GUI.Box(r, "DEV SPECTATOR MENU   (the host runs these)");
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 26, w - 20, r.height - 34));
            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            GUILayout.Label($"Cash: ${(hm ? hm.Cash : 0)}      Floor: {(hm ? hm.FloorLabel : "-")}");
            GUILayout.BeginHorizontal();
            foreach (int amount in DevCommandRules.CashAmounts) if (GUILayout.Button(DevCommandRules.Label(DevCommand.Cash, amount))) Send(DevCommand.Cash, amount);
            GUILayout.EndHorizontal();

            GUILayout.Label("Go to level");
            if (tm)
            {
                int rows = Mathf.CeilToInt(tm.totalFloors / 4f);
                for (int row = 0; row < rows; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 4; c++)
                    {
                        int lvl = row * 4 + c;
                        if (lvl >= tm.totalFloors) break;
                        if (GUILayout.Button(DevCommandRules.Label(DevCommand.GoToLevel, lvl))) Send(DevCommand.GoToLevel, lvl);
                    }
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Label("Floor tools");
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.CompleteQuota, 0))) Send(DevCommand.CompleteQuota, 0);
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.StunGuards, 0))) Send(DevCommand.StunGuards, 0);
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.GodMode, 0))) Send(DevCommand.GodMode, 0);
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.FreeTeam, 0))) Send(DevCommand.FreeTeam, 0);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.CallBackup, 0))) Send(DevCommand.CallBackup, 0);
            if (GUILayout.Button(DevCommandRules.Label(DevCommand.CancelBackup, 0))) Send(DevCommand.CancelBackup, 0);
            GUILayout.EndHorizontal();

            if (LastCommand.Length > 0) GUILayout.Label("Sent: " + LastCommand);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
#else
namespace BuildingEscape
{
    /// <summary>A release build has no spectator tools (see the real class: <c>DevSpectator.cs</c> under <c>ETR_DEV_TOOLS</c>): the places that ask whether its menu is up need no switch of their own.</summary>
    public static class DevSpectator
    {
        public static bool Active => false;
        public static bool MenuOpen => false;
    }
}
#endif

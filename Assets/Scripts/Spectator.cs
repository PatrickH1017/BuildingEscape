using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>The pure rules of spectating (the owner, 2026-10-18).</summary>
    public static class SpectateRules
    {
        /// <summary>Can the local player watch this one: somebody else, and still free (neither pinned nor busted)?</summary>
        public static bool Watchable(bool isLocal, CustodyState custody) => !isLocal && custody == CustodyState.Free;

        /// <summary>Does the local player spectate now: busted (knocked out for the floor) while somebody else is still free?</summary>
        public static bool Spectating(CustodyState mine, int watchable) => mine == CustodyState.Busted && watchable > 0;

        /// <summary>The index one step <paramref name="direction"/> (+1 next, -1 before) from <paramref name="current"/> in a ring of <paramref name="count"/>; 0 when there is nobody.</summary>
        public static int Step(int current, int count, int direction)
        {
            if (count <= 0) return 0;
            int i = (current + (direction >= 0 ? 1 : -1)) % count;
            return i < 0 ? i + count : i;
        }
    }

    /// <summary>
    /// A player who is knocked out for the floor (busted) while teammates are still going can watch them (the owner, 2026-10-18: "the player that is knocked out should be able to spectate them and go back and
    /// forth between the players with the mouse left and right click or A and D"). Left click or D: the next teammate, right click or A: the one before. Only the CAMERA moves (<see cref="CameraFollow.spectate"/>):
    /// the pack, the keys, the HUD and the voice stay with the spectator's own body, so nothing the spectator presses reaches the teammate. When every teammate is down too, or the floor ends and the player
    /// is free again, the camera goes back to the player's own body. Created automatically; purely local.
    /// </summary>
    public sealed class Spectator : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<Spectator>()) return;
            var go = new GameObject("Spectator");
            DontDestroyOnLoad(go);
            go.AddComponent<Spectator>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = false; Watching = null; WatchingNumber = 0; TestStep = 0; }

        /// <summary>Is the camera on a teammate right now?</summary>
        public static bool Active { get; private set; }
        /// <summary>The teammate being watched, and the number shown for him ("PLAYER n", the same as on the Nav map).</summary>
        public static WobblyPlayer Watching { get; private set; }
        public static int WatchingNumber { get; private set; }

        /// <summary>A test hook: +1 / -1 steps to the next / the previous teammate, as the keys do (it is taken on the next frame).</summary>
        public static int TestStep;

        CameraFollow rig;
        float nextLookup;
        int index;
        readonly List<WobblyPlayer> watchable = new List<WobblyPlayer>();

        static int SlotOf(WobblyPlayer p)
        {
            if (p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned) return Mathf.Max(0, np.Slot);
            return WobblyPlayerRegistry.All.IndexOf(p);
        }

        static readonly System.Comparison<WobblyPlayer> BySlot = (a, b) => SlotOf(a).CompareTo(SlotOf(b));

        static bool KeysFree => !EscapeMenu.Open && !Backpack.UiOpen && !Vendor.ShopOpen && !TestMode.PanelOpen && !ElevatorPanel.Open && !OutpostUi.Open;

        void Update()
        {
            if (Net.NetSession.Instance != null && Net.NetSession.Instance.IsSpectator) return;           // (a developer who watches has the camera: DevSpectator)
            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }
            if (!rig || !rig.target) { Release(); return; }
            var me = rig.target.GetComponent<WobblyPlayer>();                 // (the body the camera belongs to: this machine's own player)
            if (!me) { Release(); return; }

            watchable.Clear();
            foreach (var p in WobblyPlayerRegistry.All) if (p && SpectateRules.Watchable(p == me, p.Custody)) watchable.Add(p);
            if (!SpectateRules.Spectating(me.Custody, watchable.Count)) { Release(); return; }
            watchable.Sort(BySlot);

            int at = Watching ? watchable.IndexOf(Watching) : -1;              // (stay with the one watched while he can be)
            if (at < 0) at = Mathf.Min(index, watchable.Count - 1);
            int dir = TestStep; TestStep = 0;
            if (KeysFree)
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

        void Release()
        {
            if (rig && rig.spectate) rig.spectate = null;
            Active = false; Watching = null; WatchingNumber = 0; index = 0;
        }

        void OnDisable() { Release(); }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Active || IntroCinematic.Playing) return;
            float s = HudTheme.S;
            string text = "SPECTATING  PLAYER " + WatchingNumber;
            float w = Mathf.Max(HudTheme.Width(text, 22f) + 56f * s, 320f * s), h = 42f * s;
            var r = new Rect((Screen.width - w) * 0.5f, 92f * s, w, h);
            HudTheme.Chip(r, text, HudTheme.PlayerColour(WatchingNumber), 22f, HudTheme.Ink);
            HudTheme.Text(new Rect(r.x, r.yMax + 4f * s, r.width, 24f * s), "A / D or the mouse buttons: another player", 15f, HudTheme.Muted, TextAnchor.MiddleCenter);
        }
    }
}

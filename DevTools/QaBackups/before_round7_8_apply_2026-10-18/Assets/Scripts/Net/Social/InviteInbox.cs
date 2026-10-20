using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Party invites. When a friend invites you, a card appears at the bottom of the screen with Join party and
    /// Decline; a bar under it counts the 10 seconds down, and the invite expires when it runs out.
    /// Created automatically; works in any scene except during a run.
    /// </summary>
    public class InviteInbox : MonoBehaviour
    {
        public const float Seconds = 10f;

        public static InviteInbox Instance { get; private set; }

        struct Invite
        {
            public CSteamID lobby;
            public string from;
            public float until;
        }

        readonly Queue<Invite> waiting = new();
        Invite current;
        bool showing;
        Callback<LobbyInvite_t> onInvite;
        CSteamID joinAfterLeaving;

        // for tests
        public int Received, Accepted, Declined, Expired;
        public bool Showing => showing;
        public float TimeLeft => showing ? Mathf.Max(0f, current.until - Time.unscaledTime) : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<InviteInbox>()) return;
            var go = new GameObject("InviteInbox");
            DontDestroyOnLoad(go);
            go.AddComponent<InviteInbox>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            onInvite?.Dispose();
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // Steam only exists a moment after launch: hook the callback once it is up
            if (onInvite == null && SteamService.Instance != null && SteamService.Instance.Ready)
                onInvite = Callback<LobbyInvite_t>.Create(OnSteamInvite);

            // an accepted invite that had to leave the current lobby first
            var net = NetSession.Instance;
            if (joinAfterLeaving.IsValid() && net != null && net.State == NetSessionState.Offline)
            {
                var lobby = joinAfterLeaving;
                joinAfterLeaving = CSteamID.Nil;
                net.JoinSteam(lobby);
            }

            if (showing && Time.unscaledTime >= current.until)
            {
                showing = false;
                Expired++;
            }
            if (!showing && waiting.Count > 0)
            {
                current = waiting.Dequeue();
                current.until = Time.unscaledTime + Seconds;
                showing = true;
            }
        }

        void OnSteamInvite(LobbyInvite_t d)
        {
            if (d.m_ulGameID != SteamUtils.GetAppID().m_AppId) return; // an invite to some other game
            string name = SteamFriends.GetFriendPersonaName(new CSteamID(d.m_ulSteamIDUser));
            Enqueue(new CSteamID(d.m_ulSteamIDLobby), name);
        }

        /// <summary>Adds an invite to the queue (also used by tests).</summary>
        public void Enqueue(CSteamID lobby, string from)
        {
            var net = NetSession.Instance;
            // no point interrupting a run that has started
            if (net != null && (net.State == NetSessionState.InRun || net.State == NetSessionState.StartingRun)) return;
            if (net != null && net.CurrentLobby == lobby) return;                       // already in that party
            if (showing && current.lobby == lobby) return;                              // already showing it
            foreach (var w in waiting) if (w.lobby == lobby) return;                    // or waiting for it
            Received++;
            waiting.Enqueue(new Invite { lobby = lobby, from = string.IsNullOrEmpty(from) ? "A friend" : from });
        }

        public void Accept()
        {
            if (!showing) return;
            showing = false;
            Accepted++;
            var net = NetSession.Instance;
            if (net == null) return;
            if (net.State == NetSessionState.Offline) net.JoinSteam(current.lobby);
            else
            {
                joinAfterLeaving = current.lobby;   // leave the party you are in, then join theirs
                net.Leave("Joining your friend's party...");
            }
        }

        public void Decline()
        {
            if (!showing) return;
            showing = false;
            Declined++;
        }

        void OnGUI()
        {
            if (!showing) return;
            MenuKit.Begin();
            float s = HudTheme.S;
            float w = 500f * s, h = 100f * s;
            var card = new Rect((Screen.width - w) / 2f, Screen.height - h - 36f * s, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                HudTheme.Panel(card, 14f);
                // the countdown bar along the bottom of the card, running down from full
                float left = Mathf.Clamp01((current.until - Time.unscaledTime) / Seconds);
                HudTheme.Bar(new Rect(card.x + 16f * s, card.yMax - 18f * s, w - 32f * s, 8f * s), left, left > 0.3f ? HudTheme.Good : HudTheme.Warning, 2f);
                HudTheme.Text(new Rect(card.x + 18f * s, card.y + 12f * s, w - 36f * s, 28f * s), $"{current.from} invited you to their party", 19f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                HudTheme.Text(new Rect(card.x + 18f * s, card.y + 40f * s, 210f * s, 22f * s), $"expires in {Mathf.CeilToInt(TimeLeft)}s", 14f, HudTheme.Muted, TextAnchor.MiddleLeft, false);
            }
            if (MenuKit.Button(new Rect(card.xMax - 286f * s, card.y + 42f * s, 150f * s, 36f * s), "JOIN PARTY", HudTheme.Cash, 17f)) Accept();
            if (MenuKit.Button(new Rect(card.xMax - 130f * s, card.y + 42f * s, 114f * s, 36f * s), "DECLINE", HudTheme.Danger, 17f)) Decline();
        }
    }
}
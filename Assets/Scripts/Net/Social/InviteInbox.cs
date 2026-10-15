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

        static readonly Color SubColour = new Color(0.7f, 0.74f, 0.82f);

        void OnGUI()
        {
            if (!showing) return;
            float k = Mathf.Max(0.75f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;                 // GUI state is shared by every OnGUI of the frame: put it back when done
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float sw = Screen.width / k, sh = Screen.height / k;

            const float w = 460f, h = 84f, bar = 6f;
            var card = new Rect((sw - w) / 2f, sh - h - 30f, w, h);
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);

            // the countdown bar along the bottom of the card, running down from full
            float left = Mathf.Clamp01((current.until - Time.unscaledTime) / Seconds);
            GUI.color = new Color(0.25f, 0.28f, 0.34f, 1f);
            GUI.DrawTexture(new Rect(card.x, card.yMax - bar, w, bar), Texture2D.whiteTexture);
            GUI.color = left > 0.3f ? new Color(0.3f, 0.85f, 0.45f) : new Color(0.95f, 0.5f, 0.25f);
            GUI.DrawTexture(new Rect(card.x, card.yMax - bar, w * left, bar), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(card.x + 14f, card.y + 6f, w - 28f, 26f), $"{current.from} invited you to their party",
                HudStyles.Label(16, TextAnchor.MiddleLeft, FontStyle.Bold, Color.white));
            GUI.Label(new Rect(card.x + 14f, card.y + 30f, 200f, 20f), $"expires in {Mathf.CeilToInt(TimeLeft)}s",
                HudStyles.Label(12, TextAnchor.MiddleLeft, FontStyle.Normal, SubColour));

            if (GUI.Button(new Rect(card.xMax - 250f, card.y + 34f, 120f, 34f), "Join party")) Accept();
            if (GUI.Button(new Rect(card.xMax - 120f, card.y + 34f, 106f, 34f), "Decline")) Decline();
            GUI.matrix = oldMatrix;
        }
    }
}

#if ETR_DEV_TOOLS || UNITY_EDITOR
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The spectator's bug-hunting views (the owner's notes of 2026-10-23 for T29): a live overlay over every guard and player (what a guard is doing, whom he is after, his health, whether he is armed or a backup officer) and a small network panel (round trip, packet
    /// loss, bytes per second, the tick rate, the frame rate). What a guard is doing reaches every machine with the snapshots; his health and his target come from the host's debug record (<c>RunSync.DevView.cs</c>), which is sent to the developers who watch and to nobody else.
    /// Drawn by <see cref="DevSpectator"/> only on a machine that watches; the words and the numbers are <see cref="DevOverlayRules"/>'s. Compiled into the Editor and the friends' builds only.
    /// </summary>
    public static class DevSpectatorView
    {
        /// <summary>What the host last said about one guard.</summary>
        public struct GuardRecord { public byte health; public sbyte target; public float time; }

        /// <summary>The overlay and the network panel are on to begin with (O and N switch them).</summary>
        public static bool OverlayOn = true, NetPanelOn = true;

        static readonly Dictionary<int, GuardRecord> records = new Dictionary<int, GuardRecord>();
        static float lastRecord = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { records.Clear(); lastRecord = -1f; OverlayOn = true; NetPanelOn = true; labels.Clear(); }

        /// <summary>How many guards the host has told this machine about on this floor (the tests read it).</summary>
        public static int RecordCount => records.Count;

        public static void Record(int guardId, byte health, sbyte target, float now)
        {
            records[guardId] = new GuardRecord { health = health, target = target, time = now };
            lastRecord = now;
        }

        public static void ClearRecords() { records.Clear(); lastRecord = -1f; }

        /// <summary>The host's record for this guard, if it is a recent one.</summary>
        public static bool TryRecord(int guardId, out GuardRecord record) => records.TryGetValue(guardId, out record) && DevOverlayRules.Fresh(Time.unscaledTime - record.time);

        /// <summary>Seconds since the host's last record, or -1 when none came yet.</summary>
        public static float RecordAge => lastRecord < 0f ? -1f : Time.unscaledTime - lastRecord;

        // ------------------------------------------------------------------ the link

        public struct Link { public int rttMs; public float lossPercent, inBytes, outBytes; public int tick; }

        /// <summary>The numbers of the connection to the host: Steam's real-time status over Steam (round trip, delivery quality, bytes), the transport's round trip alone over the Editor's local transport; -1 for what is not known.</summary>
        public static Link ReadLink()
        {
            var l = new Link { rttMs = -1, lossPercent = -1f, inBytes = -1f, outBytes = -1f, tick = 0 };
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return l;
            l.tick = (int)nm.NetworkConfig.TickRate;
            var tr = nm.NetworkConfig.NetworkTransport;
            if (tr is Net.SteamNetworkTransport st && st.TryGetLinkStats(NetworkManager.ServerClientId, out var s))
            {
                l.rttMs = s.pingMs; l.lossPercent = DevOverlayRules.LossPercent(s.quality); l.inBytes = s.inBytes; l.outBytes = s.outBytes;
            }
            else if (tr != null) l.rttMs = (int)tr.GetCurrentRtt(NetworkManager.ServerClientId);
            return l;
        }

        // ------------------------------------------------------------------ the picture

        struct Label { public GuardAI guard; public float dist, x, y; }
        static readonly List<Label> labels = new List<Label>();
        static readonly System.Comparison<Label> ByDistance = (a, b) => a.dist.CompareTo(b.dist);

        static Color StateColour(GuardState s, bool dead)
        {
            if (dead) return HudTheme.Dim;
            switch (s)
            {
                case GuardState.Investigate: return HudTheme.Warning;
                case GuardState.Chase: case GuardState.Pinning: return HudTheme.Danger;
                case GuardState.Calling: return HudTheme.Quota;
                case GuardState.Stunned: return HudTheme.Info;
                default: return HudTheme.Muted;
            }
        }

        /// <summary>Draws a two-line label centred on a screen point (IMGUI, y down).</summary>
        static void DrawLabel(float x, float y, string top, string bottom, Color colour)
        {
            float s = HudTheme.S, size = 14f;
            float w = Mathf.Max(HudTheme.Width(top, size), bottom.Length > 0 ? HudTheme.Width(bottom, size - 1f) : 0f) + 14f * s;
            float h = (bottom.Length > 0 ? 38f : 22f) * s;
            var r = new Rect(x - w * 0.5f, y - h, w, h);
            HudTheme.Glass(r, 0.78f, 0.55f, null, colour, 7f);
            HudTheme.Text(new Rect(r.x, r.y + 2f * s, r.width, 18f * s), top, size, colour, TextAnchor.MiddleCenter);
            if (bottom.Length > 0) HudTheme.Text(new Rect(r.x, r.y + 19f * s, r.width, 16f * s), bottom, size - 1f, HudTheme.Muted, TextAnchor.MiddleCenter);
        }

        /// <summary>The overlay: a label over every guard within range (the nearest <see cref="DevOverlayRules.MaxLabels"/>) and over every player. Drawn on a repaint event only.</summary>
        public static void DrawOverlay(Camera cam)
        {
            if (!cam) return;
            var camPos = cam.transform.position;
            labels.Clear();
            foreach (var g in GuardAI.All)
            {
                if (!g || !g.isActiveAndEnabled) continue;
                var at = g.transform.position + Vector3.up * (g.Dead ? 0.7f : 2.15f);
                float d = Vector3.Distance(camPos, at);
                if (d > (g.Dead ? DevOverlayRules.BodyLabelRange : DevOverlayRules.LabelRange)) continue;
                var sp = cam.WorldToScreenPoint(at);
                if (sp.z <= 0.1f) continue;
                labels.Add(new Label { guard = g, dist = d, x = sp.x, y = Screen.height - sp.y });
            }
            labels.Sort(ByDistance);
            int shown = Mathf.Min(labels.Count, DevOverlayRules.MaxLabels);
            for (int i = shown - 1; i >= 0; i--)                                    // (the far ones first: the near ones lie on top)
            {
                var g = labels[i].guard;
                int hp = -1, target = 0;
                if (g.NetId >= 0 && TryRecord(g.NetId, out var rec)) { hp = g.Dead ? 0 : rec.health; target = DevOverlayRules.TargetNumber(rec.target); }
                else if (!g.IsPuppet) { hp = DevOverlayRules.HealthByte(g.Health, g.MaxHealth, g.Dead); }
                DrawLabel(labels[i].x, labels[i].y, DevOverlayRules.GuardTitle(g.State, g.Dead, g.CallProgress, target),
                    g.Dead ? "" : DevOverlayRules.GuardDetail(hp, g.Armed, g.IsBackup, g.Eating, g.IsBoss), StateColour(g.State, g.Dead));
            }
            int n = 0;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                n++;
                if (!p) continue;
                var at = p.transform.position + Vector3.up * 1.7f;
                var sp = cam.WorldToScreenPoint(at);
                if (sp.z <= 0.1f) continue;
                int number = p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned ? np.Slot + 1 : n;
                DrawLabel(sp.x, Screen.height - sp.y, DevOverlayRules.PlayerLabel(number, p.Custody), "", HudTheme.PlayerColour(number));
            }
        }

        /// <summary>The network panel: top right, small, always there while it is on.</summary>
        public static void DrawNetPanel(int players)
        {
            var link = ReadLink();
            var lines = DevOverlayRules.NetLines(link.rttMs, link.lossPercent, link.inBytes, link.outBytes, link.tick, 1f / Mathf.Max(0.0001f, Time.smoothDeltaTime), RecordAge, players);
            float s = HudTheme.S, size = 14f;
            float w = 0f;
            foreach (var line in lines) w = Mathf.Max(w, HudTheme.Width(line, size));
            w += 24f * s;
            float h = (lines.Length * 20f + 12f) * s;
            var r = new Rect(Screen.width - w - 12f * s, 12f * s, w, h);
            HudTheme.Glass(r, 0.78f, 0.55f, null, HudTheme.Info, 9f);
            for (int i = 0; i < lines.Length; i++) HudTheme.Text(new Rect(r.x + 12f * s, r.y + (6f + i * 20f) * s, r.width - 24f * s, 20f * s), lines[i], size, HudTheme.Muted, TextAnchor.MiddleLeft);
        }
    }
}
#endif

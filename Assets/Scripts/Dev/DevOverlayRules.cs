using System;
using System.Globalization;
using System.Text;

namespace BuildingEscape
{
    /// <summary>
    /// The pure rules of the spectator's bug-hunting views (the owner's notes of 2026-10-23 for T29: a free camera next to the follow camera, an overlay of what the guards and the players are doing, and a small network panel): the words that say what a guard
    /// is doing, how the host's debug record is read, how the free camera moves and how the link numbers read. No Unity types, so every rule is tested outside the editor. The words are tool words (a state, a number), not game text.
    /// </summary>
    public static class DevOverlayRules
    {
        /// <summary>Guards farther than this from the camera get no label, and no more than <see cref="MaxLabels"/> are drawn (the nearest).</summary>
        public const float LabelRange = 70f;
        public const int MaxLabels = 40;

        /// <summary>A body that is down is labelled only this close.</summary>
        public const float BodyLabelRange = 25f;

        /// <summary>The host sends its debug record about this often (seconds); one that is older than <see cref="StaleSeconds"/> is not shown.</summary>
        public const float RecordInterval = 0.25f;
        public const float StaleSeconds = 1.5f;

        // ------------------------------------------------------------------ the free camera

        /// <summary>The free camera's speed (m/s), the factor Shift gives it and the factor Ctrl takes off, and how far up and down the view tilts (degrees).</summary>
        public const float FreeSpeed = 7f, FastFactor = 4f, SlowFactor = 0.25f, FreeMinPitch = -89f, FreeMaxPitch = 89f;

        /// <summary>How far the free camera moves in <paramref name="dt"/> seconds with the keys held (Shift: fast, Ctrl: slow, both: fast).</summary>
        public static float FreeStep(bool fast, bool slow, float dt) => FreeSpeed * (fast ? FastFactor : slow ? SlowFactor : 1f) * Math.Max(0f, dt);

        // ------------------------------------------------------------------ the guard's debug record

        /// <summary>A guard's health as the percentage the host sends (0 for a guard who is down), never outside 0..100.</summary>
        public static byte HealthByte(float health, float maxHealth, bool dead)
        {
            if (dead || maxHealth <= 0f) return 0;
            int pct = (int)Math.Round(100.0 * health / maxHealth);
            return (byte)Math.Max(1, Math.Min(100, pct));                    // (alive is never shown as 0)
        }

        /// <summary>The player number shown for a slot the host sent (slots count from 0; -1, and anything outside the party of five, is nobody: 0).</summary>
        public static int TargetNumber(sbyte slot) => slot >= 0 && slot < 5 ? slot + 1 : 0;

        /// <summary>Is a record of this age (seconds) still to be shown?</summary>
        public static bool Fresh(float age) => age >= 0f && age <= StaleSeconds;

        // ------------------------------------------------------------------ the words

        public static string StateWord(GuardState s)
        {
            switch (s)
            {
                case GuardState.Patrol: return "PATROL";
                case GuardState.Investigate: return "INVESTIGATE";
                case GuardState.Chase: return "CHASE";
                case GuardState.Stunned: return "STUNNED";
                case GuardState.Calling: return "CALLING";
                case GuardState.Pinning: return "PINNING";
                case GuardState.Dragging: return "DRAGGING";
                default: return s.ToString().ToUpperInvariant();
            }
        }

        /// <summary>The first line over a guard: what he is doing, how far his radio call is, and whom he is after.</summary>
        public static string GuardTitle(GuardState state, bool dead, float callProgress, int targetNumber)
        {
            if (dead) return "DOWN";
            var sb = new StringBuilder(StateWord(state));
            if (state == GuardState.Calling && callProgress > 0f) sb.Append(' ').Append(Math.Max(0, Math.Min(100, (int)Math.Round(callProgress * 100f))).ToString(CultureInfo.InvariantCulture)).Append('%');
            if (targetNumber > 0 && state != GuardState.Patrol && state != GuardState.Stunned) sb.Append("  > P").Append(targetNumber.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>The second line over a guard: his health (when the host's record has reached us) and what he is: armed, a backup officer, eating, the boss.</summary>
        public static string GuardDetail(int healthPercent, bool armed, bool backup, bool eating, bool boss)
        {
            var sb = new StringBuilder();
            if (healthPercent >= 0) sb.Append("HP ").Append(healthPercent.ToString(CultureInfo.InvariantCulture)).Append('%');
            void Add(bool on, string word) { if (!on) return; if (sb.Length > 0) sb.Append("  "); sb.Append(word); }
            Add(boss, "BOSS"); Add(backup, "BACKUP"); Add(armed, "ARMED"); Add(eating, "EATING");
            return sb.ToString();
        }

        /// <summary>The label over a player: his number and what has happened to him.</summary>
        public static string PlayerLabel(int number, CustodyState custody)
        {
            string tail = custody == CustodyState.Pinned ? "  PINNED" : custody == CustodyState.Busted ? "  BUSTED" : "";
            return "P" + number.ToString(CultureInfo.InvariantCulture) + tail;
        }

        // ------------------------------------------------------------------ the network panel

        /// <summary>The share of packets lost (0..100, one decimal) from Steam's connection quality (1 = everything arrived); -1 when the number is not known.</summary>
        public static float LossPercent(float quality)
        {
            if (quality < 0f || quality > 1f || float.IsNaN(quality)) return -1f;
            return (float)Math.Round((1f - quality) * 100f, 1);
        }

        /// <summary>A rate in bytes per second as "12.3 kB/s" (or "850 B/s" under a kilobyte); a rate that is not known is "-".</summary>
        public static string Rate(float bytesPerSecond)
        {
            if (bytesPerSecond < 0f || float.IsNaN(bytesPerSecond)) return "-";
            if (bytesPerSecond < 999.5f) return Math.Round(bytesPerSecond).ToString(CultureInfo.InvariantCulture) + " B/s";
            return (bytesPerSecond / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " kB/s";
        }

        /// <summary>The lines of the network panel: the round trip, the loss, the rates, the tick, the frame rate and how old the host's last record is.</summary>
        public static string[] NetLines(int rttMs, float lossPercent, float inBytes, float outBytes, int tickRate, float framesPerSecond, float recordAge, int players)
        {
            string loss = lossPercent < 0f ? "-" : lossPercent.ToString("0.0", CultureInfo.InvariantCulture) + " %";
            string age = recordAge < 0f ? "none yet" : recordAge.ToString("0.0", CultureInfo.InvariantCulture) + " s ago";
            return new[]
            {
                "ROUND TRIP  " + (rttMs < 0 ? "-" : rttMs.ToString(CultureInfo.InvariantCulture) + " ms"),
                "PACKET LOSS  " + loss,
                "IN  " + Rate(inBytes) + "   OUT  " + Rate(outBytes),
                "TICK  " + tickRate.ToString(CultureInfo.InvariantCulture) + " Hz   FRAMES  " + Math.Round(framesPerSecond).ToString(CultureInfo.InvariantCulture) + " / s",
                "PLAYERS  " + players.ToString(CultureInfo.InvariantCulture) + "   HOST RECORD  " + age,
            };
        }
    }
}

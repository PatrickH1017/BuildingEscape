using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The leaderboard of the PVP room (the owner, 2026-10-20: "When a player enters the room it should add their name to a list of the side of the screen that acts as a leaderboard for eliminations in the PvP room so that players can compete
    /// for fun while they wait"): a player who walks into the room is put on the list (no eliminations yet); each time he knocks a rival out (<see cref="PvpFighter.Hurt"/>) his count goes up by one; the list stays until the basement is built
    /// again (a new run). The host (or the offline game) keeps it and tells every machine (<c>RunSync.PvpBoardRpc</c>: the whole list packed into one number); <see cref="PvpBoardHud"/> draws it at the right side of the screen. A player is his
    /// slot ("PLAYER 2": the name every other in-run screen uses).
    /// </summary>
    public static class PvpBoard
    {
        /// <summary>The players the list can hold (a run has five at the most) and the most eliminations it counts.</summary>
        public const int Slots = 6, MaxCount = 255;

        static readonly bool[] listed = new bool[Slots];
        static readonly int[] counts = new int[Slots];

        /// <summary>Goes up every time the list changes (a player is put on it, a count goes up, the host's list arrives, it is cleared).</summary>
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Reset();

        /// <summary>Empties the list (a new basement).</summary>
        public static void Reset()
        {
            System.Array.Clear(listed, 0, Slots);
            System.Array.Clear(counts, 0, Slots);
            Version++;
        }

        public static bool Listed(int slot) => slot >= 0 && slot < Slots && listed[slot];
        public static int Count(int slot) => slot >= 0 && slot < Slots ? counts[slot] : 0;

        /// <summary>Is anybody on the list?</summary>
        public static bool Any
        {
            get { for (int s = 0; s < Slots; s++) if (listed[s]) return true; return false; }
        }

        /// <summary>A player walked into the room: he is on the list. False if he already was (or the slot is nonsense).</summary>
        public static bool Enter(int slot)
        {
            if (slot < 0 || slot >= Slots || listed[slot]) return false;
            listed[slot] = true;
            Version++;
            return true;
        }

        /// <summary>A player knocked a rival out: one more for him (and he is on the list if he was not).</summary>
        public static void Eliminate(int slot)
        {
            if (slot < 0 || slot >= Slots) return;
            listed[slot] = true;
            counts[slot] = Mathf.Min(MaxCount, counts[slot] + 1);
            Version++;
        }

        /// <summary>Puts the slots that are on the list into <paramref name="into"/>, the one with the most eliminations first (a tie: the lower slot first); returns how many.</summary>
        public static int Ranked(int[] into)
        {
            int n = 0;
            for (int s = 0; s < Slots && n < into.Length; s++) if (listed[s]) into[n++] = s;
            for (int i = 1; i < n; i++)                                            // (an insertion sort: at most six)
            {
                int v = into[i], j = i - 1;
                while (j >= 0 && counts[into[j]] < counts[v]) { into[j + 1] = into[j]; j--; }
                into[j + 1] = v;
            }
            return n;
        }

        /// <summary>The whole list in one number: a bit for every slot that is on it, then a byte for every count.</summary>
        public static ulong Pack()
        {
            ulong p = 0;
            for (int s = 0; s < Slots; s++)
            {
                if (listed[s]) p |= 1UL << s;
                p |= (ulong)(byte)counts[s] << (Slots + 8 * s);
            }
            return p;
        }

        /// <summary>Takes the host's list (<see cref="Pack"/>).</summary>
        public static void Apply(ulong packed)
        {
            bool changed = false;
            for (int s = 0; s < Slots; s++)
            {
                bool l = ((packed >> s) & 1UL) != 0UL;
                int c = (int)((packed >> (Slots + 8 * s)) & 0xFFUL);
                if (listed[s] != l || counts[s] != c) { listed[s] = l; counts[s] = c; changed = true; }
            }
            if (changed) Version++;
        }
    }

    /// <summary>The PVP leaderboard at the right side of the screen while the team is in the basement (HudTheme pieces only, drawn on Repaint only): who has been in the room, the one with the most eliminations first.</summary>
    public sealed class PvpBoardHud : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<PvpBoardHud>()) return;
            var go = new GameObject("PvpBoardHud");
            DontDestroyOnLoad(go);
            go.AddComponent<PvpBoardHud>();
        }

        /// <summary>The heading (a placeholder: the owner writes the text).</summary>
        public const string Title = "PVP";

        /// <summary>How many rows were drawn the last time (for the tests).</summary>
        public static int LastRows;

        readonly int[] order = new int[PvpBoard.Slots];

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            LastRows = 0;
            var hm = HeistManager.Instance;
            if (!hm || hm.Floor != 0 || IntroCinematic.Playing || !PvpBoard.Any) return;
            int n = PvpBoard.Ranked(order);
            float s = HudTheme.S, w = 200f * s, rowH = 30f * s, head = 30f * s, pad = 10f * s;
            var r = new Rect(Screen.width - w - 16f * s, 94f * s, w, head + n * rowH + pad);          // (under the player badge and the first voice pill)
            HudTheme.Plate(r);
            HudTheme.Text(new Rect(r.x + pad, r.y + 4f * s, w - pad * 2f, head - 4f * s), Title, 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
            for (int i = 0; i < n; i++)
            {
                int slot = order[i];
                float y = r.y + head + i * rowH;
                HudTheme.Text(new Rect(r.x + pad, y, 24f * s, rowH), (i + 1).ToString(), 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
                HudTheme.Chip(new Rect(r.x + pad + 26f * s, y + 3f * s, 112f * s, rowH - 6f * s), "PLAYER " + (slot + 1), HudTheme.PlayerColour(slot + 1), 14f, HudTheme.Ink);
                HudTheme.Text(new Rect(r.xMax - pad - 44f * s, y, 44f * s, rowH), PvpBoard.Count(slot).ToString(), 20f, HudTheme.Bright, TextAnchor.MiddleRight);
                LastRows++;
            }
        }
    }
}

using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The dart board's scoreboard in a networked run (the owner, 2026-10-19 night: "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone should see the
    /// results"). The darts themselves fly on the thrower's machine only; each time one lands his machine shows its score on the board's wall at once and tells the host (<see cref="RequestDartScore"/>): the dart's number in the turn,
    /// its score, the turn, what is left or the points, and whether the turn was a bust or the game was won. The host checks that the thrower is at the board, that the numbers make sense and that the darts do not come faster than a
    /// player can throw them, and tells every other machine (<c>DartScoreRpc</c>), whose board shows it the same way.
    /// </summary>
    public partial class RunSync
    {
        readonly Dictionary<ulong, float> lastDartScore = new();
        const float DartScoreGap = 0.05f;        // s: two darts of one player cannot land closer than this

        /// <summary>A dart of this player landed: tell the host (or, on the host, everybody).</summary>
        public void RequestDartScore(int dartNo, int score, int turn, int remaining, int points, bool bust, bool won, bool cricket)
        {
            byte flags = (byte)((bust ? 1 : 0) | (won ? 2 : 0) | (cricket ? 4 : 0));
            byte d = (byte)Mathf.Clamp(dartNo, 0, 255), s = (byte)Mathf.Clamp(score, 0, 255), t = (byte)Mathf.Clamp(turn, 0, 255);
            ushort rem = (ushort)Mathf.Clamp(remaining, 0, 65535), pts = (ushort)Mathf.Clamp(points, 0, 65535);
            if (IsServer) HostDartScore(Me, d, s, t, rem, pts, flags);
            else DartScoreRequestRpc(d, s, t, rem, pts, flags);
        }

        [Rpc(SendTo.Server)]
        void DartScoreRequestRpc(byte dartNo, byte score, byte turn, ushort remaining, ushort points, byte flags, RpcParams rp = default) =>
            HostDartScore(rp.Receive.SenderClientId, dartNo, score, turn, remaining, points, flags);

        void HostDartScore(ulong who, byte dartNo, byte score, byte turn, ushort remaining, ushort points, byte flags)
        {
            var np = PlayerOf(who);
            var board = DartBoard.Instance;
            if (np == null || board == null) return;
            if (dartNo >= DartsRules.Match.DartsPerTurn || score > 60 || remaining > DartsRules.Match.Start) return;
            float reach = DartBoard.MaxDistance + 3f;                                    // (the game ends 14 m from the board: he has to be about there; his position here is a moment old)
            if ((np.transform.position - board.FaceCentre).sqrMagnitude > reach * reach) return;
            float now = Time.unscaledTime;
            if (lastDartScore.TryGetValue(who, out float t) && now - t < DartScoreGap) return;
            lastDartScore[who] = now;
            DartScoreRpc((byte)np.Slot, dartNo, score, turn, remaining, points, flags);
        }

        /// <summary>Host -> everybody: this player's dart landed (his own machine showed it at once and ignores this).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void DartScoreRpc(byte slot, byte dartNo, byte score, byte turn, ushort remaining, ushort points, byte flags)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && np.IsOwner) return;
            var board = DartBoard.Instance;
            if (board == null) return;
            board.ShowScore(slot, dartNo, score, turn, remaining, points, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0 ? DartsRules.Mode.Cricket : DartsRules.Mode.Game301);
        }
    }
}

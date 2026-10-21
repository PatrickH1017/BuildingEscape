# Round 12, batch G39 (the owner, 2026-10-20): "When a player enters the room it should add their name to a list of the side of the screen that acts as a leaderboard for eliminations in the PvP room so that players can compete for fun
# while they wait". The leaderboard of the PVP room:
#  - `PvpBoard` (new file Scripts\PvpBoard.cs): the list (a player who walks into the room is put on it with no eliminations, each knock-out he makes counts for him, ranked by eliminations), kept by the host (or the offline game), packed
#    into one number (`Pack` / `Apply`), reset when the basement is built again (a new run); `PvpBoardHud` draws it at the right side of the screen in the docks (a player is his slot: "PLAYER 2", like every other in-run screen);
#  - `PvpFighter.Hurt(damage, attackerSlot)` counts the elimination, `TryStrike` (offline), `HostPvpHit` and the host's bullets (`HostPellet`) pass who hit; `PvpFighter.Update` puts a player on the list when the host / the offline game sees him inside;
#  - `RunSync.PvpBoardRpc` (host -> everybody, when the list changes and every 3 s while there is one, for a machine that joins late); protocol "26";
#  - tests: pure (`PvpBoard`), R7 (the offline fighters), the built-player autopilot (`PvpNet`: both walk in, the client knocks the host out, both lists show it), a real-window picture of the list.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

$cs = @'
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
            var r = new Rect(Screen.width - w - 16f * s, 64f * s, w, head + n * rowH + pad);
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
'@
if ($env:R2_DRY) { Write-Output "dry run: would write $A\PvpBoard.cs" }
else {
    [IO.File]::WriteAllText("$A\PvpBoard.cs", $cs.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
    Write-Output "wrote $A\PvpBoard.cs"
}

# ---------------------------------------------------------------- the fighter counts the eliminations and puts a player on the list
Open-Src "$A\Pvp.cs"
Rep 'public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z);' @'
public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z);

        /// <summary>The player's number on the PVP leaderboard (<see cref="PvpBoard"/>): his slot in a networked run, 0 offline.</summary>
        public static int SlotOf(WobblyPlayer p) => p && p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned ? np.Slot : 0;
'@
Rep 'bool inside = Inside;' @'
bool inside = Inside;
            if (inside && player && Net.NetRole.Simulates) PvpBoard.Enter(SlotOf(player));            // (the host / the offline game puts a player who walks into the room on the leaderboard: PvpBoard, the owner, 2026-10-20)
'@
Rep 'public void Hurt(float damage)' 'public void Hurt(float damage, int attackerSlot = -1)'
Rep 'if (player) player.Stumble(' @'
if (attackerSlot >= 0) PvpBoard.Eliminate(attackerSlot);                       // (the leaderboard: one more for whoever knocked him out)
                if (player) player.Stumble(
'@
Rep 'v.Hurt(damage);' 'v.Hurt(damage, SlotOf(attacker));'
Save-Src

# ---------------------------------------------------------------- the host counts who knocked whom out and tells everybody
Open-Src "$A\Net\RunSync.Pvp.cs"
Rep 'v.Hurt(damage);' 'v.Hurt(damage, attacker.Slot);'
Rep @'
            if (np && np.TryGetComponent(out PvpFighter f)) f.SetState(health, down);
        }
'@ @'
            if (np && np.TryGetComponent(out PvpFighter f)) f.SetState(health, down);
        }

        // ---- the leaderboard of the PVP room (the owner, 2026-10-20): the host keeps it (PvpBoard) and tells everybody when it changes and now and then while there is one (a machine that joins late)
        int sentBoardVersion = -1;
        float nextBoardBeat;
        const float BoardBeatSeconds = 3f;

        /// <summary>Host, every frame: sends the leaderboard when it changed, and every few seconds while there is one.</summary>
        void HostPvpBoardTick()
        {
            float now = Time.unscaledTime;
            if (PvpBoard.Version == sentBoardVersion && !(PvpBoard.Any && now >= nextBoardBeat)) return;
            sentBoardVersion = PvpBoard.Version;
            nextBoardBeat = now + BoardBeatSeconds;
            if (NetworkManager.ConnectedClientsIds.Count > 1) PvpBoardRpc(PvpBoard.Pack());
        }

        /// <summary>Host -> everybody: the whole leaderboard of the PVP room (<see cref="PvpBoard.Pack"/>); the host's own list is the one that was sent.</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void PvpBoardRpc(ulong packed)
        {
            if (!IsServer) PvpBoard.Apply(packed);
        }
'@
Save-Src

Open-Src "$A\Net\RunSync.Guns.cs"
Rep 'vf.Hurt(PvpRules.BulletDamage);' 'vf.Hurt(PvpRules.BulletDamage, np.Slot);'
Save-Src

Open-Src "$A\Net\RunSync.cs"
Rep 'IntroServerTick();' @'
IntroServerTick();
                HostPvpBoardTick();
'@
Save-Src

Open-Src "$A\Net\NetConst.cs"
Rep 'public const string ProtocolVersion = "25";   // 25: the dart board hangs' 'public const string ProtocolVersion = "26";   // 26: the PVP room''s leaderboard (the host tells everybody who has been in the room and how many eliminations each has) and the room is built differently (a part out to the north and the pocket behind the elevator room, so old and new cannot share a run); 25: the dart board hangs'
Save-Src

Open-Src "$A\FloorGenerator.cs"
Rep 'Net.NetIds.Reset(); // a new floor: ids start again from 0, in creation order (same on every machine)' @'
Net.NetIds.Reset(); // a new floor: ids start again from 0, in creation order (same on every machine)
            if (hub) PvpBoard.Reset();            // (a new basement: a new leaderboard of the PVP room)
'@
Save-Src

# ---------------------------------------------------------------- the built-player network test
Open-Src "$A\QaAutopilot.cs"
Rep 'yield return DartsNet(true);' @'
yield return DartsNet(true);
            yield return PvpNet(true);
'@
Rep 'yield return DartsNet(false);' @'
yield return DartsNet(false);
            yield return PvpNet(false);
'@
Rep '        IEnumerator ThrowThree(DartBoard board)' @'
        /// <summary>
        /// The PVP room's leaderboard over the network (the owner, 2026-10-20), in the docks, run by the host and the client at the same moment: both walk into the room and are put on the list (the host keeps it and tells the client), then the client
        /// knocks the host out through the real request to the host (three bullets' worth of blows) and the client's slot has one elimination on both machines.
        /// </summary>
        IEnumerator PvpNet(bool host)
        {
            string who = host ? "host" : "client";
            var me = NetPlayer.Local;
            var wob = me != null ? me.GetComponent<WobblyPlayer>() : null;
            var gen = FindFirstObjectByType<FloorGenerator>();
            Check($"{who}: the docks have the PVP room and my body is here", gen != null && gen.PvpRoomArea.width > 0f && wob != null);
            if (gen == null || wob == null) yield break;
            var other = NetPlayer.All.FirstOrDefault(p => p != null && p != me);
            Check($"{who}: the other player is here", other != null);
            if (other == null) yield break;
            int hostSlot = host ? me.Slot : other.Slot, clientSlot = host ? other.Slot : me.Slot;
            var mid = new Vector3((gen.PvpRoomArea.xMin + gen.PvpRoomArea.xMax) * 0.5f, 1.1f, (gen.PvpRoomArea.yMin + gen.PvpRoomArea.yMax) * 0.5f);
            wob.TeleportTo(mid + (host ? Vector3.left : Vector3.right) * 1.2f, Quaternion.identity);
            yield return Until(() => PvpBoard.Listed(hostSlot) && PvpBoard.Listed(clientSlot), 30f, "both players on the PVP leaderboard");
            Check($"{who}: both players are on the PVP leaderboard once they are in the room, with no eliminations yet", PvpBoard.Listed(hostSlot) && PvpBoard.Listed(clientSlot) && PvpBoard.Count(hostSlot) == 0 && PvpBoard.Count(clientSlot) == 0,
                $"(host listed {PvpBoard.Listed(hostSlot)} {PvpBoard.Count(hostSlot)}, client listed {PvpBoard.Listed(clientSlot)} {PvpBoard.Count(clientSlot)})");
            if (!host)
            {
                var hostBody = other.GetComponent<WobblyPlayer>();
                yield return Wait(1.5f);                                                   // (the host's body is here, where he stands)
                for (int i = 0; i < 4 && hostBody != null; i++) { PvpFighter.TryStrike(wob, hostBody, PvpRules.BulletDamage); yield return Wait(0.4f); }
            }
            yield return Until(() => PvpBoard.Count(clientSlot) >= 1, 30f, "the client's elimination on the leaderboard");
            Check($"{who}: the knock-out counts for the client (one elimination on my list), none for the host", PvpBoard.Count(clientSlot) == 1 && PvpBoard.Count(hostSlot) == 0, $"(client {PvpBoard.Count(clientSlot)}, host {PvpBoard.Count(hostSlot)})");
            yield return Wait(PvpRules.KnockoutSeconds + 1f);
            wob.TeleportTo(new Vector3(20f, 1.1f, 9f), Quaternion.identity);                // (out of the room again)
            yield return Wait(1f);
        }

        IEnumerator ThrowThree(DartBoard board)
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src "$E\NetDevRound12.cs"
Rep @'
            // ---- backup officers spot a player much faster (the owner, 2026-10-20)
'@ @'
            // ---- the leaderboard of the PVP room (the owner, 2026-10-20)
            PvpBoard.Reset();
            bool firstEnter = PvpBoard.Enter(2), secondEnter = PvpBoard.Enter(2), badEnter = PvpBoard.Enter(9) || PvpBoard.Enter(-1);
            PvpBoard.Enter(0); PvpBoard.Eliminate(0); PvpBoard.Eliminate(0); PvpBoard.Eliminate(2); PvpBoard.Eliminate(4); PvpBoard.Eliminate(-3);
            var boardOrder = new int[PvpBoard.Slots]; int boardRows = PvpBoard.Ranked(boardOrder);
            bool rankOk = boardRows == 3 && boardOrder[0] == 0 && boardOrder[1] == 2 && boardOrder[2] == 4;
            ulong boardPacked = PvpBoard.Pack(); int versionBefore = PvpBoard.Version;
            PvpBoard.Reset(); bool emptied = !PvpBoard.Any && PvpBoard.Count(0) == 0;
            PvpBoard.Apply(boardPacked);
            bool restored = PvpBoard.Listed(0) && PvpBoard.Listed(2) && PvpBoard.Listed(4) && !PvpBoard.Listed(1) && PvpBoard.Count(0) == 2 && PvpBoard.Count(2) == 1 && PvpBoard.Count(4) == 1 && PvpBoard.Pack() == boardPacked;
            for (int i = 0; i < 300; i++) PvpBoard.Eliminate(1);
            bool capped = PvpBoard.Count(1) == PvpBoard.MaxCount && PvpBoard.Count(7) == 0 && !PvpBoard.Listed(-1);
            PvpBoard.Reset();
            Check("pvp leaderboard: a player who walks into the room is put on it once, a knock-out counts for the one who made it (and puts him on it), the list is ranked by eliminations (a tie: the lower slot), it packs into one number and comes back the same, it stops at 255, nonsense slots are refused",
                firstEnter && !secondEnter && !badEnter && rankOk && emptied && restored && capped && PvpBoard.Version > versionBefore, $"(rows {boardRows}, order {boardOrder[0]} {boardOrder[1]} {boardOrder[2]}, restored {restored}, capped {capped})");
            // ---- backup officers spot a player much faster (the owner, 2026-10-20)
'@
Save-Src

Open-Src "$E\NetDevR7.cs"
Rep 'var gen1 = FindFirstObjectByType<FloorGenerator>();' @'
var gen1 = FindFirstObjectByType<FloorGenerator>();
                PvpBoard.Reset();
'@
Rep 'R7Check("both are inside the PVP room", fa.Inside && fb.Inside);' @'
R7Check("both are inside the PVP room", fa.Inside && fb.Inside);
                R7Check("walking into the room put the player on the leaderboard, with no eliminations", PvpBoard.Listed(0) && PvpBoard.Count(0) == 0 && PvpBoard.Any);
'@
Rep 'R7Check("nothing can hurt him while he is down", !hitDown && fb.Health <= 0.01f);' @'
R7Check("nothing can hurt him while he is down", !hitDown && fb.Health <= 0.01f);
                R7Check("the knock-out counts for the one who made it: one elimination on the leaderboard (the blows that did not knock out, and the one on a rival who was down, count for nothing)", PvpBoard.Count(0) == 1, $"({PvpBoard.Count(0)})");
'@
Save-Src

Open-Src "$E\NetDevUi.cs"
Rep @'
                dartBoard.Leave();
                yield return new WaitForSeconds(0.3f);
            }
'@ @'
                dartBoard.Leave();
                yield return new WaitForSeconds(0.3f);
            }
            // ---- the leaderboard of the PVP room at the right side of the screen (the owner, 2026-10-20)
            PvpBoard.Reset(); PvpBoard.Enter(0); PvpBoard.Enter(1); PvpBoard.Enter(2);
            PvpBoard.Eliminate(1); PvpBoard.Eliminate(1); PvpBoard.Eliminate(2);
            yield return new WaitForSeconds(0.5f);
            yield return Snap("pvp_board");
            UiCheck("the PVP leaderboard is drawn: three rows", PvpBoardHud.LastRows == 3, $"(rows {PvpBoardHud.LastRows})");
            PvpBoard.Reset();
'@
Save-Src

# ---------------------------------------------------------------- the words in the playtest change list
Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep 'with shipping containers, pallets and crates to hide behind. A dart board hangs' 'with shipping containers, pallets and crates to hide behind. A list at the right side of the screen in the docks shows who has walked into the PVP room and how many eliminations each has. A dart board hangs'
Save-Src
Write-Output 'round 12 batch G39 patched'

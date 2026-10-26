using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch I, T29 part 2: the spectator's bug-hunting views (<c>DevOverlayRules</c>): the free camera's speed, the host's debug record (health, the target), the words over a guard and a player and the
    /// numbers of the network panel. Called from <c>NetDevTest.PolicyTest</c>; the two-machine half is in <c>NetDevAsks1023H.cs</c> (<c>DevSpectate</c>).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023ITest()
        {
            // ---- the free camera
            Check("spectate views (T29): the free camera flies 7 m a second, four times that with Shift, a quarter with Ctrl, and does not fly backwards in time",
                Math.Abs(DevOverlayRules.FreeStep(false, false, 1f) - 7f) < 1e-4f && Math.Abs(DevOverlayRules.FreeStep(true, false, 1f) - 28f) < 1e-4f && Math.Abs(DevOverlayRules.FreeStep(false, true, 1f) - 1.75f) < 1e-4f
                && Math.Abs(DevOverlayRules.FreeStep(true, true, 0.5f) - 14f) < 1e-4f && DevOverlayRules.FreeStep(false, false, -1f) == 0f);
            Check("spectate views (T29): the free camera tilts nearly straight up and down but not past it",
                DevOverlayRules.FreeMinPitch == -89f && DevOverlayRules.FreeMaxPitch == 89f);

            // ---- the host's debug record
            Check("spectate views (T29): health goes out as a percentage; a guard who is alive is never 0, one who is down always is, and nothing leaves 0..100",
                DevOverlayRules.HealthByte(100f, 100f, false) == 100 && DevOverlayRules.HealthByte(50f, 100f, false) == 50 && DevOverlayRules.HealthByte(0.2f, 100f, false) == 1 && DevOverlayRules.HealthByte(80f, 100f, true) == 0
                && DevOverlayRules.HealthByte(250f, 100f, false) == 100 && DevOverlayRules.HealthByte(-5f, 100f, false) == 1 && DevOverlayRules.HealthByte(30f, 0f, false) == 0 && DevOverlayRules.HealthByte(300f, 300f, false) == 100);
            Check("spectate views (T29): a slot of the host's record is the player's number (slot 0 is player 1); no target, and anything outside the party of five, is nobody",
                DevOverlayRules.TargetNumber(0) == 1 && DevOverlayRules.TargetNumber(4) == 5 && DevOverlayRules.TargetNumber(-1) == 0 && DevOverlayRules.TargetNumber(5) == 0 && DevOverlayRules.TargetNumber(100) == 0 && DevOverlayRules.TargetNumber(-128) == 0);
            Check("spectate views (T29): a record is fresh for a second and a half and not before it was made",
                DevOverlayRules.Fresh(0f) && DevOverlayRules.Fresh(1.5f) && !DevOverlayRules.Fresh(1.51f) && !DevOverlayRules.Fresh(-0.1f) && DevOverlayRules.RecordInterval < DevOverlayRules.StaleSeconds);

            // ---- the words
            bool words = true;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (GuardState s in Enum.GetValues(typeof(GuardState))) { string w = DevOverlayRules.StateWord(s); words &= w.Length > 0 && w == w.ToUpperInvariant() && seen.Add(w); }
            Check("spectate views (T29): every guard state has a word of its own, in capitals", words && seen.Count == Enum.GetValues(typeof(GuardState)).Length);
            Check("spectate views (T29): the first line over a guard says what he does, how far his radio call is and whom he is after (a patrol and a stun have no target)",
                DevOverlayRules.GuardTitle(GuardState.Patrol, false, 0f, 0) == "PATROL" && DevOverlayRules.GuardTitle(GuardState.Chase, false, 0f, 2) == "CHASE  > P2" && DevOverlayRules.GuardTitle(GuardState.Calling, false, 0.5f, 0) == "CALLING 50%"
                && DevOverlayRules.GuardTitle(GuardState.Investigate, false, 0f, 1) == "INVESTIGATE  > P1" && DevOverlayRules.GuardTitle(GuardState.Patrol, false, 0f, 3) == "PATROL" && DevOverlayRules.GuardTitle(GuardState.Stunned, false, 0f, 3) == "STUNNED"
                && DevOverlayRules.GuardTitle(GuardState.Chase, true, 0f, 2) == "DOWN" && DevOverlayRules.GuardTitle(GuardState.Calling, false, 3f, 0) == "CALLING 100%");
            Check("spectate views (T29): the second line says his health when it is known and what he is (boss, backup officer, armed, eating); nothing known, nothing said",
                DevOverlayRules.GuardDetail(-1, false, false, false, false) == "" && DevOverlayRules.GuardDetail(80, true, false, false, false) == "HP 80%  ARMED" && DevOverlayRules.GuardDetail(100, true, true, false, true) == "HP 100%  BOSS  BACKUP  ARMED"
                && DevOverlayRules.GuardDetail(-1, true, false, true, false) == "ARMED  EATING" && DevOverlayRules.GuardDetail(0, false, false, false, false) == "HP 0%");
            Check("spectate views (T29): a player's label is his number and, when it happened, PINNED or BUSTED",
                DevOverlayRules.PlayerLabel(2, CustodyState.Free) == "P2" && DevOverlayRules.PlayerLabel(2, CustodyState.Pinned) == "P2  PINNED" && DevOverlayRules.PlayerLabel(5, CustodyState.Busted) == "P5  BUSTED");
            Check("spectate views (T29): the overlay draws the nearest forty guards within 70 m, bodies only within 25 m",
                DevOverlayRules.MaxLabels == 40 && DevOverlayRules.LabelRange == 70f && DevOverlayRules.BodyLabelRange == 25f && DevOverlayRules.BodyLabelRange < DevOverlayRules.LabelRange);

            // ---- the network panel
            Check("spectate views (T29): the loss is the share of packets that did not arrive, to a tenth; a quality that is not a quality is no number",
                DevOverlayRules.LossPercent(1f) == 0f && DevOverlayRules.LossPercent(0.97f) == 3f && DevOverlayRules.LossPercent(0f) == 100f && DevOverlayRules.LossPercent(0.995f) == 0.5f
                && DevOverlayRules.LossPercent(-1f) == -1f && DevOverlayRules.LossPercent(1.5f) == -1f && DevOverlayRules.LossPercent(float.NaN) == -1f);
            Check("spectate views (T29): a rate reads in bytes under a kilobyte and in kilobytes above it; unknown is a dash",
                DevOverlayRules.Rate(-1f) == "-" && DevOverlayRules.Rate(0f) == "0 B/s" && DevOverlayRules.Rate(850f) == "850 B/s" && DevOverlayRules.Rate(999.4f) == "999 B/s" && DevOverlayRules.Rate(999.5f) == "1.0 kB/s" && DevOverlayRules.Rate(12300f) == "12.3 kB/s");
            var lines = DevOverlayRules.NetLines(23, 0.3f, 4200f, 650f, 30, 59.6f, 0.2f, 2);
            var unknown = DevOverlayRules.NetLines(-1, -1f, -1f, -1f, 30, 60f, -1f, 0);
            Check("spectate views (T29): the panel has five lines with the round trip, the loss, the rates, the tick, the frame rate, the players and the age of the host's last record; what is not known reads as a dash",
                lines.Length == 5 && lines[0] == "ROUND TRIP  23 ms" && lines[1] == "PACKET LOSS  0.3 %" && lines[2] == "IN  4.2 kB/s   OUT  650 B/s" && lines[3] == "TICK  30 Hz   FRAMES  60 / s" && lines[4] == "PLAYERS  2   HOST RECORD  0.2 s ago"
                && unknown[0] == "ROUND TRIP  -" && unknown[1] == "PACKET LOSS  -" && unknown[2] == "IN  -   OUT  -" && unknown[4] == "PLAYERS  0   HOST RECORD  none yet");
        }
    }
}

using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch C: the developer terminal and the code that unlocks the dev menu (T2, T3). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023D.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023DTest()
        {
            // ---- T3: the code, typed in the terminal
            Check("terminal (T3): the unlock code is accepted exactly: capitals count, spaces round it do not", DevTerminalRules.Accepts("@Catdog813") && DevTerminalRules.Accepts("  @Catdog813  ") && !DevTerminalRules.Accepts("@catdog813") && !DevTerminalRules.Accepts("@Catdog81") && !DevTerminalRules.Accepts("@Catdog8133") && !DevTerminalRules.Accepts("") && !DevTerminalRules.Accepts(null));
            Check("terminal (T3): the game holds only the code's SHA-256, 64 hex characters", DevTerminalRules.CodeHash.Length == 64 && DevTerminalRules.Hash("@Catdog813") == DevTerminalRules.CodeHash && DevTerminalRules.Hash("") != DevTerminalRules.CodeHash);
            Check("terminal (T3): F1 opens the dev menu only alone, once unlocked, and not while the terminal is up",
                DevTerminalRules.MenuKeyOpens(true, false, true, false) && !DevTerminalRules.MenuKeyOpens(true, false, false, false) && !DevTerminalRules.MenuKeyOpens(true, true, true, false) && !DevTerminalRules.MenuKeyOpens(true, false, true, true) && !DevTerminalRules.MenuKeyOpens(false, false, true, false));

            // ---- T2: the lines of the terminal
            Check("terminal (T2): a line is trimmed, has no control characters and is cut at 64 characters", DevTerminalRules.Clean("  help \t") == "help" && DevTerminalRules.Clean(new string('x', 200)).Length == DevTerminalRules.MaxLine && DevTerminalRules.Clean(null) == "" && DevTerminalRules.Clean("a\u0007b") == "ab");
            Check("terminal (T2): the first word is the command, in lower case, and the rest follows", DevTerminalRules.Verb("STEAMID", out var rest1) == "steamid" && rest1 == "" && DevTerminalRules.Verb("spectate  AB12 x", out var rest2) == "spectate" && rest2 == "AB12 x" && DevTerminalRules.Verb("", out var rest3) == "" && rest3 == "");
            // the up and down arrows walk the history of three lines: up from the blank line is the newest, up again the one before, it stops at the oldest, down goes back to the blank line
            int at = -1;
            at = DevTerminalRules.HistoryStep(at, 3, -1); bool up1 = at == 2;
            at = DevTerminalRules.HistoryStep(at, 3, -1); bool up2 = at == 1;
            at = DevTerminalRules.HistoryStep(at, 3, -1); bool up3 = at == 0;
            at = DevTerminalRules.HistoryStep(at, 3, -1); bool stops = at == 0;
            at = DevTerminalRules.HistoryStep(at, 3, 1); bool down1 = at == 1;
            at = DevTerminalRules.HistoryStep(at, 3, 1); at = DevTerminalRules.HistoryStep(at, 3, 1); bool blank = at == -1;
            Check("terminal (T2): the arrows walk the history: newest first, stop at the oldest, down comes back to the blank line; no history, no line", up1 && up2 && up3 && stops && down1 && blank && DevTerminalRules.HistoryStep(-1, 0, -1) == -1);
        }
    }
}

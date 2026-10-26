using System.Collections.Generic;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch H, T29: the developer spectator (<c>DevAccessRules</c>, <c>DevCommandRules</c>). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half (two machines on the loopback) is
    /// <c>NetDevAsks1023H.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023HTest()
        {
            // ---- the list of developer accounts
            var list = DevAccessRules.ParseList("# the developers\n76561198000000001\n  76561198000000002   # the second\n\nnot a number\n-5\n0\n+7\n76561198000000001\r\n99999999999999999999999\n");
            Check("spectate (T29): the list has the two accounts (comments, blanks, junk, a negative number, zero and an overflowing number are ignored; a repeat counts once)",
                list.Count == 2 && list.Contains(76561198000000001UL) && list.Contains(76561198000000002UL), $"({list.Count} accounts)");
            Check("spectate (T29): an empty or missing list admits nobody", DevAccessRules.ParseList("").Count == 0 && DevAccessRules.ParseList(null).Count == 0 && !DevAccessRules.IsDeveloper(DevAccessRules.ParseList(null), 76561198000000001UL));
            Check("spectate (T29): an account on the list is a developer; one that is not, and account 0 (Steam is not running), are not",
                DevAccessRules.IsDeveloper(list, 76561198000000002UL) && !DevAccessRules.IsDeveloper(list, 76561198000000003UL) && !DevAccessRules.IsDeveloper(list, 0UL) && !DevAccessRules.IsDeveloper(null, 76561198000000001UL));

            // ---- the connection payload
            string player = DevAccessRules.Payload("1.0/28", 76561198000000001UL, false), watcher = DevAccessRules.Payload("1.0/28", 76561198000000001UL, true);
            DevAccessRules.ParsePayload(player, out var v1, out var c1, out var s1);
            DevAccessRules.ParsePayload(watcher, out var v2, out var c2, out var s2);
            Check("spectate (T29): a player's payload is 'version|account' (as it always was) and does not ask to spectate", player == "1.0/28|76561198000000001" && v1 == "1.0/28" && c1 == 76561198000000001UL && !s1);
            Check("spectate (T29): a spectator's payload ends with the tag and says so", watcher == "1.0/28|76561198000000001|spec" && v2 == "1.0/28" && c2 == 76561198000000001UL && s2);
            DevAccessRules.ParsePayload("", out var v3, out var c3, out var s3);
            DevAccessRules.ParsePayload("1.0/28|junk|spec", out _, out var c4, out var s4);
            DevAccessRules.ParsePayload("1.0/28|5|other", out _, out _, out var s5);
            Check("spectate (T29): an empty payload is no version, no account, no spectator; a junk account reads as 0; any other tag is no spectator", v3 == "" && c3 == 0UL && !s3 && c4 == 0UL && s4 && !s5);

            // ---- the host's verdict
            Check("spectate (T29): a developer, before Start, with room and the right version is let in",
                DevAccessRules.Judge(true, false, 0, true, true) == SpectateVerdict.Allow && DevAccessRules.Judge(true, false, DevAccessRules.MaxSpectators - 1, true, true) == SpectateVerdict.Allow);
            Check("spectate (T29): after Start nobody joins to watch (the owner: before Start only)", DevAccessRules.Judge(true, true, 0, true, true) == SpectateVerdict.RunStarted);
            Check("spectate (T29): an account that is not on the list is refused, whatever else is true, and learns nothing about the match", DevAccessRules.Judge(true, true, 99, false, false) == SpectateVerdict.NotADeveloper && DevAccessRules.Judge(true, false, 0, true, false) == SpectateVerdict.NotADeveloper);
            Check("spectate (T29): a build without the dev tools lets nobody watch", DevAccessRules.Judge(false, false, 0, true, true) == SpectateVerdict.NotInThisBuild);
            Check("spectate (T29): no more than two watch at once, and a build of another version cannot watch",
                DevAccessRules.Judge(true, false, DevAccessRules.MaxSpectators, true, true) == SpectateVerdict.TooMany && DevAccessRules.Judge(true, false, 0, false, true) == SpectateVerdict.WrongVersion);
            bool allWords = true;
            foreach (SpectateVerdict v in System.Enum.GetValues(typeof(SpectateVerdict))) if (v != SpectateVerdict.Allow && string.IsNullOrEmpty(DevAccessRules.Message(v))) allWords = false;
            Check("spectate (T29): every refusal has its words, a let-in has none", allWords && DevAccessRules.Message(SpectateVerdict.Allow) == "");

            // ---- the players are counted without the watchers
            Check("spectate (T29): a party of three with one developer watching has three players (never fewer than none)",
                DevAccessRules.PlayersAmong(4, 1) == 3 && DevAccessRules.PlayersAmong(1, 0) == 1 && DevAccessRules.PlayersAmong(1, 3) == 0 && DevAccessRules.PlayersAmong(2, -1) == 2);

            // ---- the spectator's dev menu: the host's gate
            Check("spectate (T29): the host runs a dev command only from a spectator in a build with the tools",
                DevCommandRules.Accept(true, true, DevCommand.CompleteQuota, 0, 11) && !DevCommandRules.Accept(true, false, DevCommand.CompleteQuota, 0, 11) && !DevCommandRules.Accept(false, true, DevCommand.CompleteQuota, 0, 11));
            Check("spectate (T29): cash only in the amounts the menu offers", DevCommandRules.Valid(DevCommand.Cash, 100, 11) && DevCommandRules.Valid(DevCommand.Cash, 10000, 11) && !DevCommandRules.Valid(DevCommand.Cash, 99, 11) && !DevCommandRules.Valid(DevCommand.Cash, -1000, 11) && !DevCommandRules.Valid(DevCommand.Cash, int.MaxValue, 11));
            Check("spectate (T29): a level only inside the tower", DevCommandRules.Valid(DevCommand.GoToLevel, 0, 11) && DevCommandRules.Valid(DevCommand.GoToLevel, 10, 11) && !DevCommandRules.Valid(DevCommand.GoToLevel, 11, 11) && !DevCommandRules.Valid(DevCommand.GoToLevel, -1, 11) && !DevCommandRules.Valid(DevCommand.GoToLevel, 0, 0));
            Check("spectate (T29): 'none' and a command number that is not one are no commands", !DevCommandRules.Valid(DevCommand.None, 0, 11) && !DevCommandRules.Valid((DevCommand)200, 0, 11));
            bool labels = true;
            foreach (DevCommand c in System.Enum.GetValues(typeof(DevCommand))) if (c != DevCommand.None && DevCommandRules.Label(c, c == DevCommand.Cash ? 100 : 1).Length == 0) labels = false;
            Check("spectate (T29): every command has a label on the menu", labels && DevCommandRules.Label(DevCommand.Cash, 10000) == "+$10,000" && DevCommandRules.Label(DevCommand.GoToLevel, 0) == "Basement" && DevCommandRules.Label(DevCommand.GoToLevel, 3) == "F3");
        }
    }
}

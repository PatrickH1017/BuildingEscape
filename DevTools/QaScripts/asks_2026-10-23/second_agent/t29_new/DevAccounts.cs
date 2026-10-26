using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The Steam accounts of the developers (the owner, 2026-10-23: a developer is recognised by his Steam account, from a list of Steam IDs, with no shared password): read from <c>Resources/DevAccounts.txt</c> (one Steam ID per line, a # starts a comment).
    /// The list is empty until the owner adds the developers; the terminal's <c>steamid</c> prints the number to add. Only the Editor and the friends' builds read it (<see cref="DevAccess.Compiled"/>): a release build has no list and recognises nobody.
    /// </summary>
    public static class DevAccounts
    {
#if ETR_DEV_TOOLS || UNITY_EDITOR
        static HashSet<ulong> list;
        static readonly HashSet<ulong> extra = new HashSet<ulong>();          // (accounts the tests add)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { list = null; extra.Clear(); }

        static HashSet<ulong> List => list ?? (list = Load());

        static HashSet<ulong> Load()
        {
            var text = Resources.Load<TextAsset>("DevAccounts");
            return DevAccessRules.ParseList(text ? text.text : "");
        }

        /// <summary>Is this Steam account a developer's?</summary>
        public static bool IsDeveloper(ulong steamId) => DevAccessRules.IsDeveloper(List, steamId) || (steamId != 0UL && extra.Contains(steamId));

        /// <summary>How many accounts are on the list (the terminal prints it).</summary>
        public static int Count => List.Count + extra.Count;
#if UNITY_EDITOR
        /// <summary>Tests: this account counts as a developer's until the game stops.</summary>
        public static void AddForTests(ulong steamId) { if (steamId != 0UL) extra.Add(steamId); }
#endif
#else
        public static bool IsDeveloper(ulong steamId) => false;
        public static int Count => 0;
#endif
    }
}

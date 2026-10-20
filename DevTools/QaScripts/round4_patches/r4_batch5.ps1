# Round 4, batch 5 (A6 + A8, second slice): the dock workers are the default characters, an old saved look (Party / Tiny Toon) becomes one, the cutscene casts them (the boss is the Synty boss),
# the two Security people are not offered to players (a player must not look like a guard), tests follow. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\CharacterCatalogBuilder.cs')
Rep 'role = sid.Contains("_boss_") ? ToonRole.Other : ToonRole.Player,' 'role = sid.Contains("_boss_") || sid.Contains("_security_") ? ToonRole.Other : ToonRole.Player,'
Rep '(the Synty people; attachments are not people; the bosses are not dock workers).' '(the Synty people; attachments are not people; the bosses are not dock workers and the security guards are not offered either: a player must not look like a guard).'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\PlayerAppearance.cs')
Rep 'using System.Globalization;' 'using System.Collections.Generic;
using System.Globalization;'
Rep '        /// <summary>The Tiny Toon people a player is by default, in slot order (the owner, 2026-10-17: "Make the toon the default character").</summary>
        public static readonly string[] DefaultToons = { "male_a", "female", "male_b" };

        /// <summary>The toon person a slot is by default, or null on a machine without the toon pack (the slot''s Party character stands in).</summary>
        public static string DefaultToon(int slot)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return null;
            int count = 0;
            foreach (var id in DefaultToons) if (cat.Find(id) != null) count++;
            if (count == 0) return null;
            int at = ((slot % count) + count) % count;
            foreach (var id in DefaultToons) if (cat.Find(id) != null && at-- == 0) return id;
            return null;
        }' '        /// <summary>
        /// The dock workers (the Synty office people) a player is by default, in slot order, picked to look different from one another at a glance: navy vest, red plaid, orange hoodie, purple blazer, green
        /// cardigan (the owner, 2026-10-17 night: "completely change the playable characters to those office asset pack characters"; before that "Make the toon the default character").
        /// </summary>
        public static readonly string[] DefaultToons = { "syn_business_male_02", "syn_developer_female_02", "syn_developer_male_01", "syn_business_female_03", "syn_developer_female_01" };

        /// <summary>The dock worker a slot is by default, or null on a machine without the office pack (the slot''s Party character stands in). An older catalog without the defaults offers whoever can be played.</summary>
        public static string DefaultToon(int slot)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return null;
            var ids = new List<string>();
            foreach (var id in DefaultToons) if (cat.Find(id) != null) ids.Add(id);
            if (ids.Count == 0) foreach (var c in cat.Players()) ids.Add(c.id);
            if (ids.Count == 0) return null;
            return ids[((slot % ids.Count) + ids.Count) % ids.Count];
        }

        /// <summary>
        /// A saved look whose character can no longer be played (the Party character, a Tiny Toon person: the office people are the playable characters now) becomes the first slot''s dock worker; the rest
        /// of the look (build, colours) is kept. A machine without the office pack keeps what it has.
        /// </summary>
        public static PlayerAppearance Migrated(PlayerAppearance a)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return a;
            var players = cat.Players();
            if (players.Count == 0) return a;
            foreach (var c in players) if (c.id == a.model) return a;
            a.model = DefaultToon(0);
            return a;
        }'
Rep '            return TryParse(PlayerPrefs.GetString(PrefKey, ""), out a);' '            bool ok = TryParse(PlayerPrefs.GetString(PrefKey, ""), out a);
            if (ok) a = Migrated(a);                         // (a look saved before the office people came: the Party character or a Tiny Toon person)
            return ok;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Intro\IntroCinematic.cs')
Rep '        /// <summary>The Tiny Toon people the cutscene casts, in a fixed order (the same on every machine): a Party-character player is a toon person by slot.</summary>
        static readonly string[] ToonCycle = { "male_a", "female", "male_b" };' '        /// <summary>The dock workers the cutscene casts, in a fixed order (the same on every machine): a player whose character this machine does not know is one of them by slot (the office people; the owner, 2026-10-17 night).</summary>
        static string[] ToonCycle => PlayerAppearance.DefaultToons;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Intro\IntroContent.cs')
Rep '        /// <summary>A CharacterCatalog id (a Tiny Toon person: female, male_a, male_b, police, zombie); the Party character stands in when this machine has not got it.</summary>
        public const string BossModelId = "male_b";' '        /// <summary>A CharacterCatalog id (the Synty office pack''s boss since 2026-10-17 night; before that Tiny Toon male_b); the Party character stands in when this machine has not got it.</summary>
        public const string BossModelId = "syn_boss_male_01";'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep '            Check("toon look carries the model", t.ToLook("x").model == "male_a" && PlayerAppearance.ForSlot(0).ToLook("x").model == (CharacterCatalog.Instance != null ? "male_a" : null));' '            Check("toon look carries the model", t.ToLook("x").model == "male_a" && PlayerAppearance.ForSlot(0).ToLook("x").model == (CharacterCatalog.Instance != null ? PlayerAppearance.DefaultToons[0] : null));'
Rep '            bool toonDefault = CharacterCatalog.Instance == null || CharacterCatalog.Instance.Find("male_a") != null;
            string[] want = { "male_a", "female", "male_b", "male_a", "female" };' '            bool toonDefault = CharacterCatalog.Instance == null || CharacterCatalog.Instance.Find(PlayerAppearance.DefaultToons[0]) != null;
            string[] want = PlayerAppearance.DefaultToons;'
Rep '(CharacterCatalog.Instance == null || (d.IsToon && d.model == want[i]));' '(CharacterCatalog.Instance == null || (d.IsToon && d.model == want[i % want.Length]));'
Rep '            Check("the default look is a Tiny Toon person (male_a, female, male_b, then round again), kept by the network string", toonDefault && slotsOk);' '            Check("the default look is a dock worker (the five defaults in slot order, then round again), kept by the network string", toonDefault && slotsOk);'
Rep '            ElevatorTimingTest();' '            ElevatorTimingTest();
            DockWorkersTest();'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep '        /// <summary>A1 / A2 of round 4: the elevator doors' '        /// <summary>
        /// A6 / A8 of round 4 (the owner, 2026-10-17 night): the playable characters are the Synty office people, called "Dock worker 1", "Dock worker 2" and so on; the defaults, the migration of an old saved look,
        /// the cutscene cast and the boss follow. Needs the character catalog (Employees Take Revenge > Setup Characters) built with the Synty pack imported.
        /// </summary>
        public static void DockWorkersTest()
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) { Check("dock workers: the character catalog exists", false); return; }
            var players = cat.Players();
            bool allOffice = players.Count >= 5, atlas = true, numbered = true, noGuardLooks = true, fits = true;
            for (int i = 0; i < players.Count; i++)
            {
                var c = players[i];
                allOffice &= c.id.StartsWith("syn_") && c.prefab != null;
                atlas &= c.atlas != null;
                numbered &= cat.DockLabel(c) == "Dock worker " + (i + 1);
                noGuardLooks &= !c.id.Contains("security") && !c.id.Contains("boss");
                var a = PlayerAppearance.ForSlot(0); a.model = c.id;
                fits &= a.Serialize().Length <= 61 && CharacterCatalog.ValidId(c.id);
            }
            Check($"dock workers: the {players.Count} playable characters are all Synty office people with their own atlas", allOffice && atlas);
            Check("dock workers: Customize calls them Dock worker 1, Dock worker 2 ... in order, and nobody looks like a guard or the boss", numbered && noGuardLooks);
            Check("dock workers: every id fits the look string that is sent over the network", fits);
            bool defaultsPlayable = PlayerAppearance.DefaultToons.Length >= 5 && PlayerAppearance.DefaultToons.Distinct().Count() == PlayerAppearance.DefaultToons.Length;
            foreach (var id in PlayerAppearance.DefaultToons) defaultsPlayable &= players.Any(p => p.id == id);
            for (int s = 0; s < 5; s++) defaultsPlayable &= PlayerAppearance.ForSlot(s).model == PlayerAppearance.DefaultToons[s];
            Check("dock workers: the five default slots are five different playable dock workers", defaultsPlayable);
            var oldToon = PlayerAppearance.ForSlot(2); oldToon.model = "male_a";
            var party = PlayerAppearance.ForSlot(2); party.model = null;
            var chosen = PlayerAppearance.ForSlot(2); chosen.model = players[players.Count - 1].id;
            Check("dock workers: an old saved look (a Tiny Toon person, the Party character) becomes the first dock worker, a chosen dock worker stays",
                PlayerAppearance.Migrated(oldToon).model == PlayerAppearance.DefaultToons[0] && PlayerAppearance.Migrated(party).model == PlayerAppearance.DefaultToons[0] && PlayerAppearance.Migrated(chosen).model == chosen.model
                && PlayerAppearance.Migrated(party).height == party.height && PlayerAppearance.Migrated(party).body.Equals(party.body));
            var boss = cat.Find(IntroContent.BossModelId);
            Check("dock workers: the cutscene''s boss is the Synty boss and no player can be him", boss != null && boss.id == "syn_boss_male_01" && boss.role != ToonRole.Player && !players.Any(p => p.id == boss.id));
            bool cast = true; foreach (var id in PlayerAppearance.DefaultToons) cast &= cat.Find(id) != null;
            Check("dock workers: the old Tiny Toon people stay in the catalog (old saved looks, the guards, the zombie)", cat.Find("male_a") != null && cat.Find("police") != null && cat.Find("zombie") != null && cat.FirstWithRole(ToonRole.Guard) != null && cast);
        }

        /// <summary>A1 / A2 of round 4: the elevator doors'
Rep 'using UnityEngine;' 'using System.Linq;
using UnityEngine;'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevToon.cs')
Rep '            foreach (var c in cat.Players())
            {
                var a = PlayerAppearance.ForSlot(0);' '            foreach (var c in cat.Players().Take(3))
            {
                var a = PlayerAppearance.ForSlot(0);'
Rep '            ToonCheck("stage shows 3 Tiny Toon people + 1 Party character", anims.Length == 4 && clipModes == 3,' '            ToonCheck("stage shows 3 dock workers + 1 Party character", anims.Length == 4 && clipModes == 3,'
Rep '            var chars = cat.characters.ToList();' '            // (a spread of the catalog, not all of it: the dock workers, the old toon people, the guard and the boss would not fit one picture)
            var pick = new[] { "police", "male_a", "female", "zombie", "syn_business_female_01", "syn_business_male_02", "syn_cleaner_female_01", "syn_cleaner_male_01", "syn_developer_female_02", "syn_developer_male_01", "syn_security_male_01", "syn_boss_male_01" };
            var chars = pick.Select(pid => cat.characters.FirstOrDefault(x => x != null && x.id == pid && x.prefab)).Where(x => x != null).ToList();
            const float gap = 1.15f;'
Rep '                host.transform.localPosition = new Vector3((i - chars.Count / 2f) * 1.7f, 0f, 0f);' '                host.transform.localPosition = new Vector3((i - chars.Count / 2f) * gap, 0f, 0f);'
Rep '            cam.transform.localPosition = new Vector3(0f, 0.9f, -13f);' '            cam.transform.localPosition = new Vector3(0f, 0.9f, -((chars.Count + 1) * gap + 2f) / 1.0195f);'
Rep 'm.mainTexture == cat.atlas));' 'm.mainTexture == (c.atlas ? c.atlas : cat.atlas)));'
Save-Src
Write-Output 'round 4 batch 5 patched'

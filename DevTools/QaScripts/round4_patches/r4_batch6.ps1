# Round 4, batch 6 (A6 + A8, the owner's pictures): Dock worker 1-4 are the four characters the owner showed (the man with the cap, the blonde woman in dark grey, the man with glasses and
# headphones, the man with the red beard and the vest), the first four defaults too; "you can let people choose between most of them for now (not officers of course)": every other Synty person
# is offered as well, except the two security guards (a player must not look like a guard) and the cutscene's boss. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\CharacterCatalogBuilder.cs')
Rep '        /// <summary>"SM_Chr_Business_Female_01" -> "syn_business_female_01" (the Synty people; attachments are not people; the bosses are not dock workers and the security guards are not offered either: a player must not look like a guard).</summary>' '        /// <summary>
        /// The four characters the owner showed in four pictures (2026-10-17 night, "The Dock workers should be these character models"): the man with the cap (Cleaner Male 01), the blonde woman in dark grey
        /// (Boss Female 01), the man with glasses and headphones (Developer Male 02), the man with the red beard and the vest (Business Male 02). In the order of the pictures they are Dock worker 1 to 4
        /// and the first four default looks (PlayerAppearance.DefaultToons).
        /// </summary>
        public static readonly string[] DockWorkerIds = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02" };

        /// <summary>
        /// Who of the Synty people a player may be: everybody but the security guards (the owner: "let people choose between most of them for now (not officers of course)": a player must not look like
        /// a guard) and the boss of the cutscene (a player must not be him).
        /// </summary>
        static ToonRole SyntyRole(string id) => id.Contains("_security_") || id == IntroContent.BossModelId ? ToonRole.Other : ToonRole.Player;

        /// <summary>"SM_Chr_Business_Female_01" -> "syn_business_female_01" (the Synty people; attachments are not people).</summary>'
Rep 'role = sid.Contains("_boss_") || sid.Contains("_security_") ? ToonRole.Other : ToonRole.Player,' 'role = SyntyRole(sid),'
Rep '            list = list.OrderBy(c => c.role).ThenBy(c => c.id).ToList();' '            // (the four dock workers the owner showed come first, in the order of his pictures; then everybody else by id)
            list = list.OrderBy(c => c.role).ThenBy(c => { int at = System.Array.IndexOf(DockWorkerIds, c.id); return at < 0 ? int.MaxValue : at; }).ThenBy(c => c.id).ToList();'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\PlayerAppearance.cs')
Rep '        /// <summary>
        /// The dock workers (the Synty office people) a player is by default, in slot order, picked to look different from one another at a glance: navy vest, red plaid, orange hoodie, purple blazer, green
        /// cardigan (the owner, 2026-10-17 night: "completely change the playable characters to those office asset pack characters"; before that "Make the toon the default character").
        /// </summary>
        public static readonly string[] DefaultToons = { "syn_business_male_02", "syn_developer_female_02", "syn_developer_male_01", "syn_business_female_03", "syn_developer_female_01" };' '        /// <summary>
        /// The dock workers (the Synty office people) a player is by default, in slot order: the four characters the owner showed (the man with the cap, the blonde woman in dark grey, the man with glasses
        /// and headphones, the man with the red beard and the vest) and the woman cleaner from his first picture (2026-10-17 night: "The Dock workers should be these character models"; before that
        /// "Make the toon the default character").
        /// </summary>
        public static readonly string[] DefaultToons = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02", "syn_cleaner_female_01" };'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep 'bool allOffice = players.Count >= 5, atlas = true, numbered = true, noGuardLooks = true, fits = true;' 'bool allOffice = players.Count >= 12, atlas = true, numbered = true, noGuardLooks = true, fits = true;'
Rep 'noGuardLooks &= !c.id.Contains("security") && !c.id.Contains("boss");' 'noGuardLooks &= !c.id.Contains("security") && c.id != IntroContent.BossModelId;'
Rep '            Check("dock workers: Customize calls them Dock worker 1, Dock worker 2 ... in order, and nobody looks like a guard or the boss", numbered && noGuardLooks);' '            Check("dock workers: Customize calls them Dock worker 1, Dock worker 2 ... in order, and nobody looks like a guard (security) or the cutscene''s boss", numbered && noGuardLooks);
            bool four = players.Count >= 4;
            for (int i = 0; four && i < 4; i++) four &= players[i].id == CharacterCatalogBuilder.DockWorkerIds[i] && PlayerAppearance.DefaultToons[i] == players[i].id;
            Check("dock workers: Dock worker 1-4 are the four characters the owner showed (cap, blonde woman, glasses + headphones, vest) and the first four defaults", four);
            Check("dock workers: the security guards are not offered to players, but the pack''s other people are (\"most of them\")", !players.Any(p => p.id.Contains("security")) && cat.Find("syn_security_male_01") != null && cat.Find("syn_security_female_01") != null && players.Any(p => p.id == "syn_business_female_01") && players.Any(p => p.id == "syn_developer_male_01"));'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevToon.cs')
Rep 'var pick = new[] { "police", "male_a", "female", "zombie", "syn_business_female_01", "syn_business_male_02", "syn_cleaner_female_01", "syn_cleaner_male_01", "syn_developer_female_02", "syn_developer_male_01", "syn_security_male_01", "syn_boss_male_01" };' 'var pick = new[] { "police", "male_a", "female", "zombie", "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02", "syn_cleaner_female_01", "syn_business_female_01", "syn_security_male_01", "syn_boss_male_01" };'
Save-Src
Write-Output 'round 4 batch 6 patched'

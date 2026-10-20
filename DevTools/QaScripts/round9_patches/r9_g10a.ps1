# Round 9, batch G10a (the owner's ladder of unlocks, 2026-10-19): the pure rules.
#  - GunMagazine: at most three clips (the owner: "A player should be allowed to have three gun clips max"); GunRules.MaxClips
#  - RunUnlocks: a gun found and carried back to the docks unlocks its ammunition at the dealer for the party; the docks' clock in HeistManager collects, RunSync mirrors the mask
#  - SafeRules: which gun a weapon safe holds (the guns by floor, the first safe of an unlock floor always holds the new gun), the clip's kind
#  - AmmoClip: a clip is for one kind of gun
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

# ---- the magazine's cap
Open-Src "$Src\Guns\GunRules.cs"
Rep @'
        public int Reserve { get; private set; }

        public int Rounds { get; private set; }
'@ @'
        public int Reserve { get; private set; }

        /// <summary>
        /// The most rounds a player may carry for this gun, the clip in it counted (the owner, 2026-10-19: "A player should be allowed to have three gun clips max"): <see cref="GunRules.MaxClips"/> clips of it. A gun with no
        /// limit (an officer's, whose reserve is endless anyway) keeps the default.
        /// </summary>
        public int MaxRounds = int.MaxValue;
        /// <summary>How many more rounds can be taken before the limit: what is in the gun and in the reserve counts. None for a gun with an endless reserve.</summary>
        public int Room => InfiniteReserve ? 0 : Math.Max(0, MaxRounds - Rounds - Reserve);

        public int Rounds { get; private set; }
'@
Rep @'
        public int AddReserve(int rounds)
        {
            if (InfiniteReserve || rounds <= 0) return 0;
            Reserve += rounds;
            return rounds;
        }
'@ @'
        public int AddReserve(int rounds)
        {
            if (InfiniteReserve || rounds <= 0) return 0;
            rounds = Math.Min(rounds, Room);                                  // (no more than the limit: the rest is not taken)
            Reserve += rounds;
            return rounds;
        }
'@
Rep @'
        /// <summary>Ammunition is unlimited in the basement (the Loading Docks, floor 0); on every other floor a magazine is a magazine, and it is full again each time a floor starts.</summary>
'@ @'
        /// <summary>The most clips of a gun a player may carry, the one in the gun included (the owner, 2026-10-19: "A player should be allowed to have three gun clips max").</summary>
        public const int MaxClips = 3;

        /// <summary>Ammunition is unlimited in the basement (the Loading Docks, floor 0); on every other floor a magazine is a magazine, and it is full again each time a floor starts.</summary>
'@
Save-Src

Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve((spareClips < 0 ? s.spareClips : spareClips) * s.magazine);
            return m;
'@ @'
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve((spareClips < 0 ? s.spareClips : spareClips) * s.magazine);
            m.MaxRounds = GunRules.MaxClips * s.magazine;                    // (a player carries at most three clips)
            return m;
'@
Rep 'unlockFloor = 3, ammoName = "Pistol Clip", clipPrice = 240,' 'unlockFloor = 3, ammoName = "Pistol Clip", clipPrice = 120,'
Save-Src

# ---- the unlock state
$t = [IO.File]::ReadAllText('C:\bqs\gun_draft\RunUnlocks.cs').Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
$target = "$Src\Guns\RunUnlocks.cs"
if ($env:R2_DRY) { Write-Output "dry run: would write $target" } else { [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false))); Write-Output "written $target" }

Open-Src "$Src\HeistManager.cs"
Rep @'
        void Update()
        {
            if (Phase != HeistPhase.Heist) return;
            if (Net.NetRole.Simulates)
'@ @'
        float unlockClock;

        /// <summary>
        /// In the docks, twice a second (the host, or offline): every gun a player carries back to the Loading Docks is unlocked for the party (<see cref="RunUnlocks"/>: the dealer then sells its ammunition).
        /// </summary>
        void TickUnlocks(float dt)
        {
            if (!Net.NetRole.Simulates) return;
            unlockClock += dt;
            if (unlockClock < 0.5f) return;
            unlockClock = 0f;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.TryGetComponent(out Backpack bp)) RunUnlocks.CollectFrom(bp);
        }

        void Update()
        {
            if (Phase == HeistPhase.Hub) { TickUnlocks(Time.deltaTime); return; }
            if (Phase != HeistPhase.Heist) return;
            if (Net.NetRole.Simulates)
'@
Save-Src

Open-Src "$Src\TowerManager.cs"
Rep @'
            SafeIntel.Reset();      // a new run: nothing is known about any safe yet
'@ @'
            SafeIntel.Reset();      // a new run: nothing is known about any safe yet
            RunUnlocks.Reset();     // ...and nothing is unlocked at the dealer's yet (a gun found and brought back unlocks its ammunition)
'@
Save-Src

Open-Src "$Src\Net\RunSync.State.cs"
Rep @'
        readonly NetworkVariable<int> vDestination = new(-1);
'@ @'
        readonly NetworkVariable<int> vDestination = new(-1);
        // the guns the party has found and brought back (RunUnlocks): the host decides, everybody's dealer reads it
        readonly NetworkVariable<int> vUnlocks = new();
'@
Rep @'
            vBackOpen.Value = ElevatorExtraction.BackDoorOpen;
'@ @'
            vBackOpen.Value = ElevatorExtraction.BackDoorOpen;
            vUnlocks.Value = RunUnlocks.Mask;
'@
Rep @'
        void ClientApplyState()
        {
            var hm = HeistManager.Instance;
'@ @'
        void ClientApplyState()
        {
            RunUnlocks.NetSet(vUnlocks.Value);                    // (not tied to a floor: what was brought back stays unlocked)
            var hm = HeistManager.Instance;
'@
Save-Src

# ---- a clip is for one kind of gun
Open-Src "$Src\Outpost\AmmoClip.cs"
Rep @'
        /// <summary>The rounds it adds to the pew's reserve (a clip of the pew: the same as the dealer's).</summary>
        public int rounds = 8;
'@ @'
        /// <summary>The rounds it adds to the reserve of the gun it is for (a clip of that gun: the same as the dealer's).</summary>
        public int rounds = 8;
        /// <summary>Which gun it is for (since 2026-10-19 a clip fits one kind of gun: the pew's by default).</summary>
        public RangedWeapon.Kind kind = RangedWeapon.Kind.Pew;
'@
Save-Src

# ---- the safes: which gun
Open-Src "$Src\Outpost\SafeRules.cs"
Rep @'
        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@ @'
        // ------------------------------------------------------------------ which gun (the owner's ladder of unlocks, 2026-10-19)

        const int SaltGun = 41;
        /// <summary>How likely each gun is in a weapon safe, before the floor changes it: the pew, the sub machine gun, the rifle, the shotgun, the sniper rifle (the order of <see cref="RangedWeapon.Kind"/>).</summary>
        static readonly int[] GunBaseWeight = { 24, 30, 20, 20, 10 };

        /// <summary>The number of a gun among what a safe holds: <see cref="PewItem"/> plus the gun's kind (the pew 3, the sub machine gun 4, the rifle 5, the shotgun 6, the sniper rifle 7).</summary>
        public static int GunItem(RangedWeapon.Kind kind) => PewItem + (int)kind;
        public static bool IsGunItem(int item) => item >= PewItem && item < PewItem + RangedWeapon.Specs.Length;
        public static RangedWeapon.Kind GunOfItem(int item) => (RangedWeapon.Kind)(item - PewItem);

        /// <summary>
        /// How likely each gun is to be the one a weapon safe on this floor holds, by kind (the owner, 2026-10-19: the floors that unlock the guns are theirs - the pew 3, the sub machine gun 5, the shotgun 6, the assault rifle 8, the
        /// sniper rifle 10, <see cref="RangedWeapon.Spec.unlockFloor"/>): nothing for a gun before its floor, its base weight after, three times that on its own floor (the new gun is the likeliest one there).
        /// </summary>
        public static int[] GunWeights(int floor)
        {
            var w = new int[RangedWeapon.Specs.Length];
            for (int k = 0; k < w.Length; k++)
            {
                int unlock = RangedWeapon.Specs[k].unlockFloor;
                if (floor < unlock) continue;
                w[k] = GunBaseWeight[k] * (floor == unlock ? 3 : 1);
            }
            return w;
        }

        /// <summary>The kind of gun that is first found on this floor, or -1 when no gun is.</summary>
        public static int GunUnlockedOn(int floor)
        {
            for (int k = 0; k < RangedWeapon.Specs.Length; k++) if (RangedWeapon.Specs[k].unlockFloor == floor) return k;
            return -1;
        }

        /// <summary>
        /// The gun in slot <paramref name="slot"/> of weapon safe <paramref name="index"/> on this floor: the floor that unlocks a gun always has it in its first safe (the one whose code is always on record, so the team can fetch it);
        /// every other safe rolls by <see cref="GunWeights"/>. The same on every machine.
        /// </summary>
        public static RangedWeapon.Kind PickGun(int runSeed, int floor, int index, int slot)
        {
            if (index == 0 && slot == 0)
            {
                int u = GunUnlockedOn(floor);
                if (u > 0) return (RangedWeapon.Kind)u;                                  // (the pew's own floor is decided by the weights: it is the only gun there)
            }
            var w = GunWeights(floor);
            long total = 0;
            for (int k = 0; k < w.Length; k++) total += w[k];
            if (total <= 0) return RangedWeapon.Kind.Pew;
            long roll = Hash(runSeed, floor, SaltGun + index * 131 + slot * 17) % (uint)total;
            for (int k = 0; k < w.Length; k++)
            {
                if (roll < w[k]) return (RangedWeapon.Kind)k;
                roll -= w[k];
            }
            return RangedWeapon.Kind.Pew;
        }

        /// <summary>The kind of the spare clip that may lie in this weapon safe: for the gun the safe holds. The pew's when the safe holds nothing.</summary>
        public static RangedWeapon.Kind ClipKindOf(int runSeed, int floor, int index)
        {
            var items = WeaponContents(runSeed, floor, index);
            return items.Length > 0 && IsGunItem(items[0]) ? GunOfItem(items[0]) : RangedWeapon.Kind.Pew;
        }

        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@
Rep @'
        /// <summary>
        /// What a weapon safe holds, as item numbers (<see cref="PewItem"/>): ONE pew pistol (<see cref="WeaponsPerSafe"/>; the owner, 2026-10-18: "each safe should be one weapon max"). The pew is the only gun there is, and a melee
        /// weapon is longer than the safe is wide, so a safe holds guns only. Nothing for a safe on a floor with cash, or one that does not exist.
        /// </summary>
        public static int[] WeaponContents(int runSeed, int floor, int index)
        {
            if (!IsWeaponFloor(floor) || index < 0 || index >= SafeCount(runSeed, floor)) return new int[0];
            var items = new int[WeaponsPerSafe];
            for (int i = 0; i < items.Length; i++) items[i] = PewItem;
            return items;
        }
'@ @'
        /// <summary>
        /// What a weapon safe holds, as item numbers (<see cref="GunItem"/>): ONE gun (<see cref="WeaponsPerSafe"/>; the owner, 2026-10-18: "each safe should be one weapon max"), which one follows the floor (<see cref="PickGun"/>: floors 3 and
        /// 4 hold the pew only, as before). A melee weapon is longer than the safe is wide, so a safe holds guns only. Nothing for a safe on a floor with cash, or one that does not exist.
        /// </summary>
        public static int[] WeaponContents(int runSeed, int floor, int index)
        {
            if (!IsWeaponFloor(floor) || index < 0 || index >= SafeCount(runSeed, floor)) return new int[0];
            var items = new int[WeaponsPerSafe];
            for (int i = 0; i < items.Length; i++) items[i] = GunItem(PickGun(runSeed, floor, index, i));
            return items;
        }
'@
Save-Src

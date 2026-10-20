# Round 11, batch G01: armour (the owner's queue of 2026-10-19: Light / Medium / Heavy Armour found in the safes of floors 3 / 7 / 10 instead of a weapon, bought from the dealer by the whole party once its finder is back in the docks;
# worn while in the pack; a guard's bullet knocks the wearer down for less time).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

function NewFile([string]$target, [string]$draft) {
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target"; return }
  [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "written $target"
}
NewFile "$Src\Guns\ArmourRules.cs" 'C:\bqs\r11_draft\ArmourRules.cs'
NewFile "$Src\Guns\ArmourItem.cs" 'C:\bqs\r11_draft\ArmourItem.cs'

# ---- the vests: the materials are shared with the armour lying about
Open-Src "$Src\GuardArmour.cs"
Rep @'
        /// <summary>
        /// Puts a vest of this tier on the chest of this officer's model (on its chest bone) and returns it (named "BodyArmour"), or null when there is no model or no shield. <paramref name="root"/> is the officer (it gives the way he faces).
'@ @'
        /// <summary>The cloth of a vest of this tier (also of the armour lying about, <see cref="ArmourItem"/>).</summary>
        public static Material VestMaterial(ShieldTier tier)
        {
            int t = (int)tier;
            switch (tier)
            {
                case ShieldTier.Light: return Make(vestMats, t, new Color(0.20f, 0.26f, 0.38f));
                case ShieldTier.Heavy: return Make(vestMats, t, new Color(0.14f, 0.13f, 0.12f));
                default: return Make(vestMats, t, new Color(0.10f, 0.12f, 0.17f));
            }
        }

        /// <summary>The plates of a vest of this tier (a light one has none: its cloth).</summary>
        public static Material PlateMaterial(ShieldTier tier)
        {
            int t = (int)tier;
            switch (tier)
            {
                case ShieldTier.Light: return VestMaterial(tier);
                case ShieldTier.Heavy: return Make(plateMats, t, new Color(0.80f, 0.55f, 0.12f));
                default: return Make(plateMats, t, new Color(0.32f, 0.34f, 0.38f));
            }
        }

        /// <summary>
        /// Puts a vest of this tier on the chest of this officer's model (on its chest bone) and returns it (named "BodyArmour"), or null when there is no model or no shield. <paramref name="root"/> is the officer (it gives the way he faces).
'@
Rep @'
                    var cloth = Make(vestMats, t, new Color(0.20f, 0.26f, 0.38f));
'@ @'
                    var cloth = VestMaterial(tier);
'@
Rep @'
                    var dark = Make(vestMats, t, new Color(0.14f, 0.13f, 0.12f));
                    var plate = Make(plateMats, t, new Color(0.80f, 0.55f, 0.12f));
'@ @'
                    var dark = VestMaterial(tier);
                    var plate = PlateMaterial(tier);
'@
Rep @'
                    var dark = Make(vestMats, t, new Color(0.10f, 0.12f, 0.17f));
                    var plate = Make(plateMats, t, new Color(0.32f, 0.34f, 0.38f));
'@ @'
                    var dark = VestMaterial(tier);
                    var plate = PlateMaterial(tier);
'@
Rep @'
            int t = (int)tier;
            switch (tier)
            {
                case ShieldTier.Light:
                {
                    var cloth
'@ @'
            switch (tier)
            {
                case ShieldTier.Light:
                {
                    var cloth
'@
Save-Src

# ---- the safes: armour instead of a gun
Open-Src "$Src\Outpost\SafeRules.cs"
Rep @'
            for (int i = 0; i < items.Length; i++) items[i] = GunItem(PickGun(runSeed, floor, index, i));
'@ @'
            for (int i = 0; i < items.Length; i++) items[i] = HoldsArmour(runSeed, floor, index, i) ? ArmourItemNumber(PickArmour(runSeed, floor, index, i)) : GunItem(PickGun(runSeed, floor, index, i));
'@
Rep @'
        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@ @'
        // ------------------------------------------------------------------ armour instead of a gun (the owner, 2026-10-19)

        const int SaltArmour = 43, SaltArmourTier = 47;
        /// <summary>Armour among what a safe holds: <see cref="ArmourItemBase"/> plus the tier's number (Light Armour 21, Medium 22, Heavy 23).</summary>
        public const int ArmourItemBase = 20;
        public static int ArmourItemNumber(ShieldTier tier) => ArmourItemBase + (int)tier;
        public static bool IsArmourItem(int item) => item > ArmourItemBase && item <= ArmourItemBase + 3;
        public static ShieldTier ArmourOfItem(int item) => (ShieldTier)(item - ArmourItemBase);

        /// <summary>
        /// Does this weapon safe hold a piece of armour instead of a gun (the owner, 2026-10-19: "On the third floor players should have the chance of finding Light Armour instead of a weapon in the safe", the same for the Medium Armour found on
        /// the 7th and the Heavy Armour found on the 10th floor)? Never the first safe of a floor (a gun is always in it and its code is always on record) and never before floor 3; the chance is <see cref="ArmourRules.SafePercent"/>.
        /// The same on every machine.
        /// </summary>
        public static bool HoldsArmour(int runSeed, int floor, int index, int slot = 0)
        {
            if (!WeaponSafeExists(runSeed, floor, index) || index == 0) return false;
            int percent = ArmourRules.SafePercent(floor);
            return percent > 0 && Hash(runSeed, floor, SaltArmour + index * 137 + slot * 19) % 100U < (uint)percent;
        }

        /// <summary>The tier of the armour such a safe holds, by <see cref="ArmourRules.TierWeights"/> (the tier that is first found on the floor is three times as likely there).</summary>
        public static ShieldTier PickArmour(int runSeed, int floor, int index, int slot)
        {
            var w = ArmourRules.TierWeights(floor);
            long total = 0;
            for (int t = 1; t < w.Length; t++) total += w[t];
            if (total <= 0) return ShieldTier.Light;
            long roll = Hash(runSeed, floor, SaltArmourTier + index * 139 + slot * 23) % (uint)total;
            for (int t = 1; t < w.Length; t++)
            {
                if (roll < w[t]) return (ShieldTier)t;
                roll -= w[t];
            }
            return ShieldTier.Light;
        }

        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)
'@
Rep @'
            WeaponSafeExists(runSeed, floor, index) && Hash(runSeed, floor, SaltSideClip + index * 113) % 100U < (uint)SideClipPercent;
'@ @'
            WeaponSafeExists(runSeed, floor, index) && !HoldsArmour(runSeed, floor, index) && Hash(runSeed, floor, SaltSideClip + index * 113) % 100U < (uint)SideClipPercent;
'@
Save-Src

Open-Src "$Src\Outpost\FloorSafe.cs"
Rep @'
            for (int i = 0; i < n; i++)
            {
                if (!SafeRules.IsGunItem(items[i])) continue;
'@ @'
            for (int i = 0; i < n; i++)
            {
                if (SafeRules.IsArmourItem(items[i]))
                {
                    // armour instead of a gun (the owner, 2026-10-19): a piece lying flat in the hollow, switched off with its network id taken, like the guns
                    var armourTier = SafeRules.ArmourOfItem(items[i]);
                    var armourAt = new Vector3((i - (n - 1) * 0.5f) * GunSpacing, WallThickness + ArmourItem.Size(armourTier).y * 0.5f + 0.004f, -0.01f);
                    var piece = ArmourItem.Create(armourTier, transform.TransformPoint(armourAt), parent, transform.eulerAngles.y - 90f);
                    piece.SetActive(false);
                    guns.Add(piece);
                    continue;
                }
                if (!SafeRules.IsGunItem(items[i])) continue;
'@
Save-Src

# ---- the unlock: armour found and brought back to the docks
Open-Src "$Src\Guns\RunUnlocks.cs"
Rep @'
        /// <summary>A new run: nothing is unlocked.</summary>
'@ @'
        /// <summary>The bit of an armour tier (from bit 8: the guns have the first eight).</summary>
        static int ArmourBit(ShieldTier tier) => 1 << (8 + (int)tier);

        /// <summary>Has this tier of armour been found and brought back to the docks in this run (the dealer sells it to everybody then)?</summary>
        public static bool HasArmour(ShieldTier tier) => tier != ShieldTier.None && (mask & ArmourBit(tier)) != 0;

        /// <summary>Unlock a tier of armour. True when it was not unlocked before.</summary>
        public static bool UnlockArmour(ShieldTier tier)
        {
            if (tier == ShieldTier.None) return false;
            int bit = ArmourBit(tier);
            if ((mask & bit) != 0) return false;
            mask |= bit;
            return true;
        }

        /// <summary>A new run: nothing is unlocked.</summary>
'@
Rep @'
            foreach (var item in pack.Items)
                if (item && item.TryGetComponent(out RangedWeapon gun) && Unlock(gun.kind)) added++;
            return added;
'@ @'
            foreach (var item in pack.Items)
            {
                if (!item) continue;
                if (item.TryGetComponent(out RangedWeapon gun) && Unlock(gun.kind)) added++;
                else if (item.TryGetComponent(out ArmourItem armour) && UnlockArmour(armour.tier)) added++;      // (armour found and brought back: the dealer sells that tier to everybody, the owner, 2026-10-19)
            }
            return added;
'@
Save-Src

# ---- the dealer: three more products, on offer once their tier is unlocked
Open-Src "$Src\Vendor.cs"
Rep @'
            public bool isAmmo;
            public int rounds;
        }
'@ @'
            public bool isAmmo;
            public int rounds;
            /// <summary>A piece of armour (the owner, 2026-10-19): found on the floors, sold once its tier was brought back to the docks (<see cref="RunUnlocks.HasArmour"/>).</summary>
            public bool isArmour;
            public ShieldTier armour;
        }
'@
Rep @'
rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].magazine },
        };
'@ @'
rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].magazine },
            // the armour (the owner, 2026-10-19): found in the safes of floors 3 / 7 / 10, sold to the whole party once its finder is back in the docks; worn while it is in the pack
            new Product { name = "Light Armour", blurb = "armour: a guard's bullet knocks you down for 75% of the time", price = ArmourRules.Price(ShieldTier.Light), isArmour = true, armour = ShieldTier.Light },
            new Product { name = "Medium Armour", blurb = "armour: a guard's bullet knocks you down for 50% of the time", price = ArmourRules.Price(ShieldTier.Medium), isArmour = true, armour = ShieldTier.Medium },
            new Product { name = "Heavy Armour", blurb = "armour: a guard's bullet knocks you down for 30% of the time", price = ArmourRules.Price(ShieldTier.Heavy), isArmour = true, armour = ShieldTier.Heavy },
        };
'@
Rep @'
            if (p.isAmmo) return RunUnlocks.Has(p.gun) || (p.gun == RangedWeapon.Kind.Pew && PewForSale(resume));
            return true;
'@ @'
            if (p.isAmmo) return RunUnlocks.Has(p.gun) || (p.gun == RangedWeapon.Kind.Pew && PewForSale(resume));
            if (p.isArmour) return RunUnlocks.HasArmour(p.armour);                // (found and brought back to the docks)
            return true;
'@
Rep @'
        static float ItemWeight(Product p) => p.isGun ?
'@ @'
        static float ItemWeight(Product p) => p.isArmour ? ArmourRules.Weight(p.armour) : p.isGun ?
'@
Rep @'
            float wgt = ItemWeight(p);
'@ @'
            if (p.isArmour && PlayerArmour.Wears(buyer)) return ArmourRules.AlreadyWearing;
            float wgt = ItemWeight(p);
'@
Rep @'
        GameObject SpawnAt(Product p, Vector3 pos, float yaw)
        {
            if (p.isGun) return RangedWeapon.Create(p.gun, pos, transform.parent, yaw);
'@ @'
        GameObject SpawnAt(Product p, Vector3 pos, float yaw)
        {
            if (p.isArmour) return ArmourItem.Create(p.armour, pos, transform.parent, yaw);
            if (p.isGun) return RangedWeapon.Create(p.gun, pos, transform.parent, yaw);
'@
Save-Src

# ---- the pack: one piece of armour at a time
Open-Src "$Src\Backpack.cs"
Rep @'
            why = null;
            if (Weight + p.weight <= capacity + 0.001f) return true;
'@ @'
            why = null;
            if (p.TryGetComponent(out ArmourItem _) && PlayerArmour.Wears(this)) { why = ArmourRules.AlreadyWearing; return false; }        // (one piece of armour at a time)
            if (Weight + p.weight <= capacity + 0.001f) return true;
'@
Save-Src

# ---- what armour does: a guard's bullet knocks the wearer down for less time and shoves him less
Open-Src "$Src\Guns\GuardGun.cs"
Rep @'
            imp.player.Stumble(dir * knockback + Vector3.up * 2.5f, stumbleSeconds);
'@ @'
            var worn = PlayerArmour.TierOf(imp.player);                      // (armour: the bullet knocks him down for less time and shoves him less - the owner has not said what armour does, these are my numbers)
            imp.player.Stumble((dir * knockback + Vector3.up * 2.5f) * ArmourRules.HitShare(worn), ArmourRules.StumbleSeconds(stumbleSeconds, worn));
'@
Save-Src

Open-Src "$Src\WobblyPlayer.cs"
Rep @'
            if (usePartyCharacter && !GetComponent<PlayerVisual>()) gameObject.AddComponent<PlayerVisual>();
'@ @'
            if (usePartyCharacter && !GetComponent<PlayerVisual>()) gameObject.AddComponent<PlayerVisual>();
            if (!GetComponent<PlayerArmourVisual>()) gameObject.AddComponent<PlayerArmourVisual>();          // (the vest of the armour he wears: every machine reads his pack)
'@
Save-Src

# ---- the F1 menu: armour on, and the dealer sells it from then on
Open-Src "$Src\TestMode.cs"
Rep @'
void GivePew(Backpack pack) => GiveGun(pack, RangedWeapon.Kind.Pew);
'@ @'
void GiveArmour(Backpack pack, ShieldTier tier)
        {
            foreach (var held in pack.Snapshot())
                if (held && held.TryGetComponent(out ArmourItem _)) { pack.Drop(held); Destroy(held.gameObject); }          // (one piece at a time: the old one goes)
            if (tier == ShieldTier.None) { Say("No armour on you"); return; }
            var go = ArmourItem.Create(tier, SpawnPoint(pack), null, 0f);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            if (!pack.TryStore(go.GetComponent<Pickup>())) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            RunUnlocks.UnlockArmour(tier);                                 // (the dealer sells it now: it was "found")
            Say(ArmourRules.Name(tier) + " on you: a guard's bullet knocks you down for " + Mathf.RoundToInt(ArmourRules.HitShare(tier) * 100f) + "% of the time");
        }

        void GivePew(Backpack pack) => GiveGun(pack, RangedWeapon.Kind.Pew);
'@
Rep @'
if (GUILayout.Button("Show the blast of a shotgun or sniper hit
'@ @'
GUILayout.Label("Armour on you (the dealer sells that tier from then on):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("none")) GiveArmour(pack, ShieldTier.None);
            foreach (var at in new[] { ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
                if (GUILayout.Button(ArmourRules.Name(at))) GiveArmour(pack, at);
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Show the blast of a shotgun or sniper hit
'@
Save-Src

# ---- the protocol: armour in the safes and at the dealer
Open-Src "$Src\Net\NetConst.cs"
Rep @'
public const string ProtocolVersion = "22";   // 22:
'@ @'
public const string ProtocolVersion = "23";   // 23: armour (a safe may hold a piece of it instead of a gun, the dealer sells three more things, the unlock mask has more bits); 22:
'@
Save-Src

# ---- the built-player network test counts armour among the things a safe holds
Open-Src "$Src\QaAutopilot.cs"
Rep @'
            int LiveGuns() { int n = 0; foreach (var rw in FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None)) if (rw && FloorSafe.HoldsGun(rw.gameObject)) n++; return n; }
'@ @'
            int LiveGuns() { int n = 0; foreach (var rw in FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None)) if (rw && FloorSafe.HoldsGun(rw.gameObject)) n++; foreach (var ar in FindObjectsByType<ArmourItem>(FindObjectsSortMode.None)) if (ar && FloorSafe.HoldsGun(ar.gameObject)) n++; return n; }
'@
Save-Src

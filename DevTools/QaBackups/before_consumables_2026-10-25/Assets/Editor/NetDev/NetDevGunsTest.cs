using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The pure rules of the five guns (the owner, 2026-10-18/19: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack", and the ladder of unlocks in the queue of 2026-10-19): the numbers of every gun, the
        /// silencer rule by class, the ammunition (a clip for one gun, three clips at most), what a weapon safe holds on each floor, what the dealer has on offer, and what a gun carried back to the docks unlocks.
        /// </summary>
        static void GunsTest()
        {
            var S = RangedWeapon.Specs;
            const int pew = (int)RangedWeapon.Kind.Pew, smg = (int)RangedWeapon.Kind.Smg, rifle = (int)RangedWeapon.Kind.Rifle, shotgun = (int)RangedWeapon.Kind.Shotgun, sniper = (int)RangedWeapon.Kind.Sniper;

            // ---- the guns and their numbers
            Check("guns: five guns - the pew, the SMG, the rifle, the shotgun, the sniper rifle - each with its own name and class",
                S.Length == 5 && S[pew].name == "Pew" && S[smg].name == "SMG" && S[rifle].name == "Rifle" && S[shotgun].name == "Shotgun" && S[sniper].name == "Sniper"
                && S[pew].weaponClass == RangedWeapon.WeaponClass.Pistol && S[smg].weaponClass == RangedWeapon.WeaponClass.SubMachineGun && S[rifle].weaponClass == RangedWeapon.WeaponClass.Rifle
                && S[shotgun].weaponClass == RangedWeapon.WeaponClass.Shotgun && S[sniper].weaponClass == RangedWeapon.WeaponClass.Sniper);
            bool positive = true;
            foreach (var s in S) positive &= s.weight > 0f && s.length > 0.2f && s.damage > 0f && s.guardHits >= 1 && s.guardHits <= 10 && s.noise > 0f && s.magazine > 0 && s.reloadSeconds > 0.5f && s.fireInterval > 0.02f
                && s.range > 10f && s.spread >= 0f && s.clipPrice > 0 && !string.IsNullOrEmpty(s.ammoName) && !string.IsNullOrEmpty(s.ammoPlural) && s.unlockFloor >= 3 && s.Pitch > 0.4f && s.KickDegrees > 0f && s.FlashScale > 0f;
            Check("guns: every gun has real numbers (weight, length, damage, hits, noise, magazine, reload, rate, range, price, ammunition names)", positive);
            Check("guns: the owner's ladder of unlocks - the pew on floor 3, the sub machine gun on 5, the shotgun on 6, the assault rifle on 8, the sniper rifle on 10",
                S[pew].unlockFloor == 3 && S[smg].unlockFloor == 5 && S[shotgun].unlockFloor == 6 && S[rifle].unlockFloor == 8 && S[sniper].unlockFloor == 10);
            Check("guns: the sub machine gun fires fastest, then the rifle, the pew, the shotgun, the sniper rifle slowest; the sniper rifle hits hardest and reaches furthest, the sub machine gun shortest of the rifles",
                S[smg].fireInterval < S[rifle].fireInterval && S[rifle].fireInterval < S[pew].fireInterval && S[pew].fireInterval < S[shotgun].fireInterval && S[shotgun].fireInterval < S[sniper].fireInterval
                && S[sniper].damage > S[pew].damage && S[pew].damage > S[rifle].damage && S[rifle].damage > S[smg].damage && S[sniper].range > S[rifle].range && S[rifle].range > S[smg].range);
            Check("guns: a shotgun fires eight pellets (one hit of ten each), every other gun one bullet; a bullet does not knock a guard over (the owner: the new guns stun for nothing)",
                S[shotgun].Pellets == 8 && S[shotgun].guardHits == 1 && S[pew].Pellets == 1 && S[smg].Pellets == 1 && S[rifle].Pellets == 1 && S[sniper].Pellets == 1
                && S[smg].stun == 0f && S[rifle].stun == 0f && S[shotgun].stun == 0f && S[sniper].stun == 0f && S[sniper].guardHits == 10);
            Check("guns: the pistol is held in one hand, every other gun in two; the pistol and the sub machine gun sound like the handgun, the rifle and the sniper rifle like the assault rifle, the shotgun like the shotgun",
                S[pew].hold == RangedWeapon.HoldStyle.OneHand && S[smg].hold == RangedWeapon.HoldStyle.TwoHand && S[rifle].hold == RangedWeapon.HoldStyle.TwoHand && S[shotgun].hold == RangedWeapon.HoldStyle.TwoHand && S[sniper].hold == RangedWeapon.HoldStyle.TwoHand
                && S[pew].sounds == RangedWeapon.SoundSet.Handgun && S[smg].sounds == RangedWeapon.SoundSet.Handgun && S[rifle].sounds == RangedWeapon.SoundSet.Rifle && S[sniper].sounds == RangedWeapon.SoundSet.Rifle && S[shotgun].sounds == RangedWeapon.SoundSet.Shotgun);

            // ---- silencers by class (the owner, 2026-10-18), now for every gun
            Check("silencer: the pew, the SMG and the rifle can take one and are quieter with it on; the shotgun and the sniper rifle never can and are exactly as loud with the flag on",
                S[pew].CanSilence && S[smg].CanSilence && S[rifle].CanSilence && !S[shotgun].CanSilence && !S[sniper].CanSilence
                && S[smg].silencedNoise < S[smg].noise && S[rifle].silencedNoise < S[rifle].noise && S[pew].silencedNoise < S[pew].noise
                && RangedWeapon.IsSilenced(S[smg], true) && RangedWeapon.IsSilenced(S[rifle], true) && !RangedWeapon.IsSilenced(S[shotgun], true) && !RangedWeapon.IsSilenced(S[sniper], true)
                && Mathf.Approximately(S[shotgun].silencedNoise, S[shotgun].noise) && Mathf.Approximately(S[sniper].silencedNoise, S[sniper].noise));

            // ---- ammunition (the owner, 2026-10-23: "Clips should be removed and it should go based of bullets"): every gun has its own, found guns hold their one magazine, a player carries as many bullets as he finds or buys
            bool perRound = true;
            for (int k = 1; k < S.Length; k++) perRound &= S[k].clipPrice / (float)S[k].magazine >= S[pew].clipPrice / (float)S[pew].magazine;
            Check("ammunition: every gun has its own (the safes' items keep their names: pistol clip, SMG magazine, rifle magazine, shotgun shells, sniper rounds) and the dealer's rounds for the new guns cost at least what the pistol's do",
                S[pew].ammoName == "Pistol Clip" && S[smg].ammoName == "SMG Magazine" && S[rifle].ammoName == "Rifle Magazine" && S[shotgun].ammoName == "Shotgun Shells" && S[sniper].ammoName == "Sniper Rounds"
                && S[pew].clipPrice == Vendor.AmmoClipPrice && perRound);
            var fresh = RangedWeapon.NewMagazine(S[pew]);
            var found = RangedWeapon.NewMagazine(S[smg], 0);
            Check("ammunition: a pistol that was bought holds its magazine and two magazines' worth of bullets; a gun that was found holds its one magazine and no bullets besides",
                fresh.Capacity == 8 && fresh.Rounds == 8 && fresh.Reserve == 16 && found.Capacity == 24 && found.Rounds == 24 && found.Reserve == 0);
            int added = 0;
            for (int i = 0; i < 40; i++) added += fresh.AddReserve(8);
            Check("ammunition: there is no limit on the bullets carried: forty more magazines' worth are all taken, and so are a thousand for a gun that was found", added == 320 && fresh.Reserve == 336 && found.AddReserve(1000) == 1000 && found.Reserve == 1000);
            var endless = new GunMagazine(8, 2f, 0.3f);
            Check("ammunition: an officer's gun (an endless reserve) takes nothing", endless.InfiniteReserve && endless.AddReserve(8) == 0);

            // ---- the shots: pellets, sounds
            bool seeds = true; var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < 8; i++) seeds &= seen.Add(GunRules.PelletSeed(123456, i)) && GunRules.PelletSeed(123456, i) == GunRules.PelletSeed(123456, i);
            bool cone = true;
            for (int i = 0; i < 8; i++) { GunRules.Spread(GunRules.PelletSeed(98765, i), S[shotgun].spread, out float yaw, out float pitch); cone &= new Vector2(yaw, pitch).magnitude <= S[shotgun].spread + 0.001f; }
            Check("pellets: the eight pellets of a shot have eight different seeds, the same on every machine, and every one lands inside the cone", seeds && seen.Count == 8 && cone);
            var h = GunSounds.ReloadTimes(RangedWeapon.SoundSet.Handgun); var r = GunSounds.ReloadTimes(RangedWeapon.SoundSet.Rifle); var sh = GunSounds.ReloadTimes(RangedWeapon.SoundSet.Shotgun);
            bool ordered = true;
            foreach (var arr in new[] { h, r, sh }) for (int i = 1; i < arr.Length; i++) ordered &= arr[i] > arr[i - 1];
            Check("sounds: a handgun reload has five sounds, a rifle's four, a shotgun's five, each later than the one before", h.Length == 5 && r.Length == 4 && sh.Length == 5 && ordered);

            // ---- a gun found and carried back unlocks its ammunition (RunUnlocks)
            RunUnlocks.Reset();
            bool none = !RunUnlocks.Has(RangedWeapon.Kind.Pew) && !RunUnlocks.Has(RangedWeapon.Kind.Smg) && RunUnlocks.Mask == 0;
            bool first = RunUnlocks.Unlock(RangedWeapon.Kind.Smg), again = RunUnlocks.Unlock(RangedWeapon.Kind.Smg);
            Check("unlocks: a new run has nothing unlocked; unlocking the SMG sets one bit, once, and only the SMG", none && first && !again && RunUnlocks.Has(RangedWeapon.Kind.Smg) && !RunUnlocks.Has(RangedWeapon.Kind.Rifle) && RunUnlocks.Mask == (1 << smg));
            RunUnlocks.Unlock(RangedWeapon.Kind.Sniper);
            int mask = RunUnlocks.Mask;
            RunUnlocks.Reset();
            RunUnlocks.NetSet(mask);
            Check("unlocks: a client takes the host's mask as it is, a reset clears it", RunUnlocks.Has(RangedWeapon.Kind.Sniper) && RunUnlocks.Has(RangedWeapon.Kind.Smg) && !RunUnlocks.Has(RangedWeapon.Kind.Shotgun) && RunUnlocks.Mask == mask);

            // ---- what the dealer has on offer
            var cat = Vendor.Catalog;
            bool ammoRows = cat.Length == 17;                               // (the nine it was, the ammunition of the other four guns, the three pieces of armour and the walkie talkie at the end)
            for (int k = 1; k < S.Length && ammoRows; k++)
            {
                var p = cat[Vendor.AmmoIndex + k];
                ammoRows &= p.isAmmo && !p.isGun && (int)p.gun == k && p.price == S[k].clipPrice && p.rounds == S[k].magazine && p.name == (k == (int)RangedWeapon.Kind.Smg ? "SMG Bullets" : k == (int)RangedWeapon.Kind.Rifle ? "Rifle Bullets" : S[k].ammoName);
            }
            Check("vendor: the dealer's list is the nine it was, four more (the ammunition of the SMG, the rifle, the shotgun and the sniper rifle (their own names - bullets, shells, rounds - prices and magazine sizes), three pieces of armour and the walkie talkie at the end - no gun but the pew is for sale", ammoRows && cat[Vendor.PewIndex].isGun && cat[Vendor.PewIndex].gun == RangedWeapon.Kind.Pew);
            RunUnlocks.Reset();
            bool basics = true; for (int i = 0; i < Vendor.PewIndex; i++) basics &= Vendor.OnOffer(i, 1) == !Vendor.Catalog[i].machine && Vendor.OnOffer(i, 13) == !Vendor.Catalog[i].machine;
            Check("vendor: the three melee weapons are always for sale (the four consumables are the vending machines' now); before floor 3 is done nothing else is (no pew, no ammunition)", basics && !Vendor.OnOffer(Vendor.PewIndex, 3) && !Vendor.OnOffer(Vendor.AmmoIndex, 3) && !Vendor.OnOffer(9, 3) && !Vendor.OnOffer(10, 3) && !Vendor.OnOffer(11, 3) && !Vendor.OnOffer(12, 3));
            Check("vendor: once floor 3 is done the pew and its clips are for sale, and still no other gun's ammunition", Vendor.OnOffer(Vendor.PewIndex, 4) && Vendor.OnOffer(Vendor.AmmoIndex, 4) && !Vendor.OnOffer(9, 4) && !Vendor.OnOffer(10, 4) && !Vendor.OnOffer(11, 13) && !Vendor.OnOffer(12, 13));
            RunUnlocks.Unlock(RangedWeapon.Kind.Rifle);
            Check("vendor: a gun that was found and brought back puts its own ammunition on offer, at any time, and nobody else's", Vendor.OnOffer(10, 1) && !Vendor.OnOffer(9, 1) && !Vendor.OnOffer(11, 1) && !Vendor.OnOffer(12, 1) && !Vendor.OnOffer(Vendor.PewIndex, 1));
            RunUnlocks.Unlock(RangedWeapon.Kind.Pew);
            Check("vendor: a pew that was found makes its clips available, but the pew itself is still sold only after floor 3", Vendor.OnOffer(Vendor.AmmoIndex, 1) && !Vendor.OnOffer(Vendor.PewIndex, 1));
            RunUnlocks.Reset();

            // ---- the safes: which gun, on which floor (the owner's ladder)
            bool items = true;
            for (int k = 0; k < S.Length; k++) items &= SafeRules.IsGunItem(SafeRules.GunItem((RangedWeapon.Kind)k)) && (int)SafeRules.GunOfItem(SafeRules.GunItem((RangedWeapon.Kind)k)) == k;
            Check("safes: a gun is the number 3 plus its kind (the pew 3, the SMG 4, the rifle 5, the shotgun 6, the sniper rifle 7) and the numbers round-trip; 2 and 8 are no guns", items && SafeRules.GunItem(RangedWeapon.Kind.Pew) == SafeRules.PewItem && !SafeRules.IsGunItem(2) && !SafeRules.IsGunItem(8));
            Check("safes: which guns can be in a safe on which floor - none before floor 3, the pew alone on 3 and 4, the SMG from 5, the shotgun from 6, the rifle from 8, and never the sniper rifle (it lies in the guard outpost: the owner, 2026-10-19)",
                Sum(SafeRules.GunWeights(2)) == 0 && Only(SafeRules.GunWeights(3), pew) && Only(SafeRules.GunWeights(4), pew)
                && Has(SafeRules.GunWeights(5), pew, smg) && Has(SafeRules.GunWeights(7), pew, smg, shotgun) && Has(SafeRules.GunWeights(8), pew, smg, shotgun, rifle) && Has(SafeRules.GunWeights(10), pew, smg, shotgun, rifle)
                && Has(SafeRules.GunWeights(12), pew, smg, shotgun, rifle));
            var w5 = SafeRules.GunWeights(5); var w6 = SafeRules.GunWeights(6);
            Check("safes: the gun that unlocks on a floor is no likelier there than it will be later (the owner, 2026-10-19: \"even luckier to get the specific one you wanted\")", SafeRules.UnlockFloorBoost == 1 && w5[smg] == SafeRules.GunWeights(6)[smg] && w6[shotgun] == SafeRules.GunWeights(7)[shotgun] && w5[pew] == SafeRules.GunWeights(6)[pew]);
            Check("safes: the floor each gun is first found on", SafeRules.GunUnlockedOn(3) == pew && SafeRules.GunUnlockedOn(5) == smg && SafeRules.GunUnlockedOn(6) == shotgun && SafeRules.GunUnlockedOn(8) == rifle && SafeRules.GunUnlockedOn(10) == sniper
                && SafeRules.GunUnlockedOn(4) == -1 && SafeRules.GunUnlockedOn(7) == -1 && SafeRules.GunUnlockedOn(9) == -1 && SafeRules.GunUnlockedOn(11) == -1);

            bool onlyPewEarly = true, allAvailable = true, deterministic = true, atMostOne = true, clipMatches = true, noSniper = true, consistent = true;
            var seenKinds = new System.Collections.Generic.HashSet<int>();
            int safesTotal = 0, safesWithGun = 0, safesWithArmour = 0, safesEmpty = 0, runsOn5 = 0, smgFirstOn5 = 0, smgOn5 = 0, safesOn5 = 0, shotgunOn6 = 0, safesOn6 = 0;
            for (int sd = 0; sd < 150; sd++)
                for (int f = 3; f <= 12; f++)
                {
                    int rs = sd * 77 + 5, n = SafeRules.SafeCount(rs, f);
                    if (f == 5) runsOn5++;
                    for (int i = 0; i < n; i++)
                    {
                        var items1 = SafeRules.WeaponContents(rs, f, i); var items2 = SafeRules.WeaponContents(rs, f, i);
                        atMostOne &= items1.Length <= 1;
                        deterministic &= items1.Length == items2.Length && (items1.Length == 0 || items1[0] == items2[0]);
                        bool armour = items1.Length == 1 && SafeRules.IsArmourItem(items1[0]), gun = items1.Length == 1 && SafeRules.IsGunItem(items1[0]);
                        consistent &= SafeRules.HoldsArmour(rs, f, i) == armour && SafeRules.HoldsGun(rs, f, i) == gun && (items1.Length == 0 || armour || gun);
                        safesTotal++;
                        if (armour) safesWithArmour++;
                        else if (!gun) safesEmpty++;
                        if (f == 5) safesOn5++;
                        if (f == 6) safesOn6++;
                        clipMatches &= (int)SafeRules.ClipKindOf(rs, f, i) == (gun ? (int)SafeRules.GunOfItem(items1[0]) : pew);
                        if (!gun) continue;                                              // (nothing, or armour instead of a gun: ArmourTest has the armour's rules)
                        safesWithGun++;
                        int kind = (int)SafeRules.GunOfItem(items1[0]);
                        allAvailable &= f >= S[kind].unlockFloor;
                        noSniper &= kind != sniper;
                        if (f <= 4) onlyPewEarly &= kind == pew;
                        if (f == 12) seenKinds.Add(kind);
                        if (f == 5 && kind == smg) { smgOn5++; if (i == 0) smgFirstOn5++; }
                        if (f == 6 && kind == shotgun) shotgunOn6++;
                    }
                }
            int notArmour = safesTotal - safesWithArmour;
            float gunShare = notArmour > 0 ? safesWithGun / (float)notArmour : 0f;
            Check("safes: a weapon safe holds at most one thing (nothing, a gun or a piece of armour), the same on every machine, and the owner's floors keep to it - floors 3 and 4 the pew only, no gun before its floor, never the sniper rifle",
                atMostOne && deterministic && consistent && onlyPewEarly && allAvailable && noSniper, $"(at most one {atMostOne}, deterministic {deterministic}, the rules agree {consistent}, pew only early {onlyPewEarly}, available {allAvailable}, no sniper {noSniper})");
            Check("safes: it is lucky to get a gun - a safe that holds no armour holds one about 4 times in 10, and some safes hold nothing at all (the owner, 2026-10-19: \"Safes should not always have a gun\")",
                SafeRules.GunPercent == 40 && System.Math.Abs(gunShare - 0.40f) < 0.05f && safesEmpty > 0 && safesWithGun < notArmour, $"({safesWithGun} of {notArmour} safes without armour hold a gun = {gunShare:0.00}; {safesEmpty} hold nothing, {safesWithArmour} armour, of {safesTotal})");
            Check("safes: and luckier still to get the specific one - the first safe of the floor that unlocks the SMG holds it in some runs only, and one kind is a small share of all the safes (the SMG on floor 5, the shotgun on floor 6)",
                smgFirstOn5 > 0 && smgFirstOn5 < runsOn5 / 2 && smgOn5 > 0 && smgOn5 < safesOn5 / 4 && shotgunOn6 > 0 && shotgunOn6 < safesOn6 / 6, $"(SMG in the first safe of floor 5: {smgFirstOn5} of {runsOn5} runs, in any safe of floor 5: {smgOn5} of {safesOn5}; shotgun on floor 6: {shotgunOn6} of {safesOn6})");
            Check("safes: floor 12 has every kind of gun that can be in a safe about (a mix of all four over 150 runs, no sniper rifle), and the spare clip in a safe is for the gun in it (the pew's when it holds none)", seenKinds.Count == 4 && !seenKinds.Contains(sniper) && clipMatches, $"({seenKinds.Count} kinds, clips match {clipMatches})");
            Check("safes: floors 1 and 2 hold no gun, a safe that is not there holds nothing", SafeRules.WeaponContents(1, 1, 0).Length == 0 && SafeRules.WeaponContents(1, 2, 0).Length == 0 && SafeRules.WeaponContents(1, 3, 7).Length == 0 && SafeRules.WeaponContents(1, 3, -1).Length == 0);
            {
                // the test menu's words for a safe (the owner, 2026-10-22: it said "a pistol" after every safe): they come from the same rules, so each safe is named for what it really holds
                bool wordsRight = true, wordsDiffer = false; string firstWords = null;
                for (int wRun = 1; wRun <= 60; wRun++)
                    for (int wFloor = 1; wFloor <= 12; wFloor++)
                    {
                        int wCount = SafeRules.SafeCount(wRun, wFloor);
                        if (SafeRules.ContentsText(wRun, wFloor, -1) != "" || SafeRules.ContentsText(wRun, wFloor, wCount) != "") wordsRight = false;
                        for (int wIdx = 0; wIdx < wCount; wIdx++)
                        {
                            string wText = SafeRules.ContentsText(wRun, wFloor, wIdx);
                            if (firstWords == null) firstWords = wText; else if (wText != firstWords) wordsDiffer = true;
                            var wHeld = SafeRules.WeaponContents(wRun, wFloor, wIdx);
                            if (!SafeRules.IsWeaponFloor(wFloor)) { if (!wText.StartsWith("cash")) wordsRight = false; }
                            else if (wHeld.Length == 0) { if (!wText.StartsWith("no gun or armour")) wordsRight = false; }
                            else if (SafeRules.IsArmourItem(wHeld[0])) { if (!wText.StartsWith(ArmourRules.Name(SafeRules.ArmourOfItem(wHeld[0])))) wordsRight = false; }
                            else if (SafeRules.IsGunItem(wHeld[0])) { if (!wText.StartsWith(RangedWeapon.Specs[(int)SafeRules.GunOfItem(wHeld[0])].name)) wordsRight = false; }
                        }
                    }
                Check("safes: the test menu names each safe for what it really holds - cash, a gun by name, a tier of armour or nothing - and not the same word for every safe", wordsRight && wordsDiffer, $"(the words match the rules {wordsRight}, they differ from safe to safe {wordsDiffer})");
            }
        }

        static int Sum(int[] a) { int t = 0; foreach (var x in a) t += x; return t; }
        static bool Only(int[] weights, int kind) { for (int k = 0; k < weights.Length; k++) if ((weights[k] > 0) != (k == kind)) return false; return true; }
        static bool Has(int[] weights, params int[] kinds)
        {
            var want = new System.Collections.Generic.HashSet<int>(kinds);
            for (int k = 0; k < weights.Length; k++) if ((weights[k] > 0) != want.Contains(k)) return false;
            return true;
        }
    }
}

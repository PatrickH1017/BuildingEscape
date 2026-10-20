# Round 7, batch B1c: the tests for the new pistol (8 rounds a clip, 3 clips, 3 bullets drop a guard, ammunition from the vendor, the pew costs money). BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---------------------------------------------------------------- pure: the magazine, the reserve, the numbers
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep @'
            Check("gun: the pew holds ten rounds and is reloaded after every ten shots", pew.magazine == 10);

            // ten shots 0.3 s apart, then the click and the reload
            var m = RangedWeapon.NewMagazine(pew);
            Check("gun: a new pew is full", m.Rounds == 10 && !m.Reloading && m.Capacity == 10);
            float t = 0f; bool ok = true; int fired = 0;
            for (int i = 0; i < 10; i++)
            {
                var s = m.TryFire(t);
                ok &= i < 9 ? s == GunMagazine.Shot.Fired : s == GunMagazine.Shot.FiredLast;
                if (s == GunMagazine.Shot.Fired || s == GunMagazine.Shot.FiredLast) fired++;
                t += pew.fireInterval;
            }
            Check("gun: ten shots in a row are ten bullets, the tenth is the last one", ok && fired == 10 && m.Rounds == 0 && m.Fired == 10);
            Check("gun: the eleventh pull is only a click", m.TryFire(t + 0.1f) == GunMagazine.Shot.Empty && m.Fired == 10);
'@ @'
            int cap = pew.magazine;
            Check("gun: the pew holds eight rounds and is reloaded after every eight shots (the owner, 2026-10-18)", cap == 8);
            Check("gun: it comes with three clips in all: the one in the gun and two spare", pew.spareClips == 2 && (1 + pew.spareClips) * cap == 24);

            // eight shots 0.3 s apart, then the click and the reload
            var m = RangedWeapon.NewMagazine(pew);
            Check("gun: a new pew is full and carries two spare clips", m.Rounds == cap && !m.Reloading && m.Capacity == cap && !m.InfiniteReserve && m.Reserve == 2 * cap && !m.OutOfAmmo);
            float t = 0f; bool ok = true; int fired = 0;
            for (int i = 0; i < cap; i++)
            {
                var s = m.TryFire(t);
                ok &= i < cap - 1 ? s == GunMagazine.Shot.Fired : s == GunMagazine.Shot.FiredLast;
                if (s == GunMagazine.Shot.Fired || s == GunMagazine.Shot.FiredLast) fired++;
                t += pew.fireInterval;
            }
            Check("gun: eight shots in a row are eight bullets, the eighth is the last one", ok && fired == cap && m.Rounds == 0 && m.Fired == cap);
            Check("gun: the ninth pull is only a click", m.TryFire(t + 0.1f) == GunMagazine.Shot.Empty && m.Fired == cap);
'@
Rep @'
            Check("gun: the reload ends after its time with a full magazine", m.Tick(reloadAt + pew.reloadSeconds + 0.01f) == GunMagazine.Event.ReloadFinished && m.Rounds == 10 && !m.Reloading && m.Reloads == 1);
'@ @'
            Check("gun: the reload ends after its time with a full magazine, taken out of a spare clip", m.Tick(reloadAt + pew.reloadSeconds + 0.01f) == GunMagazine.Event.ReloadFinished && m.Rounds == cap && !m.Reloading && m.Reloads == 1 && m.Reserve == cap);
'@
Rep @'
            Check("gun: a second pull 0.1 s later is too soon (no round used)", m.TryFire(0.1f) == GunMagazine.Shot.TooSoon && m.Rounds == 9);
            Check("gun: the fire interval is respected exactly", m.TryFire(pew.fireInterval - 0.01f) == GunMagazine.Shot.TooSoon && m.TryFire(pew.fireInterval) == GunMagazine.Shot.Fired && m.Rounds == 8);
'@ @'
            Check("gun: a second pull 0.1 s later is too soon (no round used)", m.TryFire(0.1f) == GunMagazine.Shot.TooSoon && m.Rounds == cap - 1);
            Check("gun: the fire interval is respected exactly", m.TryFire(pew.fireInterval - 0.01f) == GunMagazine.Shot.TooSoon && m.TryFire(pew.fireInterval) == GunMagazine.Shot.Fired && m.Rounds == cap - 2);
'@
Rep @'
            Check("gun: putting the gun away stops the reload (the rounds stay as they were)", !m.Reloading && m.Rounds == 7);
            m.Fill();
            Check("gun: refill (test menu) gives ten again", m.Rounds == 10);
'@ @'
            Check("gun: putting the gun away stops the reload (the rounds stay as they were)", !m.Reloading && m.Rounds == cap - 3);
            m.Fill();
            Check("gun: refill (test menu) gives a full magazine again and leaves the spare clips alone", m.Rounds == cap && m.Reserve == 2 * cap);

            // three clips in all: 24 shots, then the gun is dry until the dealer sells more
            var all3 = RangedWeapon.NewMagazine(pew);
            float clock = 0f; int shots = 0; bool dry = false;
            for (int i = 0; i < 400 && !dry; i++)
            {
                clock += pew.fireInterval + 0.01f;
                all3.Tick(clock);
                if (all3.Reloading) { clock = all3.ReloadEndsAt + 0.01f; all3.Tick(clock); }
                var shot = all3.TryFire(clock);
                if (shot == GunMagazine.Shot.Fired || shot == GunMagazine.Shot.FiredLast) shots++;
                else if (shot == GunMagazine.Shot.Empty) dry = true;
            }
            Check("gun: three clips are 24 shots, then the pew is dry", dry && shots == 24 && all3.Reserve == 0 && all3.Rounds == 0 && all3.OutOfAmmo && all3.Reloads == 2, $"(shots {shots}, reserve {all3.Reserve}, reloads {all3.Reloads})");
            Check("gun: a dry pew cannot be reloaded and does not start a reload by itself", !all3.StartReload(clock) && !all3.CanReload && all3.Tick(clock + 5f) == GunMagazine.Event.None && !all3.Reloading);
            Check("gun: a clip from the dealer puts eight rounds in the reserve, and the reload takes them", all3.AddReserve(cap) == cap && all3.Reserve == cap && all3.CanReload && !all3.OutOfAmmo
                  && all3.StartReload(clock + 6f) && all3.Tick(clock + 6f + pew.reloadSeconds + 0.01f) == GunMagazine.Event.ReloadFinished && all3.Rounds == cap && all3.Reserve == 0);
            var part = RangedWeapon.NewMagazine(pew);
            part.TryFire(0f); part.TryFire(1f); part.TryFire(2f);
            Check("gun: a reload of a part-used gun takes only the three missing rounds from the spare clips", part.StartReload(2.1f) && part.Tick(2.1f + pew.reloadSeconds + 0.01f) == GunMagazine.Event.ReloadFinished && part.Rounds == cap && part.Reserve == 2 * cap - 3);
            var endless = new GunMagazine(10, 2.2f, 0.3f);
            Check("gun: a magazine made without a reserve (an officer's) has an endless one, which no clip adds to", endless.InfiniteReserve && endless.AddReserve(5) == 0 && GuardGun.MagazineSize == 10);
            Check("gun: the spare clips on the screen count a part of a clip as one", GunRules.SpareClips(0, cap) == 0 && GunRules.SpareClips(1, cap) == 1 && GunRules.SpareClips(cap, cap) == 1 && GunRules.SpareClips(cap + 1, cap) == 2 && GunRules.SpareClips(2 * cap, cap) == 2);
'@
Rep @'
            Check("gun: a bullet counts as several hits on a guard", pew.guardHits >= 2);
            Check("gun: two bullets knock a guard out (a melee weapon needs ten hits)", GunRules.BulletsToKill(10, pew.guardHits) == 2);
            Check("gun: the boss takes more bullets", GunRules.BulletsToKill(30, pew.guardHits) == 6 && GunRules.BulletsToKill(10, 1) == 10 && GunRules.BulletsToKill(0, 0) == 1);
'@ @'
            Check("gun: a bullet counts as several hits on a guard", pew.guardHits >= 2);
            Check("gun: three bullets knock a guard out (a melee weapon needs ten hits; the owner, 2026-10-18: 3 shots to knock down a guard)", GunRules.BulletsToKill(10, pew.guardHits) == 3);
            Check("gun: the boss takes more bullets", GunRules.BulletsToKill(30, pew.guardHits) == 8 && GunRules.BulletsToKill(10, 1) == 10 && GunRules.BulletsToKill(0, 0) == 1);
'@
Save-Src

# ---------------------------------------------------------------- pure: the vendor
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevDemoTest.cs')
Rep @'
            Check("vendor: the pew is the eighth entry, for nothing, a gun; the seven others are where they were", c.Length == 8 && Vendor.PewIndex == 7 && c[7].name == "Pew Pistol" && c[7].price == 0 && c[7].isGun
'@ @'
            Check("vendor: the pew is the eighth entry, for money (not free any more), a gun; a clip of ammunition follows it; the seven others are where they were", c.Length == 9 && Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8 && c[7].name == "Pew Pistol" && c[7].price == Vendor.PewPrice && Vendor.PewPrice > 0 && c[7].isGun
                && c[8].isAmmo && c[8].price == Vendor.AmmoClipPrice && Vendor.AmmoClipPrice > 0 && c[8].rounds == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine && !c[8].isGun
'@
Rep @'
                && Vendor.FreePewAfterFloor == 3);
'@ @'
                && Vendor.PewAfterFloor == 3);
'@
Save-Src

# ---------------------------------------------------------------- Play: the demo (offline and two processes)
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevDemo.cs')
Rep 'if (floor < Vendor.FreePewAfterFloor)' 'if (floor < Vendor.PewAfterFloor)'
Rep @'
            int cash0 = hm.Cash;
            DemoCheck("floor 4 done: the pew is for sale", Vendor.PewForSale(tm.ResumeLevel));
            string bought = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            yield return new WaitForSeconds(0.8f);
            var pew = pack.Items.FirstOrDefault(i => i && i.GetComponent<RangedWeapon>());
            DemoCheck("the vendor sells it: it is in the pack, in the hand, and it cost nothing", bought.Contains("Bought Pew Pistol") && pew != null && pack.Equipped == pew && hm.Cash == cash0, $"(\"{bought}\", cash {cash0} -> {hm.Cash})");
            var gc = player.GetComponent<GunController>();
            DemoCheck("the pew is in his hand with a full magazine", gc != null && gc.Gun != null && gc.Held != null && gc.Gun.Magazine.Rounds == 10);
            string again = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            DemoCheck("a second pew is refused (one each)", again.Contains("already") && pack.Items.Count(i => i && i.GetComponent<RangedWeapon>()) == 1, $"(\"{again}\")");
'@ @'
            DemoCheck("floor 4 done: the pew is for sale", Vendor.PewForSale(tm.ResumeLevel));
            hm.TrySpend(hm.Cash); hm.AddCash(Vendor.PewPrice - 1);                      // (one dollar short)
            string noClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("without a pew the vendor will not sell a clip", noClip.Contains("no pew") && hm.Cash == Vendor.PewPrice - 1, $"(\"{noClip}\")");
            string poor = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            DemoCheck("the pew is not free any more: one dollar short, the vendor refuses", poor.Contains("Not enough cash") && !pack.Items.Any(i => i && i.GetComponent<RangedWeapon>()) && hm.Cash == Vendor.PewPrice - 1, $"(\"{poor}\")");
            hm.AddCash(1 + Vendor.AmmoClipPrice * 2);                                   // (the pew's price, and two clips)
            int cash0 = hm.Cash;
            string bought = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            yield return new WaitForSeconds(0.8f);
            var pew = pack.Items.FirstOrDefault(i => i && i.GetComponent<RangedWeapon>());
            DemoCheck("the vendor sells it for its price: it is in the pack and in the hand", bought.Contains("Bought Pew Pistol") && pew != null && pack.Equipped == pew && cash0 - hm.Cash == Vendor.PewPrice, $"(\"{bought}\", cash {cash0} -> {hm.Cash})");
            var gc = player.GetComponent<GunController>();
            DemoCheck("the pew is in his hand with a full magazine of eight and two spare clips", gc != null && gc.Gun != null && gc.Held != null && gc.Gun.Magazine.Rounds == 8 && gc.Gun.Magazine.Reserve == 16);
            string again = BuyFrom(vendor, hm, pack, Vendor.PewIndex);
            DemoCheck("a second pew is refused (one each)", again.Contains("already") && pack.Items.Count(i => i && i.GetComponent<RangedWeapon>()) == 1, $"(\"{again}\")");
            int cashClip = hm.Cash;
            string clip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a clip costs its price and puts eight rounds in the reserve of the pew", clip.Contains("Bought a clip") && cashClip - hm.Cash == Vendor.AmmoClipPrice && gc.Gun.Magazine.Reserve == 24, $"(\"{clip}\", cash {cashClip} -> {hm.Cash}, reserve {gc.Gun.Magazine.Reserve})");
            hm.TrySpend(hm.Cash);
            string brokeClip = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("with no cash left no clip is sold, and the reserve stays", brokeClip.Contains("Not enough cash") && gc.Gun.Magazine.Reserve == 24, $"(\"{brokeClip}\")");
'@
Rep @'
            // ---- the client buys the pew
            yield return WaitSig("c_bought", 120);
'@ @'
            hm.TrySpend(hm.Cash); hm.AddCash(Vendor.PewPrice + Vendor.AmmoClipPrice + 300);        // (what the team has to spend: the pew costs money now)
            Signal("h_cash");

            // ---- the client buys the pew
            yield return WaitSig("c_bought", 120);
'@
Rep @'
            DemoNetCheck("host: it cost the team nothing", hm.Cash == 0, $"(cash {hm.Cash})");
'@ @'
            DemoNetCheck("host: it cost the team the pew's price", hm.Cash == Vendor.AmmoClipPrice + 300, $"(cash {hm.Cash})");
'@
Rep @'
            Signal("h_bought_ok");

            // ---- the client fires two bullets at the dummy
            yield return WaitSig("c_shot", 120);
'@ @'
            Signal("h_bought_ok");

            // ---- the client buys a clip: the host takes the money and every machine adds the rounds to the reserve of his pew
            yield return WaitSig("c_clip", 120);
            DemoNetCheck("host: the clip cost its price and the client's pew has eight more spare rounds here too", hm.Cash == 300 && cgc != null && cgc.Gun != null && cgc.Gun.Magazine.Reserve == 24, $"(cash {hm.Cash}, reserve {(cgc != null && cgc.Gun != null ? cgc.Gun.Magazine.Reserve : -1)})");
            Signal("h_clip_ok");

            // ---- the client fires two bullets at the dummy
            yield return WaitSig("c_shot", 120);
'@
Rep @'
            yield return WaitSig("h_ready", 120);
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => tm.ResumeLevel == 5, 30, "the host's resume level");
'@ @'
            yield return WaitSig("h_ready", 120);
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => tm.ResumeLevel == 5, 30, "the host's resume level");
            yield return WaitSig("h_cash", 60);
            yield return WaitFor(() => hm.Cash == Vendor.PewPrice + Vendor.AmmoClipPrice + 300, 30, "the team's cash");
'@
Rep @'
            DemoNetCheck("client: bought it: it is in the pack, in the hand, for nothing", pack.Equipped == pew && gc != null && gc.Gun != null && gc.Held != null && hm.Cash == 0, $"(cash {hm.Cash})");
            Signal("c_bought");
            yield return WaitSig("h_bought_ok", 60);
'@ @'
            DemoNetCheck("client: bought it: it is in the pack and in the hand, and the team paid the pew's price", pack.Equipped == pew && gc != null && gc.Gun != null && gc.Held != null && hm.Cash == Vendor.AmmoClipPrice + 300 && gc.Gun.Magazine.Reserve == 16, $"(cash {hm.Cash})");
            Signal("c_bought");
            yield return WaitSig("h_bought_ok", 60);
            RunSync.Instance.RequestBuy(Vendor.AmmoIndex);
            yield return WaitFor(() => gc.Gun != null && gc.Gun.Magazine.Reserve == 24, 20, "the clip in the reserve");
            DemoNetCheck("client: the clip he bought added eight rounds to the reserve, for its price", gc.Gun.Magazine.Reserve == 24 && hm.Cash == 300, $"(reserve {gc.Gun.Magazine.Reserve}, cash {hm.Cash})");
            Signal("c_clip");
            yield return WaitSig("h_clip_ok", 60);
'@
Save-Src

# ---------------------------------------------------------------- Play: the elevator no longer refills the guns
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR5.cs')
Rep @'
            // ---- ammunition: a gun emptied on this floor has a full magazine again once the elevator has taken everybody to the next one
'@ @'
            // ---- ammunition: a gun emptied on this floor is NOT refilled by the elevator (the owner, 2026-10-18: "more ammo should need to be purchased from the Vendor")
'@
Rep @'
                R5Check("ammo: ...and it is full again after the elevator ride to the next floor", gun.Magazine.Rounds == gun.Magazine.Capacity, $"({gun.Magazine.Rounds} of {gun.Magazine.Capacity})");
'@ @'
                R5Check("ammo: ...and it is still empty after the elevator ride to the next floor, with its two spare clips", gun.Magazine.Rounds == 0 && gun.Magazine.Reserve == 2 * gun.Magazine.Capacity, $"({gun.Magazine.Rounds} of {gun.Magazine.Capacity}, reserve {gun.Magazine.Reserve})");
'@
Save-Src

# ---------------------------------------------------------------- Play: the Pew scenario
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevPew.cs')
Rep @'
            PewCheck("the pew starts with a full magazine of 10", pewItem != null && pewItem.GetComponent<RangedWeapon>().Magazine.Rounds == 10 && pewItem.GetComponent<RangedWeapon>().Magazine.Capacity == 10);
'@ @'
            PewCheck("the pew starts with a full magazine of 8 and two spare clips", pewItem != null && pewItem.GetComponent<RangedWeapon>().Magazine.Rounds == 8 && pewItem.GetComponent<RangedWeapon>().Magazine.Capacity == 8 && pewItem.GetComponent<RangedWeapon>().Magazine.Reserve == 16);
'@
Rep 'PewCheck("the first shot leaves the gun", fired1 && pewGun.Magazine.Rounds == 9);' 'PewCheck("the first shot leaves the gun", fired1 && pewGun.Magazine.Rounds == 7);'
Rep 'PewCheck("a second pull at once is too soon", !gc.Fire() && pewGun.Magazine.Rounds == 9);' 'PewCheck("a second pull at once is too soon", !gc.Fire() && pewGun.Magazine.Rounds == 7);'
Rep 'for (int i = 2; i <= 10; i++)' 'for (int i = 2; i <= 8; i++)'
Rep @'
                PewCheck("ten shots empty the magazine", pewGun.Magazine.Rounds == 0 && pewGun.Magazine.Fired == 10 && gc.ShotsFired == 10, $"(rounds {pewGun.Magazine.Rounds}, fired {pewGun.Magazine.Fired})");
                PewCheck("ten flashes, ten tracers, ten gunshots with tails", GunFx.Get().FlashesShown - flashes0 == 10 && GunFx.Get().TracersShown - tracers0 == 10
                    && GunSounds.ShotsPlayed - shots0 == 10 && GunSounds.TailsPlayed - tails0 == 10);
                PewCheck("the bullets sparked where they landed and threw cases", GunFx.Get().SparksShown >= 10 && GunFx.Get().CasingsShown >= 10, $"(sparks {GunFx.Get().SparksShown}, cases {GunFx.Get().CasingsShown})");
'@ @'
                PewCheck("eight shots empty the magazine", pewGun.Magazine.Rounds == 0 && pewGun.Magazine.Fired == 8 && gc.ShotsFired == 8, $"(rounds {pewGun.Magazine.Rounds}, fired {pewGun.Magazine.Fired})");
                PewCheck("eight flashes, eight tracers, eight gunshots with tails", GunFx.Get().FlashesShown - flashes0 == 8 && GunFx.Get().TracersShown - tracers0 == 8
                    && GunSounds.ShotsPlayed - shots0 == 8 && GunSounds.TailsPlayed - tails0 == 8);
                PewCheck("the bullets sparked where they landed and threw cases", GunFx.Get().SparksShown >= 8 && GunFx.Get().CasingsShown >= 8, $"(sparks {GunFx.Get().SparksShown}, cases {GunFx.Get().CasingsShown})");
'@
Rep @'
                PewCheck("the reload ends with ten rounds", !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 10 && pewGun.Magazine.Reloads == 1);
'@ @'
                PewCheck("the reload ends with eight rounds, out of a spare clip", !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 8 && pewGun.Magazine.Reloads == 1 && pewGun.Magazine.Reserve == 8);
'@
Rep @'
                for (int i = 0; i < 9; i++) { AimCamera(cam, player, aimAt); gc.Fire(); yield return new WaitForSeconds(0.31f); }
                AimCamera(cam, player, aimAt);
                gc.Fire();                                              // the tenth
'@ @'
                for (int i = 0; i < 7; i++) { AimCamera(cam, player, aimAt); gc.Fire(); yield return new WaitForSeconds(0.31f); }
                AimCamera(cam, player, aimAt);
                gc.Fire();                                              // the eighth
'@
Rep @'
                PewCheck("full again after the second reload", pewGun.Magazine.Rounds == 10 && pewGun.Magazine.Reloads >= 2, $"(reloads {pewGun.Magazine.Reloads})");
                // reload by hand after a few shots, then put the gun away mid-reload
                gc.Fire(); yield return new WaitForSeconds(0.31f); gc.Fire(); yield return new WaitForSeconds(0.31f);
                PewCheck("the gun can be reloaded by hand when it is partly used", pewGun.Magazine.Rounds == 8 && pewGun.Magazine.StartReload(Time.time));
                pack.SetEquipped(null);
                yield return new WaitForSeconds(0.4f);
                PewCheck("putting the pew away takes it out of the hand and stops the reload", gc.Gun == null && gc.Held == null && !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 8);
                pack.SetEquipped(pewItem);
                yield return new WaitForSeconds(0.4f);
                PewCheck("and getting it out again puts it back, with the rounds it had", gc.Gun == pewGun && gc.Held != null && pewGun.Magazine.Rounds == 8);
'@ @'
                PewCheck("full again after the second reload, and the two spare clips are used up", pewGun.Magazine.Rounds == 8 && pewGun.Magazine.Reloads >= 2 && pewGun.Magazine.Reserve == 0, $"(reloads {pewGun.Magazine.Reloads}, reserve {pewGun.Magazine.Reserve})");
                // with no spare clip the gun stays empty: the trigger only clicks and no reload starts
                for (int i = 0; i < 8; i++) { AimCamera(cam, player, aimAt); gc.Fire(); yield return new WaitForSeconds(0.31f); }
                yield return new WaitForSeconds(0.6f);
                int clicksDry = GunSounds.DryClicksPlayed;
                bool clickedDry = !gc.Fire();
                PewCheck("with no spare clip the empty gun only clicks, and no reload starts", clickedDry && pewGun.Magazine.Rounds == 0 && pewGun.Magazine.OutOfAmmo && !pewGun.Magazine.Reloading && GunSounds.DryClicksPlayed == clicksDry + 1);
                Vendor.GiveAmmo(pack, 8);                                                  // (what a clip from the dealer does)
                PewCheck("a clip from the dealer makes the reload possible again", pewGun.Magazine.Reserve == 8 && pewGun.Magazine.CanReload);
                pewGun.Magazine.StartReload(Time.time);
                yield return new WaitForSeconds(RangedWeapon.Specs[0].reloadSeconds + 0.3f);
                PewCheck("...and it fills the magazine again", pewGun.Magazine.Rounds == 8 && pewGun.Magazine.Reserve == 0);
                // reload by hand after a few shots, then put the gun away mid-reload
                gc.Fire(); yield return new WaitForSeconds(0.31f); gc.Fire(); yield return new WaitForSeconds(0.31f);
                pewGun.Magazine.AddReserve(8);
                PewCheck("the gun can be reloaded by hand when it is partly used", pewGun.Magazine.Rounds == 6 && pewGun.Magazine.StartReload(Time.time));
                pack.SetEquipped(null);
                yield return new WaitForSeconds(0.4f);
                PewCheck("putting the pew away takes it out of the hand and stops the reload", gc.Gun == null && gc.Held == null && !pewGun.Magazine.Reloading && pewGun.Magazine.Rounds == 6);
                pack.SetEquipped(pewItem);
                yield return new WaitForSeconds(0.4f);
                PewCheck("and getting it out again puts it back, with the rounds it had", gc.Gun == pewGun && gc.Held != null && pewGun.Magazine.Rounds == 6);
'@
Rep @'
                PewCheck("in the docks the ammunition is unlimited: 14 shots from a magazine of 10 leave ten rounds and start no reload",
                    pewGun.Magazine.Unlimited && gc.ShotsFired - unlimitedShots0 >= 13 && pewGun.Magazine.Rounds == 10 && !pewGun.Magazine.Reloading && pewGun.Magazine.Reloads == unlimitedReloads0,
'@ @'
                PewCheck("in the docks the ammunition is unlimited: 14 shots from a magazine of 8 leave eight rounds and start no reload",
                    pewGun.Magazine.Unlimited && gc.ShotsFired - unlimitedShots0 >= 13 && pewGun.Magazine.Rounds == 8 && !pewGun.Magazine.Reloading && pewGun.Magazine.Reloads == unlimitedReloads0,
'@
Rep @'
            // ---- 5a. the player''s pew against a guard: two bullets, where ten swings were needed
'@ @'
            // ---- 5a. the player''s pew against a guard: three bullets, where ten swings were needed
'@ 0
Save-Src
Write-Output 'round 7 batch B1c (part 1) patched'

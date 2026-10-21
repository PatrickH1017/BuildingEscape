using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check of the four new guns (the owner, 2026-10-18/19: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack"): each one in the player's hand from the test menu - its model, silencer (the SMG and the rifle
    /// only), foregrip, magazine - firing at the training dummy (damage, rate of fire, pellets), the empty gun reloading with its own sounds, the dealer's ammunition (a gun found and brought back unlocks it, three clips at most),
    /// and a spare clip fitting its own gun only. Run: <c>qa_regress.ps1 -Methods Guns</c> (batch, no graphics needed).
    /// </summary>
    public partial class NetDevRunner
    {
        int gunFails;

        void GunCheck(string name, bool ok, string detail = "")
        {
            if (!ok) gunFails++;
            Log($"[Guns] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>Is the silencer on this gun's barrel (the catalog's measured muzzle face of the gun, on the gun as it is placed): the recess over the barrel end, the axis along the barrel, and - given a muzzle - the muzzle on the silencer's front face.</summary>
        static bool SilencerOnGun(RangedWeapon.Kind kind, Transform model, Transform silencer, Transform muzzle, out string why)
        {
            why = "(no model or silencer)";
            if (!model || !silencer) return false;
            var cat = RangedWeapon.Catalog;
            var gm = cat.Gun((int)kind);
            var tip = model.TransformPoint(gm.muzzleFace);
            var seat = silencer.TransformPoint(new Vector3(0f, 0f, gm.muzzleFace.z - gm.silencerAt.z));
            float off = Vector3.Distance(tip, seat);
            float angle = Vector3.Angle(silencer.TransformDirection(Vector3.forward), model.TransformDirection(Vector3.forward));
            float muzzleOff = muzzle ? Vector3.Distance(muzzle.position, SilencerFront(silencer)) : 0f;
            why = $"(end of the barrel to the bottom of the recess {off * 1000f:0.0} mm, axis off by {angle:0.00} degrees{(muzzle ? $", the muzzle {muzzleOff * 1000f:0.0} mm from the silencer's front face" : "")})";
            return off < 0.002f && angle < 0.5f && muzzleOff < 0.003f;
        }

        static void SetDummyHealth(TrainingDummy d, float value)
        {
            d.maxHealth = value;
            typeof(TrainingDummy).GetProperty("Health").GetSetMethod(true).Invoke(d, new object[] { value });
        }

        IEnumerator GunsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();
            var cam = Camera.main;
            var rig = FindFirstObjectByType<CameraFollow>();
            var gc = player.GetComponent<GunController>();
            var noiseHeard = new List<NoiseEvent>();
            void OnNoise(NoiseEvent n) { noiseHeard.Add(n); }
            NoiseBus.OnNoise += OnNoise;
            RunUnlocks.Reset();
            var S = RangedWeapon.Specs;

            // ================= 1. the models and the sounds are in the catalogs
            foreach (RangedWeapon.Kind k in System.Enum.GetValues(typeof(RangedWeapon.Kind)))
            {
                var gm = RangedWeapon.Catalog.Gun((int)k);
                GunCheck($"catalog: the {S[(int)k].name}'s model is in the weapon catalog, turned the way the others are, with its muzzle face{(S[(int)k].hold == RangedWeapon.HoldStyle.TwoHand ? ", its grip and its foregrip" : "")}",
                    RangedWeapon.Has(k) && gm != null && gm.muzzleFace != Vector3.zero && (S[(int)k].hold == RangedWeapon.HoldStyle.OneHand || (gm.grip != Vector3.zero && gm.foregrip != Vector3.zero)));
            }
            var ac = GameAudio.Catalog;
            GunCheck("catalog: the audio catalog holds the rifle's sounds (3 shots, 2 tails, 4 reload steps, draw, aim) and the shotgun's (3, 2, 5, draw, aim)",
                ac != null && AllSet(ac.rifleShot) && AllSet(ac.rifleTail) && AllSet(ac.rifleReload) && AllSet(ac.rifleEquip) && AllSet(ac.rifleAim) && ac.rifleShot.Length == 3 && ac.rifleTail.Length == 2 && ac.rifleReload.Length == 4
                && AllSet(ac.shotgunShot) && AllSet(ac.shotgunTail) && AllSet(ac.shotgunReload) && AllSet(ac.shotgunEquip) && AllSet(ac.shotgunAim) && ac.shotgunShot.Length == 3 && ac.shotgunTail.Length == 2 && ac.shotgunReload.Length == 5);
            bool casings = true;
            foreach (RangedWeapon.Kind k in System.Enum.GetValues(typeof(RangedWeapon.Kind))) casings &= RangedWeapon.CasingOf(k) != null;
            GunCheck("catalog: every gun throws a case of its own (the SMG the pistol's cartridge, the rifle's, the shotgun's shell and the sniper's cartridges are the pack's)", casings && RangedWeapon.CasingOf(RangedWeapon.Kind.Shotgun) != RangedWeapon.CasingOf(RangedWeapon.Kind.Pew) && RangedWeapon.CasingOf(RangedWeapon.Kind.Rifle) != RangedWeapon.CasingOf(RangedWeapon.Kind.Sniper));

            // ================= 2. every new gun: in the hand, then on the dummy
            if (rig) rig.enabled = false;
            GunController.LimitedAmmoForTests = true;
            var dummy = TrainingDummy.Current;
            GunCheck("the training dummy stands in the dock", dummy != null);
            var menu = new GameObject("TestModeForTest").AddComponent<TestMode>();
            var giveGun = typeof(TestMode).GetMethod("GiveGun", AnyInst);
            Pickup previous = null;
            foreach (var kind in new[] { RangedWeapon.Kind.Smg, RangedWeapon.Kind.Rifle, RangedWeapon.Kind.Shotgun, RangedWeapon.Kind.Sniper })
            {
                var spec = S[(int)kind];
                string tag = spec.name + ":";
                if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
                int equips0 = GunSounds.EquipsPlayed;
                giveGun.Invoke(menu, new object[] { pack, kind });
                yield return new WaitForSeconds(0.9f);
                var item = pack.Items.FirstOrDefault(i => i && i.TryGetComponent(out RangedWeapon rw) && rw.kind == kind);
                previous = item;
                var gun = item ? item.GetComponent<RangedWeapon>() : null;
                GunCheck($"{tag} the test menu puts it in the pack with its own name and weight, and in the hand", item != null && item.Label == spec.name && Mathf.Approximately(item.weight, spec.weight) && pack.Equipped == item && gc.Gun == gun);
                GunCheck($"{tag} its model is in the player's hand, on the right-hand bone, with a muzzle, an ejection port and a foregrip", gc.Held != null && gc.Held.Grip != null && gc.Held.Muzzle != null && gc.Held.Port != null && gc.Held.Foregrip != null && gc.Held.Kind == kind);
                GunCheck($"{tag} the draw sound played", GunSounds.EquipsPlayed > equips0);
                var held = gc.Held;
                var sil = held.Grip.Find("Silencer");
                bool should = spec.CanSilence;
                GunCheck($"{tag} {(should ? "it wears a silencer and is heard only " + spec.silencedNoise + " m away" : "it never wears a silencer and is as loud as it is (" + spec.noise + " m)")}",
                    gun.silenced == should && (sil != null) == should && Mathf.Approximately(gun.NoiseRadius, should ? spec.silencedNoise : spec.noise));
                if (should)
                {
                    string why = "";
                    bool on = SilencerOnGun(kind, held.Grip.Find(spec.name + "_Model"), sil, held.Muzzle, out why);
                    GunCheck($"{tag} the silencer is an extension of the barrel (its recess over the barrel's end, on its line) and the bullet leaves from its front face", on, why);
                    string whyLying = "";
                    bool lying = SilencerOnGun(kind, item.transform.Find(spec.name + "_Model"), item.transform.Find("Silencer"), null, out whyLying);
                    GunCheck($"{tag} the same on the gun lying on the floor", lying, whyLying);
                }
                else GunCheck($"{tag} the gun lying on the floor has no silencer either", item.transform.Find("Silencer") == null);
                float gripToMuzzle = Vector3.Dot(held.Muzzle.position - held.Grip.position, held.Grip.forward);
                GunCheck($"{tag} the muzzle is out in front of the hand along the barrel, and the left hand's place is on the gun between the two", gripToMuzzle > 0.25f * spec.length * held.BodyScale
                    && Vector3.Dot(held.Foregrip.position - held.Grip.position, held.Grip.forward) > 0.05f && Vector3.Dot(held.Foregrip.position - held.Grip.position, held.Grip.forward) < gripToMuzzle,
                    $"(grip to muzzle {gripToMuzzle:0.000} m, body scale {held.BodyScale:0.00}, {held.FitInfo()})");
                GunCheck($"{tag} the magazine holds {spec.magazine} and the gun comes with {spec.spareClips} spare clips, and room for the rest of its limit ({(spec.maxClips > 0 ? spec.maxClips : GunRules.MaxClips)} clips)",
                    gun.Magazine.Capacity == spec.magazine && gun.Magazine.Rounds == spec.magazine && gun.Magazine.Reserve == spec.spareClips * spec.magazine
                    && gun.Magazine.Room == ((spec.maxClips > 0 ? spec.maxClips : GunRules.MaxClips) - 1 - spec.spareClips) * spec.magazine);

                // ---- on the dummy
                if (dummy == null) continue;
                float dist = kind == RangedWeapon.Kind.Shotgun ? 4f : 8f;
                if (!OpenSpot(dummy.transform.position, dist, out var spot, false)) { GunCheck($"{tag} found a clear spot {dist} m from the dummy", false); continue; }
                player.TeleportTo(spot + Vector3.up * 1.1f, player.transform.rotation);
                Face(player, dummy.transform.position, rb);
                var aimAt = dummy.transform.position + Vector3.up * 1.0f;
                yield return new WaitForSeconds(0.6f);
                Face(player, dummy.transform.position, rb);
                AimCamera(cam, player, aimAt);
                yield return null; yield return null;
                AimCamera(cam, player, aimAt);
                SetDummyHealth(dummy, 5000f);
                var fx = GunFx.Get();
                int flashes0 = fx.FlashesShown, tracers0 = fx.TracersShown, cases0 = fx.CasingsShown, shots0 = GunSounds.ShotsPlayed, sf0 = gc.ShotsFired;
                noiseHeard.Clear();
                float h0 = dummy.Health;
                bool fired = gc.Fire();
                yield return null;
                float dmg = h0 - dummy.Health;
                int hits = Mathf.RoundToInt(dmg / spec.damage);
                GunCheck($"{tag} the first shot leaves the gun, one round fewer", fired && gun.Magazine.Rounds == spec.magazine - 1 && gc.ShotsFired == sf0 + 1);
                if (spec.Pellets == 1)
                    GunCheck($"{tag} a bullet takes {spec.damage} health off the dummy", Mathf.Approximately(dmg, spec.damage), $"({h0} -> {dummy.Health})");
                else
                    GunCheck($"{tag} the {spec.Pellets} pellets each take {spec.damage} off the dummy, from {dist} m all {spec.Pellets} land", Mathf.Abs(dmg - hits * spec.damage) < 0.01f && hits == spec.Pellets, $"({hits} of {spec.Pellets} pellets, {dmg} health)");
                GunCheck($"{tag} one flash, {spec.Pellets} tracers, one gunshot with its tail, one case thrown out", fx.FlashesShown == flashes0 + 1 && fx.TracersShown == tracers0 + spec.Pellets && GunSounds.ShotsPlayed == shots0 + 1 && fx.CasingsShown == cases0 + 1,
                    $"(flashes {fx.FlashesShown - flashes0}, tracers {fx.TracersShown - tracers0}, shots {GunSounds.ShotsPlayed - shots0}, cases {fx.CasingsShown - cases0})");
                GunCheck($"{tag} the guards hear it from {gun.NoiseRadius} m", noiseHeard.Any(n => n.Source == player.gameObject && Mathf.Approximately(n.Loudness, gun.NoiseRadius)));
                GunCheck($"{tag} a second pull at once is too soon", !gc.Fire());

                // ---- the rate of fire: the trigger held down for a few seconds
                int steps0 = GunSounds.ReloadStepsPlayed;                      // (the reload of a short magazine can start before the window is over)
                yield return new WaitForSeconds(spec.fireInterval + 0.1f);
                float window = kind == RangedWeapon.Kind.Smg ? 1.0f : kind == RangedWeapon.Kind.Rifle ? 1.0f : kind == RangedWeapon.Kind.Shotgun ? 3.5f : 4.2f;
                int fired0 = gc.ShotsFired; float until = Time.time + window;
                while (Time.time < until) { AimCamera(cam, player, aimAt); gc.Fire(); yield return null; }
                int made = gc.ShotsFired - fired0;
                float expected = window / spec.fireInterval;
                GunCheck($"{tag} held down for {window:0.0} s it fires at its own rate (about {expected:0.0} shots)", made >= Mathf.FloorToInt(expected * 0.75f) && made <= Mathf.CeilToInt(expected) + 1, $"({made} shots)");

                // ---- empty it, the reload starts by itself with the sounds of its own set
                while (gun.Magazine.Rounds > 0 && !gun.Magazine.Reloading)
                {
                    AimCamera(cam, player, aimAt); gc.Fire(); yield return null;
                    if (Time.time > until + 40f) break;
                }
                yield return new WaitForSeconds(0.6f);
                GunCheck($"{tag} the empty gun reloads by itself", gun.Magazine.Reloading);
                yield return new WaitForSeconds(spec.reloadSeconds + 0.4f);
                int steps = GunSounds.ReloadStepsPlayed - steps0;
                int want = GunSounds.ReloadTimes(spec.sounds).Length;
                GunCheck($"{tag} the reload takes {spec.reloadSeconds} s, fills the magazine out of a spare clip and plays all {want} of its sounds", !gun.Magazine.Reloading && gun.Magazine.Rounds == spec.magazine && gun.Magazine.Reserve == (spec.spareClips - 1) * spec.magazine && steps == want, $"(rounds {gun.Magazine.Rounds}, reserve {gun.Magazine.Reserve}, {steps} steps)");

                // ---- another machine's view of a shot: his flash and his pellets
                int trA = fx.TracersShown, flA = fx.FlashesShown;
                gc.ShowRemoteShot(dummy.transform.position + Vector3.up, 2, 4711);
                GunCheck($"{tag} a shot another machine reports is drawn here too: one flash and {spec.Pellets} tracer(s)", fx.FlashesShown == flA + 1 && fx.TracersShown == trA + spec.Pellets, $"(flashes {fx.FlashesShown - flA}, tracers {fx.TracersShown - trA})");
            }
            GunController.LimitedAmmoForTests = false;

            // ================= 3. a gun found and carried back to the docks unlocks its ammunition at the dealer
            if (previous) { pack.Drop(previous); Destroy(previous.gameObject); previous = null; yield return new WaitForSeconds(0.3f); }
            RunUnlocks.Reset();
            var rifleObj = RangedWeapon.Create(RangedWeapon.Kind.Rifle, player.transform.position + Vector3.up * 1.2f + player.transform.forward * 0.6f, null);
            var rifleItem = rifleObj.GetComponent<Pickup>();
            pack.TryStore(rifleItem);
            GunCheck("unlock: nothing is unlocked at the start of a run", RunUnlocks.Mask == 0);
            yield return new WaitForSeconds(1.3f);
            GunCheck("unlock: a rifle carried in the docks is unlocked for the party within a second, and nothing else is", RunUnlocks.Has(RangedWeapon.Kind.Rifle) && !RunUnlocks.Has(RangedWeapon.Kind.Smg) && !RunUnlocks.Has(RangedWeapon.Kind.Sniper) && !RunUnlocks.Has(RangedWeapon.Kind.Shotgun), $"(mask {RunUnlocks.Mask})");
            var rifle = rifleObj.GetComponent<RangedWeapon>();
            GunCheck("unlock: a rifle made by the dealer's own rules (the pack's spare clips), carried in the pack, is a rifle with two spare clips", rifle.Magazine.Reserve == 2 * S[(int)RangedWeapon.Kind.Rifle].magazine && rifle.Magazine.Room == 0);

            // ================= 4. the dealer: ammunition for the gun that was found, three clips at most
            var vendor = Vendor.Current;
            var buyFor = typeof(Vendor).GetMethod("BuyFor", AnyInst);
            hm.AddCash(10000);
            string Buy(int index) => (string)buyFor.Invoke(vendor, new object[] { hm, pack, index });
            const int smgAmmo = 9, rifleAmmo = 10, shotgunAmmo = 11;
            if (vendor == null) GunCheck("dealer: the docks have a dealer", false);
            else
            {
                int cash0 = hm.Cash;
                string said = Buy(rifleAmmo);
                GunCheck("dealer: a rifle with its two spare clips already has all three clips: the dealer will not sell a fourth, and takes no money", said.Contains("three clips") && hm.Cash == cash0 && rifle.Magazine.Reserve == 60, $"(\"{said}\")");
                rifle.Magazine.SetReserve(0);
                cash0 = hm.Cash;
                said = Buy(rifleAmmo);
                GunCheck("dealer: with room, a rifle magazine adds its 30 rounds to the rifle's reserve and costs what the list says", rifle.Magazine.Reserve == 30 && hm.Cash == cash0 - S[(int)RangedWeapon.Kind.Rifle].clipPrice, $"(\"{said}\", reserve {rifle.Magazine.Reserve}, cash {cash0} -> {hm.Cash})");
                Buy(rifleAmmo);
                cash0 = hm.Cash;
                said = Buy(rifleAmmo);
                GunCheck("dealer: two magazines fill the reserve to three clips in all, a third is refused", rifle.Magazine.Reserve == 60 && rifle.Magazine.Room == 0 && said.Contains("three clips") && hm.Cash == cash0);
                cash0 = hm.Cash;
                said = Buy(smgAmmo);
                GunCheck("dealer: ammunition for a gun that has not been found is not for sale", said.Contains("nothing like that") && hm.Cash == cash0, $"(\"{said}\")");
                RunUnlocks.Unlock(RangedWeapon.Kind.Shotgun);
                said = Buy(shotgunAmmo);
                GunCheck("dealer: a gun that was found but is not in the pack: you carry none to load, and nothing is paid", said.Contains("no Shotgun") && hm.Cash == cash0, $"(\"{said}\")");
                var offeredNow = new List<int>(); for (int i = 0; i < Vendor.Catalog.Length; i++) if (Vendor.OnOffer(i, 1)) offeredNow.Add(i);
                GunCheck("dealer: the shop shows the first seven, and the ammunition of the rifle and the shotgun that were found - nothing else", offeredNow.Count == 9 && offeredNow.Contains(rifleAmmo) && offeredNow.Contains(shotgunAmmo) && !offeredNow.Contains(smgAmmo) && !offeredNow.Contains(Vendor.PewIndex));
            }

            // ================= 5. a spare clip fits its own gun, never beyond three clips
            var smgObj = RangedWeapon.Create(RangedWeapon.Kind.Smg, player.transform.position + Vector3.up * 1.2f, null, float.NaN, 0);
            var clipGo = new GameObject("test SMG magazine");
            clipGo.transform.position = player.transform.position + Vector3.up * 1.0f + player.transform.forward * 0.5f;
            clipGo.AddComponent<Rigidbody>();
            clipGo.AddComponent<BoxCollider>();
            var clipPk = clipGo.AddComponent<Pickup>(); clipPk.weight = 0.2f; clipPk.label = "SMG Magazine";
            var clipAmmo = clipGo.AddComponent<AmmoClip>(); clipAmmo.kind = RangedWeapon.Kind.Smg; clipAmmo.rounds = S[(int)RangedWeapon.Kind.Smg].magazine;
            pack.TryStore(clipPk);
            yield return null;
            GunCheck("clip: an SMG magazine is not taken by somebody who carries no SMG: it stays where it lies", clipGo != null && clipGo.activeSelf);
            pack.TryStore(smgObj.GetComponent<Pickup>());
            var smg = smgObj.GetComponent<RangedWeapon>();
            GunCheck("clip: a gun that was found holds its one clip and no spare (the owner: \"Guns found should have one clip in them\")", smg.Magazine.Rounds == 24 && smg.Magazine.Reserve == 0 && smg.Magazine.Room == 48);
            pack.TryStore(clipPk);
            yield return null;
            GunCheck("clip: with an SMG in the pack the magazine is taken: its 24 rounds go into the reserve and the magazine is gone", clipGo == null && smg.Magazine.Reserve == 24, $"(reserve {smg.Magazine.Reserve})");

            NoiseBus.OnNoise -= OnNoise;
            Log($"[Guns] DONE fails={gunFails}");
            Quit(gunFails == 0 ? 0 : 1);
        }
    }
}

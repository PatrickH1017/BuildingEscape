using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Round 7 (the owner's list of the first two real playtests, 2026-10-18): the pure rules of the items that have one. Footsteps of teammates have a radius, quotas are higher, a safe on every floor,
        /// the backup comes faster on the higher floors, floor 3 has more guards, from floor 4 the guards wear body armour; (later batches add their checks here).
        /// </summary>
        static bool MunchSoundOk()
        {
            for (int v = 0; v < OutpostSound.MunchVariants; v++)
            {
                var data = OutpostSound.Munch(v);
                float peak = 0f; foreach (var x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
                if (data.Length < Dsp.Samples(0.2f) || data.Length > Dsp.Samples(0.8f) || peak < 0.3f || peak > 1.001f) return false;
            }
            return true;
        }

        public static void Round7Test()
        {
            // ---- footsteps: the player's own steps are flat, a teammate's and a guard's are 3D (heard near, silent far away)
            Check("footsteps: the player the camera follows hears his own steps flat; a teammate's and a guard's are in 3D", FootstepPlayer.IsFlat(false, true) && !FootstepPlayer.IsFlat(false, false) && !FootstepPlayer.IsFlat(true, false) && !FootstepPlayer.IsFlat(true, true));
            Check("footsteps: a teammate's steps carry a short way (under the guards' 30 m, over his own metre)", FootstepPlayer.TeammateRange > 8f && FootstepPlayer.TeammateRange < GameAudio.DefaultMaxDistance && FootstepPlayer.TeammateNear < FootstepPlayer.TeammateRange);

            // ---- quotas: higher than the old ones by the multiplier, the loot's calibration unchanged
            Check("quota: floor 1 asks for the old quota times the multiplier", TowerRules.QuotaFor(1) == (int)(System.Math.Round(TowerRules.FirstQuota * TowerRules.QuotaMultiplier / 100.0) * 100.0) && TowerRules.QuotaMultiplier > 1.0);
            bool climbs = true; for (int f = 1; f < 12; f++) climbs &= TowerRules.QuotaFor(f + 1) > TowerRules.QuotaFor(f);
            Check("quota: still rising with the floor, none on the docks and the boss floor", climbs && TowerRules.QuotaFor(0) == 0 && TowerRules.QuotaFor(13) == 0);
            Check("quota: the loot's scale of a floor is as it was calibrated (floor 1: 1.0, the same numbers as before the multiplier)", System.Math.Abs(Valuables.Scale(1) - 1.0) < 1e-9 && Valuables.Scale(4) > 1.0 && Valuables.Scale(4) < 3.0);

            // ---- a safe on every floor (floors 1-12), none on the docks or the boss floor
            bool all = true; for (int seed = 0; seed < 50; seed++) for (int f = 1; f <= 12; f++) all &= SafeRules.HasSafe(seed * 977 + 3, f);
            Check("safes: every office floor from 1 to 12 has a safe, whatever the run seed", all && !SafeRules.HasSafe(1, 0) && !SafeRules.HasSafe(1, 13) && SafeRules.SafeFloors(5).Length == 12);
            bool notes = true; for (int f = 1; f <= 12; f++) notes &= SafeRules.NoteFor(11, f) >= 1;
            Check("safes: every guard outpost holds a note about a safe now (each outpost holds one)", notes);

            // ---- the backup comes faster on the higher floors
            bool faster = true; float last = 1000f;
            for (int f = 1; f <= 14; f++) { float i = CustodyRules.BackupInterval(f); if (i > last + 1e-4f) faster = false; last = i; }
            Check("backup: the interval shrinks with the floor (never longer)", faster);
            Check("backup: 30 s on floor 1 as before, 12 s on floor 12 and above, in between on the floors between", Mathf.Approximately(CustodyRules.BackupInterval(1), 30f) && Mathf.Approximately(CustodyRules.BackupInterval(12), 12f) && Mathf.Approximately(CustodyRules.BackupInterval(13), 12f)
                  && CustodyRules.BackupInterval(4) < 30f && CustodyRules.BackupInterval(4) > 12f && CustodyRules.BackupInterval(0) == 30f);
            var clock = new BackupClock();
            clock.Start(CustodyRules.BackupInterval(12));
            bool ran = Mathf.Approximately(clock.Interval, 12f) && !clock.Tick(11f) && clock.Tick(1.1f);
            clock.Start(30f);                                      // (already running: changes nothing)
            Check("backup: the clock runs with the interval it was started with, and a later start does not change it", ran && Mathf.Approximately(clock.Interval, 12f));

            // ---- floor 3 has a decent step more guards than floor 2; every floor has more than the one before
            bool more = true; for (int f = 1; f < 9; f++) more &= GuardRules.PatrolGuards(f + 1, 3) > GuardRules.PatrolGuards(f, 3);
            Check("guards: floor 2 has 5, floor 3 has 8 (three more: a decent step), floor 4 has 9; each floor has more than the one before", GuardRules.PatrolGuards(2, 3) == 5 && GuardRules.PatrolGuards(3, 3) == 8 && GuardRules.PatrolGuards(4, 3) == 9 && GuardRules.PatrolGuards(1, 3) == 4 && more);

            // ---- body armour from floor 4 on: a bullet counts for half the hits, so the pew needs five bullets instead of three
            Check("armour: floors 1-3 have none, floors 4-12 have it, the boss floor does not", !GuardRules.Armoured(1) && !GuardRules.Armoured(3) && GuardRules.Armoured(4) && GuardRules.Armoured(12) && !GuardRules.Armoured(13) && !GuardRules.Armoured(0));
            var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("armour: an armoured officer takes five pew bullets where an unarmoured one takes three", GunRules.BulletsToKill(10, pew.guardHits) == 3 && GunRules.BulletsToKill(10, GuardRules.ArmouredHits(pew.guardHits)) == 5, $"({pew.guardHits} hits a bullet, {GuardRules.ArmouredHits(pew.guardHits)} against armour)");
            Check("armour: a bullet always counts for at least one hit", GuardRules.ArmouredHits(1) == 1 && GuardRules.ArmouredHits(0) == 1 && GuardRules.ArmouredHits(2) == 1 && GuardRules.ArmouredHits(8) == 4);

            // ---- a body goes away (the owner: "Guards bodies should de-spawn"): 15 s, then a second of sinking
            Check("corpse: a body lies there for 15 s, sinks away in one more second and is gone", GuardRules.CorpseSink(0f) == 0f && GuardRules.CorpseSink(14.9f) == 0f && GuardRules.CorpseSink(15.5f) > 0.4f && GuardRules.CorpseSink(15.5f) < 0.6f && GuardRules.CorpseSink(16f) >= 1f && GuardRules.CorpseSink(60f) == 1f);
            Check("corpse: with a shorter wait (the tests) it is the same curve", GuardRules.CorpseSink(1.2f, 1.5f) == 0f && GuardRules.CorpseSink(2.0f, 1.5f) > 0.4f && GuardRules.CorpseSink(2.6f, 1.5f) == 1f);

            // ---- stamina (the owner: "There should be stamina with a stamina bar on the HUD")
            var sm = new StaminaMeter();
            Check("stamina: a new meter is full and the player can sprint", Mathf.Approximately(sm.Fraction, 1f) && sm.CanSprint && !sm.Exhausted);
            sm.Step(3f, true);
            Check("stamina: three seconds of sprinting take 48 of 100", Mathf.Abs(sm.Value - 52f) < 0.01f && sm.CanSprint);
            sm.Step(4f, true);
            Check("stamina: a little over six seconds of sprinting run it dry: winded, no sprint", sm.Value <= 0.001f && sm.Exhausted && !sm.CanSprint);
            sm.Step(0.9f, false);
            Check("stamina: a breather of a second first, nothing comes back in it", sm.Value <= 0.001f && sm.Exhausted);
            sm.Step(1.1f, false);
            Check("stamina: then twelve a second come back (still winded)", Mathf.Abs(sm.Value - 13.2f) < 0.01f && sm.Exhausted && !sm.CanSprint);
            sm.Step(1.1f, false);
            Check("stamina: a quarter of the bar ends the winded spell", sm.Value >= 25f && !sm.Exhausted && sm.CanSprint);
            var m2 = new StaminaMeter(); var m3 = new StaminaMeter();
            m2.Step(2f, true); m3.Step(2f, true, 0.4f);
            Check("stamina: with the drain turned down to 40 % (a buff) the same two seconds cost 40 % as much", Mathf.Abs((100f - m3.Value) - 0.4f * (100f - m2.Value)) < 0.01f);
            var m4 = new StaminaMeter(); m4.Step(5f, false);
            Check("stamina: walking and standing cost nothing", Mathf.Approximately(m4.Value, 100f));
            m2.Refill();
            Check("stamina: a refill (a new floor, a test) makes it full at once", Mathf.Approximately(m2.Value, 100f) && !m2.Exhausted);

            // ---- the silencer: the player's pew is quiet and the officers' stays loud
            var pewSpec = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("silencer: a silenced shot is heard from 12 m, an officer's shot from 45 m", Mathf.Approximately(pewSpec.silencedNoise, 12f) && Mathf.Approximately(pewSpec.noise, 45f) && pewSpec.silencerLength > 0.05f && pewSpec.silencerLength < 0.3f);
            Check("silencer: the shot and its tail are quieter (the flash is smaller, in GunFx)", GunSounds.SilencedShotVolume < 0.6f && GunSounds.SilencedTailVolume < GunSounds.SilencedShotVolume);

            // ---- the terminal: OUTPOST LOCK / OUTPOST UNLOCK work the door of the outpost itself (the owner, 2026-10-18)
            FloorDirectory.ParseTerminal("Outpost Lock", out var tr1, out var tv1);
            FloorDirectory.ParseTerminal("outpost UNLOCK", out var tr2, out var tv2);
            FloorDirectory.ParseTerminal("lock outpost", out var tr3, out var tv3);
            Check("terminal: OUTPOST LOCK and OUTPOST UNLOCK (any capitals, either order) name the outpost's own door and the verb",
                FloorDirectory.IsOutpostName(tr1) && string.Equals(tv1, "lock", System.StringComparison.OrdinalIgnoreCase)
                && FloorDirectory.IsOutpostName(tr2) && string.Equals(tv2, "unlock", System.StringComparison.OrdinalIgnoreCase)
                && FloorDirectory.IsOutpostName(tr3) && string.Equals(tv3, "lock", System.StringComparison.OrdinalIgnoreCase));

            // ---- the PVP room (the owner, 2026-10-17)
            Check("pvp: health 100, a fist takes 8, a bullet 34 (three bullets knock a player out, two do not), a knock-out lasts a few seconds", Mathf.Approximately(PvpRules.MaxHealth, 100f) && PvpRules.FistDamage == 8f && PvpRules.BulletDamage == 34f && 3 * PvpRules.BulletDamage > PvpRules.MaxHealth && 2 * PvpRules.BulletDamage < PvpRules.MaxHealth && PvpRules.KnockoutSeconds >= 3f && PvpRules.KnockoutSeconds <= 6f);
            Check("pvp: health never goes below nothing, and knocked out is at nothing", PvpRules.After(10f, 34f) == 0f && PvpRules.KnockedOut(0f) && !PvpRules.KnockedOut(0.1f) && PvpRules.After(50f, -5f) == 50f);
            Check("pvp: a hit counts only when both are inside the room, neither is down and they are two players", PvpRules.CanHurt(true, true, false, false, false) && !PvpRules.CanHurt(false, true, false, false, false) && !PvpRules.CanHurt(true, false, false, false, false) && !PvpRules.CanHurt(true, true, true, false, false) && !PvpRules.CanHurt(true, true, false, true, false) && !PvpRules.CanHurt(true, true, false, false, true));
            var pvpRoom = new Rect(0.2f, 13.2f, 9.1f, 4.6f);
            Check("pvp: inside the room means inside its rectangle (a hand's breadth of the doorway counts); an empty room holds nobody", PvpRules.Inside(pvpRoom, 5f, 15f) && PvpRules.Inside(pvpRoom, 9.35f, 15f) && !PvpRules.Inside(pvpRoom, 12f, 15f) && !PvpRules.Inside(pvpRoom, 5f, 8f) && !PvpRules.Inside(default, 0f, 0f));

            // ---- spectating (the owner, 2026-10-18)
            Check("spectate: only a teammate who is still free can be watched (not oneself, not a pinned or a busted one)", SpectateRules.Watchable(false, CustodyState.Free) && !SpectateRules.Watchable(true, CustodyState.Free) && !SpectateRules.Watchable(false, CustodyState.Pinned) && !SpectateRules.Watchable(false, CustodyState.Busted));
            Check("spectate: a busted player with a free teammate spectates; a free or pinned one does not; with nobody left to watch nobody does", SpectateRules.Spectating(CustodyState.Busted, 1) && !SpectateRules.Spectating(CustodyState.Free, 2) && !SpectateRules.Spectating(CustodyState.Pinned, 2) && !SpectateRules.Spectating(CustodyState.Busted, 0));
            Check("spectate: D / the left button step forward, A / the right button step back, round a ring of players", SpectateRules.Step(0, 3, 1) == 1 && SpectateRules.Step(2, 3, 1) == 0 && SpectateRules.Step(0, 3, -1) == 2 && SpectateRules.Step(1, 3, -1) == 0 && SpectateRules.Step(0, 0, 1) == 0 && SpectateRules.Step(0, 1, 1) == 0);

            // ---- a donut break
            Check("donut: an unarmed patrolling guard has a small chance; an armed guard, the boss and a guard on a post never stop for one",
                GuardRules.DonutBreak(0f, false, false, false) && !GuardRules.DonutBreak(0.5f, false, false, false) && GuardRules.DonutBreak(0.0149f, false, false, false) && !GuardRules.DonutBreak(0.0151f, false, false, false)
                && !GuardRules.DonutBreak(0f, true, false, false) && !GuardRules.DonutBreak(0f, false, true, false) && !GuardRules.DonutBreak(0f, false, false, true));
            Check("donut: a break lasts a few seconds (long enough to be seen, short enough not to be a nap), and the munch is a real clip", GuardRules.DonutSeconds >= 4f && GuardRules.DonutSeconds <= 10f && MunchSoundOk());
        }
    }
}

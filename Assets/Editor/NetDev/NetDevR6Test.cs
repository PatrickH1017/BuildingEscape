using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Round 6 (the owner's task list of 2026-10-18): the pure rules of the items that have one. Armed officers get faster the higher the floor; (later items add their checks here).
        /// </summary>
        static bool PunchSoundOk()
        {
            for (int v = 0; v < 3; v++)
            {
                var data = OutpostSound.Punch(v);
                float peak = 0f; foreach (var x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
                if (data.Length < Dsp.Samples(0.1f) || data.Length > Dsp.Samples(0.5f) || peak < 0.3f || peak > 1.001f) return false;
            }
            return true;
        }

        public static void Round6Test()
        {
            // ---- armed officers: faster to attack as the floors go up
            bool monotonic = true, bounded = true;
            float la = 9f, ls = 9f, lr = 9f;
            for (int floor = 1; floor <= 14; floor++)
            {
                GunRules.GuardTempo(floor, 4, out float a, out float s, out float r);
                if (a > la + 1e-4f || s > ls + 1e-4f || r > lr + 1e-4f) monotonic = false;
                la = a; ls = s; lr = r;
                bounded &= a >= 0.29f && a <= 0.81f && s >= 0.54f && s <= 0.91f && r >= 0.69f && r <= 0.96f;
            }
            GunRules.GuardTempo(4, 4, out float a4, out float s4, out float r4);
            GunRules.GuardTempo(12, 4, out float a12, out float s12, out float r12);
            GunRules.GuardTempo(13, 4, out float a13, out float s13, out float r13);
            Check("armed officers: the higher the floor, the shorter the aim, the pause between shots and the reload (never longer)", monotonic && bounded);
            Check("...the first armed floor is already a little faster than before (all multipliers under 1) and floor 12 is the fastest", a4 < 1f && s4 < 1f && r4 < 1f && a12 < a4 && s12 < s4 && r12 < r4 && Mathf.Approximately(a12, 0.3f) && Mathf.Approximately(s12, 0.55f));
            Check("...above the top floor nothing gets faster still", Mathf.Approximately(a13, a12) && Mathf.Approximately(s13, s12) && Mathf.Approximately(r13, r12));
            GunRules.GuardTempo(8, ArmedRules.Never, out float an, out _, out _);
            Check("...with no armed floor (the test menu's never) the first-floor values apply", Mathf.Approximately(an, 0.8f));

            // ---- a wipe: the players who are not the host get "Waiting for host..." over the Exit button
            Check("wipe: online, not the host, the team wiped: the pop-up shows", HeistHud.ShowWaitingForHost(true, false, HeistPhase.Failed));
            Check("wipe: the host does not get it (they choose: leave with the party, leave solo, stay)", !HeistHud.ShowWaitingForHost(true, true, HeistPhase.Failed));
            Check("wipe: offline there is nobody to wait for", !HeistHud.ShowWaitingForHost(false, false, HeistPhase.Failed));
            Check("wipe: a won run does not show it either, and neither does a run in progress", !HeistHud.ShowWaitingForHost(true, false, HeistPhase.Won) && !HeistHud.ShowWaitingForHost(true, false, HeistPhase.Heist));
            Check("wipe: the words are the owner's", HeistHud.WaitingText == "Waiting for host...");

            // ---- punching: weaker than any weapon, only with empty hands
            float minStun = 99f, minCool = 99f, minDamage = 99f, minNoise = 99f;
            foreach (var w in MeleeWeapon.Specs) { minStun = Mathf.Min(minStun, w.stun); minCool = Mathf.Min(minCool, w.cooldown); minDamage = Mathf.Min(minDamage, w.damage); minNoise = Mathf.Min(minNoise, w.noise); }
            Check("punch: it stuns less than any weapon, hits for less, makes less noise and comes quicker than the quickest weapon", PunchRules.StunSeconds < minStun && PunchRules.Damage < minDamage && PunchRules.Noise < minNoise && PunchRules.Cooldown < minCool);
            Check("punch: a miss is quieter than a hit, the zone is smaller than a weapon's (1.4 m)", PunchRules.MissNoise < PunchRules.Noise && PunchRules.Radius < 1.4f && PunchRules.HitDelay < 0.22f);
            Check("punch: allowed with empty hands, no menu, free, not busy and the cooldown over", PunchRules.CanPunch(true, true, false, false, false, 5f, 5f));
            Check("punch: not with something in the hands (a weapon, a gun, a thing to throw) or carrying a heavy thing", !PunchRules.CanPunch(false, true, false, false, false, 5f, 0f) && !PunchRules.CanPunch(true, false, false, false, false, 5f, 0f));
            Check("punch: not with a menu up, not pinned or busted, not in the middle of a blow, not before the cooldown is over", !PunchRules.CanPunch(true, true, true, false, false, 5f, 0f) && !PunchRules.CanPunch(true, true, false, true, false, 5f, 0f) && !PunchRules.CanPunch(true, true, false, false, true, 5f, 0f) && !PunchRules.CanPunch(true, true, false, false, false, 4.9f, 5f));
            Check("punch: it has its own sound (three variants, short, audible)", SoundBank.Variants(SoundId.Punch) == 3 && PunchSoundOk());
            Check("punch: a fist has its own kind on the network and no weapon uses it", MeleeWeapon.PunchKind == 255 && System.Enum.GetValues(typeof(MeleeWeapon.Kind)).Length <= 3);

            // ---- the intro: the filled bar saves the vote
            float held = 0f; bool saved = false, droppedWhenReleased = false;
            for (int i = 0; i < 90; i++) { held = IntroRules.Hold(held, true, 1f / 60f); saved = IntroRules.VoteSaved(saved, held); }          // 1.5 s of holding: the bar fills at 1 s
            bool savedAtEnd = saved;
            for (int i = 0; i < 600; i++) { held = IntroRules.Hold(held, false, 1f / 60f); saved = IntroRules.VoteSaved(saved, held); }       // ten seconds with Space let go: the bar is empty
            droppedWhenReleased = !saved;
            Check("intro: the bar fills in a second of holding and the vote is saved", savedAtEnd && !IntroRules.VoteSaved(false, 0.9f) && IntroRules.VoteSaved(false, IntroRules.HoldSeconds));
            Check("intro: letting go of Space afterwards does not take the saved vote back (the bar itself empties)", !droppedWhenReleased && held <= 0.0001f);
            Check("intro: a short hold that does not fill the bar saves nothing", !IntroRules.VoteSaved(false, IntroRules.Hold(0f, true, 0.6f)));
            Check("intro: the vote still takes half of the party (2 of 3, 2 of 4, 3 of 5; 1 of 1)", IntroRules.SkipPassed(2, 3) && IntroRules.SkipPassed(2, 4) && !IntroRules.SkipPassed(1, 3) && IntroRules.SkipPassed(3, 5) && IntroRules.SkipPassed(1, 1));
        }
    }
}

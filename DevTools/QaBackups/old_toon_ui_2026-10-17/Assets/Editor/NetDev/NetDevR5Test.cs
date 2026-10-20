using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The last round's changes that can be checked without playing: where melee weapons may be (only the outpost's desk: two on floor 1, a chance of one on floors 2-12), the officers and the player clips,
        /// the room directory the security computer uses, the spotting delay and the metal lock sound.
        /// </summary>
        public static void Round5Test()
        {
            // ---- melee weapons: only the outpost's desk (OutpostLoot.Roll): floor 1 two of them, floors 2-12 one at most
            bool floor1Two = true, laterAtMostOne = true, deterministic = true;
            int later = 0, laterOne = 0, laterZero = 0;
            for (int seed = 1; seed <= 400; seed++)
                for (int floor = 1; floor <= 12; floor++)
                {
                    var s = OutpostLoot.Roll(seed, floor);
                    var again = OutpostLoot.Roll(seed, floor);
                    deterministic &= s.weapons.SequenceEqual(again.weapons);
                    int melee = s.weapons.Count(w => w != OutpostLoot.PewKind);
                    if (floor == 1) floor1Two &= melee == 2;
                    else
                    {
                        laterAtMostOne &= melee <= 1;
                        later++; if (melee == 1) laterOne++; else laterZero++;
                    }
                }
            Check("melee: floor 1 always has two melee weapons on the outpost's desk", floor1Two);
            Check("melee: floors 2-12 never have two", laterAtMostOne);
            Check($"melee: floors 2-12 have one about half the time ({laterOne} of {later}), and none the rest ({laterZero})", laterOne * 10 >= later * 4 && laterOne * 10 <= later * 8 && laterZero > 0);
            Check("melee: the same run seed and floor always roll the same weapons", deterministic);

            // ---- the officers are the Synty security people, the players use the Kevin Iglesias clips
            var cat = CharacterCatalog.Instance;
            if (cat == null) Check("characters: the catalog exists", false);
            else
            {
                var officers = cat.WithRole(ToonRole.Guard);
                Check($"officers: the Synty security people are the officers ({officers.Count}), the old Tiny Toon police is not",
                    officers.Count >= 2 && officers.All(o => o.id.StartsWith("syn_security")) && cat.Find("police") != null && cat.Find("police").role != ToonRole.Guard);
                bool clipsOk = cat.humanMale != null && cat.humanMale.idle && cat.humanMale.walk && cat.humanMale.run && cat.humanMale.jump
                            && cat.humanFemale != null && cat.humanFemale.idle && cat.humanFemale.walk && cat.humanFemale.run && cat.humanFemale.jump;
                Check("clips: the Human Animations idle, walk, run and jump are in the catalog for the male and the female set", clipsOk);
                bool sets = true;
                foreach (var c in cat.characters.Where(c => c != null && c.id.StartsWith("syn_")))
                    sets &= c.clips == (c.id.Contains("female") ? ToonClipSetId.HumanFemale : ToonClipSetId.HumanMale);
                Check("clips: every Synty person plays the Human Animations set of their gender", sets);
                var blank = ScriptableObject.CreateInstance<CharacterCatalog>();            // (a catalog without the Human clips, as one built without the pack would be)
                Check("clips: a Human set that is missing falls back to the Tiny Toon one (nothing breaks without the pack)",
                    blank.Clips(ToonClipSetId.HumanMale) == blank.male && blank.Clips(ToonClipSetId.HumanFemale) == blank.female);
                Object.DestroyImmediate(blank);
            }

            // ---- the room directory: numbers (any capitals / spaces), executive suites, the server room
            bool found = true, unique = true, roles = true, nothing = true;
            foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4) })
                foreach (int sd in new[] { 4101, 777, 9172 })
                    foreach (int floor in new[] { 1, 2, 7, 12 })
                    {
                        var shape = FloorShape.Maze(new System.Random(sd), cols, rows, 0.12);
                        var lay = OfficePlanner.Build(shape.PlanFor(floor, sd, 18f, 3.5f, 5 * sd + floor));
                        var dir = FloorDirectory.Build(lay, floor, null);
                        unique &= dir.rooms.Select(r => r.label).Distinct().Count() == dir.rooms.Count && dir.rooms.Count > 0;
                        foreach (var room in dir.rooms)
                        {
                            found &= dir.Find(room.label) == room && dir.Find("  " + room.label.ToLowerInvariant() + " ") == room;
                        }
                        nothing &= dir.Find("999") == null && dir.Find("") == null && dir.Find(null) == null && dir.Find("abc") == null;
                        roles &= dir.rooms.Any(r => r.role == OfficePlanner.RoleSuite) && dir.rooms.Count(r => r.role == OfficePlanner.RoleServer) == 1;
                    }
            Check("directory: every numbered room is found by its number whatever the capitals or spaces", found);
            Check("directory: room numbers are unique on a floor, and a wrong number finds nothing", unique && nothing);
            Check("directory: every floor shows its executive suites and its one server room", roles);

            // ---- spotting takes longer the further away the player is
            float s3 = GuardRules.SpotSeconds(3f), s14 = GuardRules.SpotSeconds(14f), s40 = GuardRules.SpotSeconds(40f);
            Check($"spotting: the further away, the longer it takes ({s3:0.00} s at 3 m, {s14:0.00} s at 14 m, {s40:0.00} s at 40 m)",
                s3 < s14 && s14 < s40 && s3 > 0.2f && s3 < 0.8f && s14 > 1.4f && s14 < 2.4f && s40 > 4f && s40 < 6f && GuardRules.SpotSeconds(-5f) == GuardRules.SpotBase);

            // ---- the metal lock sound: loud, ringing, dying away
            var lockClip = OutpostSound.MetalLock();
            float peak = 0f; foreach (var v in lockClip) peak = Mathf.Max(peak, Mathf.Abs(v));
            float early = 0f, late = 0f;
            int a0 = Dsp.Samples(0.0f), a1 = Dsp.Samples(0.25f), b0 = lockClip.Length - Dsp.Samples(0.25f);
            for (int i = a0; i < a1; i++) early += lockClip[i] * lockClip[i];
            for (int i = b0; i < lockClip.Length; i++) late += lockClip[i] * lockClip[i];
            var ring = new float[Dsp.Samples(0.35f)];
            System.Array.Copy(lockClip, Dsp.Samples(0.2f), ring, 0, ring.Length);
            float atRing = Goertzel(ring, 640f), off = Goertzel(ring, 910f);
            Check($"metal lock sound: {lockClip.Length / (float)Dsp.SampleRate:0.00} s long, loud (peak {peak:0.00}), rings at 640 Hz ({atRing / Mathf.Max(1e-9f, off):0}x the level beside it) and dies away",
                lockClip.Length > Dsp.Samples(0.9f) && lockClip.Length < Dsp.Samples(1.4f) && peak > 0.9f && peak <= 1.0f && early > late * 20f && atRing > off * 3f && SoundBank.Variants(SoundId.MetalLock) == 1);
        }
    }
}
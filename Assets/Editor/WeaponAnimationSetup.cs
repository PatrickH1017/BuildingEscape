using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The weapon animations made in Blender (the owner, 2026-10-27): the dock worker's silenced pistol ("I want all available character you pick to be able to use this animation"; <see cref="PistolAnimation"/>), and the officers' pistol,
    /// the SMG, the rifle, the shotgun, the sniper rifle, the crowbar, the bat and the hatchet ("do the same thing with these files"; <see cref="WeaponAnimation"/>). The files in <c>Assets/Resources/Animations</c> are read as HUMANOID
    /// clips, so they play on every character of the game. They hold only the Synty skeleton and its animation; their avatar is the files' own skeleton with the Synty characters' bone mapping AND rest pose copied from
    /// <c>PolygonConstruction/Models/Characters.fbx</c> (an automatic mapping could give the Synty "Finger_01" chain to another finger, and then the hand would not close on the grip; the officers' file was made on the same skeleton),
    /// and the clips are cut from the takes at the frames below. The settings live in the files' .meta: this is needed again only after a new export from Blender.
    /// Menu: Employees Take Revenge > Setup Weapon Animations. Batch: -executeMethod BuildingEscape.EditorTools.WeaponAnimationSetup.BuildBatch
    /// </summary>
    public static class WeaponAnimationSetup
    {
        const string MappingModel = "Assets/Synty/PolygonConstruction/Models/Characters.fbx";
        const string Folder = "Assets/Resources/" + PistolAnimation.Folder + "/";

        /// <summary>A clip cut from a take: its name, the take's frames (Blender's frame numbers, 30 a second) and whether it loops.</summary>
        struct Cut
        {
            public string name; public int first, last; public bool loop;
            public Cut(string n, int f, int l, bool lp = false) { name = n; first = f; last = l; loop = lp; }
        }

        static Cut[] Gun(string n, int raiseFrom, int aimFrom, int aimTo, int shotFrom, int shotTo) => new[]
        {
            new Cut(n + WeaponAnimation.Raise, raiseFrom, aimFrom), new Cut(n + WeaponAnimation.Aim, aimFrom, aimTo, true), new Cut(n + WeaponAnimation.Fire, shotFrom, shotTo),
        };

        static Cut[] Melee(MeleeWeapon.Kind k)
        {
            string n = WeaponAnimation.MeleeName(k);
            return new[] { new Cut(n + WeaponAnimation.Ready, 1, 4, true), new Cut(n + WeaponAnimation.Swing, 1, 46) };
        }

        // the frames of the Blender files:
        //   the dock worker's one-hand file is lowered 1-6, raises 6-18, aims 18-26, fires at 26 (the kick peaks at 28, back on aim at 33), shoots twice more and lowers 64-76 (the raise played backwards); the reload starts and ends on the aim;
        //   the officers' one-hand file has the pistol at his side 1-8, raises 8-24, aims 24-36, fires at 37 (the kick peaks at 38, back on aim at 46) and again at 55; their reload is the dock worker's (the same moments);
        //   the long guns are at the low ready (the muzzle 35 degrees down) 1-8, raise 8-19 (the sniper rifle 8-21), aim until the first shot (24, the sniper rifle 28); the shot clip starts on the aim the frame before a shot that
        //   settles before the next one (the SMG fires every 2-3 frames: its clip is one shot's kick), and the file lowers the gun at its end;
        //   the swings are held ready 1-4, wind up 5-13, strike 17-24 (the weapon's head at its fastest at 19) and are back on the ready at 46.
        static readonly Dictionary<string, Cut[]> Cuts = new Dictionary<string, Cut[]>
        {
            { PistolAnimation.OneHandFile, new[] { new Cut(PistolAnimation.Raise, 6, 18), new Cut(PistolAnimation.Aim, 18, 26, true), new Cut(PistolAnimation.Fire, 26, 33) } },
            { PistolAnimation.ReloadFile, new[] { new Cut(PistolAnimation.Reload, 1, 66) } },
            { WeaponAnimation.GuardOneHandFile, Gun(WeaponAnimation.GuardPistol, 8, 24, 36, 36, 46) },
            { WeaponAnimation.GuardReloadFile, new[] { new Cut(WeaponAnimation.GuardPistol + WeaponAnimation.Reload, 1, 66) } },
            { WeaponAnimation.GunFile(RangedWeapon.Kind.Smg), Gun("Smg", 8, 19, 23, 23, 26) },
            { WeaponAnimation.GunFile(RangedWeapon.Kind.Rifle), Gun("Rifle", 8, 19, 23, 51, 61) },
            { WeaponAnimation.GunFile(RangedWeapon.Kind.Shotgun), Gun("Shotgun", 8, 19, 23, 23, 49) },
            { WeaponAnimation.GunFile(RangedWeapon.Kind.Sniper), Gun("Sniper", 8, 21, 27, 27, 67) },
            { WeaponAnimation.MeleeFile(MeleeWeapon.Kind.Crowbar), Melee(MeleeWeapon.Kind.Crowbar) },
            { WeaponAnimation.MeleeFile(MeleeWeapon.Kind.Bat), Melee(MeleeWeapon.Kind.Bat) },
            { WeaponAnimation.MeleeFile(MeleeWeapon.Kind.Hatchet), Melee(MeleeWeapon.Kind.Hatchet) },
        };

        // the stance files (the owner, 2026-10-27): for each gun the hip-fire and the aimed pose, standing (60 frames) and running (20 / 24 frames), each ONE loop of the whole take, named like its file
        static IEnumerable<KeyValuePair<string, Cut[]>> StanceCuts()
        {
            foreach (var gun in new[] { "Pistol", "Smg", "Rifle", "Shotgun", "Sniper" })
                foreach (var aimed in new[] { false, true })
                    foreach (var run in new[] { false, true })
                    {
                        string f = WeaponAnimation.StanceFile(gun, aimed, run);
                        yield return new KeyValuePair<string, Cut[]>(f, new[] { new Cut(f, 1, run ? (aimed ? 24 : 20) : 60, true) });
                    }
        }

        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]
        static void Menu()
        {
            bool ok = Setup(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Weapon animations", ok ? "The weapon animations are set up as humanoid clips." : "Something went wrong - see the Console.\n\n" + report, "OK");
        }

        /// <summary>For a batch run: set the files up, log what came out and exit (0 = every clip is a humanoid clip).</summary>
        public static void BuildBatch()
        {
            bool ok = Setup(out string report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Setup(out string report)
        {
            var log = new System.Text.StringBuilder("[WeaponAnim] setup\n");
            var source = AssetImporter.GetAtPath(MappingModel) as ModelImporter;
            if (source == null || source.animationType != ModelImporterAnimationType.Human || source.humanDescription.human == null || source.humanDescription.human.Length == 0)
            {
                report = "[WeaponAnim] FAIL: " + MappingModel + " is missing or is not a humanoid model";
                return false;
            }
            var mapping = source.humanDescription;
            bool ok = true;
            foreach (var pair in Cuts.Concat(StanceCuts()))
            {
                string path = Folder + pair.Key + ".fbx";
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { log.AppendLine("[WeaponAnim] FAIL: no model at " + path); ok = false; continue; }

                // 1. a humanoid import with Unity's own mapping: this writes the files' own skeleton (every transform at its rest pose) into the description
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importCameras = false; imp.importLights = false; imp.importVisibility = false;
                imp.importAnimation = true;
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.autoGenerateAvatarMappingIfUnspecified = true;
                if (imp.humanDescription.skeleton == null || imp.humanDescription.skeleton.Length == 0) imp.SaveAndReimport();

                // 2. the Synty characters' mapping and THEIR rest pose (the bone names are the same: Blender's "Thumb_01.001" was exported as Unity's "Thumb_01 1"). The pose Unity reads from the file is the
                // animation's first frame (an arm raised, the body turned), which it would then take for the T pose: the clip would be measured against the wrong pose and come out turned on every character.
                var hd = imp.humanDescription;
                var own = hd.skeleton ?? new SkeletonBone[0];
                var names = new HashSet<string>(own.Select(b => b.name));
                var missing = mapping.human.Where(h => !names.Contains(h.boneName)).Select(h => h.boneName).ToList();
                if (missing.Count > 0 || own.Length == 0) { log.AppendLine($"[WeaponAnim] FAIL: {path} lacks the bones {string.Join(", ", missing)}"); ok = false; continue; }
                var rest = mapping.skeleton.Skip(1).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.First());
                int copied = 0;
                for (int i = 1; i < own.Length; i++)
                {
                    if (!rest.TryGetValue(own[i].name, out var r)) continue;
                    own[i].position = r.position; own[i].rotation = r.rotation; own[i].scale = r.scale;          // (the bone keeps its own parent: only the pose is the characters')
                    copied++;
                }
                hd.skeleton = own;
                log.AppendLine($"[WeaponAnim] {path}: the rest pose of {copied} of {own.Length - 1} bones taken from {MappingModel}");
                hd.human = mapping.human;
                hd.upperArmTwist = mapping.upperArmTwist; hd.lowerArmTwist = mapping.lowerArmTwist;
                hd.upperLegTwist = mapping.upperLegTwist; hd.lowerLegTwist = mapping.lowerLegTwist;
                hd.armStretch = mapping.armStretch; hd.legStretch = mapping.legStretch; hd.feetSpacing = mapping.feetSpacing;
                hd.hasTranslationDoF = false;
                imp.humanDescription = hd;

                // 3. the clips, cut from the take; the pose is kept exactly as it was made (no root motion taken out, nothing squeezed)
                var take = imp.defaultClipAnimations.FirstOrDefault();
                if (take == null) { log.AppendLine("[WeaponAnim] FAIL: " + path + " has no animation take"); ok = false; continue; }
                log.AppendLine($"[WeaponAnim] {path}: take '{take.takeName}' frames {take.firstFrame}-{take.lastFrame}");
                imp.animationCompression = ModelImporterAnimationCompression.Off;
                imp.clipAnimations = pair.Value.Select(c => new ModelImporterClipAnimation
                {
                    name = c.name, takeName = take.takeName, firstFrame = c.first, lastFrame = c.last, loopTime = c.loop,
                    lockRootRotation = true, keepOriginalOrientation = true, lockRootHeightY = true, keepOriginalPositionY = true, lockRootPositionXZ = true, keepOriginalPositionXZ = true,
                    heightFromFeet = false, maskType = ClipAnimationMaskType.None,
                }).ToArray();
                imp.SaveAndReimport();

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                var avatar = assets.OfType<Avatar>().FirstOrDefault();
                log.AppendLine($"[WeaponAnim]   avatar {(avatar ? avatar.name : "none")}: valid {(avatar && avatar.isValid)}, human {(avatar && avatar.isHuman)}");
                if (!avatar || !avatar.isValid || !avatar.isHuman) ok = false;
                foreach (var c in pair.Value)
                {
                    var clip = assets.OfType<AnimationClip>().FirstOrDefault(a => a.name == c.name);
                    log.AppendLine($"[WeaponAnim]   clip {c.name}: {(clip ? $"{clip.length:0.000} s, humanoid {clip.isHumanMotion}, {clip.frameRate} fps" : "MISSING")}");
                    if (!clip || !clip.isHumanMotion) ok = false;
                }
            }
            log.Append(ok ? "[WeaponAnim] DONE ok" : "[WeaponAnim] DONE with FAILURES");
            report = log.ToString();
            return ok;
        }
    }
}

using System.Linq;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The Blender animations of the other weapons (the owner, 2026-10-27: "guards pistol shots/reloads, the hatchet swing, crowbar swing, bat swing, sniper fire, shotgun fire, rife fire and smg fire ... do the same thing with these files"),
    /// made like the dock worker's pistol (<see cref="PistolAnimation"/>) and set up the same way (Editor/WeaponAnimationSetup): humanoid clips in <c>Resources/Animations</c>, so they play on every character the game has.
    ///   - the long guns (the SMG, the rifle, the shotgun, the sniper rifle): from the low ready (the gun across the body, the muzzle 35 degrees down) up to the aim in both hands, the aim, one shot; <see cref="HeldGun"/> holds the gun where
    ///     the Blender files hold it and turns both arms with it to the crosshair (first person keeps the sight picture of 2026-10-23);
    ///   - the officers' pistol: raised from the side, aimed, fired, and the magazine change (the dock worker's pistol clips are the players');
    ///   - the crowbar, the bat and the hatchet: held ready in front, and the swing (a wind-up, the blow, the follow-through and back to the ready), played by <see cref="MeleeController"/>.
    /// <see cref="CharacterAnimator.GunPose"/> and <see cref="CharacterAnimator.MeleePose"/> play them on the upper body over the walk and the run.
    /// </summary>
    public static class WeaponAnimation
    {
        public const string Folder = PistolAnimation.Folder;

        /// <summary>The long guns and the officers' pistol are held with the Blender animations (false: posed by code as before 2026-10-27).</summary>
        public static bool GunsEnabled = true;
        /// <summary>The melee weapons are held and swung with the Blender animations (false: the old slash clip).</summary>
        public static bool MeleeEnabled = true;

        /// <summary>A gun's clips: from the low ready (a pistol: from the side) to the aim, the aim, one shot (it starts on the aim, the kick follows a frame later) and, for a pistol, the reload.</summary>
        public sealed class GunClips
        {
            public string name;
            public AnimationClip raise, aim, fire, reload;
            /// <summary>Held in both hands (the long guns): the left arm is in the clips too, and the gun stays at the low ready when it is not raised.</summary>
            public bool twoHand;
            public bool Valid => raise && aim && fire && raise.isHumanMotion && aim.isHumanMotion && fire.isHumanMotion && (!reload || reload.isHumanMotion);
            /// <summary>The stances (the owner, 2026-10-27): the gun held at the hip and aimed, standing and running, in both hands, as loops (the pistol too, now in both hands).</summary>
            public AnimationClip hipStand, hipRun, aimStand, aimRun;
            /// <summary>The four stance loops are in the build and <see cref="StancesEnabled"/>: the gun is held with them (the raise clip is the low ready only).</summary>
            public bool Stances => StancesEnabled && hipStand && hipRun && aimStand && aimRun && hipStand.isHumanMotion && hipRun.isHumanMotion && aimStand.isHumanMotion && aimRun.isHumanMotion;
        }

        /// <summary>A melee weapon's clips: held ready (a still pose) and the swing (it starts and ends on the ready).</summary>
        public sealed class MeleeClips
        {
            public string name;
            public AnimationClip ready, swing;
            public bool Valid => ready && swing && ready.isHumanMotion && swing.isHumanMotion;
        }

        // ---- names: the files (Resources/Animations/<file>.fbx) and the clips cut from them (Editor/WeaponAnimationSetup)
        public static string GunName(RangedWeapon.Kind k) => k == RangedWeapon.Kind.Smg ? "Smg" : k == RangedWeapon.Kind.Rifle ? "Rifle" : k == RangedWeapon.Kind.Shotgun ? "Shotgun" : k == RangedWeapon.Kind.Sniper ? "Sniper" : "Pistol";
        public static string GunFile(RangedWeapon.Kind k) => GunName(k) + "_Fire";
        public const string GuardPistol = "GuardPistol", GuardOneHandFile = "GuardPistol_OneHand", GuardReloadFile = "GuardPistol_Reload";
        public static string MeleeName(MeleeWeapon.Kind k) => k == MeleeWeapon.Kind.Crowbar ? "Crowbar" : k == MeleeWeapon.Kind.Bat ? "Bat" : "Hatchet";
        public static string MeleeFile(MeleeWeapon.Kind k) => MeleeName(k) + "_Swing";
        public const string Raise = "_Raise", Aim = "_Aim", Fire = "_Fire", Reload = "_Reload", Ready = "_Ready", Swing = "_Swing";

        /// <summary>The guns are held with the owner's stance loops of 2026-10-27 (false: the raise / aim / fire clips of before are used alone).</summary>
        public static bool StancesEnabled = true;

        /// <summary>The file (and the clip in it) of a stance: "Rifle_HipFire_Stand", "Rifle_Aimed_Run" ... (<paramref name="gun"/>: <see cref="GunName"/>).</summary>
        public static string StanceFile(string gun, bool aimed, bool run) => gun + (aimed ? "_Aimed" : "_HipFire") + (run ? "_Run" : "_Stand");

        /// <summary>Loads a gun's four stance loops into <paramref name="c"/> (they stay unset when a file is missing: the gun is then held the way it was before the stances).</summary>
        public static void LoadStances(GunClips c, string gun)
        {
            AnimationClip One(bool aimed, bool run) { string f = StanceFile(gun, aimed, run); return Find(Resources.LoadAll<AnimationClip>(Folder + "/" + f), f); }
            c.hipStand = One(false, false); c.hipRun = One(false, true); c.aimStand = One(true, false); c.aimRun = One(true, true);
            if (!(c.hipStand && c.hipRun && c.aimStand && c.aimRun)) Debug.LogWarning($"[WeaponAnimation] the {gun} stance clips are missing from Resources/{Folder}: the gun is held the old way");
        }

        static GunClips[] guns;
        static bool[] gunLoaded;
        static GunClips guard;
        static bool guardLoaded;
        static MeleeClips[] melee;
        static bool[] meleeLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { guns = null; gunLoaded = null; guard = null; guardLoaded = false; melee = null; meleeLoaded = null; }

        static AnimationClip Find(AnimationClip[] all, string name) => all.FirstOrDefault(a => a && a.name == name);

        /// <summary>A long gun's clips (the pistol's are <see cref="PistolAnimation.Load"/>'s and the officers' <see cref="Guard"/>'s), or null when they are not in the build.</summary>
        public static GunClips Gun(RangedWeapon.Kind k)
        {
            if (k == RangedWeapon.Kind.Pew) return null;
            if (guns == null) { guns = new GunClips[RangedWeapon.Specs.Length]; gunLoaded = new bool[guns.Length]; }
            int i = (int)k;
            if (gunLoaded[i]) return guns[i];
            gunLoaded[i] = true;
            string n = GunName(k);
            var all = Resources.LoadAll<AnimationClip>(Folder + "/" + GunFile(k));
            var c = new GunClips { name = n, raise = Find(all, n + Raise), aim = Find(all, n + Aim), fire = Find(all, n + Fire), twoHand = true };
            if (c.Valid) { LoadStances(c, n); guns[i] = c; }
            else Debug.LogWarning($"[WeaponAnimation] the {n} clips are missing from Resources/{Folder}: the gun is held the old way");
            return guns[i];
        }

        /// <summary>The officers' pistol (raised from the side, aimed, fired, reloaded), or null when the clips are not in the build.</summary>
        public static GunClips Guard()
        {
            if (guardLoaded) return guard;
            guardLoaded = true;
            var one = Resources.LoadAll<AnimationClip>(Folder + "/" + GuardOneHandFile);
            var rel = Resources.LoadAll<AnimationClip>(Folder + "/" + GuardReloadFile);
            var c = new GunClips { name = GuardPistol, raise = Find(one, GuardPistol + Raise), aim = Find(one, GuardPistol + Aim), fire = Find(one, GuardPistol + Fire), reload = Find(rel, GuardPistol + Reload) };
            if (c.Valid && c.reload) guard = c;
            else Debug.LogWarning($"[WeaponAnimation] the officers' pistol clips are missing from Resources/{Folder}: their pistol is held the old way");
            return guard;
        }

        /// <summary>A melee weapon's clips, or null when they are not in the build.</summary>
        public static MeleeClips Melee(MeleeWeapon.Kind k)
        {
            if (melee == null) { melee = new MeleeClips[MeleeWeapon.Specs.Length]; meleeLoaded = new bool[melee.Length]; }
            int i = (int)k;
            if (meleeLoaded[i]) return melee[i];
            meleeLoaded[i] = true;
            string n = MeleeName(k);
            var all = Resources.LoadAll<AnimationClip>(Folder + "/" + MeleeFile(k));
            var c = new MeleeClips { name = n, ready = Find(all, n + Ready), swing = Find(all, n + Swing) };
            if (c.Valid) melee[i] = c;
            else Debug.LogWarning($"[WeaponAnimation] the {n} swing is missing from Resources/{Folder}: it is swung the old way");
            return melee[i];
        }

        /// <summary>The upper body and both arms and hands: what a long gun's clips and a swing move (the legs walk and run as the clips under them have it).</summary>
        public static AvatarMask UpperBody => PistolAnimation.ReloadMask;

        // ---- the swing's moments (all three swings of the Blender files have the same timing, 46 frames at 30 a second): held ready 1-4, the wind-up 5-13, held up 13-16, the blow 17-24 (the head of the weapon at its fastest
        // at 19), held through 24-31, back to the ready 32-46
        /// <summary>When the blow lands, in seconds of the swing clip as it was made (frame 19).</summary>
        public const float SwingContact = 18f / 30f;

        /// <summary>How fast a swing is played: the whole clip in the weapon's cooldown, so a weapon swings as often as before (the crowbar 1.7 times as fast as it was made, the bat 1.9, the hatchet 1.5).</summary>
        public static float SwingSpeed(MeleeClips c, MeleeWeapon.Kind k) => c != null && c.swing ? c.swing.length / Mathf.Max(0.2f, MeleeWeapon.Specs[(int)k].cooldown) : 1f;

        // ---- where the weapon is in the right hand (measured in the Blender files with the weapon's model, see DevTools\WeaponAnim_2026-10-27\measure.py): the hand's frame of PistolAnimation.HandFrame (f to the knuckles, u to the
        // thumb side, the palm), the model's origin along them in units of the character's size, its own +Y and +Z along them, and its scale against the pack's model (the long guns are smaller than the pack makes them, so that both
        // hands reach them as in Blender; a gun's top points to the thumb side and its barrel along the fingers, a melee weapon's handle runs through the fist with its head on the thumb side)
        public struct Hold
        {
            public Vector3 origin, y, z;
            public float scale;
            public Hold(Vector3 o, Vector3 yAxis, Vector3 zAxis, float s) { origin = o; y = yAxis; z = zAxis; scale = s; }
        }

        static readonly Vector3 LongY = new Vector3(-0.06976f, 0.99756f, 0f), LongZ = new Vector3(0.99756f, 0.06975f, 0f);
        static readonly Hold[] GunHolds =
        {
            new Hold(PistolAnimation.GunOrigin, Vector3.up, Vector3.right, 0f),                                // (the pistol: placed by PistolAnimation.PlaceInHand at the game's own size)
            new Hold(new Vector3(0.07835f, 0.00953f, 0.03274f), LongY, LongZ, 0.9171f),                        // SMG (SM_Wep_SubMGun_Lite_01)
            new Hold(new Vector3(0.0922f, 0.02944f, 0.03135f), LongY, LongZ, 0.7882f),                         // rifle (SM_Wep_Rifle_Assault_01)
            new Hold(new Vector3(0.08231f, 0.01014f, 0.03628f), LongY, LongZ, 0.7621f),                        // shotgun (SM_Wep_Shotgun_01)
            new Hold(new Vector3(0.08936f, 0.02198f, 0.02801f), LongY, LongZ, 0.5745f),                        // sniper rifle (SM_Wep_Sniper_Heavy_01)
        };
        static readonly Hold[] MeleeHolds =
        {
            new Hold(new Vector3(0.19176f, 0.41463f, 0.025f), Vector3.up, Vector3.right, 1.0f),                 // crowbar (SM_Wep_Crowbar_01: its +Y to the hook)
            new Hold(new Vector3(0.075f, -0.14f, 0.025f), Vector3.up, Vector3.back, 1.0073f),                   // bat (SM_Wep_Bat_01: its origin at the knob)
            new Hold(new Vector3(0.07306f, 0.16371f, 0.025f), Vector3.up, Vector3.right, 0.7165f),             // hatchet (SM_Wep_Fireaxe_01)
        };
        public static Hold GunHold(RangedWeapon.Kind k) => GunHolds[(int)k];
        public static Hold MeleeHold(MeleeWeapon.Kind k) => MeleeHolds[(int)k];

        /// <summary>The right arm's length (shoulder to elbow to wrist) over the one of the character the clips were made on: how much bigger or smaller this character holds a weapon.</summary>
        public static float SizeOf(Animator anim)
        {
            if (!anim || !anim.isHuman) return 1f;
            var u = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var f = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var h = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (!u || !f || !h) return 1f;
            float l = (f.position - u.position).magnitude + (h.position - f.position).magnitude;
            return l > 0.05f ? l / PistolAnimation.ArmAtSizeOne : 1f;
        }

        /// <summary>The world rotation of a model held as <paramref name="h"/> says in a hand of this frame.</summary>
        static Quaternion HeldRotation(Hold h, Vector3 f, Vector3 u, Vector3 palm)
            => Quaternion.LookRotation(f * h.z.x + u * h.z.y + palm * h.z.z, f * h.y.x + u * h.y.y + palm * h.y.z);

        /// <summary>
        /// Puts a gun's grip transform (<paramref name="grip"/>, a child of the right hand that carries the model <paramref name="model"/> and the gun's markers) where the Blender file holds the gun: the model's origin and axes as
        /// <paramref name="h"/> says, and the model as big as Blender's for this character (<paramref name="size"/>, <see cref="SizeOf"/>; the grip is scaled, so the sight, the silencer, the muzzle and the foregrip come along).
        /// False when the rig has no fingers to measure.
        /// </summary>
        public static bool PlaceGun(Animator anim, Transform grip, Transform model, Hold h, float size)
        {
            if (!grip || !model || !PistolAnimation.HandFrame(anim, out var wrist, out var f, out var u, out var palm)) return false;
            float now = model.lossyScale.x, want = h.scale * size;
            if (now > 1e-6f && want > 1e-6f) grip.localScale *= want / now;
            var rot = HeldRotation(h, f, u, palm);
            grip.rotation = rot * Quaternion.Inverse(model.localRotation);
            grip.position = wrist + (f * h.origin.x + u * h.origin.y + palm * h.origin.z) * size - grip.rotation * Vector3.Scale(model.localPosition, grip.lossyScale);
            return true;
        }

        /// <summary>Puts a melee weapon's model (a child of the right hand) where the Blender file holds it, as big as Blender's for this character. False when the rig has no fingers to measure.</summary>
        public static bool PlaceMelee(Animator anim, Transform model, MeleeWeapon.Kind k, float size)
        {
            if (!model || !model.parent || !PistolAnimation.HandFrame(anim, out var wrist, out var f, out var u, out var palm)) return false;
            var h = MeleeHold(k);
            float parent = model.parent.lossyScale.x;
            if (parent > 1e-6f) model.localScale = Vector3.one * (h.scale * size / parent);
            model.rotation = HeldRotation(h, f, u, palm);
            model.position = wrist + (f * h.origin.x + u * h.origin.y + palm * h.origin.z) * size;
            return true;
        }

        // ---- the long guns' moving parts, as the Blender files move them (in the model's own units, along its -Z; the times after the shot): the SMG's, the rifle's and the sniper rifle's bolt is thrown back by a shot and comes home
        // in two frames; the shotgun is pumped after every shot - the pump and the bolt go back 7-11 frames after it, stay there two frames and go home 13-17 frames after it
        public static readonly float[] BoltKick = { 0f, 0.03177f, 0.05075f, 0f, 0.06962f };
        public const string BoltPart = "Slide", PumpPart = "Pump";
        public const float PumpTravel = 0.1114f, PumpBoltTravel = 0.1004f, PumpBackFrom = 7f / 30f, PumpBack = 11f / 30f, PumpHomeFrom = 13f / 30f, PumpHome = 17f / 30f, BoltBack = 2f / 30f;

        /// <summary>How far back the shotgun's pump is (0..1) this long after a shot.</summary>
        public static float PumpOut(float sinceShot)
        {
            if (sinceShot < PumpBackFrom || sinceShot >= PumpHome) return 0f;
            if (sinceShot < PumpBack) return Mathf.SmoothStep(0f, 1f, (sinceShot - PumpBackFrom) / (PumpBack - PumpBackFrom));
            if (sinceShot < PumpHomeFrom) return 1f;
            return Mathf.SmoothStep(1f, 0f, (sinceShot - PumpHomeFrom) / (PumpHome - PumpHomeFrom));
        }

        /// <summary>How far back a bolt thrown by a shot is (0..1) this long after it.</summary>
        public static float BoltOut(float sinceShot) => sinceShot >= 0f && sinceShot < BoltBack ? 1f - sinceShot / BoltBack : 0f;
    }
}

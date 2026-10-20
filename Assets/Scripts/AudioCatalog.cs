using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The sound effects the game uses (Footsteps Volume 02 + Deadly Kombat packs).
    /// Lives at Assets/Resources/AudioCatalog.asset, created by the editor (Employees Take Revenge > Setup Audio).
    /// </summary>
    public class AudioCatalog : ScriptableObject
    {
        public AudioClip[] stepsPlayer, stepsGuard;
        public AudioClip[] whoosh;
        public AudioClip[] hitBat, hitCrowbar, hitHatchet;
        public AudioClip[] killBat, killCrowbar, killHatchet;
        [Tooltip("Played on top of a killing blow.")]
        public AudioClip[] layerBone, layerGore;

        [Header("Guns (Free Weapon SFX pack, Handgun)")]
        [Tooltip("The pew's gunshots; a tail of the same number is played under each.")]
        public AudioClip[] gunShot, gunTail;
        [Tooltip("The reload in order: slide lock, magazine out, magazine drawn, magazine in, charge.")]
        public AudioClip[] gunReload;
        [Tooltip("Drawing the gun (equipping it), and the rustle of a guard bringing his up to aim.")]
        public AudioClip[] gunEquip, gunAim;

        [Header("Guns: the assault rifle and the shotgun of the same pack (the owner, 2026-10-19)")]
        [Tooltip("The assault rifle's sounds (the pack's Assault Rifle folder): gunshots with their tails, the reload in order (magazine out, drawn, in, charge), drawing it and bringing it up. The sniper rifle uses them too.")]
        public AudioClip[] rifleShot, rifleTail, rifleReload, rifleEquip, rifleAim;
        [Tooltip("The shotgun's sounds (the pack's Shotgun folder): gunshots with their tails, the reload in order (charge, pump back, shell drawn, chambering, pump forward), drawing it and bringing it up.")]
        public AudioClip[] shotgunShot, shotgunTail, shotgunReload, shotgunEquip, shotgunAim;

        // ---- Placeholders. While these are empty the game plays sounds and music it generates in code (see SoundBank and
        // ---- MusicDirector). Drop real clips in and they are used instead, with no other change.
        [Header("Doors, elevator and officers (empty = generated placeholder)")]
        public AudioClip[] doorOpen, doorClose;
        [Tooltip("The elevator's arrival chime.")]
        public AudioClip[] elevatorDing;
        [Tooltip("An officer's key ring jingling as he walks.")]
        public AudioClip[] keys;
        [Tooltip("The same, faster, while an officer runs (empty: the walking clips are used).")]
        public AudioClip[] keysRun;
        [Tooltip("Walkie-talkie static that crackles from an officer every now and then.")]
        public AudioClip[] radioStatic;
        [Tooltip("An officer keying his radio to call for backup.")]
        public AudioClip[] radioCall;

        [Header("Music (empty = generated placeholder)")]
        [Tooltip("Loops while the team is in the elevator.")]
        public AudioClip[] elevatorMusic;
        [Tooltip("Loops from the moment backup is called until the floor is over. A different clip on every floor of a run.")]
        public AudioClip[] intenseMusic;
    }
}

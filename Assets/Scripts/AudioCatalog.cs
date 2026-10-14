using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The sound effects the game uses (Footsteps Volume 02 + Deadly Kombat packs).
    /// Lives at Assets/Resources/AudioCatalog.asset, created by the editor (Overtime Escape > Setup Audio).
    /// </summary>
    public class AudioCatalog : ScriptableObject
    {
        public AudioClip[] stepsPlayer, stepsGuard;
        public AudioClip[] whoosh;
        public AudioClip[] hitBat, hitCrowbar, hitHatchet;
        public AudioClip[] killBat, killCrowbar, killHatchet;
        [Tooltip("Played on top of a killing blow.")]
        public AudioClip[] layerBone, layerGore;
    }
}

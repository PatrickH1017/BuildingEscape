using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The owner's restraint, drag and rescue clips (2026-10-28), humanoid loops and one-shots in <c>Resources/Animations</c> (set up by Editor/WeaponAnimationSetup like the weapons'): each file holds one clip named like itself. The two-character files were
    /// split in two, one clip for each character, and the characters stand at the same origin in both (the dragged body's place behind the dragger is in its own hips): played together at one place they are the scene the Blender file has.
    /// </summary>
    public static class RestraintClips
    {
        public const string Folder = PistolAnimation.Folder;

        // a player pinned by an officer: tied up once (76 frames), then the hogtied loop (90)
        public const string TieUp = "Hogtie_TieUp", Hogtied = "Hogtied_Struggle";
        // a teammate drags a hogtied player by the head, or an eliminated officer by the foot (36-frame loops, in place)
        public const string DragHeadDragger = "DragHead_Dragger", DragHeadDragged = "DragHead_Dragged", DragFootDragger = "DragFoot_Dragger", DragFootDragged = "DragFoot_Dragged";
        // an officer drags a hogtied player by the head (36-frame loops), the captive struggling or not; the officer is shot and drops the head (75 frames, once)
        public const string GuardDragGuard = "GuardDrag_Guard", GuardDragDragged = "GuardDrag_Dragged", GuardDragStruggleDragged = "GuardDragStruggle_Dragged";
        public const string GuardShotGuard = "GuardShot_Guard", GuardShotDragged = "GuardShot_Dragged";
        // a teammate frees a hogtied player (180 frames, once): the rescuer kneels beside him, the ties come off
        public const string FreeUpRescuer = "FreeUp_Rescuer", FreeUpFreed = "FreeUp_Freed";

        public static readonly string[] All =
        {
            TieUp, Hogtied, DragHeadDragger, DragHeadDragged, DragFootDragger, DragFootDragged, GuardDragGuard, GuardDragDragged, GuardDragStruggleDragged, GuardShotGuard, GuardShotDragged, FreeUpRescuer, FreeUpFreed,
        };

        /// <summary>The clips that loop (the others play once).</summary>
        public static bool Loops(string name) => name == Hogtied || name == DragHeadDragger || name == DragHeadDragged || name == DragFootDragger || name == DragFootDragged || name == GuardDragGuard || name == GuardDragDragged || name == GuardDragStruggleDragged;

        static readonly Dictionary<string, AnimationClip> cache = new Dictionary<string, AnimationClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        /// <summary>The clip of this name, or null when it is not in the build (the player is then shown the old way: knocked down).</summary>
        public static AnimationClip Get(string name)
        {
            if (cache.TryGetValue(name, out var c)) return c;
            c = Resources.LoadAll<AnimationClip>(Folder + "/" + name).FirstOrDefault(a => a && a.name == name && a.isHumanMotion);
            if (!c) Debug.LogWarning($"[RestraintClips] the clip {name} is missing from Resources/{Folder}: it is shown the old way");
            cache[name] = c;
            return c;
        }

        /// <summary>Are all the clips there (for the tests)?</summary>
        public static bool AllThere => All.All(n => Get(n) != null);
    }
}

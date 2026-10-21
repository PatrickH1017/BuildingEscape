using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Who may wear a character: players pick Player ones in Customize, guards use Guard ones.</summary>
    public enum ToonRole { Player, Guard, Other }

    /// <summary>Which animation set a character uses (the pack ships separate male, female and zombie clips).</summary>
    /// <remarks>HumanMale / HumanFemale are Kevin Iglesias' Human Animations (idle, walk, run); death and attack fall back to the Tiny Toon male set.</remarks>
    public enum ToonClipSetId { Male, Female, Zombie, HumanMale, HumanFemale }

    /// <summary>
    /// The clips of one Tiny Toon animation set. The rigs are Humanoid, so any set plays on any character;
    /// a missing clip (the female set has no pistol clips) falls back to the male set's.
    /// </summary>
    [System.Serializable]
    public class ToonClipSet
    {
        public AnimationClip idle, walk, run, death, attack, jump;
        public AnimationClip combatIdle, combatRun;
        public AnimationClip pistolIdle, pistolRun, pistolShoot;
        /// <summary>The dazed loop (Blink's StunnedLoop: standing hunched, head down, arms hanging): what a stunned officer plays instead of falling down (the owner, 2026-10-20). Empty = he falls down as before.</summary>
        public AnimationClip stunned;
    }

    /// <summary>A head attachment of a character (a hat, a hard hat, goggles or a beard of the Synty Construction pack): parented to the Head bone with no offset (the pack's pivots are made for that). <see cref="atlas"/> is the colour theme it is drawn in; empty = the character's own atlas.</summary>
    [System.Serializable]
    public class ToonDress
    {
        public GameObject prefab;
        public Texture2D atlas;
    }

    [System.Serializable]
    public class ToonCharacter
    {
        [Tooltip("Stable name used in the saved / shared look string (letters, digits, underscore). Never rename it once friends have saved it.")]
        public string id;
        public string label;
        public GameObject prefab;
        public ToonRole role;
        public ToonClipSetId clips;
        [Tooltip("This character's own colour atlas (the Synty office people); empty = the catalog's atlas (the Tiny Toon people).")]
        public Texture2D atlas;
        [Tooltip("What this character wears on the head (the Construction builders: the pack's character prefabs leave the heads bare). Empty = nothing.")]
        public ToonDress[] dress;
    }

    /// <summary>
    /// The Tiny Toon People characters' and animation clips, so Customize, the lobby stage and the guards can
    /// use them at runtime. Lives at Assets/Resources/CharacterCatalog.asset, created by the editor
    /// (Employees Take Revenge > Setup Characters). Adding characters to the pack and running the setup again is enough.
    /// </summary>
    public class CharacterCatalog : ScriptableObject
    {
        public ToonCharacter[] characters = new ToonCharacter[0];
        [Tooltip("The pack's colour atlas (the pack's own material uses the URP shader, which this project does not render).")]
        public Texture2D atlas;
        public ToonClipSet male = new(), female = new(), zombie = new(), humanMale = new(), humanFemale = new();

        static CharacterCatalog instance;
        /// <summary>The catalog, or null if the pack / setup is missing (the Party Characters then stay).</summary>
        public static CharacterCatalog Instance => instance ? instance : (instance = Resources.Load<CharacterCatalog>("CharacterCatalog"));

        public ToonClipSet Clips(ToonClipSetId id) =>
            id == ToonClipSetId.Female ? female : id == ToonClipSetId.Zombie ? zombie
            : id == ToonClipSetId.HumanMale ? (humanMale != null && humanMale.idle ? humanMale : male)
            : id == ToonClipSetId.HumanFemale ? (humanFemale != null && humanFemale.idle ? humanFemale : female) : male;

        /// <summary>The character with this id, or null.</summary>
        public ToonCharacter Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var c in characters) if (c != null && c.prefab && c.id == id) return c;
            return null;
        }

        public ToonCharacter FirstWithRole(ToonRole role)
        {
            foreach (var c in characters) if (c != null && c.prefab && c.role == role) return c;
            return null;
        }

        /// <summary>Every usable character with this role, in catalog order.</summary>
        public List<ToonCharacter> WithRole(ToonRole role)
        {
            var list = new List<ToonCharacter>();
            foreach (var c in characters) if (c != null && c.prefab && c.role == role) list.Add(c);
            return list;
        }

        /// <summary>The characters a player may pick in Customize (one list, no grouping), in catalog order.</summary>
        public List<ToonCharacter> Players() => WithRole(ToonRole.Player);

        /// <summary>What Customize calls a character: the players are loading dock workers ("Dock worker 1", "Dock worker 2" ... in catalog order; the owner, 2026-10-17 night: it helps players understand they are
        /// roleplaying as one), everybody else keeps the label they have.</summary>
        public string DockLabel(ToonCharacter c)
        {
            if (c == null) return "";
            if (c.role == ToonRole.Player)
            {
                int n = 0;
                foreach (var p in characters)
                {
                    if (p == null || !p.prefab || p.role != ToonRole.Player) continue;
                    n++;
                    if (p == c || p.id == c.id) return "Dock worker " + n;
                }
            }
            return string.IsNullOrEmpty(c.label) ? c.id : c.label;
        }

        /// <summary>Is this a safe id to put in a look string?</summary>
        public static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 24) return false;
            foreach (char ch in id)
                if (!(ch >= 'a' && ch <= 'z') && !(ch >= '0' && ch <= '9') && ch != '_') return false;
            return true;
        }
    }
}

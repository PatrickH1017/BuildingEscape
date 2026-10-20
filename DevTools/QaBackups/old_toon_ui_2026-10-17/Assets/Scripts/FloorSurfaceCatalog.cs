using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The carpets the owner added to the project (2026-10-17: "I added carpet materials to use"), for the rooms of the office floors. Lives at Assets/Resources/FloorSurfaceCatalog.asset,
    /// made by the editor (Employees Take Revenge > Setup Floor Surfaces: it takes every material or texture in the project with "carpet" in its name). It keeps the picture and the tint, not
    /// the material, so no shader of the pack (a URP one would be pink in this built-in pipeline, and would drag a render pipeline into the build) is ever used: the floor builder makes
    /// its own Standard material from them. With no catalog, or an empty one, the rooms get the carpets the game paints itself.
    /// </summary>
    public class FloorSurfaceCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Carpet
        {
            [Tooltip("The material's (or texture's) name.")]
            public string label;
            public Texture2D albedo;
            public Color tint = Color.white;
            [Tooltip("How many metres one repeat of the picture covers on the floor.")]
            public float tileMeters = 2f;
        }

        public Carpet[] carpets = new Carpet[0];

        static FloorSurfaceCatalog instance;
        /// <summary>The catalog, or null when there is none (the painted carpets are used then).</summary>
        public static FloorSurfaceCatalog Instance => instance ? instance : (instance = Resources.Load<FloorSurfaceCatalog>("FloorSurfaceCatalog"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        /// <summary>The carpets that can be used (a picture is there).</summary>
        public int UsableCount
        {
            get { int n = 0; foreach (var c in carpets) if (c != null && c.albedo) n++; return n; }
        }

        /// <summary>The n-th usable carpet (wrapping round), or null when there is none.</summary>
        public Carpet Pick(int n)
        {
            int count = UsableCount;
            if (count == 0) return null;
            int at = ((n % count) + count) % count;
            foreach (var c in carpets) if (c != null && c.albedo && at-- == 0) return c;
            return null;
        }
    }
}

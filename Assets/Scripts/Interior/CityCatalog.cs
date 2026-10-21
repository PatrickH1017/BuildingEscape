using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The plane, the cars and the buildings of the city outside the windows, from the packs the owner added on 2026-10-19 ("use the white plane as the plane in the cinematic now and use the car
    /// assets as the cars driving around the city ... use [the city assets] to make the city outside the windows look more realistic"). A player cannot load a pack's prefab by its path, so the
    /// scenery asks this catalog, which lives at Assets/Resources/CityCatalog.asset (made by the editor: CityCatalogBuilder). With the catalog, or a prefab of it, missing, the scenery falls
    /// back to its plain boxes.
    /// </summary>
    public class CityCatalog : ScriptableObject
    {
        [Tooltip("The white plane of the Intro (the pack's ComPlaneV1; its nose is +z).")]
        public GameObject plane;
        [Tooltip("The cars that drive round the streets of the night city (their noses are +z).")]
        public GameObject[] cars;
        [Tooltip("The buildings that stand round the building (houses, shops, small towers).")]
        public GameObject[] buildings;

        static CityCatalog instance;

        /// <summary>The catalog in Resources (null if it is not there).</summary>
        public static CityCatalog Instance => instance ? instance : (instance = Resources.Load<CityCatalog>("CityCatalog"));
    }

    /// <summary>What the scenery does to a pack's model so that it is only scenery: no colliders, bodies, lights or scripts, and a material that glows by itself at night.</summary>
    public static class CityProps
    {
        /// <summary>Takes the colliders, rigid bodies, lights and scripts off a model made from a pack's prefab (the city is cosmetic: it costs the physics and the NavMesh nothing).</summary>
        public static void Strip(GameObject model)
        {
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
            foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            foreach (var m in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(m);
        }

        /// <summary>True if the material glows by itself (the pack's lamps are made so).</summary>
        public static bool Glows(Material m) => m != null && m.IsKeywordEnabled("_EMISSION") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.01f;

        /// <summary>
        /// A copy of a pack's material that only glows: a matt black Standard material whose emission is the pack's picture times its colour times <paramref name="gain"/>. Light, shadow and
        /// the dim night ambient change nothing of it, like the towers (see ExteriorCity). The caller frees it (OwnedObjects).
        /// </summary>
        public static Material NightCopy(Material src, float gain)
        {
            var m = new Material(Shader.Find("Standard")) { name = "CityGlow " + (src ? src.name : "none"), color = Color.black, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            Color tint = src != null && src.HasProperty("_Color") ? src.color : Color.white;
            if (src != null && src.mainTexture != null) m.SetTexture("_EmissionMap", src.mainTexture);
            m.SetColor("_EmissionColor", new Color(tint.r, tint.g, tint.b, 1f) * gain);
            return m;
        }
    }
}

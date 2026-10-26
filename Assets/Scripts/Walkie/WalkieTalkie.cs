using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The walkie talkie item (the owner, 2026-10-23: "There should be a walkie talkie that the Vendor sells and the player should have to hold out the walkie talkie to use it, please use the SPAWNCAMPGAMES asset"): a pack item (<see cref="Pickup"/>) the Vendor
    /// sells. It is equipped like a weapon (the weapon bar, the pack's EQUIP button): the player HOLDS IT OUT, and only while it is held out is his voice on the radio - <c>VoiceChat.RadioOf</c> answers from the pack every machine mirrors - so two players who both
    /// hold one out hear each other at any distance (<c>VoiceRules.RouteFor</c>). The model is the SpawnCampGames walkie talkie (<see cref="WeaponCatalog.walkie"/>), shown lying about and in the right hand (<see cref="HeldWalkie"/>). It does not equip itself
    /// when it is bought or picked up: holding it out is the player's choice.
    /// </summary>
    public sealed class WalkieTalkie : MonoBehaviour
    {
        /// <summary>Does the weapon catalog have the model?</summary>
        public static bool Available => MeleeWeapon.Catalog && MeleeWeapon.Catalog.walkie;

        public static bool IsWalkie(Pickup p) => p && p.TryGetComponent(out WalkieTalkie _);

        /// <summary>Is a walkie talkie held out (it is the item in the hand) by the owner of this pack?</summary>
        public static bool HoldsOut(Backpack pack) => pack && pack.Equipped && pack.Equipped.TryGetComponent(out WalkieTalkie _);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() { Net.VoiceChat.RadioOf = RadioOf; }

        /// <summary>Does the client with this id hold a walkie talkie out? Every machine mirrors every pack and the item in each hand, so every machine can answer for every player.</summary>
        static bool RadioOf(ulong clientId)
        {
            foreach (var np in Net.NetPlayer.All)
                if (np && np.OwnerClientId == clientId) return HoldsOut(np.GetComponent<Backpack>());
            return false;
        }

        /// <summary>
        /// How the model stands when it is lying about and when it is held: measured from the prefab (its longest side is its length, its thinnest its thickness), so any orientation of the source model works. Returns the rotation that lays the
        /// model flat (its thickness vertical, its length along z) and the scale that makes it <see cref="WalkieRules.ModelHeight"/> long; <paramref name="centre"/> is the middle of its box, <paramref name="size"/> its size before scaling.
        /// </summary>
        public static bool Measure(out Quaternion lay, out float scale, out Vector3 centre, out Vector3 size)
        {
            if (!measured)
            {
                measured = true;
                if (Available && !WeaponCatalog.Measure(MeleeWeapon.Catalog.walkie, Quaternion.identity, null, out _)) Debug.LogWarning("WalkieTalkie: the catalog's walkie prefab has no renderer to measure (is the SpawnCampGames .blend model imported?): a dark box is used.");
                if (Available && WeaponCatalog.Measure(MeleeWeapon.Catalog.walkie, Quaternion.identity, null, out var b))
                {
                    mSize = b.size; mCentre = b.center;
                    int thin = 0, lng = 0;
                    for (int i = 1; i < 3; i++) { if (mSize[i] < mSize[thin]) thin = i; if (mSize[i] > mSize[lng]) lng = i; }
                    if (thin == lng) lng = (thin + 1) % 3;
                    var up = Vector3.zero; up[thin] = 1f;
                    var along = Vector3.zero; along[lng] = 1f;
                    mLay = Quaternion.Inverse(Quaternion.LookRotation(along, up));        // (model axes -> lying axes: the thin side up, the long side along z)
                    mScale = WalkieRules.ModelHeight / Mathf.Max(0.001f, mSize[lng]);
                    mOk = true;
                }
            }
            lay = mLay; scale = mScale; centre = mCentre; size = mSize;
            return mOk;
        }

        static bool measured, mOk;
        static Quaternion mLay = Quaternion.identity;
        static float mScale = 1f;
        static Vector3 mCentre, mSize = Vector3.one * 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { measured = false; mOk = false; }

        /// <summary>A walkie talkie at <paramref name="pos"/>: a real item (a rigid body that falls and is picked up, a <see cref="Pickup"/> with its network id), lying on its back.</summary>
        public static GameObject Create(Vector3 pos, Transform parent, float yaw)
        {
            var go = new GameObject(WalkieRules.Name);
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var box = new Vector3(0.07f, 0.03f, WalkieRules.ModelHeight);                // (the collider when there is no model: a box)
            if (Measure(out var lay, out float scale, out var centre, out var size))
            {
                var model = Object.Instantiate(MeleeWeapon.Catalog.walkie, go.transform, false);
                model.name = "Walkie";
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                model.transform.localRotation = lay;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = -(lay * (centre * scale));              // (its middle on the item's middle)
                var lying = lay * (Vector3.Scale(size, Vector3.one) * scale);
                box = new Vector3(Mathf.Max(0.02f, Mathf.Abs(lying.x)), Mathf.Max(0.015f, Mathf.Abs(lying.y)), Mathf.Max(0.02f, Mathf.Abs(lying.z)));
            }
            else
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);                 // (no model in the catalog: a dark box)
                body.name = "Walkie";
                var col0 = body.GetComponent<Collider>(); if (col0) Object.Destroy(col0);
                body.transform.SetParent(go.transform, false);
                body.transform.localScale = box;
                var r = body.GetComponent<Renderer>();
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse")) { color = new Color(0.12f, 0.13f, 0.15f) };
            }
            var collider = go.AddComponent<BoxCollider>();
            collider.size = box;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = WalkieRules.Weight;
            rb.isKinematic = Net.NetRole.IsRemoteClient;                                  // (the host simulates it; the others follow its snapshots)
            var pk = go.AddComponent<Pickup>();                                           // (registers its network id now)
            pk.weight = WalkieRules.Weight;
            pk.label = WalkieRules.Name;
            go.AddComponent<WalkieTalkie>();
            return go;
        }
    }
}

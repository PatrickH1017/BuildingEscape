using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A piece of armour (the owner, 2026-10-19: Light / Medium / Heavy Armour, found in the safes of floors 3 / 7 / 10 and bought from the dealer once its finder is back in the Loading Docks; <see cref="ArmourRules"/>): an item lying about
    /// - in a safe, on the dealer's shelf - that goes into the pack like any item and is WORN while it is in the pack (<see cref="PlayerArmour"/>); one piece at a time. A piece found and carried back to the docks unlocks its tier at the dealer
    /// for everybody (<see cref="RunUnlocks.CollectFrom"/>). Since 2026-10-23 the piece is the Synty Battle Royale pick-up armour of its tier (<see cref="ArmourFitRules"/>) when the weapon catalog has it; without it, a flat model of boxes in the colours of the guards' vests (<see cref="GuardArmour"/>).
    /// </summary>
    public sealed class ArmourItem : MonoBehaviour
    {
        public ShieldTier tier;

        /// <summary>Pieces made on this machine (for the tests).</summary>
        public static int Made { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Made = 0;

        /// <summary>The size of a piece lying down: across, thick, deep (metres). It lies on its thin side.</summary>
        public static Vector3 Size(ShieldTier tier)
        {
            if (PickupPrefab(tier))
            {
                ArmourFitRules.PickupSize(tier, out float across, out float thick, out float deep);
                return new Vector3(across, thick, deep);
            }
            switch (tier)
            {
                case ShieldTier.Heavy: return new Vector3(0.64f, 0.14f, 0.36f);
                case ShieldTier.Medium: return new Vector3(0.42f, 0.11f, 0.36f);
                default: return new Vector3(0.42f, 0.06f, 0.36f);
            }
        }

        /// <summary>The pack's model of a piece of this tier lying about, or null when the weapon catalog has none (then the boxes are built).</summary>
        public static GameObject PickupPrefab(ShieldTier tier)
        {
            var cat = MeleeWeapon.Catalog;
            return cat ? cat.ArmourPickup(tier) : null;
        }

        static void Part(Transform parent, string partName, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// A piece of this tier at <paramref name="pos"/> (the middle of the piece), turned by <paramref name="yaw"/>: a real item (a rigid body that falls and is picked up, a <see cref="Pickup"/> with the piece's weight and its network id).
        /// The host simulates it; a remote client's copy follows the host's snapshots.
        /// </summary>
        public static GameObject Create(ShieldTier tier, Vector3 pos, Transform parent, float yaw)
        {
            var go = new GameObject(ArmourRules.Name(tier));
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var size = Size(tier);
            var packModel = PickupPrefab(tier);
            if (packModel)
            {
                // the pack's piece, lying flat as it is modelled, in the middle of its collider (the owner, 2026-10-23)
                var model = Object.Instantiate(packModel, go.transform, false);
                model.name = "Armour";
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                ArmourFitRules.PickupCenter(tier, out float cx, out float cy, out float cz);
                model.transform.localScale = Vector3.one * ArmourFitRules.PickupScale;
                model.transform.localPosition = -new Vector3(cx, cy, cz) * ArmourFitRules.PickupScale;
                model.transform.localRotation = Quaternion.identity;
            }
            else
            {
                float bottom = -size.y * 0.5f;
                float slab = tier == ShieldTier.Light ? 0.06f : tier == ShieldTier.Medium ? 0.07f : 0.08f;
                Part(go.transform, "Body", new Vector3(0f, bottom + slab * 0.5f, 0f), new Vector3(0.42f, slab, 0.36f), GuardArmour.VestMaterial(tier));
                if (tier != ShieldTier.Light)
                {
                    bool heavy = tier == ShieldTier.Heavy;
                    var plate = GuardArmour.PlateMaterial(tier);
                    Part(go.transform, "Plate", new Vector3(0f, bottom + slab + 0.02f, 0f), new Vector3(heavy ? 0.36f : 0.30f, 0.04f, heavy ? 0.30f : 0.26f), plate);
                    if (heavy)
                    {
                        Part(go.transform, "PadLeft", new Vector3(-0.26f, bottom + 0.05f, 0f), new Vector3(0.12f, 0.10f, 0.26f), plate);
                        Part(go.transform, "PadRight", new Vector3(0.26f, bottom + 0.05f, 0f), new Vector3(0.12f, 0.10f, 0.26f), plate);
                    }
                }
            }
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = ArmourRules.Weight(tier);
            rb.isKinematic = Net.NetRole.IsRemoteClient;           // (the host simulates it; the others follow its snapshots)
            var pk = go.AddComponent<Pickup>();                    // (registers its network id now, in the same order on every machine)
            pk.weight = ArmourRules.Weight(tier);
            pk.label = ArmourRules.Name(tier);
            go.AddComponent<ArmourItem>().tier = tier;
            Made++;
            return go;
        }
    }

    /// <summary>The armour a player wears: the best piece in his pack (a piece in the pack is on him).</summary>
    public static class PlayerArmour
    {
        public static ShieldTier TierOf(Backpack pack)
        {
            var best = ShieldTier.None;
            if (!pack) return best;
            foreach (var item in pack.Items)
                if (item && item.TryGetComponent(out ArmourItem a) && a.tier > best) best = a.tier;
            return best;
        }

        public static ShieldTier TierOf(WobblyPlayer player) => player ? TierOf(player.GetComponent<Backpack>()) : ShieldTier.None;

        public static bool Wears(Backpack pack) => TierOf(pack) != ShieldTier.None;
    }

    /// <summary>
    /// Shows a player's armour on him: the vest of his tier on his chest (the guards' vests, <see cref="GuardArmour.AddVest"/>), on every machine - each reads the pack it has for that player (every machine mirrors every pack), so it needs no network
    /// message. Rebuilt when the tier changes or the player's model is replaced. Added to every player by <see cref="WobblyPlayer"/>.
    /// </summary>
    public sealed class PlayerArmourVisual : MonoBehaviour
    {
        PlayerVisual visual;
        Backpack pack;
        Transform vest;
        CharacterAnimator vestFor;
        ShieldTier shown;
        float nextCheck;

        /// <summary>The tier on show now (for the tests).</summary>
        public ShieldTier Shown => vest ? shown : ShieldTier.None;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            if (!visual) visual = GetComponent<PlayerVisual>();
            if (!pack) pack = GetComponent<Backpack>();
            var anim = visual ? visual.Anim : null;
            if (!anim || !pack) return;
            var tier = PlayerArmour.TierOf(pack);
            if (tier == shown && anim == vestFor && (vest || tier == ShieldTier.None)) return;
            if (vest) Destroy(vest.gameObject);
            vest = tier != ShieldTier.None ? GuardArmour.AddVest(anim, transform, tier) : null;
            shown = tier;
            vestFor = anim;
        }
    }
}

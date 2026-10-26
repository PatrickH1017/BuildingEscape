using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A consumable of the vending machines (the owner, 2026-10-25; <see cref="ConsumableRules"/>): a pack item like the walkie talkie - a rigid body with a <see cref="Pickup"/> - that is used up when it is drunk: the number key of its kind (<see cref="Keys"/>) or the USE button of the
    /// Tab window (<see cref="Backpack.UseConsumable"/>). The model is a can made of one primitive, tinted by kind (the owner supplies the real models).
    /// </summary>
    public sealed class ConsumableItem : MonoBehaviour
    {
        public ConsumableKind kind;

        public static bool IsConsumable(Pickup p) => p && p.TryGetComponent(out ConsumableItem _);

        /// <summary>The key that uses each kind: the digits after the weapon bar's 1 to 6, and the minus key for the fifth.</summary>
        public static readonly KeyCode[] Keys = { KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus };

        /// <summary>The key's name on the screen ("7", "8", "9", "0", "-").</summary>
        public static readonly string[] KeyNames = { "7", "8", "9", "0", "-" };

        static readonly Color[] tints =
        {
            new Color(1f, 0.85f, 0.1f), new Color(0.2f, 0.8f, 0.3f), new Color(0.45f, 0.55f, 0.75f), new Color(0.75f, 0.6f, 0.95f), new Color(0.75f, 0.15f, 0.1f),
        };

        /// <summary>A can of this kind at <paramref name="pos"/>: a real item (a rigid body that falls and is picked up, a <see cref="Pickup"/> with its network id).</summary>
        public static GameObject Create(ConsumableKind kind, Vector3 pos, Transform parent, float yaw)
        {
            var d = ConsumableRules.Of(kind);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = d.name;
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(0.07f, 0.075f, 0.07f);                          // (a can: 7 cm across, 15 cm tall)
            var pb = new MaterialPropertyBlock();
            var c = tints[Mathf.Clamp((int)kind, 0, tints.Length - 1)];
            pb.SetColor("_Color", c);
            pb.SetColor("_BaseColor", c);
            go.GetComponent<Renderer>().SetPropertyBlock(pb);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = d.weight;
            rb.isKinematic = Net.NetRole.IsRemoteClient;                                          // (the host simulates it; the others follow its snapshots)
            var pk = go.AddComponent<Pickup>();                                                   // (registers its network id now)
            pk.weight = d.weight;
            pk.label = d.name;
            pk.bankValue = 0;                                                                     // (a drink is no loot: it cannot be banked in the elevator)
            go.AddComponent<ConsumableItem>().kind = kind;
            return go;
        }
    }
}

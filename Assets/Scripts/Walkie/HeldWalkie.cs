using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows the walkie talkie in the right hand of the player who holds it out (the owner, 2026-10-23: "the player should have to hold out the walkie talkie to use it"). Added to every player by <see cref="WobblyPlayer"/>; every machine reads the pack
    /// it has for that player (every machine mirrors every pack and the item in each hand), so the radio shows for everybody and needs no network message. The model is held like the handle of a weapon (<see cref="MeleeWeapon.HoldRotation"/>): the
    /// antenna up and forward, the palm round the lower half. Visual only.
    /// </summary>
    public sealed class HeldWalkie : MonoBehaviour
    {
        Backpack pack;
        PlayerVisual vis;
        GameObject model;
        Pickup shown;
        float nextCheck;

        /// <summary>The model in the hand now (for the tests), or null.</summary>
        public GameObject Model => model;

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.15f;
            if (!pack) pack = GetComponent<Backpack>();
            if (!vis) vis = GetComponent<PlayerVisual>();
            if (!pack) return;
            var eq = pack.Equipped;
            if (eq && !eq.TryGetComponent(out WalkieTalkie _)) eq = null;
            if (eq == shown && (model != null || eq == null)) return;           // (the same item in the hand, and its model there)
            Show(eq);
        }

        void OnDisable() { if (model) Destroy(model); model = null; shown = null; }

        void Show(Pickup eq)
        {
            if (model) Destroy(model);
            model = null; shown = eq;
            if (!eq) return;
            var anim = vis ? vis.Anim : null;
            if (!anim) { shown = null; return; }                                  // (the character is not ready yet: try again)
            model = BuildHeld(anim);
            if (!model) shown = null;
        }

        /// <summary>The walkie talkie in the right hand of this character, or null (no model in the catalog, no hand bone). The long side of the radio runs along the weapon's handle direction; the point a little below its middle goes into the palm.</summary>
        public static GameObject BuildHeld(CharacterAnimator anim)
        {
            if (!anim) return null;
            var hand = anim.RightHand;
            if (!hand) hand = anim.transform.Find("Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand");
            if (!hand) hand = FindDeep(anim.transform, "RightHand");
            if (!hand) { Debug.LogWarning("HeldWalkie: RightHand bone not found on the character."); return null; }
            GameObject model;
            if (WalkieTalkie.Measure(out _, out float scale, out var centre, out var size))
            {
                model = Instantiate(MeleeWeapon.Catalog.walkie);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            }
            else
            {
                // no model to measure (none in the catalog, or the .blend did not import): the dark box the item has when it lies about
                scale = 1f; centre = Vector3.zero; size = new Vector3(0.07f, 0.03f, WalkieRules.ModelHeight);
                model = new GameObject();
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(model.transform, false);
                box.transform.localScale = size;
                if (!boxMaterial) boxMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse")) { color = new Color(0.12f, 0.13f, 0.15f) };
                box.GetComponent<Renderer>().sharedMaterial = boxMaterial;
            }
            model.name = "Walkie_Held";
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            model.transform.SetParent(hand, false);
            int lng = 0; for (int i = 1; i < 3; i++) if (size[i] > size[lng]) lng = i;
            var along = Vector3.zero; along[lng] = 1f;
            var turn = MeleeWeapon.HoldRotation * Quaternion.FromToRotation(along, Vector3.up);
            model.transform.localRotation = turn;
            model.transform.localScale = Vector3.one * scale / Mathf.Max(0.01f, hand.lossyScale.y);
            var grip = centre; grip[lng] = centre[lng] - size[lng] * 0.17f;            // (a little below the middle)
            var palm = hand.position + hand.TransformDirection(Vector3.left) * 0.07f;
            model.transform.position = palm - hand.rotation * (turn * (grip * scale));
            return model;
        }

        static Material boxMaterial;

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
            return null;
        }
    }
}

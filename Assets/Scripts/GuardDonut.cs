using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The donut an unarmed guard stops to eat now and then (the owner, 2026-10-18: "Crazy chances that a guard (not the armed guards) will randomly stop what they are doing to eat a donut"). Only the
    /// picture: a ring of dough with pink icing and a dark hole, in his right hand while <see cref="GuardAI.Eating"/>; the rule is <see cref="GuardRules.DonutBreak"/> and the stop is in
    /// <see cref="GuardAI"/>'s patrol. Every machine shows it from the flag it has.
    /// </summary>
    public static class GuardDonut
    {
        static Material dough, icing, hole;

        static Material Make(ref Material cache, Color colour)
        {
            if (cache) return cache;
            var sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) sh = Shader.Find("Sprites/Default");
            cache = new Material(sh) { name = "Donut", color = colour };
            if (cache.HasProperty("_Glossiness")) cache.SetFloat("_Glossiness", 0.2f);
            return cache;
        }

        static void Disc(Transform parent, string name, float y, float diameter, float thickness, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);               // (the primitive is 2 m tall)
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>Puts a donut in the right hand of this officer's model (hidden: <see cref="GuardVisual"/> shows it while he eats) and returns it, or null when there is no hand to put it in.</summary>
        public static Transform Attach(CharacterAnimator model)
        {
            if (!model) return null;
            var hand = model.RightHand;
            if (!hand) return null;
            var root = new GameObject("Donut").transform;
            root.SetParent(hand, false);
            root.localPosition = new Vector3(0f, 0.06f, 0.05f);
            root.localScale = Vector3.one / Mathf.Max(0.01f, hand.lossyScale.y);                      // (the hand bone may be scaled)
            Disc(root, "Dough", 0f, 0.12f, 0.035f, Make(ref dough, new Color(0.80f, 0.56f, 0.28f)));
            Disc(root, "Icing", 0.02f, 0.112f, 0.012f, Make(ref icing, new Color(0.95f, 0.42f, 0.62f)));
            Disc(root, "Hole", 0.027f, 0.036f, 0.006f, Make(ref hole, new Color(0.14f, 0.09f, 0.06f)));
            root.gameObject.SetActive(false);
            return root;
        }
    }
}

using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Body armour (the owner, 2026-10-18: "Wave 4 guards should have body armour"): the vest an armoured officer wears (<see cref="GuardRules.Armoured"/>: from floor 4 on).
    /// This is only the picture, built from boxes and put on the chest bone so that it moves and falls with the officer; what armour does (a bullet counts for half the hits)
    /// is <see cref="GuardAI.Armoured"/>. Every machine builds it by itself (it only needs the floor number), so it needs no network message.
    /// </summary>
    public static class GuardArmour
    {
        static Material vestMat, plateMat;

        static Material Make(ref Material cache, Color colour)
        {
            if (cache) return cache;
            var sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) sh = Shader.Find("Sprites/Default");
            cache = new Material(sh) { name = "GuardArmour", color = colour };
            if (cache.HasProperty("_Glossiness")) cache.SetFloat("_Glossiness", 0.25f);
            return cache;
        }

        static void Part(Transform parent, string partName, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = partName;
            var col = go.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>
        /// Puts a vest on the chest of this officer's model (on its chest bone) and returns it, or null when there is no model. <paramref name="root"/> is the officer (it gives the way he faces).
        /// </summary>
        public static Transform AddVest(CharacterAnimator model, Transform root)
        {
            if (!model) return null;
            var animator = model.GetComponent<Animator>();
            Transform bone = null;
            if (animator && animator.isHuman)
            {
                bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (!bone) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (!bone) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            if (!bone) bone = model.transform;
            var vest = new GameObject("BodyArmour").transform;
            // built upright and facing the way he faces, then handed to the bone (keeping where it is), so that it follows the body
            vest.SetPositionAndRotation(bone.position, root ? root.rotation : bone.rotation);
            var dark = Make(ref vestMat, new Color(0.10f, 0.12f, 0.17f));
            var plate = Make(ref plateMat, new Color(0.32f, 0.34f, 0.38f));
            Part(vest, "Vest", Vector3.zero, new Vector3(0.40f, 0.34f, 0.27f), dark);
            Part(vest, "PlateFront", new Vector3(0f, 0.01f, 0.14f), new Vector3(0.30f, 0.26f, 0.04f), plate);
            Part(vest, "PlateBack", new Vector3(0f, 0.01f, -0.14f), new Vector3(0.30f, 0.26f, 0.04f), plate);
            vest.SetParent(bone, true);
            return vest;
        }
    }
}

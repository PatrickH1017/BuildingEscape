using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The shield an officer wears (the owner, 2026-10-18: "Wave 4 guards should have body armour"; 2026-10-19: "light shield", "medium shield", "heavy shield", "no shield"): the vest, in three tiers (<see cref="ShieldTier"/>). This is only the
    /// picture, built from boxes and put on the chest bone so that it moves and falls with the officer; what a shield does (it takes a share of every bullet: <see cref="GuardLoadouts.BulletShare"/>) is <see cref="GuardAI.BulletHitHost"/>.
    /// A light one is a plain blue-grey vest, a medium one a dark vest with a plate front and back, a heavy one a bulky amber-trimmed vest with big plates and shoulder pads. The host streams which tier he wears
    /// (<see cref="GuardLoadout.Pack"/>), so every machine builds the same.
    /// </summary>
    public static class GuardArmour
    {
        static readonly Material[] vestMats = new Material[4], plateMats = new Material[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { for (int i = 0; i < 4; i++) { vestMats[i] = null; plateMats[i] = null; } }

        static Material Make(Material[] cache, int tier, Color colour)
        {
            if (cache[tier]) return cache[tier];
            var sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "GuardArmour " + (ShieldTier)tier, color = colour };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
            cache[tier] = m;
            return m;
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

        /// <summary>The cloth of a vest of this tier (also of the armour lying about, <see cref="ArmourItem"/>).</summary>
        public static Material VestMaterial(ShieldTier tier)
        {
            int t = (int)tier;
            switch (tier)
            {
                case ShieldTier.Light: return Make(vestMats, t, new Color(0.20f, 0.26f, 0.38f));
                case ShieldTier.Heavy: return Make(vestMats, t, new Color(0.14f, 0.13f, 0.12f));
                default: return Make(vestMats, t, new Color(0.10f, 0.12f, 0.17f));
            }
        }

        /// <summary>The plates of a vest of this tier (a light one has none: its cloth).</summary>
        public static Material PlateMaterial(ShieldTier tier)
        {
            int t = (int)tier;
            switch (tier)
            {
                case ShieldTier.Light: return VestMaterial(tier);
                case ShieldTier.Heavy: return Make(plateMats, t, new Color(0.80f, 0.55f, 0.12f));
                default: return Make(plateMats, t, new Color(0.32f, 0.34f, 0.38f));
            }
        }

        /// <summary>The Synty Battle Royale vest of this tier (the owner, 2026-10-23), or null when the weapon catalog has none: then the boxes below are used.</summary>
        public static GameObject WornPrefab(ShieldTier tier)
        {
            var cat = MeleeWeapon.Catalog;
            return cat ? cat.ArmourWorn(tier) : null;
        }

        /// <summary>How many parts the "BodyArmour" object of this tier has now: one when the pack's models are in the catalog (<see cref="ArmourFitRules.VestParts"/>).</summary>
        public static int VestParts(ShieldTier tier) => ArmourFitRules.VestParts(tier, WornPrefab(tier) != null);

        /// <summary>
        /// The pack's vest of this tier on a person: scaled to his torso (his hips to his neck, his shoulder to shoulder: <see cref="ArmourFitRules.WornScale"/>), turned the way he faces, put on his hips and handed to <paramref name="chestBone"/> so that it
        /// moves with him. Null when he is no humanoid with hips and a neck, or the catalog has no model: then the old boxes are built. A holder without a scale carries the model, so that a bone that is turned does not shear it.
        /// </summary>
        static Transform AddPackVest(Animator animator, Transform root, ShieldTier tier, Transform chestBone)
        {
            var prefab = WornPrefab(tier);
            if (!prefab || !animator || !animator.isHuman || !chestBone) return null;
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (!neck) neck = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!hips || !neck) return null;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float shoulders = 0f;
            if (left && right) { var d = left.position - right.position; d.y = 0f; shoulders = d.magnitude; }
            if (!ArmourFitRules.WornScale(hips.position.y, neck.position.y, shoulders, out float sx, out float sy, out float sz)) return null;

            var holder = new GameObject("BodyArmour").transform;
            float yaw = root ? root.eulerAngles.y : chestBone.eulerAngles.y;
            holder.SetPositionAndRotation(hips.position, Quaternion.Euler(0f, yaw, 0f));
            var model = Object.Instantiate(prefab, holder, false);
            model.name = "Armour";
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);
            model.transform.localPosition = new Vector3(0f, -ArmourFitRules.WornDrop(sy), 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = new Vector3(sx, sy, sz);
            holder.SetParent(chestBone, true);
            return holder;
        }

        /// <summary>
        /// Puts a vest of this tier on the chest of this officer's model (on its chest bone) and returns it (named "BodyArmour"), or null when there is no model or no shield. <paramref name="root"/> is the officer (it gives the way he faces).
        /// Since 2026-10-23 it is the Synty Battle Royale armour of the tier when the weapon catalog has it (<see cref="AddPackVest"/>), else the boxes.
        /// </summary>
        public static Transform AddVest(CharacterAnimator model, Transform root, ShieldTier tier = ShieldTier.Medium)
        {
            if (!model || tier == ShieldTier.None) return null;
            var animator = model.GetComponent<Animator>();
            Transform bone = null;
            if (animator && animator.isHuman)
            {
                bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (!bone) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (!bone) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            if (!bone) bone = model.transform;
            var packVest = AddPackVest(animator, root, tier, bone);
            if (packVest) return packVest;
            var vest = new GameObject("BodyArmour").transform;
            // built upright and facing the way he faces, then handed to the bone (keeping where it is), so that it follows the body
            vest.SetPositionAndRotation(bone.position, root ? root.rotation : bone.rotation);
            switch (tier)
            {
                case ShieldTier.Light:
                {
                    var cloth = VestMaterial(tier);
                    Part(vest, "Vest", Vector3.zero, new Vector3(0.38f, 0.30f, 0.25f), cloth);
                    break;
                }
                case ShieldTier.Heavy:
                {
                    var dark = VestMaterial(tier);
                    var plate = PlateMaterial(tier);
                    Part(vest, "Vest", Vector3.zero, new Vector3(0.48f, 0.42f, 0.33f), dark);
                    Part(vest, "PlateFront", new Vector3(0f, 0.01f, 0.18f), new Vector3(0.40f, 0.36f, 0.06f), plate);
                    Part(vest, "PlateBack", new Vector3(0f, 0.01f, -0.18f), new Vector3(0.40f, 0.36f, 0.06f), plate);
                    Part(vest, "PadLeft", new Vector3(-0.28f, 0.19f, 0f), new Vector3(0.14f, 0.10f, 0.26f), plate);
                    Part(vest, "PadRight", new Vector3(0.28f, 0.19f, 0f), new Vector3(0.14f, 0.10f, 0.26f), plate);
                    break;
                }
                default:
                {
                    var dark = VestMaterial(tier);
                    var plate = PlateMaterial(tier);
                    Part(vest, "Vest", Vector3.zero, new Vector3(0.40f, 0.34f, 0.27f), dark);
                    Part(vest, "PlateFront", new Vector3(0f, 0.01f, 0.14f), new Vector3(0.30f, 0.26f, 0.04f), plate);
                    Part(vest, "PlateBack", new Vector3(0f, 0.01f, -0.14f), new Vector3(0.30f, 0.26f, 0.04f), plate);
                    break;
                }
            }
            vest.SetParent(bone, true);
            return vest;
        }
    }
}

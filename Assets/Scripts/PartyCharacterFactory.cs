using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>One character design: a Party Characters body colour, face, hat and build, or a Tiny Toon person.</summary>
    public sealed class CharacterLook
    {
        public readonly string name;
        public readonly Color body;
        /// <summary>The pack's face textures: 1 = big grin, 2 = angry, 3 = sad.</summary>
        public readonly int face;
        /// <summary>"chef hat", "orange fedora", "party hat" or null for no hat.</summary>
        public readonly string hat;
        /// <summary>Build multipliers on top of the base scale (1 = normal).</summary>
        public readonly float height, width;
        /// <summary>A CharacterCatalog id (a Tiny Toon person; colour, face and hat then do not apply), or null for the Party character.</summary>
        public readonly string model;

        public CharacterLook(string name, Color body, int face, string hat, float height = 1f, float width = 1f, string model = null)
        {
            this.name = name; this.body = body; this.face = face; this.hat = hat;
            this.height = height; this.width = width; this.model = model;
        }

        /// <summary>The same look wearing another model (keeps the build, so guards stay different sizes).</summary>
        public CharacterLook WithModel(string modelId) => new CharacterLook(name, body, face, hat, height, width, modelId);
    }

    /// <summary>
    /// Dresses a greybox capsule (player, guard, vendor) as a Party Characters pack character: swaps the
    /// capsule's look for the pack's animated model and applies a colour, face and hat.
    ///
    /// The pack's own materials use the URP shader, which would render pink in this built-in
    /// render pipeline project. So the colours, faces and hats are rebuilt here with the Standard shader
    /// (the pack's materials are only used as the source of the colours). The capsule collider stays
    /// for physics; only its renderers are switched off.
    /// </summary>
    public static class PartyCharacterFactory
    {
        const string PrefabPath = "Prefabs/character_default";
        const string HeadPath = "Armature/Hips/Spine/Spine1/Spine2/Head/customize_objects";

        /// <summary>Makes the pack's character about as tall as the 2 m greybox capsule.</summary>
        public const float BaseScale = 1.05f;

        // Teammates: bright and cheerful. Player 1 gets the first look, player 2 the second, ...
        public static readonly CharacterLook[] PlayerLooks =
        {
            new CharacterLook("Mint",   new Color(0.251f, 0.902f, 0.696f), 1, "party hat"),
            new CharacterLook("Sunny",  new Color(1.000f, 1.000f, 0.000f), 1, null),
            new CharacterLook("Bubble", new Color(1.000f, 0.400f, 0.700f), 1, "chef hat"),
            new CharacterLook("Splash", new Color(0.000f, 1.000f, 1.000f), 1, null),
            new CharacterLook("Ember",  new Color(1.000f, 0.480f, 0.200f), 1, "orange fedora"),
        };

        // Guards: dark colours, angry or manic faces, different builds and hats.
        public static readonly CharacterLook[] GuardLooks =
        {
            new CharacterLook("Brute",         new Color(0.400f, 0.000f, 0.000f), 2, null,            1.25f, 1.35f),
            new CharacterLook("Boss",          new Color(0.113f, 0.113f, 0.113f), 2, "orange fedora", 1.10f, 1.00f),
            new CharacterLook("Line Cook",     new Color(0.400f, 0.200f, 0.000f), 2, "chef hat",      1.00f, 1.15f),
            new CharacterLook("Party Crasher", new Color(0.200f, 0.000f, 0.400f), 1, "party hat",     1.00f, 1.00f),
            new CharacterLook("Creeper",       new Color(0.000f, 0.400f, 0.000f), 1, null,            1.20f, 0.80f),
            new CharacterLook("Enforcer",      new Color(0.200f, 0.100f, 0.040f), 2, null,            1.05f, 1.20f),
        };

        /// <summary>The basement shopkeeper: friendly orange salesman.</summary>
        public static readonly CharacterLook VendorLook =
            new CharacterLook("Dealer", new Color(0.090f, 0.060f, 0.120f), 2, "orange fedora", 0.95f, 1.1f); // dark hoodie, angry eyes, hunched

        static readonly Dictionary<Color, Material> bodyMats = new();
        static readonly Dictionary<int, Material> faceMats = new();
        static Material hatMat;

        /// <summary>
        /// Puts the model on <paramref name="host"/> (a 2 m tall capsule centred on its transform) and returns its
        /// animation driver, or null if the pack isn't there (the greybox capsule then stays).
        /// </summary>
        public static CharacterAnimator Attach(GameObject host, CharacterLook look)
        {
            if (!string.IsNullOrEmpty(look.model))
            {
                // a Tiny Toon person; if this machine lacks that character, the Party model below is used instead
                var toon = ToonCharacterFactory.Attach(host, look);
                if (toon) return toon;
            }

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (!prefab)
            {
                Debug.LogWarning($"Party character prefab not found (Resources/{PrefabPath}); keeping the greybox look for {host.name}.");
                return null;
            }

            // retire the greybox look (capsule, face parts, nose); colliders stay for physics
            foreach (var r in host.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var model = Object.Instantiate(prefab, host.transform);
            model.name = $"Model_{look.name}";
            model.transform.localPosition = new Vector3(0f, -1f, 0f); // feet on the floor
            model.transform.localRotation = Quaternion.identity;      // the pack's characters face +Z
            model.transform.localScale = new Vector3(BaseScale * look.width, BaseScale * look.height, BaseScale * look.width);

            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr)
            {
                var mats = smr.sharedMaterials;
                var body = BodyMaterial(look.body);
                var face = FaceMaterial(look.face);
                if (mats.Length > 0 && body) mats[0] = body;
                if (mats.Length > 1 && face) mats[1] = face;
                smr.sharedMaterials = mats;
            }

            var anchor = model.transform.Find(HeadPath);
            if (anchor && !string.IsNullOrEmpty(look.hat))
            {
                var hatPrefab = Resources.Load<GameObject>("Prefabs/Hats/" + look.hat);
                var m = HatMaterial();
                if (hatPrefab)
                {
                    var hat = Object.Instantiate(hatPrefab, anchor, false);
                    if (m) foreach (var r in hat.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m;
                }
            }

            var animator = model.GetComponent<Animator>();
            if (animator) animator.applyRootMotion = false; // the capsule moves, not the animation

            return model.AddComponent<CharacterAnimator>();
        }

        // ------------------------------------------------------------------ materials

        static Shader Lit() => Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");

        static void SetFloat(Material m, string prop, float v) { if (m.HasProperty(prop)) m.SetFloat(prop, v); }

        static Material BodyMaterial(Color c)
        {
            if (bodyMats.TryGetValue(c, out var m) && m) return m;
            var sh = Lit(); if (!sh) return null;
            m = new Material(sh) { name = "PartyBody", color = c };
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Glossiness", 0.35f);
            bodyMats[c] = m;
            return m;
        }

        /// <summary>The face PNGs have transparent backgrounds, so this one renders in Standard's Fade mode.</summary>
        static Material FaceMaterial(int n)
        {
            n = Mathf.Clamp(n, 1, 3);
            if (faceMats.TryGetValue(n, out var m) && m) return m;
            var sh = Lit(); if (!sh) return null;
            m = new Material(sh)
            {
                name = $"PartyFace{n}",
                mainTexture = Resources.Load<Texture2D>($"Materials/Face Images/face {n}"),
                color = Color.white
            };
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Glossiness", 0f);
            if (m.HasProperty("_Mode"))
            {
                m.SetFloat("_Mode", 2f); // Fade
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            faceMats[n] = m;
            return m;
        }

        /// <summary>Hats are painted from the pack's colour palette texture.</summary>
        static Material HatMaterial()
        {
            if (hatMat) return hatMat;
            var sh = Lit(); if (!sh) return null;
            hatMat = new Material(sh)
            {
                name = "PartyHat",
                mainTexture = Resources.Load<Texture2D>("Materials/ColorPalette"),
                color = Color.white
            };
            SetFloat(hatMat, "_Metallic", 0f);
            SetFloat(hatMat, "_Glossiness", 0.3f);
            return hatMat;
        }
    }
}

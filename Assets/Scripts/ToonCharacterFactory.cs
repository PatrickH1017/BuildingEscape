using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Dresses a greybox capsule (player, guard) as a Tiny Toon person from the CharacterCatalog and gives it the
    /// clip-mode <see cref="CharacterAnimator"/>. Called by <see cref="PartyCharacterFactory.Attach"/> when the look
    /// names a model; it returns null (and the Party character is used instead) if this machine has no such character.
    ///
    /// The pack's own material uses the URP shader, which this built-in render pipeline project cannot draw (pink),
    /// so one Standard-shader material with the pack's colour atlas is put on every renderer instead.
    /// </summary>
    public static class ToonCharacterFactory
    {
        /// <summary>Visible height in metres at build 1.0 (the capsule is 2 m, feet at its bottom).</summary>
        public const float Height = 1.85f;

        static Material atlasMat;

        public static CharacterAnimator Attach(GameObject host, CharacterLook look)
        {
            var cat = CharacterCatalog.Instance;
            var ch = cat ? cat.Find(look.model) : null;
            if (ch == null) return null;
            var set = cat.Clips(ch.clips);
            if (set == null || !set.idle) set = cat.male;
            if (set == null || !set.idle) return null;

            // retire the greybox look (capsule, face parts, nose); colliders stay for physics
            foreach (var r in host.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            var model = Object.Instantiate(ch.prefab, host.transform);
            model.name = $"Model_{ch.label}";
            model.transform.localPosition = new Vector3(0f, -1f, 0f); // feet on the floor
            model.transform.localRotation = Quaternion.identity;      // the pack's characters face +Z
            model.transform.localScale = Vector3.one;

            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.Destroy(c); // physics belongs to the capsule
            var mat = ch.atlas ? AtlasMaterialFor(ch.atlas) : AtlasMaterial(cat);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (mat)
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
                if (r.gameObject.name.StartsWith("w_")) r.gameObject.SetActive(false); // the police prefab's pistol: off until a guard is armed
            }

            // size it to the capsule from its own mesh bounds, then apply the player's / guard's build
            float s = FitScale(model, host.transform);
            model.transform.localScale = new Vector3(s * look.width, s * look.height, s * look.width);
            AddDress(model.transform, ch, cat);                                  // (after the fit: a hard hat must not make the body smaller)

            var animator = model.GetComponent<Animator>();
            if (!animator) animator = model.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var ca = model.GetComponent<CharacterAnimator>();
            if (!ca) ca = model.AddComponent<CharacterAnimator>();
            if (!ca.UseClips(set, cat.male))
            {
                Object.Destroy(model);
                foreach (var r in host.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                return null;
            }
            return ca;
        }

        /// <summary>The character's head attachments (a cap, a hard hat with goggles, a beard): each is parented to the Head bone with no offset and drawn in its own colour theme (the owner, 2026-10-20: the builders of his pictures wear them).</summary>
        static void AddDress(Transform model, ToonCharacter ch, CharacterCatalog cat)
        {
            if (ch.dress == null || ch.dress.Length == 0) return;
            Transform head = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { head = t; break; }
            if (!head) return;
            foreach (var d in ch.dress)
            {
                if (d == null || !d.prefab) continue;
                var item = Object.Instantiate(d.prefab, head, false);
                item.name = "Dress_" + d.prefab.name;
                item.transform.localPosition = Vector3.zero; item.transform.localRotation = Quaternion.identity; item.transform.localScale = Vector3.one;
                foreach (var c in item.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                var atlas = d.atlas ? d.atlas : ch.atlas ? ch.atlas : cat.atlas;
                var mat = atlas ? AtlasMaterialFor(atlas) : null;
                if (!mat) continue;
                foreach (var r in item.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>Scale that makes the model <see cref="Height"/> metres tall in the host''s space.</summary>
        static float FitScale(GameObject model, Transform host)
        {
            Bounds b = default; bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            float h = b.size.y / Mathf.Max(0.0001f, host.lossyScale.y);
            return any && h > 0.05f ? Height / h : 1f;
        }

        static readonly System.Collections.Generic.Dictionary<Texture2D, Material> atlasMats = new System.Collections.Generic.Dictionary<Texture2D, Material>();

        /// <summary>A Standard material with one character's own atlas (the Synty people: their Shader Graph material is pink in this pipeline).</summary>
        public static Material AtlasMaterialFor(Texture2D atlas)
        {
            if (atlasMats.TryGetValue(atlas, out var m) && m) return m;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) return null;
            m = new Material(sh) { name = "Atlas_" + atlas.name, mainTexture = atlas, color = Color.white };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            atlasMats[atlas] = m;
            return m;
        }

        /// <summary>The pack's single Standard-shader material (colour atlas, matt).</summary>
        static Material AtlasMaterial(CharacterCatalog cat)
        {
            if (atlasMat) return atlasMat;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (!sh || !cat.atlas) return null;
            atlasMat = new Material(sh) { name = "ToonAtlas", mainTexture = cat.atlas, color = Color.white };
            if (atlasMat.HasProperty("_Metallic")) atlasMat.SetFloat("_Metallic", 0f);
            if (atlasMat.HasProperty("_Glossiness")) atlasMat.SetFloat("_Glossiness", 0.15f);
            return atlasMat;
        }

        /// <summary>Show or hide the pistol the police prefab carries (for the armed guards on the higher floors).</summary>
        public static void SetArmed(CharacterAnimator anim, bool armed)
        {
            if (!anim) return;
            foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("w_")) t.gameObject.SetActive(armed);
        }
    }
}

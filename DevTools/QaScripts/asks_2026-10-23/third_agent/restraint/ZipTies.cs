using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The zip ties of a hogtied character (the owner's Blender files have three black meshes skinned to the body: a loop round each wrist, one round each ankle, and a long loop through both pairs; "the game can instead just switch the three objects on at those
    /// moments"): made here from three kinds of plain black cylinders that ride the character's own bones, so they fit every character the game has, whatever his size - a band on each forearm next to the wrist, a band on each shin next to the ankle, and a thin
    /// strap from the middle of the wrists to the middle of the ankles. <see cref="RestraintVisual"/> switches them on and off by the clip's time (<see cref="RestraintRules.TieUpWrists"/> ...).
    /// </summary>
    public sealed class ZipTies : MonoBehaviour
    {
        const float WristDiameter = 0.085f, AnkleDiameter = 0.115f, BandHeight = 0.035f, StrapDiameter = 0.022f;

        Animator animator;
        Transform handL, handR, footL, footR;
        readonly Transform[] bands = new Transform[4];       // wrists left and right, ankles left and right
        Transform strap;
        bool wristsOn, anklesOn, linkOn;
        static Mesh cylinder;
        static Material black;

        /// <summary>The ties of this character (made the first time).</summary>
        public static ZipTies On(Animator an)
        {
            if (!an || !an.isHuman) return null;
            var z = an.GetComponent<ZipTies>();
            if (!z) z = an.gameObject.AddComponent<ZipTies>();
            z.Build(an);
            return z;
        }

        /// <summary>The ties of this character if he has them, else null (nothing is made).</summary>
        public static ZipTies Existing(Animator an) => an ? an.GetComponent<ZipTies>() : null;

        public bool WristsOn => wristsOn;
        public bool AnklesOn => anklesOn;
        public bool LinkOn => linkOn;

        void Build(Animator an)
        {
            if (animator == an && bands[0]) return;
            animator = an;
            handL = an.GetBoneTransform(HumanBodyBones.LeftHand); handR = an.GetBoneTransform(HumanBodyBones.RightHand);
            footL = an.GetBoneTransform(HumanBodyBones.LeftFoot); footR = an.GetBoneTransform(HumanBodyBones.RightFoot);
            if (!handL || !handR || !footL || !footR) return;
            if (!cylinder) cylinder = MakeCylinder();
            if (!black)
            {
                var sh = Shader.Find("Standard");
                if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
                if (!sh) sh = Shader.Find("Sprites/Default");
                black = new Material(sh) { name = "ZipTie", color = new Color(0.04f, 0.04f, 0.045f) };
                if (black.HasProperty("_Glossiness")) black.SetFloat("_Glossiness", 0.35f);
            }
            bands[0] = Band("ZipTie_WristL", handL, WristDiameter); bands[1] = Band("ZipTie_WristR", handR, WristDiameter);
            bands[2] = Band("ZipTie_AnkleL", footL, AnkleDiameter); bands[3] = Band("ZipTie_AnkleR", footR, AnkleDiameter);
            var s = new GameObject("ZipTie_Link");
            s.transform.SetParent(an.transform, false);
            s.AddComponent<MeshFilter>().sharedMesh = cylinder;
            var sr = s.AddComponent<MeshRenderer>(); sr.sharedMaterial = black; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            strap = s.transform;
            Set(false, false, false);
        }

        /// <summary>A band on the bone above this hand or foot, next to it: it rides the bone, round the limb's own direction.</summary>
        Transform Band(string name, Transform end, float diameter)
        {
            var parent = end.parent;
            var g = new GameObject(name);
            var t = g.transform;
            t.SetParent(parent, false);
            var along = end.localPosition;                                        // (the limb's direction in its parent's space: from the elbow to the wrist, from the knee to the ankle)
            t.localPosition = along * 0.86f;
            t.localRotation = along.sqrMagnitude > 1e-8f ? Quaternion.FromToRotation(Vector3.up, along.normalized) : Quaternion.identity;
            float s = 1f / Mathf.Max(0.0001f, parent.lossyScale.x);
            t.localScale = new Vector3(diameter * s, BandHeight * s, diameter * s);
            g.AddComponent<MeshFilter>().sharedMesh = cylinder;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = black; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Which ties are on: the bands round the wrists, the bands round the ankles, the strap between them.</summary>
        public void Set(bool wrists, bool ankles, bool link)
        {
            wristsOn = wrists; anklesOn = ankles; linkOn = link;
            if (!bands[0]) return;
            bands[0].gameObject.SetActive(wrists); bands[1].gameObject.SetActive(wrists);
            bands[2].gameObject.SetActive(ankles); bands[3].gameObject.SetActive(ankles);
            if (strap) strap.gameObject.SetActive(link);
        }

        void LateUpdate()
        {
            if (!linkOn || !strap || !handL) return;
            var a = (handL.position + handR.position) * 0.5f;
            var b = (footL.position + footR.position) * 0.5f;
            var d = b - a;
            float len = d.magnitude;
            if (len < 0.03f) { strap.gameObject.SetActive(false); return; }
            strap.gameObject.SetActive(true);
            strap.position = (a + b) * 0.5f;
            strap.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            float s = 1f / Mathf.Max(0.0001f, strap.parent.lossyScale.x);
            strap.localScale = new Vector3(StrapDiameter * s, len * s, StrapDiameter * s);
        }

        /// <summary>A cylinder one unit across and one high, its axis along Y, closed at both ends.</summary>
        static Mesh MakeCylinder()
        {
            const int n = 14;
            var v = new Vector3[n * 2 + 2];
            var tri = new int[n * 12];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float x = Mathf.Cos(a) * 0.5f, z = Mathf.Sin(a) * 0.5f;
                v[i] = new Vector3(x, -0.5f, z); v[n + i] = new Vector3(x, 0.5f, z);
            }
            v[2 * n] = new Vector3(0f, -0.5f, 0f); v[2 * n + 1] = new Vector3(0f, 0.5f, 0f);
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tri[k++] = i; tri[k++] = n + i; tri[k++] = j;                       // (the side)
                tri[k++] = j; tri[k++] = n + i; tri[k++] = n + j;
                tri[k++] = 2 * n; tri[k++] = i; tri[k++] = j;                       // (the bottom)
                tri[k++] = 2 * n + 1; tri[k++] = n + j; tri[k++] = n + i;           // (the top)
            }
            var m = new Mesh { name = "ZipTieCylinder", vertices = v, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}

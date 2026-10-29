using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Scuff marks on the floor behind a dragged body (the owner's text, 2026-10-28: "drag marks"): flat dark smudges along the way, laid by <see cref="RestraintVisual"/> every step of a drag on every machine, that fade out after
    /// <see cref="Life"/> seconds. At most <see cref="Max"/> are kept (the oldest is used again). Pure decoration: nothing here is networked and no guard looks at it. PLACEHOLDER look (a generated smudge on a plain sprite material).
    /// </summary>
    public sealed class DragMarks : MonoBehaviour
    {
        public const int Max = 48;
        public const float Life = 30f, Length = 0.7f, Width = 0.32f;

        sealed class Mark { public Transform t; public MeshRenderer r; public float born; public bool live; }

        static DragMarks instance;
        static readonly List<Mark> marks = new List<Mark>();
        static Mesh quad;
        static Material mat;
        static MaterialPropertyBlock block;
        static int cursor;

        /// <summary>How many marks are on the floor now (for the tests).</summary>
        public static int Count { get { int n = 0; for (int i = 0; i < marks.Count; i++) if (marks[i].live) n++; return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; marks.Clear(); quad = null; mat = null; block = null; cursor = 0; }

        /// <summary>A mark where a body is dragged: <paramref name="at"/> is a point over the floor, <paramref name="along"/> the way it goes. Nothing happens when there is no floor under the point.</summary>
        public static void Add(Vector3 at, Vector3 along)
        {
            if (!Physics.Raycast(at + Vector3.up * 0.8f, Vector3.down, out var hit, 2.2f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider && hit.collider.GetComponentInParent<WobblyPlayer>() != null) return;
            if (!instance)
            {
                var go = new GameObject("DragMarks");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<DragMarks>();
            }
            along.y = 0f;
            if (along.sqrMagnitude < 1e-4f) along = Vector3.forward;
            var m = Take();
            m.live = true; m.born = Time.time;
            m.t.gameObject.SetActive(true);
            m.t.SetPositionAndRotation(hit.point + hit.normal * 0.012f, Quaternion.LookRotation(along.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f));
            Paint(m, 0.6f);
        }

        /// <summary>Takes every mark off the floor (a new floor).</summary>
        public static void Clear()
        {
            for (int i = 0; i < marks.Count; i++) { marks[i].live = false; if (marks[i].t) marks[i].t.gameObject.SetActive(false); }
        }

        static Mark Take()
        {
            for (int i = 0; i < marks.Count; i++) if (!marks[i].live) return marks[i];
            if (marks.Count < Max) return Make();
            var m = marks[cursor % marks.Count];                // (the oldest goes first: they are taken in turn)
            cursor++;
            return m;
        }

        static Mark Make()
        {
            if (!quad)
            {
                quad = new Mesh { name = "DragMarkQuad" };
                float w = Width * 0.5f, l = Length * 0.5f;
                quad.vertices = new[] { new Vector3(-w, -l, 0f), new Vector3(w, -l, 0f), new Vector3(w, l, 0f), new Vector3(-w, l, 0f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quad.RecalculateNormals(); quad.RecalculateBounds();
                quad.hideFlags = HideFlags.HideAndDontSave;
            }
            if (!mat)
            {
                mat = new Material(Shader.Find("Sprites/Default")) { name = "DragMark", mainTexture = Smudge(), hideFlags = HideFlags.HideAndDontSave };
                block = new MaterialPropertyBlock();
            }
            var go = new GameObject("DragMark");
            go.transform.SetParent(instance.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var m = new Mark { t = go.transform, r = r };
            marks.Add(m);
            return m;
        }

        /// <summary>A soft dark streak, thin at both ends (a placeholder; generated).</summary>
        static Texture2D Smudge()
        {
            const int W = 32, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "DragMarkSmudge", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = Mathf.Abs(x / (W - 1f) * 2f - 1f), v = Mathf.Abs(y / (H - 1f) * 2f - 1f);
                    float a = Mathf.Clamp01(1f - u * u) * Mathf.Clamp01(1f - v * v * v * v) * (0.75f + 0.25f * Mathf.Sin(y * 0.9f + x * 0.3f));
                    px[y * W + x] = new Color32(24, 20, 18, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return tex;
        }

        static void Paint(Mark m, float alpha)
        {
            block.SetColor("_Color", new Color(1f, 1f, 1f, alpha));
            m.r.SetPropertyBlock(block);
        }

        void Update()
        {
            for (int i = 0; i < marks.Count; i++)
            {
                var m = marks[i];
                if (!m.live) continue;
                float age = Time.time - m.born;
                if (age >= Life) { m.live = false; m.t.gameObject.SetActive(false); continue; }
                Paint(m, 0.6f * Mathf.Clamp01((Life - age) / (Life * 0.5f)));          // (full for the first half of its life, then fading)
            }
        }
    }
}

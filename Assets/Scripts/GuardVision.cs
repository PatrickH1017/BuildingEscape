using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A guard's flashlight and the cone of vision the player can see.
    /// - A small flashlight is held at the guard's right hand and aimed where he looks (with a real spot light).
    /// - A translucent fan lies on the floor, exactly as wide and as long as the guard's sight check
    ///   (GuardAI.sightAngle / sightRange), and is cut short by walls so you can see what he can really see.
    ///   It is warm yellow while he patrols, orange while he investigates, and turns red as his backup call fills.
    /// It is only a picture: it reads the guard's synced position / rotation / state, so it works the same on a client.
    /// Added to every guard by GuardVisual.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    [DefaultExecutionOrder(200)]     // after HeldGun (100): an armed guard's flashlight sits on the muzzle of the gun as it was posed this frame
    public class GuardVision : MonoBehaviour
    {
        const string HandPath = "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand";
        const int Segments = 36;
        const float EdgeWidth = 0.35f;     // the bright band along the rim of the cone
        const float CullDistance = 45f;    // beyond this from the camera the cone is not drawn
        const float LightDistance = 25f;   // beyond this the real spot light is switched off
        const int FanEvery = 2;            // the cone (37 view rays) is rebuilt every this many frames; guards take turns so the cost is spread

        GuardAI ai;
        Transform hand, torch;
        Light beam;
        bool initialised;

        GameObject fanObject;
        Mesh mesh;
        Material fanMat, torchMat, lensMat;
        Vector3[] verts;
        Color[] cols;
        readonly RaycastHit[] hits = new RaycastHit[24];
        int fanTurn;                       // which of the FanEvery frames this guard rebuilds its cone on
        bool fanDrawn;                     // the cone has been built since it was last switched on

        void Awake()
        {
            ai = GetComponent<GuardAI>();
            fanTurn = (int)((uint)GetInstanceID() % FanEvery);
        }

        void Start() { if (!initialised) Init(null); }

        /// <summary>Called by GuardVisual once the guard's model exists (so the hand bone can be found).</summary>
        public void Init(CharacterAnimator anim)
        {
            if (initialised) return;
            initialised = true;
            if (!ai) ai = GetComponent<GuardAI>();
            if (anim)
            {
                hand = anim.RightHand;
                if (!hand) hand = anim.transform.Find(HandPath);
                if (!hand) hand = FindDeep(anim.transform, "RightHand");
            }
            BuildTorch();
            BuildFan();
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
            return null;
        }

        static Shader FindShader(params string[] names)
        {
            foreach (var n in names) { var s = Shader.Find(n); if (s) return s; }
            return null;
        }

        // ------------------------------------------------------------------ the flashlight

        void BuildTorch()
        {
            torch = new GameObject("Flashlight").transform;
            torch.SetParent(transform, false);

            var sh = FindShader("Standard", "Legacy Shaders/Diffuse", "Sprites/Default");
            if (sh)
            {
                torchMat = new Material(sh) { color = new Color(0.12f, 0.12f, 0.14f) };
                lensMat = new Material(sh) { color = new Color(1f, 0.95f, 0.7f) };
                lensMat.EnableKeyword("_EMISSION");
                if (lensMat.HasProperty("_EmissionColor")) lensMat.SetColor("_EmissionColor", new Color(1f, 0.9f, 0.55f) * 2.5f);
                // a cylinder along the beam (local Z): barrel, head, glowing lens
                Part("Barrel", 0.14f, 0.085f, 0.28f, torchMat);
                Part("Head", 0.31f, 0.15f, 0.07f, torchMat);
                Part("Lens", 0.347f, 0.13f, 0.01f, lensMat);
            }

            var lg = new GameObject("Beam");
            lg.transform.SetParent(torch, false);
            lg.transform.localPosition = new Vector3(0f, 0f, 0.36f);
            beam = lg.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = ai.sightRange;
            beam.spotAngle = Mathf.Clamp(ai.sightAngle * 2f, 30f, 130f);
            beam.intensity = 2.2f;
            beam.color = new Color(1f, 0.93f, 0.7f);
            beam.shadows = LightShadows.None;
            beam.renderMode = LightRenderMode.ForcePixel;   // always a real per-pixel light, even in the lit/dark night office
        }

        /// <summary>A cylinder lying along the torch's Z axis: centre at z, thickness in metres, length in metres.</summary>
        void Part(string partName, float z, float diameter, float length, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = partName;
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            go.transform.SetParent(torch, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, length * 0.5f, diameter); // the primitive is 2 m tall
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        Renderer[] torchBody;
        bool torchBodyShown = true;

        void PlaceTorch()
        {
            // an armed guard has no flashlight in his hand: the light sits on the front of his gun and shines where it points
            var gunMuzzle = ai.Gun != null ? ai.Gun.Muzzle : null;
            if (torchBody == null) torchBody = torch.GetComponentsInChildren<Renderer>(true);
            bool showBody = gunMuzzle == null;
            if (showBody != torchBodyShown)
            {
                torchBodyShown = showBody;
                foreach (var r in torchBody) if (r) r.enabled = showBody;
            }
            if (gunMuzzle != null)
            {
                torch.SetPositionAndRotation(gunMuzzle.position, gunMuzzle.rotation);
                return;
            }
            var fwd = transform.forward;
            Vector3 pos = hand
                ? hand.position
                : transform.position + Vector3.up * 1.0f + transform.right * 0.3f + fwd * 0.25f;
            // it follows the hand, but always points where the guard is looking (so the beam matches the cone)
            torch.SetPositionAndRotation(pos, Quaternion.LookRotation((fwd + Vector3.down * 0.1f).normalized, Vector3.up));
        }

        // ------------------------------------------------------------------ the cone on the floor

        void BuildFan()
        {
            var sh = FindShader("Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit");
            if (!sh) { Debug.LogWarning("GuardVision: no transparent shader found, so the vision cone is not drawn."); return; }
            fanMat = new Material(sh);

            fanObject = new GameObject(name + "_VisionCone");
            var mf = fanObject.AddComponent<MeshFilter>();
            var mr = fanObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fanMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            int n = Segments;
            // index 0 = the guard, 1..n+1 = the inner edge of the rim band, n+2..2n+2 = the rim itself
            verts = new Vector3[1 + 2 * (n + 1)];
            cols = new Color[verts.Length];
            var tris = new int[n * 3 * 3];
            int t = 0;
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 2 + i;          // inner i, i + 1
                int c = n + 2 + i, d = n + 3 + i;  // rim i, i + 1
                tris[t++] = 0; tris[t++] = b; tris[t++] = a;   // the fill
                tris[t++] = a; tris[t++] = b; tris[t++] = d;   // the rim band (two triangles)
                tris[t++] = a; tris[t++] = d; tris[t++] = c;
            }
            mesh = new Mesh { name = "VisionCone" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.colors = cols;
            mf.sharedMesh = mesh;
            fanObject.SetActive(false);
        }

        /// <summary>How far a horizontal view ray gets before a wall or a piece of furniture stops it.</summary>
        float Clip(Vector3 from, Vector3 dir, float range)
        {
            int count = Physics.RaycastNonAlloc(from, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
            float best = range;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.distance >= best) continue;
                var col = h.collider;
                if (col.attachedRigidbody != null) continue;      // people and loose objects do not cut the cone short
                if (col.transform.IsChildOf(transform)) continue; // not the guard himself
                best = h.distance;
            }
            return best;
        }

        /// <summary>The height of the floor under the guard (the nearest solid thing below him that is not himself).</summary>
        float FloorHeight(Vector3 at)
        {
            int count = Physics.RaycastNonAlloc(at + Vector3.up * 0.5f, Vector3.down, hits, 3f, ~0, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue, y = at.y;
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                if (h.collider.attachedRigidbody != null || h.collider.transform.IsChildOf(transform)) continue;
                if (h.distance < bestDist) { bestDist = h.distance; y = h.point.y; }
            }
            return y;
        }

        Color StateColor()
        {
            if (ai.IsCalling) return Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.1f, 0.08f), ai.CallProgress);
            if (ai.State == GuardState.Chase || ai.State == GuardState.Pinning) return new Color(1f, 0.1f, 0.08f);
            if (ai.State == GuardState.Investigate) return new Color(1f, 0.72f, 0.25f);
            return new Color(1f, 0.93f, 0.55f);
        }

        static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        void UpdateFan(bool on)
        {
            if (!fanObject) return;
            if (fanObject.activeSelf != on) { fanObject.SetActive(on); fanDrawn = false; }
            if (!on) return;
            // between its turns the cone stays as it was (a frame old is invisible), but it is always built the moment it appears
            if (fanDrawn && (Time.frameCount + fanTurn) % FanEvery != 0) return;
            fanDrawn = true;

            var origin = transform.position;
            float floorY = FloorHeight(origin) + 0.06f;

            float range = ai.sightRange, half = ai.sightAngle;
            var rayOrigin = origin + Vector3.up * 0.9f;
            var fwd = transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();

            var col = StateColor();
            float pulse = ai.IsCalling ? 1f + 0.25f * Mathf.Sin(Time.time * 14f) : 1f;
            int n = Segments;

            verts[0] = new Vector3(origin.x, floorY, origin.z);
            cols[0] = Alpha(col, 0.32f * pulse);
            for (int i = 0; i <= n; i++)
            {
                float ang = Mathf.Lerp(-half, half, i / (float)n);
                var dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                float d = Clip(rayOrigin, dir, range);
                var rim = origin + dir * d;
                var inner = origin + dir * Mathf.Max(0.3f, d - EdgeWidth);
                verts[1 + i] = new Vector3(inner.x, floorY, inner.z);
                verts[n + 2 + i] = new Vector3(rim.x, floorY, rim.z);
                cols[1 + i] = Alpha(col, 0.18f * pulse);
                cols[n + 2 + i] = Alpha(col, 0.6f * pulse);
            }
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.RecalculateBounds();
        }

        // ------------------------------------------------------------------ every frame

        void LateUpdate()
        {
            if (!ai || !initialised) return;
            bool awake = !ai.Dead && ai.State != GuardState.Stunned; // a guard who is down has no beam
            var cam = Camera.main;
            float sq = cam ? (cam.transform.position - transform.position).sqrMagnitude : 0f;
            bool near = sq < CullDistance * CullDistance;

            if (torch)
            {
                if (torch.gameObject.activeSelf != awake) torch.gameObject.SetActive(awake);
                if (awake) PlaceTorch();
            }
            if (beam)
            {
                beam.enabled = awake && sq < LightDistance * LightDistance;
                beam.color = Color.Lerp(new Color(1f, 0.93f, 0.7f), StateColor(), 0.6f);
            }
            UpdateFan(awake && near);
        }

        void OnDisable() { if (fanObject) fanObject.SetActive(false); }

        void OnDestroy()
        {
            if (fanObject) Destroy(fanObject);
            if (mesh) Destroy(mesh);
            if (fanMat) Destroy(fanMat);
            if (torchMat) Destroy(torchMat);
            if (lensMat) Destroy(lensMat);
        }
    }
}

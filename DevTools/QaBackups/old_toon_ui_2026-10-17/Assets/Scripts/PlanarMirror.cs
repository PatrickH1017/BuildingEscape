using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A real mirror: a flat surface (the elevator's mirror wall) that shows what is in front of it. Whenever a game camera is about to
    /// draw the mirror, a second camera draws the world as seen through the mirror (the camera is reflected in the mirror's plane, and
    /// everything behind the glass is clipped off) into a texture, and the mirror's shader (BuildingEscape/ElevatorMirror) shows that
    /// texture lined up with the screen. So the player sees himself, the guards and everything else move in it.
    ///
    /// This costs a second, smaller drawing of the scene, so it is only done when it matters: the camera is in front of the glass
    /// and either inside the car or can actually see the mirror (not blocked by a wall or a shut door). The picture is
    /// <see cref="quality"/> times the screen's size. Purely cosmetic and local to each machine.
    /// </summary>
    public sealed class PlanarMirror : MonoBehaviour
    {
        /// <summary>The way the glass faces (world space, towards the people looking into it).</summary>
        public Vector3 normal = Vector3.forward;
        /// <summary>How big the mirror's picture is, as a share of the screen's size.</summary>
        [Range(0.2f, 1f)] public float quality = 0.6f;
        /// <summary>Farther from the glass than this (outside the car) the mirror is not drawn.</summary>
        public float maxDistance = 16f;
        /// <summary>The mirror camera does not draw anything farther than this.</summary>
        public float farClip = 40f;
        /// <summary>A camera inside this box (world space) always counts as seeing the mirror.</summary>
        public Bounds insideBounds;
        /// <summary>
        /// While nobody can see the mirror drawn (too far, or something in the way) show a plain dark surface instead of the last picture,
        /// which would be lined up with a screen that has long since moved on. For a mirror that can be seen from afar (the outpost's window).
        /// </summary>
        public bool blankWhenIdle;

        /// <summary>How many times the mirror has been drawn (for the tests).</summary>
        public int RenderCount { get; private set; }
        /// <summary>The picture's size at the last drawing (for the tests).</summary>
        public Vector2Int LastSize { get; private set; }

        // the clipping plane sits this far in front of the glass, so the glass and the wall behind it are cut off
        const float ClipOffset = 0.01f;
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        static bool drawing;

        Renderer rend;
        MaterialPropertyBlock block;
        Camera mirrorCamera;
        RenderTexture picture;
        bool blank;

        void Awake()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void OnDestroy()
        {
            if (picture)
            {
                picture.Release();
                Destroy(picture);
                picture = null;
            }
            if (mirrorCamera) Destroy(mirrorCamera.gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { drawing = false; }

        // ------------------------------------------------------------------ when a camera is about to draw the mirror

        void OnWillRenderObject()
        {
            if (drawing || !isActiveAndEnabled || !rend) return;
            var cam = Camera.current;
            if (!cam || cam == mirrorCamera || cam.cameraType != CameraType.Game) return;
            if (!CanSee(cam.transform.position)) { if (blankWhenIdle) ShowBlank(); return; }
            Draw(cam);
        }

        void ShowBlank()
        {
            if (blank) return;
            blank = true;
            rend.GetPropertyBlock(block);
            block.SetTexture(MainTexId, Texture2D.blackTexture);
            rend.SetPropertyBlock(block);
        }

        /// <summary>
        /// Would a camera at this position see the mirror? In front of the glass, and either in the car or with a clear line to the glass
        /// (an open door); a wall or a shut door in between means no (rigidbodies, such as people and crates, never block).
        /// </summary>
        public bool CanSee(Vector3 cameraPosition)
        {
            var at = transform.position;
            if (Vector3.Dot(normal, cameraPosition - at) <= 0.02f) return false;          // behind the glass
            if (insideBounds.size.sqrMagnitude > 0f && insideBounds.Contains(cameraPosition)) return true;
            if ((cameraPosition - at).sqrMagnitude > maxDistance * maxDistance) return false;
            for (int i = -1; i <= 1; i++)
            {
                var target = transform.TransformPoint(new Vector3(i * 0.3f, 0f, 0f)) + normal * 0.05f;
                if (!Blocked(cameraPosition, target)) return true;
            }
            return false;
        }

        static bool Blocked(Vector3 from, Vector3 to)
        {
            var d = to - from;
            float len = d.magnitude;
            if (len < 0.05f) return false;
            int n = Physics.RaycastNonAlloc(from, d / len, hitBuffer, len - 0.02f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (hitBuffer[i].rigidbody == null) return true;
            return false;
        }

        // ------------------------------------------------------------------ drawing the picture

        /// <summary>Draws the picture for this camera now (the tests call it directly). False if it could not be drawn.</summary>
        public bool Draw(Camera cam)
        {
            if (!cam || drawing || !rend) return false;
            drawing = true;
            try
            {
                MirrorMath.TextureSize(cam.pixelWidth, cam.pixelHeight, quality, out int w, out int h);
                EnsurePicture(w, h);
                EnsureCamera();
                if (!picture || !mirrorCamera) return false;

                var cm = mirrorCamera;
                cm.CopyFrom(cam);
                cm.enabled = false;                       // it only draws when we say so
                cm.targetTexture = picture;
                cm.useOcclusionCulling = false;
                cm.allowHDR = false;
                cm.allowMSAA = false;
                cm.farClipPlane = Mathf.Min(cam.farClipPlane, farClip);

                var at = transform.position;
                var n = normal.normalized;
                float d = MirrorMath.PlaneD(n.x, n.y, n.z, at.x, at.y, at.z);
                var r = MirrorMath.Reflection(n.x, n.y, n.z, d);
                var reflection = new Matrix4x4();
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 4; col++) reflection[row, col] = r[row * 4 + col];

                cm.worldToCameraMatrix = cam.worldToCameraMatrix * reflection;
                cm.transform.position = reflection.MultiplyPoint(cam.transform.position);
                cm.transform.rotation = cam.transform.rotation;
                // an oblique near plane on the glass: nothing behind the mirror is drawn into the picture
                cm.projectionMatrix = cam.CalculateObliqueMatrix(CameraSpacePlane(cm, at + n * ClipOffset, n));

                GL.invertCulling = true;                  // the reflection turns the triangles inside out
                cm.Render();

                rend.GetPropertyBlock(block);
                block.SetTexture(MainTexId, picture);
                rend.SetPropertyBlock(block);
                blank = false;
                RenderCount++;
                LastSize = new Vector2Int(w, h);
                return true;
            }
            finally
            {
                GL.invertCulling = false;
                drawing = false;
            }
        }

        static Vector4 CameraSpacePlane(Camera cam, Vector3 point, Vector3 normal)
        {
            var m = cam.worldToCameraMatrix;
            var cp = m.MultiplyPoint(point);
            var cn = m.MultiplyVector(normal).normalized;
            return new Vector4(cn.x, cn.y, cn.z, -Vector3.Dot(cp, cn));
        }

        void EnsurePicture(int w, int h)
        {
            if (picture && picture.width == w && picture.height == h) return;
            if (picture)
            {
                picture.Release();
                Destroy(picture);
            }
            picture = new RenderTexture(w, h, 16, RenderTextureFormat.Default)
            {
                name = "MirrorPicture",
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        void EnsureCamera()
        {
            if (mirrorCamera) return;
            var go = new GameObject("MirrorCamera") { hideFlags = HideFlags.HideAndDontSave };
            mirrorCamera = go.AddComponent<Camera>();
            mirrorCamera.enabled = false;
        }
    }
}

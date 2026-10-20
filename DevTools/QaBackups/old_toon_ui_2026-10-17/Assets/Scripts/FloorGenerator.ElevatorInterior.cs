using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The inside of the service elevator, dressed like a real passenger elevator: wood-veneer wall panels, a cream lower wall, stainless
    /// kick plate / chair rail / handrail / door jambs, carpet, a wood ceiling with round downlights, steel doors, and on the wall opposite the
    /// floor panel a real mirror (<see cref="PlanarMirror"/>) in which the player sees what he is doing. The layout is
    /// <see cref="ElevatorLook"/> (tested outside Unity); here it becomes one mesh per material. Visual only: nothing in it has a collider
    /// (the car's walls are the ones that were always there), so the camera and the players are not affected.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Elevator interior")]
        [Tooltip("Dress the service elevator like a real elevator (wood panels, steel trim, carpet, downlights). Off = the plain old box.")]
        public bool elevatorLook = true;
        [Tooltip("The wall opposite the floor panel is a real mirror. It draws the scene a second time (a smaller picture) while you can see it.")]
        public bool elevatorMirror = true;
        [Tooltip("How sharp the mirror's picture is, as a share of the screen's size. Lower = faster.")]
        [Range(0.3f, 1f)] public float mirrorQuality = 0.6f;
        [Tooltip("How bright the lights in the elevator's ceiling are (a multiplier; they only exist with After hours on).")]
        [Range(0.5f, 4f)] public float carLampBrightness = 1f;

        const float CarLampRange = 8f, CarLampIntensity = 2f;

        Material[] carMats;
        Material mirrorMat, mirrorFallbackMat;
        Texture2D carWoodTex;
        /// <summary>World positions of the car's ceiling lights, waiting for BuildAfterHours to turn them into real lights.</summary>
        readonly List<Vector3> carLamps = new List<Vector3>();

        /// <summary>The elevator gets its new look (only in Play Mode, like the night look: the editor tools do not need it).</summary>
        bool UseCarLook => elevatorLook && Application.isPlaying;

        void DestroyCarMaterials()
        {
            if (carMats != null) foreach (var m in carMats) if (m) Kill(m);
            carMats = null;
            if (mirrorMat) Kill(mirrorMat);
            if (mirrorFallbackMat) Kill(mirrorFallbackMat);
            if (carWoodTex) Kill(carWoodTex);
            mirrorMat = mirrorFallbackMat = null;
            carWoodTex = null;
        }

        // ------------------------------------------------------------------ materials

        Texture2D CarWoodTexture()
        {
            if (carWoodTex) return carWoodTex;
            int w = ProceduralWood.Width, h = ProceduralWood.Height;
            var bytes = ProceduralWood.Pixels(w, h, 11);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(bytes[i * 4], bytes[i * 4 + 1], bytes[i * 4 + 2], 255);
            carWoodTex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                name = "CarWood",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.HideAndDontSave
            };
            carWoodTex.SetPixels32(px);
            carWoodTex.Apply(true, true);
            return carWoodTex;
        }

        Material CarMaterial(int id)
        {
            if (carMats == null) carMats = new Material[ElevatorLook.MaterialCount];
            if (carMats[id]) return carMats[id];
            Material m;
            switch (id)
            {
                case ElevatorLook.Wood:
                    m = Surface("CarWood", Color.white, 0.35f, false);
                    m.mainTexture = CarWoodTexture();
                    break;
                case ElevatorLook.Ceiling:
                    // the ceiling gets hardly any light from below, so the wood glows a little by itself
                    m = Surface("CarCeiling", Color.white, 0.25f, false);
                    m.mainTexture = CarWoodTexture();
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", CarWoodTexture());
                    if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.30f, 0.30f, 0.30f));
                    break;
                case ElevatorLook.Cream: m = Surface("CarCream", new Color(0.90f, 0.85f, 0.70f), 0.45f, false); break;
                case ElevatorLook.Steel: m = Surface("CarSteel", new Color(0.80f, 0.82f, 0.84f), 0.65f, false, 0.55f); break;
                case ElevatorLook.Carpet: m = Surface("CarCarpet", new Color(0.55f, 0.30f, 0.28f), 0.02f, false); break;
                case ElevatorLook.Seam: m = Surface("CarSeam", new Color(0.09f, 0.05f, 0.03f), 0.1f, false); break;
                default: m = Surface("CarLens", new Color(1f, 0.95f, 0.82f), 0f, true); break;
            }
            carMats[id] = m;
            return m;
        }

        /// <summary>The mirror's material: the mirror shader, or (if the shader is missing) a polished metal that only looks shiny.</summary>
        Material MirrorMaterial(out bool real)
        {
            if (!mirrorMat)
            {
                var sh = Shader.Find("BuildingEscape/ElevatorMirror");
                if (sh && sh.isSupported) mirrorMat = new Material(sh) { name = "CarMirror", hideFlags = HideFlags.HideAndDontSave };
            }
            real = mirrorMat != null;
            if (real) return mirrorMat;
            Debug.LogWarning("[Elevator] The mirror shader (Assets/Resources/Shaders/ElevatorMirror.shader) was not found or is not supported, so the mirror is a plain shiny wall.");
            if (!mirrorFallbackMat) mirrorFallbackMat = Surface("CarMirrorFallback", new Color(0.78f, 0.84f, 0.90f), 0.95f, false, 1f);
            return mirrorFallbackMat;
        }

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Dresses the inside of the service elevator whose car is centred on <paramref name="center"/>. <paramref name="backSide"/> is the
        /// <see cref="ElevatorLook.Side"/> of the locked back door (floor 12) or -1.
        /// </summary>
        void BuildElevatorInterior(Transform room, Vector3 center, bool doorEast, int backSide, int seed)
        {
            carLamps.Clear();
            var input = new ElevatorLook.Input
            {
                height = wallHeight,
                front = doorEast ? ElevatorLook.Side.East : ElevatorLook.Side.North,
                back = backSide,
                panelWall = doorEast ? ElevatorLook.Side.North : ElevatorLook.Side.East,
                mirror = elevatorMirror,
                seed = seed ^ 0x3E1E
            };
            var plan = ElevatorLook.Build(input);
            var parts = CarMesh.From(plan).parts;

            var root = new GameObject("CarInterior").transform;
            root.SetParent(room, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            for (int id = 0; id < parts.Length; id++)
            {
                var p = parts[id];
                if (p.VertexCount == 0) continue;
                var verts = new List<Vector3>(p.VertexCount);
                var normals = new List<Vector3>(p.VertexCount);
                var uvs = new List<Vector2>(p.VertexCount);
                for (int i = 0; i < p.VertexCount; i++)
                {
                    verts.Add(new Vector3(p.vertices[i * 3], p.vertices[i * 3 + 1], p.vertices[i * 3 + 2]));
                    normals.Add(new Vector3(p.normals[i * 3], p.normals[i * 3 + 1], p.normals[i * 3 + 2]));
                    uvs.Add(new Vector2(p.uvs[i * 2], p.uvs[i * 2 + 1]));
                }
                var mesh = new Mesh { name = "CarInterior" + id };
                if (verts.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(p.triangles, 0);
                mesh.RecalculateBounds();

                var go = new GameObject("Car_" + id);
                go.transform.SetParent(root, false);              // (the vertices are in car coordinates; the car is not turned)
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = CarMaterial(id);
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<OwnedMesh>().mesh = mesh;         // freed with the floor
            }

            if (plan.mirror != null) BuildMirror(root, center, plan.mirror);

            foreach (var s in plan.lamps) carLamps.Add(center + new Vector3(s.x, s.y, s.z));
        }

        void BuildMirror(Transform root, Vector3 center, ElevatorLook.Mirror m)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "CarMirror";
            Kill(quad.GetComponent<Collider>());                  // never in the way of the camera or of anybody
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(m.cx, m.cy, m.cz);
            // a Quad is seen from its back (-z), so it must look INTO the wall for its visible side to face into the car
            quad.transform.localRotation = Quaternion.LookRotation(new Vector3(-m.nx, 0f, -m.nz), Vector3.up);
            quad.transform.localScale = new Vector3(m.width, m.height, 1f);

            var rend = quad.GetComponent<Renderer>();
            rend.sharedMaterial = MirrorMaterial(out bool real);
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (!real) return;

            var mirror = quad.AddComponent<PlanarMirror>();
            mirror.normal = new Vector3(m.nx, 0f, m.nz);
            mirror.quality = mirrorQuality;
            mirror.insideBounds = new Bounds(center + Vector3.up * (wallHeight * 0.5f), new Vector3(ElevatorLook.Half * 2f, wallHeight, ElevatorLook.Half * 2f));
        }
    }
}

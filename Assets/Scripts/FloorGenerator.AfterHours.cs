using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The heist happens at night, after hours. Every floor (the basement included) gets a ceiling, ceiling light panels all over, and a
    /// night look: dim blue-grey ambient light and a faint moon instead of daylight. Only some areas have their lights on (see
    /// <see cref="AfterHours"/>, the planner: which rooms and corridors are lit follows from the floor's seed, so every player sees the
    /// same ones), so the floor is a patchwork of lit rooms and dim, dark ones. The lit panels glow and the real lights under them
    /// are managed by <see cref="FloorLights"/>. Built after the NavMesh bake (a ceiling would otherwise be baked as a walkable
    /// surface); the ceiling has colliders, so the camera cannot rise above it and thrown things stay in the room.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("After hours (the night look)")]
        [Tooltip("Ceilings, ceiling lights (only some rooms lit) and a dim night look. Off = the old bright, open-topped floors.")]
        public bool afterHours = true;
        [Tooltip("The dim light everywhere on an office floor. Raise it if the dark areas are too dark.")]
        public Color nightAmbient = new Color(0.25f, 0.27f, 0.34f);
        [Tooltip("The same in the basement (the loading docks), which is kept brighter so the shop is easy to read.")]
        public Color dockAmbient = new Color(0.34f, 0.35f, 0.40f);
        [Tooltip("How strong the faint moonlight (the scene's directional light) is. It lights everything a little, lit or not.")]
        [Range(0f, 1f)] public float moonIntensity = 0.2f;
        public Color moonColour = new Color(0.62f, 0.72f, 1f);
        [Tooltip("1 = as designed (about 40 % of the lights on). Lower: darker floors. Higher: more rooms left lit.")]
        [Range(0.3f, 2f)] public float litScale = 1f;
        [Tooltip("How bright the lights over the lit areas are (a multiplier).")]
        [Range(0.3f, 3f)] public float lampBrightness = 1f;
        [Tooltip("The most lights switched on at once (the ones nearest the camera). Each costs some frame time.")]
        [Range(2, 12)] public int maxActiveLights = 8;

        /// <summary>The size of the floor that is on show, in 18 m cells (for the test tools).</summary>
        public int CellColumns => cols;
        public int CellRows => rows;

        static readonly Color WarmLight = new Color(1f, 0.92f, 0.78f), CoolLight = new Color(0.80f, 0.90f, 1f);
        const float CeilingThickness = 0.3f, CeilingSink = 0.01f, LampDrop = 0.15f, LampAngle = 125f;

        Material ceilingMat, panelFrameMat, panelOffMat, panelWarmMat, panelCoolMat, nightSky;
        Light moon;

        void DestroyNightMaterials()
        {
            foreach (var m in new[] { ceilingMat, panelFrameMat, panelOffMat, panelWarmMat, panelCoolMat, nightSky }) if (m) Kill(m);
            ceilingMat = panelFrameMat = panelOffMat = panelWarmMat = panelCoolMat = nightSky = null;
        }

        // ------------------------------------------------------------------ materials

        static Material Surface(string name, Color albedo, float gloss, bool glow, float metallic = 0f)
        {
            var sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { name = name, color = albedo, hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", albedo * 1.7f);
            }
            return m;
        }

        Material CeilingMat()
        {
            if (ceilingMat) return ceilingMat;
            var src = wallMat ? wallMat : floorMat;
            if (!src) return null;
            ceilingMat = new Material(src) { hideFlags = HideFlags.HideAndDontSave };
            if (ceilingMat.HasProperty("_Color")) ceilingMat.color = new Color(0.80f, 0.80f, 0.78f);
            if (ceilingMat.HasProperty("_Glossiness")) ceilingMat.SetFloat("_Glossiness", 0.05f);
            return ceilingMat;
        }

        Material PanelMat(int which)
        {
            switch (which)
            {
                case PanelGeometry.Frame: return panelFrameMat ? panelFrameMat : (panelFrameMat = Surface("PanelFrame", new Color(0.82f, 0.84f, 0.86f), 0.2f, false));
                case PanelGeometry.Off: return panelOffMat ? panelOffMat : (panelOffMat = Surface("PanelOff", new Color(0.26f, 0.29f, 0.33f), 0.6f, false));
                case PanelGeometry.OnWarm: return panelWarmMat ? panelWarmMat : (panelWarmMat = Surface("PanelWarm", new Color(1f, 0.93f, 0.78f), 0f, true));
                default: return panelCoolMat ? panelCoolMat : (panelCoolMat = Surface("PanelCool", new Color(0.82f, 0.92f, 1f), 0f, true));
            }
        }

        // ------------------------------------------------------------------ the night look

        /// <summary>Dim blue-grey ambient light, a faint moon and a dark sky, in place of the scene's daylight.</summary>
        void ApplyNightLook(bool dock)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = dock ? dockAmbient : nightAmbient;
            RenderSettings.reflectionIntensity = 0.3f;     // glossy floors would otherwise mirror the daylight sky

            if (!moon) moon = FindSun();
            if (moon)
            {
                moon.intensity = moonIntensity;
                moon.color = moonColour;
            }

            if (!nightSky && RenderSettings.skybox)
            {
                nightSky = new Material(RenderSettings.skybox) { hideFlags = HideFlags.HideAndDontSave };
                if (nightSky.HasProperty("_Exposure")) nightSky.SetFloat("_Exposure", 0.22f);
                if (nightSky.HasProperty("_SunSize")) nightSky.SetFloat("_SunSize", 0f);
                if (nightSky.HasProperty("_SkyTint")) nightSky.SetColor("_SkyTint", new Color(0.12f, 0.15f, 0.30f));
                if (nightSky.HasProperty("_GroundColor")) nightSky.SetColor("_GroundColor", new Color(0.05f, 0.05f, 0.07f));
            }
            if (nightSky) RenderSettings.skybox = nightSky;
        }

        static Light FindSun()
        {
            if (RenderSettings.sun) return RenderSettings.sun;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        // ------------------------------------------------------------------ building

        /// <summary>The ceiling, the light panels and the real lights of the floor that was just built.</summary>
        void BuildAfterHours(int floor, int seed, Vector2Int elevCell, Vector2Int guestCell, bool hub)
        {
            if (!afterHours || !Application.isPlaying) return;   // (the editor tools that build floors do not need them)
            ApplyNightLook(hub);

            var root = new GameObject("AfterHours").transform;
            root.SetParent(current, false);
            BuildCeiling(root);

            bool boss = TowerRules.IsBoss(floor);
            var input = new AfterHours.Input
            {
                layout = hub || boss ? null : layout,
                floor = floor,
                seed = seed ^ 0x51A7E5,
                footprint = new Rc(0f, 0f, cols * cellSize, rows * cellSize),
                cell = cellSize,
                allLit = hub || boss,
                litScale = litScale
            };

            // the service elevator: its walls (no panels over them) and its car (one panel inside)
            var c = CellCenter(elevCell);
            input.solids.Add(new Rc(c.x - 4.2f, c.z - 4.2f, c.x + 4.2f, c.z + 4.2f));
            input.cars.Add(new Rc(c.x - 3.8f, c.z - 3.8f, c.x + 3.8f, c.z + 3.8f));
            if (!hub)
            {
                var g = CellCenter(guestCell);                  // the guest elevator that brings the backup
                input.solids.Add(new Rc(g.x - 2.8f, g.z - 2.8f, g.x + 2.8f, g.z + 2.8f));
                input.cars.Add(new Rc(g.x - 2.4f, g.z - 2.4f, g.x + 2.4f, g.z + 2.4f));
            }
            if (floor == TowerRules.KeyFloor)
            {
                // the private hallway behind the elevator's back door (see BuildHiddenHallway)
                bool east = cols > 1 && IsOpen(elevCell, elevCell + Vector2Int.right);
                bool north = !east && rows > 1 && IsOpen(elevCell, elevCell + Vector2Int.up);
                if (!east && !north) east = true;
                var o = new Vector3(elevCell.x * cellSize, 0f, elevCell.y * cellSize);
                var hall = east ? new Rc(o.x, c.z - 4f, c.x - 4f, c.z + 4f) : new Rc(c.x - 4f, o.z, c.x + 4f, c.z - 4f);
                input.solids.Add(hall.Grow(0.2f));
                input.cars.Add(hall.Grow(-0.5f));
            }

            var plan = AfterHours.Build(input);
            BuildPanels(plan, root);
            BuildLamps(plan, root);
        }

        /// <summary>A ceiling over every 18 m cell, flush with the top of the walls. Its underside is what you see; it has colliders.</summary>
        void BuildCeiling(Transform parent)
        {
            var mat = CeilingMat();
            var holder = new GameObject("Ceiling").transform;
            holder.SetParent(parent, false);
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    // the tiles at the edge reach over the outer walls (which stand half outside the cell)
                    float x0 = c * cellSize - (c == 0 ? 0.2f : 0f), x1 = (c + 1) * cellSize + (c == cols - 1 ? 0.2f : 0f);
                    float z0 = r * cellSize - (r == 0 ? 0.2f : 0f), z1 = (r + 1) * cellSize + (r == rows - 1 ? 0.2f : 0f);
                    var go = Box($"Ceiling_{c}_{r}", new Vector3((x0 + x1) * 0.5f, wallHeight - CeilingSink + CeilingThickness * 0.5f, (z0 + z1) * 0.5f),
                                 new Vector3(x1 - x0, CeilingThickness, z1 - z0), mat, holder);
                    var rend = go.GetComponent<Renderer>();
                    rend.shadowCastingMode = ShadowCastingMode.Off;
                    rend.receiveShadows = false;
                }
        }

        /// <summary>The light panels: one mesh per cell with up to four materials (frame, dark lens, warm lit lens, cold lit lens).</summary>
        void BuildPanels(AfterHours.Plan plan, Transform parent)
        {
            var perCell = new Dictionary<int, PanelGeometry>();
            foreach (var p in plan.panels)
            {
                int cx = Mathf.Clamp((int)(p.x / cellSize), 0, cols - 1), cz = Mathf.Clamp((int)(p.z / cellSize), 0, rows - 1);
                int key = cz * cols + cx;
                if (!perCell.TryGetValue(key, out var geo)) perCell[key] = geo = new PanelGeometry();
                geo.Add(p, wallHeight - CeilingSink);
            }

            var holder = new GameObject("Panels").transform;
            holder.SetParent(parent, false);
            foreach (var kv in perCell)
            {
                var geo = kv.Value;
                var verts = new List<Vector3>(geo.VertexCount);
                var normals = new List<Vector3>(geo.VertexCount);
                for (int i = 0; i < geo.VertexCount; i++)
                {
                    verts.Add(new Vector3(geo.vertices[i * 3], geo.vertices[i * 3 + 1], geo.vertices[i * 3 + 2]));
                    normals.Add(new Vector3(geo.normals[i * 3], geo.normals[i * 3 + 1], geo.normals[i * 3 + 2]));
                }
                var mesh = new Mesh { name = "AfterHoursPanels" };
                if (verts.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);

                var mats = new List<Material>();
                var used = new List<int>();
                for (int m = 0; m < 4; m++) if (geo.triangles[m].Count > 0) used.Add(m);
                mesh.subMeshCount = used.Count;
                for (int s = 0; s < used.Count; s++)
                {
                    mesh.SetTriangles(geo.triangles[used[s]], s);
                    mats.Add(PanelMat(used[s]));
                }
                mesh.RecalculateBounds();

                var go = new GameObject($"Panels_{kv.Key % cols}_{kv.Key / cols}");
                go.transform.SetParent(holder, false);          // (the vertices are in world space, like every other box of the floor)
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats.ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.AddComponent<OwnedMesh>().mesh = mesh;        // freed with the floor
            }
        }

        /// <summary>The real lights under the lit panels. They all start off; <see cref="FloorLights"/> switches on the ones near the camera.</summary>
        void BuildLamps(AfterHours.Plan plan, Transform parent)
        {
            if (plan.sources.Count == 0 && carLamps.Count == 0) return;
            var lights = parent.gameObject.AddComponent<FloorLights>();
            lights.maxActive = Mathf.Max(2, Mathf.Min(maxActiveLights, 4 + QualitySettings.pixelLightCount * 2));
            foreach (var s in plan.sources)
                lights.Add(new Vector3(s.x, wallHeight - LampDrop, s.z), s.range, s.intensity * lampBrightness, s.cool ? CoolLight : WarmLight, LampAngle);
            // the elevator car's own lights: point lights in the ceiling, so the wood walls are lit all the way up
            foreach (var p in carLamps) lights.AddPoint(p, CarLampRange, CarLampIntensity * carLampBrightness, WarmLight);
            carLamps.Clear();
        }
    }
}

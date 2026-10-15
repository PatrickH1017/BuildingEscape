using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The real lights of one floor's after-hours lighting (the ceiling panels are only meshes). A floor has a couple of hundred of
    /// these, but a pixel light costs time on everything it touches, so only the few nearest the camera are switched on at any moment;
    /// the rest are off, and a light fades in or out over a moment as the player walks, so nothing pops. Purely cosmetic and local to
    /// each machine: every machine lights the area around its own camera.
    /// </summary>
    public sealed class FloorLights : MonoBehaviour
    {
        const float ScanEvery = 0.25f, FadeSeconds = 0.4f;
        /// <summary>A light that is already on counts as this much nearer than it is, so two lights that are about equally near do not take turns.</summary>
        const float Stickiness = 0.7f;

        /// <summary>How many lights may be on at once.</summary>
        public int maxActive = 8;
        /// <summary>Lights farther than this from the camera (along the floor) stay off.</summary>
        public float activeRange = 30f;

        sealed class Lamp
        {
            public Light light;
            public Vector3 at;
            public float full, level;
            public bool target;
        }

        readonly List<Lamp> lamps = new List<Lamp>();
        bool[] chosen = new bool[0];
        float nextScan;

        public int Count => lamps.Count;

        /// <summary>How many lights are on, or fading in (for the tests).</summary>
        public int WantedOn { get { int n = 0; foreach (var l in lamps) if (l.target) n++; return n; } }

        /// <summary>How many light components are actually switched on right now (for the tests).</summary>
        public int Enabled { get { int n = 0; foreach (var l in lamps) if (l.light && l.light.enabled) n++; return n; } }

        /// <summary>A downward-pointing spot light, off until the camera comes near.</summary>
        public void Add(Vector3 position, float range, float intensity, Color colour, float spotAngle)
        {
            Make(LightType.Spot, position, range, intensity, colour, spotAngle);
        }

        /// <summary>A point light (shines all round), off until the camera comes near.</summary>
        public void AddPoint(Vector3 position, float range, float intensity, Color colour)
        {
            Make(LightType.Point, position, range, intensity, colour, 0f);
        }

        void Make(LightType type, Vector3 position, float range, float intensity, Color colour, float spotAngle)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.range = range;
            if (type == LightType.Spot) l.spotAngle = spotAngle;
            l.color = colour;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;   // one huge floor box would otherwise get these as vertex lights, which show nothing
            l.enabled = false;
            lamps.Add(new Lamp { light = l, at = position, full = intensity });
            if (chosen.Length < lamps.Count) chosen = new bool[lamps.Count + 64];
        }

        void Update()
        {
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + ScanEvery;
                Scan();
            }
            float step = Time.deltaTime / FadeSeconds;
            for (int i = 0; i < lamps.Count; i++)
            {
                var l = lamps[i];
                float goal = l.target ? 1f : 0f;
                if (l.level == goal) continue;
                l.level = Mathf.MoveTowards(l.level, goal, step);
                Apply(l);
            }
        }

        static void Apply(Lamp l)
        {
            if (!l.light) return;
            float k = l.level * l.level * (3f - 2f * l.level);
            l.light.intensity = l.full * k;
            bool on = l.level > 0.002f;
            if (l.light.enabled != on) l.light.enabled = on;
        }

        /// <summary>Picks the lights nearest the camera (for the tests: also callable directly).</summary>
        public void Scan()
        {
            var cam = Camera.main;
            if (!cam) return;
            Scan(cam.transform.position);
        }

        public void Scan(Vector3 from)
        {
            int n = lamps.Count;
            if (chosen.Length < n) chosen = new bool[n + 64];
            for (int i = 0; i < n; i++) chosen[i] = false;

            float limit = activeRange * activeRange;
            int budget = Mathf.Min(maxActive, n);
            for (int k = 0; k < budget; k++)
            {
                int best = -1;
                float bestD = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (chosen[i]) continue;
                    var l = lamps[i];
                    float dx = l.at.x - from.x, dz = l.at.z - from.z;
                    float d = dx * dx + dz * dz;
                    if (d > limit) continue;
                    if (l.target) d *= Stickiness;
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) break;
                chosen[best] = true;
            }
            for (int i = 0; i < n; i++) lamps[i].target = chosen[i];
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The alarm beacons of one floor (the owner, 2026-10-24: "when the big alarm is activated there should be red lights activated that spin in circles (similar to a police car light). I added lamp assets to use for this, please use the one I sent a reference image of
    /// and add these lights around the floors on corners near the ceiling"): the pack's small roof lamp (<c>Resources/AlarmBeacon</c>: a prefab of the PBR Lamps Pack's Small_roof_lamp, made by <c>BeaconPrefabBuilder</c>), three times its size,
    /// mounted on the walls high up (<see cref="BeaconRules.Plan"/>: the lamp's ring flat against the wall, facing out into the room), dark like any lamp until the Big Alarm goes on; then every lamp glows red and a red beam turns in each (the lights nearest the camera cast real light, the others only glow). Purely local: every machine reads the Big Alarm from the run state (<see cref="HeistManager.BigAlarmActive"/>).
    /// Without the pack (no prefab) a plain red dome takes its place.
    /// </summary>
    public sealed class AlarmBeacons : MonoBehaviour
    {
        sealed class Beacon
        {
            public Transform lamp, beam;
            public Renderer[] renderers;
            public Light light;
            public Vector3 at;
            public float yaw;
            public int index;
            public bool live;
            public float glow = -1f;
        }

        const string PrefabName = "AlarmBeacon";
        static readonly Color Red = new Color(1f, 0.06f, 0.03f);
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor"), ColorId = Shader.PropertyToID("_Color");

        readonly List<Beacon> beacons = new List<Beacon>();
        MaterialPropertyBlock block;                                   // (made in Awake: a MonoBehaviour's field initializer may not make one)
        float level, onFor, nextPick;
        bool usedPrefab;
        static Material fallbackMat;

        /// <summary>The beacons of the floor on show (null between floors).</summary>
        public static AlarmBeacons Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; fallbackMat = null; }

        public int Count => beacons.Count;
        /// <summary>How lit the beacons are, 0 (dark) to 1 (the alarm is fully on).</summary>
        public float Level => level;
        /// <summary>How many beacons cast real light now.</summary>
        public int LiveLights { get { int n = 0; foreach (var b in beacons) if (b.light && b.light.enabled) n++; return n; } }
        /// <summary>Where beacon <paramref name="i"/> hangs, and the turn of its beam (degrees about the vertical; for the tests).</summary>
        public Vector3 PositionOf(int i) => beacons[i].at;
        public float BeamYaw(int i) => beacons[i].beam ? beacons[i].beam.eulerAngles.y : 0f;
        /// <summary>Do the lamps come from the pack (true) or are they the plain red domes used when the prefab is missing (false)?</summary>
        public bool FromPack => usedPrefab;
        /// <summary>Where beacon <paramref name="i"/>'s back touches the wall.</summary>
        public Vector3 WallPointOf(int i) { var b = beacons[i]; var fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward; return b.at - fwd * (BeaconRules.Standoff + BeaconRules.RingBack); }

        /// <summary>How many beacons have their back against a solid wall: a ray from just in front of the lamp straight back hits a collider where the wall's face is (the lamps have no colliders of their own).</summary>
        public int TouchingWall()
        {
            int n = 0;
            foreach (var b in beacons)
            {
                var fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
                float expect = 0.1f + BeaconRules.Standoff + BeaconRules.RingBack;
                if (Physics.Raycast(b.at + fwd * 0.1f, -fwd, out var hit, expect + 0.1f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(hit.distance - expect) < 0.03f) n++;
            }
            return n;
        }

        public bool HasColliders { get { foreach (var b in beacons) if (b.lamp && b.lamp.GetComponentInChildren<Collider>(true)) return true; return false; } }

        void Awake() { block = new MaterialPropertyBlock(); }

        void OnDestroy() { if (Current == this) Current = null; }

        /// <summary>Hangs the beacons of the plan under the ceiling at <paramref name="ceilingY"/> (the underside of the ceiling or of its tiles).</summary>
        public void Build(IList<BeaconSpot> spots, float ceilingY)
        {
            Current = this;
            var prefab = Resources.Load<GameObject>(PrefabName);
            usedPrefab = prefab != null;
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var out2 = Quaternion.Euler(0f, s.yaw, 0f) * Vector3.forward;                                   // (straight out of the wall into the room)
                float mount = BeaconRules.Standoff + BeaconRules.RingBack;                                    // (the ring's back is 2 cm off the wall: the lamp's own middle a little further)
                var b = new Beacon { index = i, yaw = s.yaw, at = new Vector3(s.x + out2.x * mount, ceilingY - BeaconRules.Drop, s.z + out2.z * mount) };
                var lamp = prefab ? Instantiate(prefab, transform) : MakeFallback();
                lamp.name = PrefabName + "_" + i;
                lamp.transform.position = b.at;
                lamp.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);               // (the ring flat on the wall, the dome facing out into the room)
                lamp.transform.localScale = Vector3.one * BeaconRules.LampScale;
                foreach (var c in lamp.GetComponentsInChildren<Collider>(true)) Destroy(c);          // (a lamp in the corner is not in anybody's way)
                b.lamp = lamp.transform;
                b.renderers = lamp.GetComponentsInChildren<Renderer>(true);
                foreach (var r in b.renderers) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }

                var beam = new GameObject("Beam_" + i).transform;
                beam.SetParent(transform, false);
                beam.position = b.at + out2 * 0.12f;
                beam.rotation = Quaternion.Euler(BeaconRules.BeamPitch, s.yaw, 0f);
                var l = beam.gameObject.AddComponent<Light>();
                l.type = LightType.Spot; l.color = Red; l.range = BeaconRules.BeamRange; l.spotAngle = BeaconRules.BeamAngle; l.intensity = 0f;
                l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel; l.enabled = false;
                b.beam = beam; b.light = l;
                beacons.Add(b);
            }
            foreach (var b in beacons) Paint(b, 0f);
        }

        /// <summary>The plain red dome used when the pack's lamp is not in the project.</summary>
        static GameObject MakeFallback()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.1f;
            if (!fallbackMat) fallbackMat = new Material(Shader.Find("Sprites/Default")) { name = "AlarmBeaconFallback", hideFlags = HideFlags.HideAndDontSave };
            go.GetComponent<Renderer>().sharedMaterial = fallbackMat;
            return go;
        }

        static bool AlarmOn()
        {
            var hm = HeistManager.Instance;
            return hm != null && hm.Phase == HeistPhase.Heist && hm.BigAlarmActive;
        }

        void Update()
        {
            bool on = AlarmOn();
            float before = level;
            level = Mathf.MoveTowards(level, on ? 1f : 0f, Time.deltaTime / (on ? 0.4f : 0.7f));
            if (on) onFor += Time.deltaTime; else if (level <= 0f) onFor = 0f;
            if (level <= 0f && before <= 0f) return;                                         // (dark: nothing to do)

            var cam = Camera.main;
            Vector3 eye = cam ? cam.transform.position : Vector3.zero;
            if (Time.unscaledTime >= nextPick) { nextPick = Time.unscaledTime + 0.25f; PickLive(cam ? eye : (Vector3?)null); }
            for (int i = 0; i < beacons.Count; i++)
            {
                var b = beacons[i];
                float turn = BeaconRules.Spin(onFor, b.index);
                b.beam.rotation = Quaternion.Euler(BeaconRules.BeamPitch, b.yaw + turn, 0f);
                if (b.light)
                {
                    b.light.intensity = BeaconRules.BeamIntensity * level;
                    bool want = b.live && level > 0.01f;
                    if (b.light.enabled != want) b.light.enabled = want;
                }
                // the glow swells as the beam turns past the camera, like a police light seen from the side
                float off = cam ? Mathf.DeltaAngle(b.yaw + turn, Mathf.Atan2(eye.x - b.at.x, eye.z - b.at.z) * Mathf.Rad2Deg) : 0f;
                float glow = level * BeaconRules.Glow(off);
                if (Mathf.Abs(glow - b.glow) > 0.02f) { b.glow = glow; Paint(b, glow); }
            }
        }

        void PickLive(Vector3? from)
        {
            foreach (var b in beacons) b.live = false;
            if (!from.HasValue) return;
            float limit = BeaconRules.LiveRange * BeaconRules.LiveRange;
            for (int k = 0; k < BeaconRules.MaxLive; k++)
            {
                Beacon best = null; float bestD = float.MaxValue;
                foreach (var b in beacons)
                {
                    if (b.live) continue;
                    float dx = b.at.x - from.Value.x, dz = b.at.z - from.Value.z, d = dx * dx + dz * dz;
                    if (d <= limit && d < bestD) { bestD = d; best = b; }
                }
                if (best == null) break;
                best.live = true;
            }
        }

        /// <summary>The lamp at <paramref name="glow"/> (0 dark, 1 full): the pack's glowing parts (its emission map) turn red, the whole lamp is tinted a little.</summary>
        void Paint(Beacon b, float glow)
        {
            block.Clear();
            block.SetColor(EmissionId, Red * (glow * 3f));
            block.SetColor(ColorId, Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), Mathf.Clamp01(glow)));
            foreach (var r in b.renderers) if (r) r.SetPropertyBlock(block);
        }
    }
}

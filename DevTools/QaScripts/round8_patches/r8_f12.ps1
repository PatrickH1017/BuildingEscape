# Round 8, batch F12 (the owner, 2026-10-18: "I added more Synty assets for the weapons, please utilize and implement them and make sure the silencer is on the exit barrel of the pew"):
# the weapons use the Synty Battle Royale models - the crowbar, the baseball bat, the fire axe (the hatchet), the heavy pistol (the pew), its silencer, its magazine (the spare clip in a safe) and its cartridge (the case thrown out).
# Each model is measured and turned the way the game holds it (the catalog says how), the silencer is the Synty one and sits on the barrel's own line with its rear recess over the barrel's end (the old tube sat at the
# middle of the model, not on the barrel), and the hand closes round a melee weapon on its handle (the pack's pivots are not in the hand).
# Everything the runtime cannot measure (mesh vertices are unreadable in a build) is measured by the editor when it fills the catalog: the muzzle face of the pistol, the seat of the silencer, the handles' own axes.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

function RepBetween([string]$startMarker, [string]$endMarker, [string]$replacement) {
  $a = $script:s.IndexOf($startMarker)
  if ($a -lt 0) { throw "start marker not found: $startMarker" }
  if ($script:s.IndexOf($startMarker, $a + 1) -ge 0) { throw "start marker twice: $startMarker" }
  $b = $script:s.IndexOf($endMarker, $a)
  if ($b -lt 0) { throw "end marker not found: $endMarker" }
  $new = $replacement.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $script:s = $script:s.Substring(0, $a) + $new + $script:s.Substring($b)
}
function SetWhole([string]$text) {
  $script:s = $text.Replace("`r`n", "`n").Replace("`n", $script:eol) + $script:eol
}

# ================================================================ WeaponCatalog: how each model sits, and the measuring helper
Open-Src "$Src\WeaponCatalog.cs"
SetWhole @'
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The weapon prefabs and the slash animation retargeted to the Party Characters rig. Lives at Assets/Resources/WeaponCatalog.asset, created by the editor
    /// (Employees Take Revenge > Setup Weapons). Since 2026-10-18 the weapons are the Synty Battle Royale models (the owner added the pack): the crowbar, the baseball bat,
    /// the fire axe (the hatchet) and the heavy pistol (the pew) with its silencer; the editor fills in how each one sits (turn, grip, muzzle, silencer seat).
    /// </summary>
    public class WeaponCatalog : ScriptableObject
    {
        public GameObject crowbar, bat, hatchet;
        [Tooltip("The pew pistol (Synty Battle Royale: SM_Wep_Pistol_Heavy_01) and its cartridge (SM_Wep_Pistol_Ammo_01), which is thrown out of the gun as a case.")]
        public GameObject pew, pewBullet;
        [Tooltip("slash1 from the Free Sword Animation pack, retargeted to the party character rig.")]
        public AnimationClip slash;

        [Header("How the models sit (the owner, 2026-10-18: the Synty weapons)")]
        [Tooltip("The silencer that goes on the pew's muzzle (Synty Battle Royale: SM_Wep_Attachment_Silencer_01). Empty = a plain dark tube.")]
        public GameObject silencer;
        [Tooltip("How each model must be turned (Euler degrees) so that a melee weapon runs along +Y with its head up and the pew's barrel runs along +X with its top +Y (the game's frame). The Synty melee models already stand that way; the pistol's barrel runs along +Z, a quarter turn about Y.")]
        public Vector3 crowbarTurn, batTurn, hatchetTurn, pewTurn;
        [Tooltip("Where the hand closes round each melee weapon, in the model's own space: on the handle's own axis, a hand's width from the end opposite the head. Measured by the editor; zero = the model's own origin.")]
        public Vector3 crowbarGrip, batGrip, hatchetGrip;
        [Tooltip("In the pistol model's own space: the middle of the muzzle face (the barrel's own line), and where the silencer model's origin goes so that its rear recess sits over the barrel's end, with how it is turned there. Measured by the editor.")]
        public Vector3 pewMuzzleFace, silencerAt, silencerTurn;
        [Tooltip("Which set of models the editor filled in last. It fills the Synty ones once, so a model chosen by hand is not put back.")]
        public int modelsVersion;

        /// <summary>
        /// The size and place of a model once it is turned by <paramref name="turn"/>, around the model's own origin: all its parts, or only those whose name <paramref name="part"/> accepts.
        /// Measured from a copy of the prefab that is gone again at once (a renderer only knows its bounds while it is alive). False when nothing was found to measure.
        /// </summary>
        public static bool Measure(GameObject prefab, Quaternion turn, System.Func<string, bool> part, out Bounds bounds)
        {
            bounds = default;
            if (!prefab) return false;
            var origin = new Vector3(0f, -4000f, 0f);
            var probe = Object.Instantiate(prefab, origin, turn);
            foreach (var c in probe.GetComponentsInChildren<Collider>()) c.enabled = false;
            bool any = false;
            foreach (var r in probe.GetComponentsInChildren<Renderer>())
            {
                if (part != null && !part(r.name)) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (Application.isPlaying) Object.Destroy(probe); else Object.DestroyImmediate(probe);
            bounds.center -= origin;
            return any;
        }
    }
}
'@
Save-Src

# ================================================================ the builder: fill the catalog with the Synty models once, measuring what the runtime cannot
Open-Src "$Ed\WeaponCatalogBuilder.cs"
Rep @'
        const string GunPack = "Assets/GunsAssetPack/guns/Prefabs/";
'@ @'
        const string GunPack = "Assets/GunsAssetPack/guns/Prefabs/";
        const string Synty = "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/";
        const int SyntyVersion = 1;
'@
Rep @'
                else if (!cat.pew || !cat.pewBullet) FillGuns(cat, false);
'@ @'
                else
                {
                    if (!cat.pew || !cat.pewBullet) FillGuns(cat, false);
                    if (cat.modelsVersion < SyntyVersion) FillSynty(cat, false);
                }
'@
Rep @'
        [MenuItem("Employees Take Revenge/Setup Weapons", priority = 31)]
'@ @'
        /// <summary>
        /// The weapons from the Synty Battle Royale pack (the owner, 2026-10-18): the crowbar, the bat, the fire axe (the hatchet), the heavy pistol (the pew) with its silencer, and a cartridge for the case that is thrown out.
        /// The melee models lie as the pack makes them (along +Y, head up: no turn); the pistol and the silencer run along +Z, so the pistol is turned a quarter about Y to put its barrel along the game's +X. What the game cannot
        /// measure for itself - a build cannot read a mesh's vertices - is measured here and stored: where the hand closes on each handle, the middle of the pistol's muzzle face, and where the silencer's origin goes so that its
        /// rear recess sits over the end of the barrel on the barrel's own line. Done once (<see cref="WeaponCatalog.modelsVersion"/>), so a model chosen by hand is not put back.
        /// </summary>
        static void FillSynty(WeaponCatalog cat, bool log)
        {
            var crowbar = Load<GameObject>(Synty + "SM_Wep_Crowbar_01.prefab");
            var bat = Load<GameObject>(Synty + "SM_Wep_Bat_01.prefab");
            var axe = Load<GameObject>(Synty + "SM_Wep_Fireaxe_01.prefab");
            var pistol = Load<GameObject>(Synty + "SM_Wep_Pistol_Heavy_01.prefab");
            var silencer = Load<GameObject>(Synty + "SM_Wep_Attachment_Silencer_01.prefab");
            var cartridge = Load<GameObject>(Synty + "SM_Wep_Pistol_Ammo_01.prefab");
            if (!crowbar || !bat || !axe || !pistol || !silencer)
            {
                if (log) Debug.LogWarning("The Synty Battle Royale weapons were not found where expected (" + Synty + "): the old models stay.");
                return;
            }
            if (!MuzzleFace(pistol, out var face) || !SilencerSeat(silencer, out var seat))
            {
                Debug.LogWarning("The Synty pistol or silencer could not be measured (no mesh to read): the old models stay.");
                return;
            }
            cat.crowbar = crowbar; cat.bat = bat; cat.hatchet = axe;
            cat.crowbarTurn = cat.batTurn = cat.hatchetTurn = Vector3.zero;
            cat.crowbarGrip = HandleGrip(crowbar, MeleeWeapon.Kind.Crowbar);
            cat.batGrip = HandleGrip(bat, MeleeWeapon.Kind.Bat);
            cat.hatchetGrip = HandleGrip(axe, MeleeWeapon.Kind.Hatchet);
            cat.pew = pistol; cat.pewTurn = new Vector3(0f, 90f, 0f);
            cat.pewMuzzleFace = face;
            cat.silencer = silencer; cat.silencerAt = face - new Vector3(0f, 0f, seat); cat.silencerTurn = Vector3.zero;
            if (cartridge) cat.pewBullet = cartridge;
            cat.modelsVersion = SyntyVersion;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"Weapon catalog: the Synty weapons are in (muzzle face {face:F4}, silencer seat {seat:F4}, grips {cat.crowbarGrip:F3} {cat.batGrip:F3} {cat.hatchetGrip:F3}).");
        }

        /// <summary>The middle of the pistol's muzzle face: the front-most ring of vertices of its body mesh (the end of the barrel), in the model's own space.</summary>
        static bool MuzzleFace(GameObject pistol, out Vector3 face)
        {
            face = default;
            var mf = pistol.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return false;
            var vs = mf.sharedMesh.vertices;
            if (vs.Length == 0) return false;
            float front = float.MinValue;
            foreach (var v in vs) front = Mathf.Max(front, v.z);
            Bounds ring = default; bool any = false;
            foreach (var v in vs)
            {
                if (v.z < front - 0.0005f) continue;
                if (!any) { ring = new Bounds(v, Vector3.zero); any = true; } else ring.Encapsulate(v);
            }
            face = new Vector3(ring.center.x, ring.center.y, front);
            return any;
        }

        /// <summary>Where the silencer's rear recess ends (its own z): the barrel's end goes in as far as this. The first centre vertex behind the rear face is the bottom of the recess.</summary>
        static bool SilencerSeat(GameObject silencer, out float seatZ)
        {
            seatZ = 0f;
            var mf = silencer.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return false;
            bool any = false; float z = float.MaxValue;
            foreach (var v in mf.sharedMesh.vertices)
                if (new Vector2(v.x, v.y).sqrMagnitude < 0.0005f * 0.0005f && v.z < z) { z = v.z; any = true; }
            seatZ = z;
            return any;
        }

        /// <summary>
        /// Where the hand closes round a melee weapon (model space): <see cref="MeleeWeapon.GripFromEnd"/> metres (as held) up from the end opposite the head - the model runs along +Y, head up, so that is its lowest end -
        /// on the handle's own axis, which is the middle of the ring of vertices nearest to that height (the pack's origin is not always on the handle: the crowbar's is a hand's width beside it).
        /// </summary>
        static Vector3 HandleGrip(GameObject prefab, MeleeWeapon.Kind kind)
        {
            var mf = prefab.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return Vector3.zero;
            var vs = mf.sharedMesh.vertices;
            var b = mf.sharedMesh.bounds;
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scale = longest > 0.0001f ? MeleeWeapon.Specs[(int)kind].length / longest : 1f;
            float y = b.min.y + MeleeWeapon.GripFromEnd[(int)kind] / scale;
            float nearest = float.MaxValue, ringY = y;
            foreach (var v in vs) { float d = Mathf.Abs(v.y - y); if (d < nearest) { nearest = d; ringY = v.y; } }
            Bounds ring = default; bool any = false;
            foreach (var v in vs)
            {
                if (Mathf.Abs(v.y - ringY) > 0.004f) continue;
                if (!any) { ring = new Bounds(v, Vector3.zero); any = true; } else ring.Encapsulate(v);
            }
            return any ? new Vector3(ring.center.x, y, ring.center.z) : new Vector3(0f, y, 0f);
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): fills the catalog with the Synty models now, the same as the first editor start with the pack in, and exits.</summary>
        public static void UpgradeBatch()
        {
            try
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) { Debug.LogError("[Weapons] no catalog"); return; }
                cat.modelsVersion = 0;
                FillSynty(cat, true);
                Debug.Log($"[Weapons] catalog: crowbar {(cat.crowbar ? cat.crowbar.name : "-")}, bat {(cat.bat ? cat.bat.name : "-")}, hatchet {(cat.hatchet ? cat.hatchet.name : "-")}, pew {(cat.pew ? cat.pew.name : "-")}, silencer {(cat.silencer ? cat.silencer.name : "-")}, bullet {(cat.pewBullet ? cat.pewBullet.name : "-")}, version {cat.modelsVersion}");
            }
            finally { EditorApplication.Exit(0); }
        }

        [MenuItem("Employees Take Revenge/Setup Weapons", priority = 31)]
'@
Rep @'
            cat.crowbar = crowbar; cat.bat = bat; cat.hatchet = hatchet; cat.slash = dst;
            FillGuns(cat, false);
'@ @'
            cat.crowbar = crowbar; cat.bat = bat; cat.hatchet = hatchet; cat.slash = dst;
            cat.modelsVersion = 0;
            FillGuns(cat, false);
            FillSynty(cat, false);
'@
Save-Src

# ================================================================ melee: the Synty models, held by the handle
Open-Src "$Src\MeleeWeapon.cs"
SetWhole @'
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A melee weapon item (crowbar, bat, hatchet): the crowbar, baseball bat and fire axe of the Synty Battle Royale pack (the owner added it, 2026-10-18; the weapon catalog says which models, how they are turned
    /// and where the hand closes on them). It is a normal Pickup (weighs something, goes in the backpack); equip it in the inventory (Tab)
    /// and it appears in the player's right hand. LMB swings it with the slash animation.
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        public enum Kind { Crowbar, Bat, Hatchet }

        /// <summary>What the network calls a punch (a weapon is its <see cref="Kind"/>, 0-2).</summary>
        public const byte PunchKind = 255;

        public struct Spec
        {
            public string name;
            public float weight, length, stun, knock, cooldown, noise;
            /// <summary>Health taken off a training dummy per hit (guards go down by hit count instead).</summary>
            public float damage;
        }

        // length = how long the weapon is in metres when held; stun in seconds; knock = shove on hit.
        public static readonly Spec[] Specs =
        {
            new Spec { name = "Crowbar",      weight = 3f, length = 0.85f, stun = 4f, knock = 5f, cooldown = 0.9f, noise = 16f, damage = 14f },
            new Spec { name = "Baseball Bat", weight = 2f, length = 1.00f, stun = 3f, knock = 9f, cooldown = 0.8f, noise = 14f, damage = 11f },
            new Spec { name = "Hatchet",      weight = 3f, length = 0.70f, stun = 6f, knock = 4f, cooldown = 1.0f, noise = 18f, damage = 22f },
        };

        /// <summary>How far up the handle (m, as held) the hand closes, from the end opposite the head: a hand's width, so that a little of the handle shows below the fist (crowbar, bat, hatchet).</summary>
        public static readonly float[] GripFromEnd = { 0.16f, 0.14f, 0.12f };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        static WeaponCatalog catalog;
        public static WeaponCatalog Catalog => catalog ? catalog : (catalog = Resources.Load<WeaponCatalog>("WeaponCatalog"));
        public static bool Available => Catalog && Catalog.crowbar && Catalog.bat && Catalog.hatchet;

        public static GameObject PrefabOf(Kind k) => k == Kind.Crowbar ? Catalog.crowbar : k == Kind.Bat ? Catalog.bat : Catalog.hatchet;

        /// <summary>How the catalog says the model must be turned so that it runs along +Y with its head up.</summary>
        public static Quaternion TurnOf(Kind k) => Quaternion.Euler(k == Kind.Crowbar ? Catalog.crowbarTurn : k == Kind.Bat ? Catalog.batTurn : Catalog.hatchetTurn);

        static Vector3 GripOf(Kind k) => k == Kind.Crowbar ? Catalog.crowbarGrip : k == Kind.Bat ? Catalog.batGrip : Catalog.hatchetGrip;

        // How the weapon sits in the right hand: the weapon's +Y (its head) points the way the sword in the
        // sword pack's sample character points, and the flat of the blade matches the sword's.
        static readonly Vector3 HeadInHand = new Vector3(-0.982f, 0.116f, -0.152f);
        static readonly Vector3 FlatInHand = new Vector3(0.190f, 0.668f, -0.719f);

        public static Quaternion HoldRotation
        {
            get
            {
                var y = HeadInHand.normalized;
                var x = (FlatInHand - y * Vector3.Dot(FlatInHand, y)).normalized;
                var z = Vector3.Cross(x, y);
                return Quaternion.LookRotation(z, y);
            }
        }

        // ------------------------------------------------------------------ the model

        /// <summary>Where a weapon's parts are once its model is turned the way the game holds it (head up along +Y), and what follows from that. Measured once per weapon, from the prefab.</summary>
        public struct Layout
        {
            public bool valid;
            /// <summary>Scale that makes the weapon as long as its spec says (the packs use different units).</summary>
            public float scale;
            /// <summary>The turn that takes the model into the game's frame.</summary>
            public Quaternion turn;
            /// <summary>The model's box around its own origin, in the game's frame, before scaling.</summary>
            public Bounds bounds;
            /// <summary>Where the hand closes round it (on the handle's own axis): in the game's frame around the model's origin, before scaling.</summary>
            public Vector3 grip;
        }

        static Layout[] layouts = new Layout[Specs.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { layouts = new Layout[Specs.Length]; }

        /// <summary>The measurements of the model (made the first time, from a copy of the prefab that is gone again at once).</summary>
        public static Layout LayoutOf(Kind k)
        {
            var l = layouts[(int)k];
            if (l.valid) return l;
            var turn = TurnOf(k);
            l.turn = turn;
            l.scale = 1f;
            l.bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            if (WeaponCatalog.Measure(PrefabOf(k), turn, null, out var b))
            {
                l.bounds = b;
                float len = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (len > 0.0001f) l.scale = Specs[(int)k].length / len;
            }
            var grip = GripOf(k);
            l.grip = grip != Vector3.zero ? turn * grip : new Vector3(0f, l.bounds.min.y + GripFromEnd[(int)k] / l.scale, 0f);
            l.valid = true;
            layouts[(int)k] = l;
            return l;
        }

        static void StripColliders(GameObject go)
        {
            // (switched off first: a mesh collider that is not convex must not sit on a rigid body even for the rest of this frame)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Object.Destroy(c); }
        }

        /// <summary>A weapon lying around as a pickup item.</summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? Random.Range(0f, 360f) : yaw, 90f); // lying on its side

            var model = Object.Instantiate(PrefabOf(k), root.transform);
            StripColliders(model);
            model.transform.localRotation = lay.turn;
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localPosition = -(lay.bounds.center * lay.scale);        // the middle of the box at the item's origin (the pack's own origin can be anywhere on the weapon)

            var bc = root.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = lay.bounds.size * lay.scale;
            root.AddComponent<Rigidbody>().mass = 2f;
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            root.AddComponent<MeleeWeapon>().kind = k;
            return root;
        }

        /// <summary>The weapon model parented into a hand bone (visual only).</summary>
        public static GameObject BuildHeld(Kind k, Transform hand)
        {
            var lay = LayoutOf(k);
            var go = Object.Instantiate(PrefabOf(k));
            go.name = Specs[(int)k].name + "_Held";
            StripColliders(go);
            go.transform.SetParent(hand, false);
            var hold = HoldRotation;
            go.transform.localRotation = hold * lay.turn;
            go.transform.localScale = Vector3.one * lay.scale / Mathf.Max(0.01f, hand.lossyScale.y);
            // the palm sits a little along the hand from the wrist; the point of the handle the fingers close round goes there
            var palm = hand.position + hand.TransformDirection(Vector3.left) * 0.07f;
            go.transform.position = palm - hand.rotation * (hold * (lay.grip * lay.scale));
            return go;
        }
    }
}
'@
Save-Src

# ================================================================ the pistol: the Synty model, its silencer on the barrel, its magazine as the spare clip
Open-Src "$Src\Guns\RangedWeapon.cs"
SetWhole @'
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A gun item: the pew pistol (the Synty Battle Royale heavy pistol, the owner, 2026-10-18). Like the melee weapons it is a normal Pickup (it weighs something and goes in the
    /// backpack) and is equipped in the inventory; unlike them it fires bullets and has a magazine that has to be reloaded.
    /// It lies in the guard outpost of floor 3 and the dealer sells it once that floor is finished (for money); the numbers below make it the strongest weapon in
    /// the game: one bullet counts as four of the ten hits it takes to knock a guard out (three bullets), a swing of the hatchet as one. It holds a clip of eight
    /// rounds and comes with two spare clips (three in all, the owner, 2026-10-18); more rounds are bought from the dealer.
    /// </summary>
    public class RangedWeapon : MonoBehaviour
    {
        public enum Kind { Pew }

        public struct Spec
        {
            public string name;
            /// <summary>Kilograms in the pack, and how long the gun is in metres when held (the pack models are not built to scale).</summary>
            public float weight, length;
            /// <summary>Health a bullet takes off a training dummy (a melee hit takes 11 to 22).</summary>
            public float damage;
            /// <summary>How many of a guard's hits to knock-out one bullet counts as (a melee hit counts as one).</summary>
            public int guardHits;
            /// <summary>Seconds a guard hit by a bullet is stunned, and the shove (m/s) a bullet gives a loose object.</summary>
            public float stun, knock;
            /// <summary>How far a gunshot is heard: guards within this many metres come to look.</summary>
            public float noise;
            /// <summary>The same with the silencer on (the player's pew has one; an officer's does not), and how long the silencer is (m) - the plain tube's length when the catalog has no silencer model (the Synty one measures about the same as scaled).</summary>
            public float silencedNoise, silencerLength;
            public int magazine;
            /// <summary>The spare clips a new gun comes with (the clip in the gun is the first of the three).</summary>
            public int spareClips;
            public float reloadSeconds, fireInterval;
            /// <summary>How far a bullet flies (m) and the cone it is fired into (degrees).</summary>
            public float range, spread;
        }

        // The strongest weapon there is: damage / stun / knock / hits beat every melee weapon (see GunTest in NetDevTest).
        public static readonly Spec[] Specs =
        {
            new Spec
            {
                name = "Pew", weight = 1.2f, length = 0.30f,
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f, silencedNoise = 12f, silencerLength = 0.16f,
                magazine = 8, spareClips = 2, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
            },
        };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        /// <summary>
        /// The silencer (the owner, 2026-10-18: "If possible please add a silencer to the pew pistol"): the player's pew has one, built in (an officer's does not). A silenced shot is quiet, its flash is small,
        /// and guards only hear it from <see cref="Spec.silencedNoise"/> metres.
        /// </summary>
        public bool silenced = true;

        /// <summary>How far a shot of this gun is heard by the guards (m).</summary>
        public float NoiseRadius => silenced ? Data.silencedNoise : Data.noise;

        static Material silencerMat;
        static Material SilencerMaterial()
        {
            if (silencerMat) return silencerMat;
            var sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) sh = Shader.Find("Sprites/Default");
            silencerMat = new Material(sh) { name = "Silencer", color = new Color(0.09f, 0.09f, 0.1f) };
            if (silencerMat.HasProperty("_Glossiness")) silencerMat.SetFloat("_Glossiness", 0.45f);
            return silencerMat;
        }

        /// <summary>A dark tube along the barrel (visual only; what is used when the catalog has no silencer model): its middle at <paramref name="centre"/>, its axis along <paramref name="rotation"/>'s up.</summary>
        static void AddSilencer(Transform parent, Vector3 centre, Quaternion rotation, float length)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Silencer";
            var col = go.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rotation;
            go.transform.localScale = new Vector3(0.032f, length * 0.5f, 0.032f);            // (the primitive is 2 m tall)
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = SilencerMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>
        /// The Synty silencer on the barrel (the owner, 2026-10-18: "make sure the silencer is on the exit barrel of the pew"): a child of <paramref name="parent"/> called "Silencer", scaled like the gun, with its rear recess over the
        /// end of the barrel and its own axis on the barrel's line (both measured from the models by the editor, see WeaponCatalogBuilder). <paramref name="origin"/> is where the gun model's own origin is in the parent's space,
        /// <paramref name="frame"/> how the game's frame (barrel along +X, top +Y) is turned there.
        /// </summary>
        static GameObject AttachSilencer(Transform parent, Vector3 origin, Quaternion frame, Layout lay)
        {
            var go = Object.Instantiate(Catalog.silencer, parent, false);
            StripColliders(go);
            go.name = "Silencer";
            go.transform.localPosition = origin + frame * (lay.silencerAt * lay.scale);
            go.transform.localRotation = frame * lay.silencerRot;
            go.transform.localScale = Vector3.one * lay.scale;
            return go;
        }

        GunMagazine magazine;
        /// <summary>The rounds in this gun: the state travels with the item (drop it and pick it up again, it is as you left it).</summary>
        public GunMagazine Magazine => magazine ??= NewMagazine(Data);

        // (The elevator used to give every gun a full magazine again when a floor started. Since 2026-10-18 it does not: "more ammo should need to be purchased from the Vendor".)

        /// <summary>A new gun: a full clip in it and two spare ones (a gun that has been bought or found is loaded, the rest costs money).</summary>
        public static GunMagazine NewMagazine(Spec s)
        {
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve(s.spareClips * s.magazine);
            return m;
        }

        public static WeaponCatalog Catalog => MeleeWeapon.Catalog;
        public static bool Available => Catalog && Catalog.pew;
        public static GameObject PrefabOf(Kind k) => Catalog.pew;

        /// <summary>True for any item that is a weapon (the backpack equips these).</summary>
        public static bool IsWeapon(Pickup p) => p && (p.TryGetComponent(out MeleeWeapon _) || p.TryGetComponent(out RangedWeapon _));

        // ------------------------------------------------------------------ the model

        /// <summary>Where things are on the pack's model, measured once from the prefab and turned into the game's frame (its barrel points along +X, its top is +Y); all before scaling, around the model's own origin.</summary>
        public struct Layout
        {
            public bool valid;
            /// <summary>Model scale that makes the gun as long as the spec says.</summary>
            public float scale;
            /// <summary>The front of the barrel (where a shot of the bare gun leaves) and the middle of the grip.</summary>
            public Vector3 muzzle, grip;
            /// <summary>The gun alone, and with its silencer on (the same box when there is no silencer model).</summary>
            public Bounds bounds, fullBounds;
            /// <summary>The Synty silencer: there is one, where its origin goes and how it is turned there, and where a silenced shot leaves (the middle of its front face).</summary>
            public bool hasSilencer;
            public Vector3 silencerAt, silencedMuzzle;
            public Quaternion silencerRot;
        }

        static Layout[] layouts = new Layout[Specs.Length];
        static bool magMeasured, hasMag;
        static Bounds magBounds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { layouts = new Layout[Specs.Length]; magMeasured = false; }

        static void Discard(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        /// <summary>The measurements of the model (made the first time, from a copy of the prefab that is gone again at once).</summary>
        public static Layout LayoutOf(Kind k)
        {
            var l = layouts[(int)k];
            if (l.valid) return l;

            var spec = Specs[(int)k];
            var cat = Catalog;
            var turn = Quaternion.Euler(cat.pewTurn);                                      // the pack's own frame -> the game's (barrel along +X, top +Y)
            var origin = new Vector3(0f, -4000f, 0f);
            var probe = Object.Instantiate(PrefabOf(k), origin, turn);
            foreach (var c in probe.GetComponentsInChildren<Collider>()) c.enabled = false;
            var rs = probe.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(origin, new Vector3(0.14f, 0.09f, 0.014f));      // (what the old pack's pistol measures, if it has no renderer)
            if (rs.Length > 0) { b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); }
            Renderer slide = null;
            foreach (var r in rs)
            {
                var n = r.name.ToLowerInvariant();
                if (n.Contains("haut") || n.Contains("slide")) slide = r;                  // "pewhaut" / "SM_Wep_Pistol_Heavy_Slide_01": the slide
            }
            var sb = slide ? slide.bounds : b;
            Discard(probe);

            b.center -= origin; sb.center -= origin;
            l.valid = true;
            l.bounds = b;
            l.scale = b.size.x > 0.001f ? spec.length / b.size.x : 1f;
            // a shot of the bare gun leaves the end of the barrel: the muzzle face the editor measured, or else the front of the slide
            l.muzzle = cat.pewMuzzleFace != Vector3.zero ? turn * cat.pewMuzzleFace : new Vector3(sb.max.x, sb.center.y, sb.center.z);
            l.grip = new Vector3(b.min.x + 0.26f * b.size.x, b.center.y - 0.18f * b.size.y, b.center.z);
            l.fullBounds = b;
            if (cat.silencer && cat.pewMuzzleFace != Vector3.zero && WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sil))
            {
                l.hasSilencer = true;
                l.silencerAt = turn * cat.silencerAt;
                l.silencerRot = turn * Quaternion.Euler(cat.silencerTurn);
                l.silencedMuzzle = l.silencerAt + l.silencerRot * new Vector3(0f, 0f, sil.max.z);          // (the tube runs along its own +Z: its front face is at the box's far end)
                for (int i = 0; i < 8; i++)
                {
                    var corner = sil.center + Vector3.Scale(sil.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    l.fullBounds.Encapsulate(l.silencerAt + l.silencerRot * corner);
                }
            }
            layouts[(int)k] = l;
            return l;
        }

        static void StripColliders(GameObject go)
        {
            // (switched off first: a mesh collider that is not convex must not sit on a rigid body even for the rest of this frame)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Object.Destroy(c); }
        }

        /// <summary>The gun lying around as a pickup item (on its side, like the melee weapons).</summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var turn = Quaternion.Euler(Catalog.pewTurn);
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? Random.Range(0f, 360f) : yaw, 0f);

            var lying = Quaternion.Euler(90f, 0f, 0f);                                        // lying on its side: the thin way is up
            var model = Object.Instantiate(PrefabOf(k), root.transform);
            StripColliders(model);
            model.name = spec.name + "_Model";
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = lying * turn;
            model.transform.localPosition = -(lying * (lay.fullBounds.center * lay.scale));   // the middle of the whole gun (the silencer on it counts) at the item's origin

            var size = lay.fullBounds.size * lay.scale;
            var bc = root.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = new Vector3(size.x, Mathf.Max(size.z, 0.03f), size.y);                  // (the side view's height is now along z)
            root.AddComponent<Rigidbody>().mass = 0.8f;
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            var rw = root.AddComponent<RangedWeapon>();
            rw.kind = k;
            rw.silenced = true;
            if (lay.hasSilencer) AttachSilencer(root.transform, model.transform.localPosition, lying, lay);
            else AddSilencer(root.transform, new Vector3(size.x * 0.5f + spec.silencerLength * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), spec.silencerLength);      // (the plain tube lies along the barrel, a little to the front of the model)
            return root;
        }

        /// <summary>
        /// The gun held in a hand bone (visual only): a "grip" transform on the hand whose +Z is the barrel and +Y the top of the gun, the
        /// model on it, and the muzzle at the front of the barrel (of the silencer, when there is one on). <paramref name="grip"/> is what HeldGun aims.
        /// </summary>
        public static GameObject BuildHeld(Kind k, Transform hand, out Transform grip, out Transform muzzle, bool silenced = false)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var packTurn = Quaternion.Euler(Catalog.pewTurn);

            var root = new GameObject(spec.name + "_Held");
            grip = root.transform;
            grip.SetParent(hand, false);
            float inv = 1f / Mathf.Max(0.01f, hand.lossyScale.y);                              // the hand bone may be scaled
            grip.localScale = Vector3.one * inv;

            var model = Object.Instantiate(PrefabOf(k), grip, false);
            StripColliders(model);
            model.name = spec.name + "_Model";
            var turn = Quaternion.Euler(0f, -90f, 0f);                                         // the game's +X (barrel) becomes the grip's +Z
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = turn * packTurn;
            model.transform.localPosition = -(turn * (lay.grip * lay.scale));                  // the middle of the grip sits in the palm

            var m = new GameObject("Muzzle").transform;
            m.SetParent(grip, false);
            m.localRotation = Quaternion.identity;
            if (silenced && lay.hasSilencer)
            {
                // the Synty silencer sits on the end of the barrel, and the shot leaves from the middle of its front face
                AttachSilencer(grip, model.transform.localPosition, turn, lay);
                m.localPosition = model.transform.localPosition + turn * (lay.silencedMuzzle * lay.scale);
            }
            else
            {
                m.localPosition = model.transform.localPosition + turn * (lay.muzzle * lay.scale);
                if (silenced)
                {
                    // (no silencer model in the catalog: a plain tube on the front of the barrel, and the shot leaves from its end)
                    AddSilencer(grip, m.localPosition + new Vector3(0f, 0f, spec.silencerLength * 0.5f), Quaternion.Euler(90f, 0f, 0f), spec.silencerLength);
                    m.localPosition += new Vector3(0f, 0f, spec.silencerLength);
                }
            }
            muzzle = m;
            return root;
        }

        /// <summary>
        /// A spare clip for the safes (the owner, 2026-10-18): the pistol model's own magazine part, as big as it is on the gun and lying on its side with its length along +X (the way the pew lies), its middle at the parent's origin.
        /// <paramref name="size"/> is its box (x along the clip). Null, and a zero size, when the model has no magazine part - the caller builds a plain box then.
        /// </summary>
        public static GameObject CreateClipModel(Transform parent, out Vector3 size)
        {
            size = default;
            if (!Available) return null;
            if (!magMeasured)
            {
                magMeasured = true;
                hasMag = WeaponCatalog.Measure(Catalog.pew, Quaternion.identity, n => n.ToLowerInvariant().Contains("_mag"), out magBounds);
            }
            if (!hasMag) return null;
            GameObject part = null;
            foreach (var mf in Catalog.pew.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.ToLowerInvariant().Contains("_mag")) { part = mf.gameObject; break; }
            if (!part) return null;
            var lay = LayoutOf(Kind.Pew);
            var clone = Object.Instantiate(part, parent, false);
            clone.name = "Clip_Model";
            StripColliders(clone);
            var rot = Quaternion.Euler(0f, 0f, -90f);                                          // its length (the model's +Y) along +X, its thin way up
            clone.transform.localScale = Vector3.one * lay.scale;
            clone.transform.localRotation = rot;
            clone.transform.localPosition = -(rot * (magBounds.center * lay.scale));
            size = new Vector3(magBounds.size.y, magBounds.size.x, magBounds.size.z) * lay.scale;
            return clone;
        }
    }
}
'@
Save-Src

# ================================================================ GunFx: the Synty cartridge is 3.2 cm long
Open-Src "$Src\Guns\GunFx.cs"
Rep @'
        /// <summary>The pack's bullet is 2 cm long; the gun is scaled up about twice, and so are the cartridges.</summary>
        const float CasingScale = 2.4f;
'@ @'
        /// <summary>The Synty cartridge is 3.2 cm long and the gun is scaled down to about three quarters; the case is thrown out a little bigger than that (3.8 cm) so that it can be seen on the floor.</summary>
        const float CasingScale = 1.2f;
'@
Save-Src

# ================================================================ the safe: the pistol lies in the middle of the hollow, the spare clip is the pistol's own magazine
Open-Src "$Src\Outpost\FloorSafe.cs"
Rep @'
                // (the pew is 0.30 m long and its silencer sticks out 0.13 m more in front; the hollow is 0.51 m deep, so the middle of the gun lies a little towards the back wall)
                var local = new Vector3((i - (n - 1) * 0.5f) * GunSpacing, WallThickness + 0.022f, -0.075f);
'@ @'
                // (the pew with its silencer on is about 0.46 m long and the hollow 0.51 m deep, from the back wall at z -0.265 to the door at 0.245: the middle of the gun lies in the middle of the hollow)
                var local = new Vector3((i - (n - 1) * 0.5f) * GunSpacing, WallThickness + 0.024f, -0.01f);
'@
RepBetween '            var go = new GameObject("Pistol clip");' '            go.SetActive(false);                                 // out of the world until the safe is open' @'
            var go = new GameObject("Pistol clip");
            go.transform.SetParent(parent, true);
            // the pistol's own magazine (the Mag part of its model, as big as it is on the gun) lying on its side; a plain box when the model has no such part
            var model = RangedWeapon.CreateClipModel(go.transform, out var size);
            bool real = model != null;
            if (!real) size = ClipSize;
            var local = new Vector3(0.27f, WallThickness + size.y * 0.5f + 0.006f, -0.10f);
            go.transform.SetPositionAndRotation(transform.TransformPoint(local), Quaternion.Euler(0f, transform.eulerAngles.y - 90f, 0f));      // (its long side along the pistol's barrel, out of the door)
            if (!real)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform, false);
                body.transform.localScale = ClipSize;
                body.GetComponent<Renderer>().sharedMaterial = clipBody;
                Kill(body.GetComponent<Collider>());
                var rounds = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rounds.name = "Rounds";
                rounds.transform.SetParent(go.transform, false);
                rounds.transform.localPosition = new Vector3(0.012f, ClipSize.y * 0.5f + 0.003f, 0f);
                rounds.transform.localScale = new Vector3(ClipSize.x * 0.72f, 0.008f, ClipSize.z * 0.6f);
                rounds.GetComponent<Renderer>().sharedMaterial = clipBrass;
                Kill(rounds.GetComponent<Collider>());
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = real ? Vector3.zero : new Vector3(0f, 0.004f, 0f);
            box.size = real ? size + new Vector3(0.004f, 0.004f, 0.004f) : ClipSize + new Vector3(0f, 0.008f, 0f);
            go.AddComponent<Rigidbody>().mass = 0.15f;
            var pk = go.AddComponent<Pickup>();                  // (registers its network id now, in the same order on every machine)
            pk.weight = 0.2f; pk.label = "Pistol clip";
            go.AddComponent<AmmoClip>().rounds = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine;

'@
Save-Src
Write-Output 'round 8 batch F12 (Synty weapons: catalog, builder, melee, pistol, silencer, clip, casing, safe) patched'

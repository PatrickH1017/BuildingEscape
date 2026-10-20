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

        /// <summary>What kind of gun a weapon is (the owner, 2026-10-18: "Silencers should be on pistols, sub machines, and rifles" and "shotguns and snipers should not be silenced either").</summary>
        public enum WeaponClass { Pistol, SubMachineGun, Rifle, Shotgun, Sniper }

        /// <summary>The rule for silencers: a pistol, a sub machine gun and a rifle can take one; a shotgun and a sniper rifle never can - their shots stay loud.</summary>
        public static bool TakesSilencer(WeaponClass c) => c == WeaponClass.Pistol || c == WeaponClass.SubMachineGun || c == WeaponClass.Rifle;

        /// <summary>Is a gun silenced: only when a silencer was put on it and one fits it, whatever is asked.</summary>
        public static bool IsSilenced(Spec spec, bool silencerPutOn) => silencerPutOn && spec.CanSilence;

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
            /// <summary>What kind of gun it is: it decides whether a silencer fits (<see cref="RangedWeapon.TakesSilencer"/>).</summary>
            public WeaponClass weaponClass;
            /// <summary>Does a silencer fit this gun? Pistols, sub machine guns and rifles take one; shotguns and sniper rifles never do.</summary>
            public bool CanSilence => TakesSilencer(weaponClass);
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
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f, silencedNoise = 12f, silencerLength = 0.16f, weaponClass = WeaponClass.Pistol,
                magazine = 8, spareClips = 2, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
            },
        };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        /// <summary>
        /// The silencer (the owner, 2026-10-18: "If possible please add a silencer to the pew pistol"): the player's pew has one, built in (an officer's does not). A silenced shot is quiet, its flash is small,
        /// and guards only hear it from <see cref="Spec.silencedNoise"/> metres.
        /// </summary>
        public bool silenced
        {
            get => IsSilenced(Data, silencerOn);
            set => silencerOn = value;
        }
        bool silencerOn = true;

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
            /// <summary>The front of the barrel (where a shot of the bare gun leaves), the middle of the grip, and the ejection port on the right of the slide (where the empty case is thrown out).</summary>
            public Vector3 muzzle, grip, port;
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
            l.port = new Vector3(sb.center.x - 0.05f * sb.size.x, sb.center.y + 0.1f * sb.size.y, sb.min.z);       // (the case leaves the slide's right side, a little behind its middle; the right of the gun is the game frame's -z)
            l.fullBounds = b;
            if (spec.CanSilence && cat.silencer && cat.pewMuzzleFace != Vector3.zero && WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sil))
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
            if (!spec.CanSilence) return root;                                                // (a shotgun or a sniper rifle never has a silencer on it)
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
            silenced = IsSilenced(spec, silenced);                                             // (only a gun a silencer fits can wear one)

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
            // the empty case leaves the slide's ejection port, not the muzzle (with a silencer on, the muzzle is the far end of the gun)
            var port = new GameObject("Port").transform;
            port.SetParent(grip, false);
            port.localPosition = model.transform.localPosition + turn * (lay.port * lay.scale);
            port.localRotation = Quaternion.identity;
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

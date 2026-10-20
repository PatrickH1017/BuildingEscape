using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A gun item: the pew pistol (the Synty Battle Royale heavy pistol, the owner, 2026-10-18) and, since 2026-10-19, the sub machine gun, the assault rifle, the shotgun and the sniper rifle of the same pack (the owner: "Yes, add
    /// the SMG, rifle, shotgun and sniper guns from the Synty pack"). Like the melee weapons a gun is a normal Pickup (it weighs something and goes in the backpack) and is equipped in the inventory; unlike them it fires bullets and has a
    /// magazine that has to be reloaded. Every kind has its own numbers (<see cref="Specs"/>), its own model and hold, its own sounds and its own ammunition.
    /// The pew lies in the guard outpost of floor 3 and the dealer sells it once that floor is finished (for money); its numbers make it the strongest weapon of the early floors: one bullet counts as four of the ten hits it takes to
    /// knock a guard out (three bullets), a swing of the hatchet as one. It holds a clip of eight rounds and comes with two spare clips; more rounds are bought from the dealer. The other guns are found, not sold (the owner's ladder of
    /// unlocks: <see cref="Spec.unlockFloor"/>), and what they cost is their ammunition.
    /// </summary>
    public class RangedWeapon : MonoBehaviour
    {
        /// <summary>The kinds of gun, in the order of <see cref="Specs"/> (and of <c>WeaponCatalog.longGuns</c> after the pew). The numbers travel (a vendor product, an outpost's weapon list as 3 + kind): add new kinds at the end.</summary>
        public enum Kind { Pew, Smg, Rifle, Shotgun, Sniper }

        /// <summary>What kind of gun a weapon is (the owner, 2026-10-18: "Silencers should be on pistols, sub machines, and rifles" and "shotguns and snipers should not be silenced either").</summary>
        public enum WeaponClass { Pistol, SubMachineGun, Rifle, Shotgun, Sniper }

        /// <summary>How a gun is held: in the right hand alone (the pistol), or in both (the right on the grip, the left on the forend, the pump or the foregrip).</summary>
        public enum HoldStyle { OneHand, TwoHand }

        /// <summary>Which recordings of the Free Weapon SFX pack a gun uses (the pack has a Handgun, an Assault Rifle and a Shotgun folder).</summary>
        public enum SoundSet { Handgun, Rifle, Shotgun }

        /// <summary>The rule for silencers: a pistol, a sub machine gun and a rifle can take one; a shotgun and a sniper rifle never can - their shots stay loud.</summary>
        public static bool TakesSilencer(WeaponClass c) => c == WeaponClass.Pistol || c == WeaponClass.SubMachineGun || c == WeaponClass.Rifle;

        /// <summary>Is a gun silenced: only when a silencer was put on it and one fits it, whatever is asked.</summary>
        public static bool IsSilenced(Spec spec, bool silencerPutOn) => silencerPutOn && spec.CanSilence;

        public struct Spec
        {
            public string name;
            /// <summary>Kilograms in the pack, and how long the gun is in metres when held (the pack models are not built to scale).</summary>
            public float weight, length;
            /// <summary>Health a bullet takes off a training dummy (a melee hit takes 11 to 22); for a shotgun, what each pellet takes.</summary>
            public float damage;
            /// <summary>How many of a guard's hits to knock-out one bullet counts as (a melee hit counts as one); for a shotgun, what each pellet counts as.</summary>
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
            /// <summary>The spare clips a new gun comes with when it is bought or handed over by the test menu (the clip in the gun is the first of the three). A gun FOUND in a safe has none: one clip, the one in it (the owner, 2026-10-19).</summary>
            public int spareClips;
            public float reloadSeconds, fireInterval;
            /// <summary>How far a bullet flies (m) and the cone it is fired into (degrees).</summary>
            public float range, spread;

            // ---- since 2026-10-19 (the other guns of the pack)
            /// <summary>Bullets in one shot: a shotgun fires several pellets, each with its own spread; every other gun one. Zero counts as one.</summary>
            public int pellets;
            public int Pellets => pellets < 1 ? 1 : pellets;
            public HoldStyle hold;
            public SoundSet sounds;
            /// <summary>Degrees the barrel kicks up when a shot is fired (zero counts as 9, the pistol's), and the size of the muzzle flash compared with the pistol's (zero counts as 1).</summary>
            public float kick, flash;
            public float KickDegrees => kick > 0f ? kick : 9f;
            public float FlashScale => flash > 0f ? flash : 1f;
            /// <summary>The floor where this gun is first found (the owner's ladder, 2026-10-19: the pew 3, the sub machine gun 5, the shotgun 6, the assault rifle 8, the sniper rifle 10).</summary>
            public int unlockFloor;
            /// <summary>What the dealer calls this gun's ammunition, and what one clip (a full magazine) of it costs.</summary>
            public string ammoName;
            public int clipPrice;
            /// <summary>The same in the plural and in lower case, for a sentence ("the dealer sells pistol clips"), and the pitch of the gun's shot (a sniper rifle's is deeper, a sub machine gun's lighter; zero counts as 1).</summary>
            public string ammoPlural;
            public float pitch;
            public float Pitch => pitch > 0f ? pitch : 1f;
        }

        // The numbers: the pew is the strongest weapon of the early floors (see GunTest in NetDevTest); the others trade punch for rate (the sub machine gun), reach (the rifle), a crowd of pellets (the shotgun) or a single
        // very heavy bullet (the sniper rifle). Placeholders for the owner to tune, like the pew's were; a bullet does not knock a guard over (the owner, 2026-10-19), so the new guns stun for nothing.
        public static readonly Spec[] Specs =
        {
            new Spec
            {
                name = "Pew", weight = 1.2f, length = 0.30f,
                damage = 60f, guardHits = 4, stun = 0f, knock = 16f, noise = 45f, silencedNoise = 12f, silencerLength = 0.16f, weaponClass = WeaponClass.Pistol,
                magazine = 8, spareClips = 2, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
                hold = HoldStyle.OneHand, sounds = SoundSet.Handgun, kick = 9f, flash = 1f, unlockFloor = 3, ammoName = "Pistol Clip", clipPrice = 120, ammoPlural = "pistol clips", pitch = 1f,
            },
            new Spec
            {
                name = "SMG", weight = 1.9f, length = 0.50f,
                damage = 24f, guardHits = 2, stun = 0f, knock = 4f, noise = 40f, silencedNoise = 11f, silencerLength = 0.16f, weaponClass = WeaponClass.SubMachineGun,
                magazine = 24, spareClips = 2, reloadSeconds = 2.0f, fireInterval = 0.085f, range = 38f, spread = 3.4f,
                hold = HoldStyle.TwoHand, sounds = SoundSet.Handgun, kick = 3f, flash = 0.9f, unlockFloor = 5, ammoName = "SMG Magazine", clipPrice = 480, ammoPlural = "SMG magazines", pitch = 1.08f,
            },
            new Spec
            {
                name = "Rifle", weight = 3.4f, length = 0.90f,
                damage = 42f, guardHits = 4, stun = 0f, knock = 8f, noise = 55f, silencedNoise = 14f, silencerLength = 0.20f, weaponClass = WeaponClass.Rifle,
                magazine = 30, spareClips = 2, reloadSeconds = 2.6f, fireInterval = 0.12f, range = 70f, spread = 1.6f,
                hold = HoldStyle.TwoHand, sounds = SoundSet.Rifle, kick = 4f, flash = 1.1f, unlockFloor = 8, ammoName = "Rifle Magazine", clipPrice = 750, ammoPlural = "rifle magazines", pitch = 1f,
            },
            new Spec
            {
                name = "Shotgun", weight = 3.6f, length = 1.00f,
                damage = 14f, guardHits = 1, stun = 0f, knock = 2f, noise = 60f, silencedNoise = 60f, silencerLength = 0f, weaponClass = WeaponClass.Shotgun,
                magazine = 6, spareClips = 2, reloadSeconds = 3.0f, fireInterval = 0.85f, range = 24f, spread = 5.5f,
                pellets = 8, hold = HoldStyle.TwoHand, sounds = SoundSet.Shotgun, kick = 12f, flash = 1.6f, unlockFloor = 6, ammoName = "Shotgun Shells", clipPrice = 420, ammoPlural = "shotgun shells", pitch = 1f,
            },
            new Spec
            {
                name = "Sniper", weight = 4.8f, length = 1.15f,
                damage = 150f, guardHits = 10, stun = 0f, knock = 20f, noise = 85f, silencedNoise = 85f, silencerLength = 0f, weaponClass = WeaponClass.Sniper,
                magazine = 5, spareClips = 2, reloadSeconds = 3.4f, fireInterval = 1.35f, range = 120f, spread = 0.25f,
                hold = HoldStyle.TwoHand, sounds = SoundSet.Rifle, kick = 14f, flash = 1.5f, unlockFloor = 10, ammoName = "Sniper Rounds", clipPrice = 600, ammoPlural = "sniper rounds", pitch = 0.78f,
            },
        };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        /// <summary>
        /// The spare clips this particular gun starts with: -1 = what its spec says (a gun bought from the dealer, or handed over by the test menu); 0 = none, the way a gun FOUND in a safe is (the owner, 2026-10-19: "Guns found
        /// should have one clip in them"). Read when the magazine is first needed.
        /// </summary>
        public int startClips = -1;

        /// <summary>
        /// The silencer (the owner, 2026-10-18: "If possible please add a silencer to the pew pistol"): the player's pew has one, built in (an officer's does not); so does a player's sub machine gun and rifle. A silenced shot is
        /// quiet, its flash is small, and guards only hear it from <see cref="Spec.silencedNoise"/> metres.
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
        public GunMagazine Magazine => magazine ??= NewMagazine(Data, startClips);

        // (The elevator used to give every gun a full magazine again when a floor started. Since 2026-10-18 it does not: "more ammo should need to be purchased from the Vendor".)

        /// <summary>A new gun: a full clip in it and, unless <paramref name="spareClips"/> says otherwise (-1 = the spec's), its spare ones (a gun that has been bought is loaded, the rest costs money; one that was found has the one clip).</summary>
        public static GunMagazine NewMagazine(Spec s, int spareClips = -1)
        {
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve((spareClips < 0 ? s.spareClips : spareClips) * s.magazine);
            m.MaxRounds = GunRules.MaxClips * s.magazine;                    // (a player carries at most three clips)
            return m;
        }

        public static WeaponCatalog Catalog => MeleeWeapon.Catalog;
        /// <summary>The pistol's model is in the catalog (the other guns' are checked with <see cref="Has"/>).</summary>
        public static bool Available => Catalog && Catalog.pew;
        static WeaponCatalog.GunModel ModelOf(Kind k) => Catalog ? Catalog.Gun((int)k) : null;
        /// <summary>Is this kind's model in the catalog?</summary>
        public static bool Has(Kind k) { var m = ModelOf(k); return m != null && m.prefab; }
        public static GameObject PrefabOf(Kind k) => ModelOf(k).prefab;
        /// <summary>The empty case this kind of gun throws out: its own cartridge (a rifle's, a shotgun's shell), else the pistol's.</summary>
        public static GameObject CasingOf(Kind k) { var m = ModelOf(k); return m != null && m.casing ? m.casing : (Catalog ? Catalog.pewBullet : null); }

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
            /// <summary>Where the left hand holds a gun that is held in both hands (the forend, the pump, the foregrip).</summary>
            public bool hasForegrip;
            public Vector3 foregrip;
        }

        static Layout[] layouts = new Layout[Specs.Length];
        static bool[] magMeasured = new bool[Specs.Length], hasMag = new bool[Specs.Length];
        static Bounds[] magBounds = new Bounds[Specs.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { layouts = new Layout[Specs.Length]; magMeasured = new bool[Specs.Length]; hasMag = new bool[Specs.Length]; magBounds = new Bounds[Specs.Length]; }

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
            var gm = ModelOf(k);
            var turn = Quaternion.Euler(gm.turn);                                          // the pack's own frame -> the game's (barrel along +X, top +Y)
            var origin = new Vector3(0f, -4000f, 0f);
            var probe = Object.Instantiate(gm.prefab, origin, turn);
            foreach (var c in probe.GetComponentsInChildren<Collider>()) c.enabled = false;
            var rs = probe.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(origin, new Vector3(0.14f, 0.09f, 0.014f));      // (what the old pack's pistol measures, if it has no renderer)
            if (rs.Length > 0) { b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); }
            Renderer slide = null;
            foreach (var r in rs)
            {
                var n = r.name.ToLowerInvariant();
                if (n.Contains("haut") || n.Contains("slide") || n.Contains("bolt")) slide = r;      // "pewhaut" / "..._Slide_01" / a bolt-action's "..._Bolt_01": the part the empty case leaves from
            }
            var sb = slide ? slide.bounds : b;
            Discard(probe);

            b.center -= origin; sb.center -= origin;
            l.valid = true;
            l.bounds = b;
            l.scale = b.size.x > 0.001f ? spec.length / b.size.x : 1f;
            // a shot of the bare gun leaves the end of the barrel: the muzzle face the editor measured, or else the front of the slide
            l.muzzle = gm.muzzleFace != Vector3.zero ? turn * gm.muzzleFace : new Vector3(sb.max.x, sb.center.y, sb.center.z);
            // the right hand closes on the pistol grip (measured behind the trigger); the pew's own old formula when the catalog has none
            l.grip = gm.grip != Vector3.zero ? turn * gm.grip : new Vector3(b.min.x + 0.26f * b.size.x, b.center.y - 0.18f * b.size.y, b.center.z);
            if (gm.foregrip != Vector3.zero) { l.hasForegrip = true; l.foregrip = turn * gm.foregrip; }
            l.port = new Vector3(sb.center.x - 0.05f * sb.size.x, sb.center.y + 0.1f * sb.size.y, sb.min.z);       // (the case leaves the slide's right side, a little behind its middle; the right of the gun is the game frame's -z)
            l.fullBounds = b;
            if (spec.CanSilence && cat.silencer && gm.muzzleFace != Vector3.zero && WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sil))
            {
                l.hasSilencer = true;
                l.silencerAt = turn * gm.silencerAt;
                l.silencerRot = turn * Quaternion.Euler(gm.silencerTurn);
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

        /// <summary>
        /// The gun lying around as a pickup item (on its side, like the melee weapons). <paramref name="startClips"/> is the spare clips it starts with (-1: what its spec says; 0: the way a gun found in a safe is).
        /// </summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN, int startClips = -1)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var turn = Quaternion.Euler(ModelOf(k).turn);
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
            // (the side view's height is now along z; a long gun is picked up by its body: the box is no wider than that, not as wide as its scope and its magazine, so that in a safe it does not cover what lies beside it)
            bc.size = new Vector3(size.x, Mathf.Max(size.z, 0.03f), spec.hold == HoldStyle.TwoHand ? Mathf.Min(size.y, 0.10f) : size.y);
            root.AddComponent<Rigidbody>().mass = Mathf.Max(0.8f, spec.weight * 0.67f);
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            if (spec.hold == HoldStyle.TwoHand) p.aimBox = new Bounds(Vector3.zero, new Vector3(Mathf.Min(size.x, 0.32f), Mathf.Max(size.z, 0.05f), Mathf.Min(size.y, 0.10f)));      // (a long gun is aimed at by its middle: the receiver, the grip)
            var rw = root.AddComponent<RangedWeapon>();
            rw.kind = k;
            rw.startClips = startClips;
            rw.silenced = true;
            if (!spec.CanSilence) return root;                                                // (a shotgun or a sniper rifle never has a silencer on it)
            if (lay.hasSilencer) AttachSilencer(root.transform, model.transform.localPosition, lying, lay);
            else AddSilencer(root.transform, new Vector3(size.x * 0.5f + spec.silencerLength * 0.5f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), spec.silencerLength);      // (the plain tube lies along the barrel, a little to the front of the model)
            return root;
        }

        /// <summary>
        /// The gun held in a hand bone (visual only): a "grip" transform on the hand whose +Z is the barrel and +Y the top of the gun, the
        /// model on it, and the muzzle at the front of the barrel (of the silencer, when there is one on). <paramref name="grip"/> is what HeldGun aims. A gun held in both hands also gets a "Foregrip" child: where the left hand goes.
        /// </summary>
        public static GameObject BuildHeld(Kind k, Transform hand, out Transform grip, out Transform muzzle, bool silenced = false, float bodyScale = 1f)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var packTurn = Quaternion.Euler(ModelOf(k).turn);
            silenced = IsSilenced(spec, silenced);                                             // (only a gun a silencer fits can wear one)

            var root = new GameObject(spec.name + "_Held");
            grip = root.transform;
            grip.SetParent(hand, false);
            float inv = 1f / Mathf.Max(0.01f, hand.lossyScale.y);                              // the hand bone may be scaled
            grip.localScale = Vector3.one * inv * bodyScale;                                  // (a long gun is scaled to the arms that hold it: HeldGun)

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
            if (lay.hasForegrip)
            {
                var fg = new GameObject("Foregrip").transform;                                  // (where the left hand holds a gun carried in both hands)
                fg.SetParent(grip, false);
                fg.localPosition = model.transform.localPosition + turn * (lay.foregrip * lay.scale);
                fg.localRotation = Quaternion.identity;
            }
            muzzle = m;
            return root;
        }

        /// <summary>
        /// A spare clip for the safes (the owner, 2026-10-18): the pistol model's own magazine part, as big as it is on the gun and lying on its side with its length along +X (the way the pew lies), its middle at the parent's origin.
        /// <paramref name="size"/> is its box (x along the clip). Null, and a zero size, when the model has no magazine part - the caller builds a plain box then. <paramref name="k"/> is the gun the clip is for (the pew's).
        /// </summary>
        public static GameObject CreateClipModel(Transform parent, out Vector3 size, Kind k = Kind.Pew)
        {
            size = default;
            if (!Available || !Has(k)) return null;
            int ki = (int)k;
            var gm = ModelOf(k);
            string part = string.IsNullOrEmpty(gm.magazinePart) ? "_mag" : gm.magazinePart.ToLowerInvariant();
            if (!magMeasured[ki])
            {
                magMeasured[ki] = true;
                hasMag[ki] = WeaponCatalog.Measure(gm.prefab, Quaternion.identity, n => n.ToLowerInvariant().Contains(part), out magBounds[ki]);
            }
            if (!hasMag[ki]) return null;
            GameObject found = null;
            foreach (var mf in gm.prefab.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.ToLowerInvariant().Contains(part)) { found = mf.gameObject; break; }
            if (!found) return null;
            var lay = LayoutOf(k);
            var clone = Object.Instantiate(found, parent, false);
            clone.name = "Clip_Model";
            StripColliders(clone);
            var rot = Quaternion.Euler(0f, 0f, -90f);                                          // its length (the model's +Y) along +X, its thin way up
            clone.transform.localScale = Vector3.one * lay.scale;
            clone.transform.localRotation = rot;
            clone.transform.localPosition = -(rot * (magBounds[ki].center * lay.scale));
            size = new Vector3(magBounds[ki].size.y, magBounds[ki].size.x, magBounds[ki].size.z) * lay.scale;
            return clone;
        }

        /// <summary>
        /// The gun's model alone, for a shelf or a rack (the owner, 2026-10-19: the vendor's shop shows his stock): lying on its side the way <see cref="Create"/> has it, with no collider, no item and no silencer, its middle at the parent's local
        /// <paramref name="at"/>. <paramref name="thickness"/> is how thick it is lying down (so that it can be set on a board). Null when the pack has no such model.
        /// </summary>
        public static GameObject CreateDisplay(Kind k, Transform parent, Vector3 at, float yaw, out float thickness, bool upright = false)
        {
            thickness = 0f;
            if (!Available || !Has(k)) return null;
            var lay = LayoutOf(k);
            var turn = Quaternion.Euler(ModelOf(k).turn);
            var root = new GameObject(Specs[(int)k].name + "_Display");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var lying = upright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);          // (upright: hung on a wall, its barrel along x, its flat side to the viewer)
            var model = Object.Instantiate(PrefabOf(k), root.transform);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Discard(c); }
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = lying * turn;
            model.transform.localPosition = -(lying * (lay.bounds.center * lay.scale));
            thickness = lay.bounds.size.z * lay.scale;
            return root;
        }
    }
}

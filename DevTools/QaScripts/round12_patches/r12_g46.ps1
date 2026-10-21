# Round 12, batch G46 (the owner, 2026-10-20): "The sniper asset should be the barret 50 cal we have, the weapons should sound silenced other than the shotgun, sniper, and rocket launcher" and "Please add red dot sights for the machine guns and shotgun".
#  (1) the sniper rifle is the Barrett .50 cal of the Synty pack (SM_Wep_Sniper_Heavy_01; it was the bolt-action Mil model): the long gun table of the weapon catalog builder;
#  (2) a gun that wears a silencer (pistol, SMG, rifle) now SOUNDS silenced: a suppressed shot made from maths (a dull "pap" with its low thump and the slide / bolt working a moment later; a few variants for the handgun's and the rifle's set), not the loud recording
#      turned down; the shotgun, the sniper rifle and (when it comes) the rocket launcher stay loud (GunSounds.SoundsSilenced = the silencer rule). Officers' guns are unchanged (no silencer, loud, heard from 45 m);
#  (3) red dot sights on the SMG, the rifle and the shotgun (RangedWeapon.TakesRedDot): the Synty SM_Wep_Attachment_Redot_01 on top of the body, on the barrel's line (measured by the editor like the silencer's seat), with a little red lit dot in it;
#      on the held gun, the gun lying on the floor and the gun on the vendor's wall.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

# ---------------------------------------------------------------- the weapon catalog: a red dot prefab and where each sight sits
Open-Src "$A\WeaponCatalog.cs"
Rep '        public GameObject silencer;' @'
        public GameObject silencer;
        [Tooltip("The red dot sight that sits on top of the sub machine gun, the rifle and the shotgun (Synty Battle Royale: SM_Wep_Attachment_Redot_01; the owner, 2026-10-20). Empty = none.")]
        public GameObject redDot;
'@
Rep '            public Vector3 silencerAt, silencerTurn;' @'
            public Vector3 silencerAt, silencerTurn;
            [Tooltip("Where the red dot sight's origin goes (on top of the body, on the barrel's own line) and how it is turned there, for the guns that carry one (the SMG, the rifle, the shotgun); zero sightAt = no sight. Measured by the editor.")]
            public Vector3 sightAt, sightTurn;
'@
Save-Src

# ---------------------------------------------------------------- the weapon catalog builder: the Barrett and the sights
Open-Src "$E\WeaponCatalogBuilder.cs"
Rep 'if (cat.longGuns == null || cat.longGuns.Length < LongGunNames.Length || !cat.longGuns[0].prefab) FillLongGuns(cat, false);' 'if (cat.longGuns == null || cat.longGuns.Length < LongGunNames.Length || !cat.longGuns[0].prefab || !cat.redDot || !cat.longGuns[LongGunNames.Length - 1].prefab || cat.longGuns[LongGunNames.Length - 1].prefab.name != LongGunNames[LongGunNames.Length - 1]) FillLongGuns(cat, false);'
Rep '        static readonly string[] LongGunNames = { "SM_Wep_SubMGun_Lite_01", "SM_Wep_Rifle_Assault_01", "SM_Wep_Shotgun_01", "SM_Wep_Sniper_Mil_01" };' @'
        /// <summary>The four long guns. The sniper rifle is the Barrett .50 cal of the pack (SM_Wep_Sniper_Heavy_01; the owner, 2026-10-20: "The sniper asset should be the barret 50 cal we have"); it was the bolt-action SM_Wep_Sniper_Mil_01.</summary>
        static readonly string[] LongGunNames = { "SM_Wep_SubMGun_Lite_01", "SM_Wep_Rifle_Assault_01", "SM_Wep_Shotgun_01", "SM_Wep_Sniper_Heavy_01" };
'@
Rep '        static readonly bool[] LongGunSilenced = { true, true, false, false };' @'
        static readonly bool[] LongGunSilenced = { true, true, false, false };
        /// <summary>Which of the four carry a red dot sight (the owner, 2026-10-20: "Please add red dot sights for the machine guns and shotgun"), and how far along the gun's body from the rear (0 to 1) it stands: the sub machine gun and the rifle on their top rails, the shotgun on top of its receiver. The sniper rifle has its own scope.</summary>
        static readonly bool[] LongGunSight = { true, true, true, false };
        static readonly float[] LongGunSightAlong = { 0.46f, 0.46f, 0.38f, 0f };
'@
Rep '/// The sub machine gun (SM_Wep_SubMGun_Lite_01), the assault rifle (SM_Wep_Rifle_Assault_01), the shotgun (SM_Wep_Shotgun_01) and the sniper rifle (SM_Wep_Sniper_Mil_01) of the Synty pack' '/// The sub machine gun (SM_Wep_SubMGun_Lite_01), the assault rifle (SM_Wep_Rifle_Assault_01), the shotgun (SM_Wep_Shotgun_01) and the sniper rifle (the Barrett, SM_Wep_Sniper_Heavy_01) of the Synty pack'
Rep '            var list = new WeaponCatalog.GunModel[LongGunNames.Length];' @'
            var redDot = Load<GameObject>(Synty + "SM_Wep_Attachment_Redot_01.prefab");
            cat.redDot = redDot;
            var list = new WeaponCatalog.GunModel[LongGunNames.Length];
'@
Rep '                    gm.muzzleFace = FrontFace(inst);' @'
                    gm.muzzleFace = FrontFace(inst);
                    BodyRange(inst, out float bodyZ0, out float bodyZ1);
                    if (log) Debug.Log($"[Weapons] {LongGunNames[i]}: body z {bodyZ0:F3} .. {bodyZ1:F3}, its top at the middle {BodyTop(inst, (bodyZ0 + bodyZ1) * 0.5f):F3}");
                    if (LongGunSight[i] && redDot) gm.sightAt = SightSeat(inst, redDot, gm.muzzleFace, LongGunSightAlong[i]);
'@
Rep 'default: gm.foregrip = Underside(inst, 0.22f, 0.32f); break;' 'default: gm.foregrip = Underside(inst, bodyZ0 + 0.50f * (bodyZ1 - bodyZ0), bodyZ0 + 0.62f * (bodyZ1 - bodyZ0)); break;'
Rep 'silencer at {gm.silencerAt:F4}, casing' 'silencer at {gm.silencerAt:F4}, red dot at {gm.sightAt:F4}, casing'
Rep '        /// <summary>For the QA runs (<c>-executeMethod</c>): measures the long guns now (the same as the first editor start with them missing) and exits.</summary>' @'
        /// <summary>The parts of a gun that are its body: not the magazine, the trigger, the grips, the stock, the pump, the iron sights, the scope or the bipod (what a sight may stand on, what the hand may hold under).</summary>
        static bool IsBodyPart(string name)
        {
            var n = name.ToLowerInvariant();
            return !(n.Contains("_mag") || n.Contains("trigger") || n.Contains("grip") || n.Contains("sight") || n.Contains("flash") || n.Contains("stock") || n.Contains("pump") || n.Contains("scope") || n.Contains("bipod"));
        }

        /// <summary>How far the gun's body reaches along the barrel (its own z: the guns run along +z).</summary>
        static void BodyRange(GameObject gun, out float z0, out float z1)
        {
            z0 = float.MaxValue; z1 = float.MinValue;
            foreach (var mf in Meshes(gun))
            {
                if (!mf.sharedMesh || !IsBodyPart(mf.name)) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    float z = gun.transform.InverseTransformPoint(mf.transform.TransformPoint(v)).z;
                    z0 = Mathf.Min(z0, z); z1 = Mathf.Max(z1, z);
                }
            }
            if (z0 > z1) { z0 = 0f; z1 = 0.5f; }
        }

        /// <summary>The highest point of the gun's body within three centimetres of <paramref name="z"/> along the barrel.</summary>
        static float BodyTop(GameObject gun, float z)
        {
            float top = float.MinValue;
            foreach (var mf in Meshes(gun))
            {
                if (!mf.sharedMesh || !IsBodyPart(mf.name)) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = gun.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (Mathf.Abs(w.z - z) <= 0.03f && w.y > top) top = w.y;
                }
            }
            return top > float.MinValue ? top : 0.08f;
        }

        /// <summary>Where the red dot sight's origin goes (the owner, 2026-10-20): on the barrel's own line (the muzzle face's x), <paramref name="along"/> of the way from the rear of the body to its front, its underside three millimetres into the highest point of the body there.</summary>
        static Vector3 SightSeat(GameObject gun, GameObject sight, Vector3 muzzleFace, float along)
        {
            BodyRange(gun, out float z0, out float z1);
            float zc = Mathf.Lerp(z0, z1, along);
            float top = BodyTop(gun, zc);
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(sight);
            Bounds sb;
            try { sb = PartBounds(probe.transform); }
            finally { Object.DestroyImmediate(probe); }
            return new Vector3(muzzleFace.x, top - sb.min.y - 0.003f, zc - sb.center.z);
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): measures the long guns now (the same as the first editor start with them missing) and exits.</summary>
'@
Save-Src

# ---------------------------------------------------------------- the gun: the red dot rule, where the sight is, the sight on the held gun, the gun on the floor and the gun on the wall
Open-Src "$A\Guns\RangedWeapon.cs"
Rep '        public static bool IsSilenced(Spec spec, bool silencerPutOn) => silencerPutOn && spec.CanSilence;' @'
        public static bool IsSilenced(Spec spec, bool silencerPutOn) => silencerPutOn && spec.CanSilence;

        /// <summary>The rule for red dot sights (the owner, 2026-10-20: "Please add red dot sights for the machine guns and shotgun"): a sub machine gun, a rifle and a shotgun carry one; a pistol (its own sights) and a sniper rifle (its scope) do not.</summary>
        public static bool TakesRedDot(WeaponClass c) => c == WeaponClass.SubMachineGun || c == WeaponClass.Rifle || c == WeaponClass.Shotgun;
'@
Rep '            public bool CanSilence => TakesSilencer(weaponClass);' @'
            public bool CanSilence => TakesSilencer(weaponClass);
            /// <summary>Does this gun carry a red dot sight? Sub machine guns, rifles and shotguns do (<see cref="RangedWeapon.TakesRedDot"/>).</summary>
            public bool CanSight => TakesRedDot(weaponClass);
'@
Rep '            public Quaternion silencerRot;' @'
            public Quaternion silencerRot;
            /// <summary>The red dot sight: this gun carries one, where its origin goes and how it is turned there (measured by the editor).</summary>
            public bool hasSight;
            public Vector3 sightAt;
            public Quaternion sightRot;
'@
Rep '            layouts[(int)k] = l;
            return l;' @'
            if (spec.CanSight && cat.redDot && gm.sightAt != Vector3.zero)
            {
                l.hasSight = true;
                l.sightAt = turn * gm.sightAt;
                l.sightRot = turn * Quaternion.Euler(gm.sightTurn);
            }
            layouts[(int)k] = l;
            return l;
'@
Rep '        GunMagazine magazine;' @'
        static Material dotMat;
        static Bounds redDotBox; static bool redDotMeasured, redDotHasBox;

        /// <summary>The red dot sight on top of the gun (the owner, 2026-10-20): a child of <paramref name="parent"/> called "RedDot", scaled like the gun, with its little red dot lit in the window. <paramref name="origin"/> and <paramref name="frame"/> are as for the silencer.</summary>
        static GameObject AttachSight(Transform parent, Vector3 origin, Quaternion frame, Layout lay)
        {
            var go = Object.Instantiate(Catalog.redDot, parent, false);
            StripColliders(go);
            go.name = "RedDot";
            go.transform.localPosition = origin + frame * (lay.sightAt * lay.scale);
            go.transform.localRotation = frame * lay.sightRot;
            go.transform.localScale = Vector3.one * lay.scale;
            AddDot(go.transform);
            return go;
        }

        /// <summary>The red dot itself: a small sphere that glows red whatever the light, in the middle of the sight's box (the model's own space, before it is scaled).</summary>
        static void AddDot(Transform sight)
        {
            if (!redDotMeasured) { redDotMeasured = true; redDotHasBox = WeaponCatalog.Measure(Catalog.redDot, Quaternion.identity, null, out redDotBox); }
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Dot";
            var col = dot.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            dot.transform.SetParent(sight, false);
            dot.transform.localPosition = redDotHasBox ? new Vector3(redDotBox.center.x, redDotBox.center.y + 0.10f * redDotBox.size.y, redDotBox.center.z) : new Vector3(0f, 0.04f, 0f);
            dot.transform.localScale = Vector3.one * 0.008f;
            var r = dot.GetComponent<Renderer>();
            if (!dotMat)
            {
                var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");
                dotMat = new Material(sh) { name = "RedDot", color = Color.black };
                dotMat.EnableKeyword("_EMISSION");
                dotMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                if (dotMat.HasProperty("_EmissionColor")) dotMat.SetColor("_EmissionColor", new Color(3.2f, 0.12f, 0.08f));
                else dotMat.color = new Color(1f, 0.1f, 0.06f);
            }
            r.sharedMaterial = dotMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        GunMagazine magazine;
'@
Rep '        static void ResetStatics() { layouts = new Layout[Specs.Length]; magMeasured = new bool[Specs.Length]; hasMag = new bool[Specs.Length]; magBounds = new Bounds[Specs.Length]; }' '        static void ResetStatics() { layouts = new Layout[Specs.Length]; magMeasured = new bool[Specs.Length]; hasMag = new bool[Specs.Length]; magBounds = new Bounds[Specs.Length]; redDotMeasured = false; dotMat = null; }'
Rep '            if (!spec.CanSilence) return root;                                                // (a shotgun or a sniper rifle never has a silencer on it)' @'
            if (lay.hasSight) AttachSight(root.transform, model.transform.localPosition, lying, lay);          // (the red dot sight: the SMG, the rifle and the shotgun carry one)
            if (!spec.CanSilence) return root;                                                // (a shotgun or a sniper rifle never has a silencer on it)
'@
Rep '            // the empty case leaves the slide''s ejection port, not the muzzle (with a silencer on, the muzzle is the far end of the gun)' @'
            if (lay.hasSight) AttachSight(grip, model.transform.localPosition, turn, lay);                   // (the red dot sight on top)
            // the empty case leaves the slide''s ejection port, not the muzzle (with a silencer on, the muzzle is the far end of the gun)
'@
Rep '            thickness = lay.bounds.size.z * lay.scale;' @'
            if (lay.hasSight) AttachSight(root.transform, model.transform.localPosition, lying, lay);
            thickness = lay.bounds.size.z * lay.scale;
'@
Save-Src

# ---------------------------------------------------------------- the sound of a gun with a silencer
Open-Src "$A\Guns\GunSounds.cs"
Rep '            click = null;' @'
            click = null;
            silencedHand = silencedRifle = null; silencedTail = null; SilencedShotsPlayed = 0;
'@
Rep '        public static int ShotsPlayed, TailsPlayed, ReloadStepsPlayed, DryClicksPlayed, EquipsPlayed, AimsPlayed;' @'
        public static int ShotsPlayed, TailsPlayed, ReloadStepsPlayed, DryClicksPlayed, EquipsPlayed, AimsPlayed;
        /// <summary>How many of the shots played were the suppressed ones of a gun with a silencer on (for the tests).</summary>
        public static int SilencedShotsPlayed;
'@
Rep '            foreach (var set in new[] { c.gunShot, c.gunTail' @'
            SilencedShots(RangedWeapon.SoundSet.Handgun); SilencedShots(RangedWeapon.SoundSet.Rifle); SilencedTail();            // (made now, not at the first shot)
            foreach (var set in new[] { c.gunShot, c.gunTail
'@
Rep '            var shots = Shots(c, set);
            if (!Has(shots)) return false;
            int i = Random.Range(0, shots.Length);
            float p = Random.Range(0.97f, 1.03f) * pitch;
            Play(shots[i], pos, silenced ? SilencedShotVolume : 1f, silenced ? p * 1.12f : p, own);        // (a silenced shot: a dull, quiet pop)
            ShotsPlayed++;
            var tails = Tails(c, set);
            if (Has(tails)) { Play(tails[i % tails.Length], pos, silenced ? SilencedTailVolume : 1f, 1f, own); TailsPlayed++; }
            return true;' @'
            if (silenced)
            {
                // a gun with a silencer on (the owner, 2026-10-20: "the weapons should sound silenced"): the suppressed shot made here - a dull pap with its thump and the action working - and a soft room under it, not the loud recording turned down
                var quiet = SilencedShots(set);
                Play(quiet[Random.Range(0, quiet.Length)], pos, SilencedShotVolume, Random.Range(0.97f, 1.03f) * pitch, own);
                ShotsPlayed++; SilencedShotsPlayed++;
                Play(SilencedTail(), pos, SilencedTailVolume, 1f, own); TailsPlayed++;
                return true;
            }
            var shots = Shots(c, set);
            if (!Has(shots)) return false;
            int i = Random.Range(0, shots.Length);
            float p = Random.Range(0.97f, 1.03f) * pitch;
            Play(shots[i], pos, 1f, p, own);
            ShotsPlayed++;
            var tails = Tails(c, set);
            if (Has(tails)) { Play(tails[i % tails.Length], pos, 1f, 1f, own); TailsPlayed++; }
            return true;
'@
Rep '        public const float SilencedShotVolume = 0.35f, SilencedTailVolume = 0.2f;' @'
        public const float SilencedShotVolume = 0.5f, SilencedTailVolume = 0.2f;

        /// <summary>Does a gun of this class sound silenced (the owner, 2026-10-20: "the weapons should sound silenced other than the shotgun, sniper, and rocket launcher")? The same rule as the silencer itself (<see cref="RangedWeapon.TakesSilencer"/>): the pistol, the sub machine gun and the rifle; the shotgun, the sniper rifle and a rocket launcher (when there is one) never.</summary>
        public static bool SoundsSilenced(RangedWeapon.WeaponClass c) => RangedWeapon.TakesSilencer(c);

        // ------------------------------------------------------------------ the shot of a gun with a silencer (made from maths: the pack has none)

        static AudioClip[] silencedHand, silencedRifle;
        static AudioClip silencedTail;
        const int SilencedVariants = 3;

        /// <summary>The suppressed shots of a gun with a silencer on: the handgun's (the pew, the sub machine gun) or the rifle's, a few of each so that a burst does not repeat itself. Made on first use.</summary>
        public static AudioClip[] SilencedShots(RangedWeapon.SoundSet set)
        {
            bool rifle = set == RangedWeapon.SoundSet.Rifle;
            var have = rifle ? silencedRifle : silencedHand;
            if (have != null && have.Length == SilencedVariants && have[0]) return have;
            have = new AudioClip[SilencedVariants];
            for (int v = 0; v < have.Length; v++) have[v] = MakeSilencedShot(rifle, v);
            if (rifle) silencedRifle = have; else silencedHand = have;
            return have;
        }

        /// <summary>The soft room that is left of a silenced shot: a low rumble that is gone in a quarter of a second.</summary>
        public static AudioClip SilencedTail()
        {
            if (silencedTail) return silencedTail;
            var r = new SynthRandom(7777);
            int n = Dsp.Samples(0.30f);
            var d = new float[n];
            var lp = new Svf(); lp.Set(850f, 0.7f);
            float env = 1f, dec = Dsp.DecayPerSample(0.075f);
            int ramp = Dsp.Samples(0.012f);
            for (int i = 0; i < n; i++)
            {
                d[i] = lp.Low(r.Signed()) * env * (i < ramp ? i / (float)ramp : 1f);
                env *= dec;
            }
            Dsp.FadeEnds(d, 0.02f);
            Dsp.Normalize(d, 0.5f);
            silencedTail = AudioClip.Create("gun silenced tail", n, 1, Dsp.SampleRate, false);
            silencedTail.SetData(d, 0);
            silencedTail.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return silencedTail;
        }

        /// <summary>
        /// One suppressed shot: the muzzle's "pap" (noise through a band pass, gone in a few hundredths of a second), the gas' low thump (a sine falling away) and, a moment later, the slide or the bolt working - the click a silenced gun is known by.
        /// A handgun's is higher and snappier, a rifle's has more body and a longer thump.
        /// </summary>
        static AudioClip MakeSilencedShot(bool rifle, int variant)
        {
            var r = new SynthRandom((rifle ? 7300 : 7100) + variant * 37);
            int n = Dsp.Samples(rifle ? 0.34f : 0.26f);
            var d = new float[n];
            var pap = new Svf(); pap.Set((rifle ? 1700f : 2500f) * r.Range(0.92f, 1.08f), 0.85f);
            var body = new Svf(); body.Set((rifle ? 480f : 640f) * r.Range(0.92f, 1.08f), 0.7f);
            float envPap = 1f, decPap = Dsp.DecayPerSample(rifle ? 0.032f : 0.021f);
            float envBody = 1f, decBody = Dsp.DecayPerSample(rifle ? 0.085f : 0.055f);
            float phase = 0f;
            float attack = Dsp.Samples(0.0007f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Dsp.SampleRate;
                float noise = r.Signed();
                float a = pap.Band(noise) * envPap * 2.4f;
                float b = body.Low(noise) * envBody * 1.3f;
                float hz = Mathf.Lerp(rifle ? 130f : 160f, rifle ? 62f : 76f, Mathf.Min(1f, t / 0.05f));
                phase += hz / Dsp.SampleRate; phase -= Mathf.Floor(phase);
                float thump = Dsp.Sine(phase) * Mathf.Exp(-t / (rifle ? 0.045f : 0.032f)) * 0.7f;
                d[i] = (a + b + thump) * Mathf.Min(1f, (i + 1) / attack);
                envPap *= decPap; envBody *= decBody;
            }
            // the action working: the slide (or the bolt) goes back and forward a moment after the shot
            Tick(d, Dsp.Samples(rifle ? 0.040f : 0.030f), r, 3200f * r.Range(0.9f, 1.1f), 0.0030f, 0.55f);
            Tick(d, Dsp.Samples(rifle ? 0.070f : 0.052f), r, 1700f * r.Range(0.9f, 1.1f), 0.0045f, 0.42f);
            int fade = Dsp.Samples(0.02f);                                  // (only the end is faded: the shot begins at once)
            for (int i = 0; i < fade && i < n; i++) d[n - 1 - i] *= i / (float)fade;
            Dsp.Normalize(d, 0.9f);
            var clip = AudioClip.Create(rifle ? "rifle shot silenced" : "handgun shot silenced", n, 1, Dsp.SampleRate, false);
            clip.SetData(d, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src "$E\NetDev\NetDevR7Test.cs"
Rep '                && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Shotgun }, true) && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Sniper }, true));' @'
                && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Shotgun }, true) && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Sniper }, true));
            bool madeShots;
            try
            {
                var hand = GunSounds.SilencedShots(RangedWeapon.SoundSet.Handgun); var rifleShots = GunSounds.SilencedShots(RangedWeapon.SoundSet.Rifle);
                madeShots = hand.Length >= 3 && rifleShots.Length >= 3 && hand.All(c => c && c.length > 0.15f && c.length < 0.45f) && rifleShots.All(c => c && c.length > 0.15f && c.length < 0.45f) && GunSounds.SilencedTail() != null && GunSounds.SilencedTail().length < 0.5f;
            }
            catch (System.Exception e) { madeShots = false; Debug.Log("[NetTest] silenced shot clips could not be made: " + e.Message); }
            Check("silencer: a gun with a silencer SOUNDS silenced (the owner, 2026-10-20): its shot is a suppressed pop made here (a few variants of the handgun's and of the rifle's, 0.15 to 0.45 s, with a soft tail), not the loud recording turned down", madeShots);
            Check("silencer: the sound follows the silencer rule - the pistol, the sub machine gun and the rifle sound silenced; the shotgun and the sniper rifle (and a rocket launcher) never do",
                GunSounds.SoundsSilenced(RangedWeapon.WeaponClass.Pistol) && GunSounds.SoundsSilenced(RangedWeapon.WeaponClass.SubMachineGun) && GunSounds.SoundsSilenced(RangedWeapon.WeaponClass.Rifle)
                && !GunSounds.SoundsSilenced(RangedWeapon.WeaponClass.Shotgun) && !GunSounds.SoundsSilenced(RangedWeapon.WeaponClass.Sniper));
            Check("red dot sights: the sub machine gun, the rifle and the shotgun carry one; the pistol and the sniper rifle do not (the owner, 2026-10-20: \"Please add red dot sights for the machine guns and shotgun\")",
                RangedWeapon.TakesRedDot(RangedWeapon.WeaponClass.SubMachineGun) && RangedWeapon.TakesRedDot(RangedWeapon.WeaponClass.Rifle) && RangedWeapon.TakesRedDot(RangedWeapon.WeaponClass.Shotgun)
                && !RangedWeapon.TakesRedDot(RangedWeapon.WeaponClass.Pistol) && !RangedWeapon.TakesRedDot(RangedWeapon.WeaponClass.Sniper)
                && RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg].CanSight && RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].CanSight && RangedWeapon.Specs[(int)RangedWeapon.Kind.Shotgun].CanSight
                && !RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].CanSight && !RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].CanSight);
'@
Save-Src

Open-Src "$E\NetDev\NetDevGuns.cs"
Rep '            var ac = GameAudio.Catalog;' @'
            GunCheck("catalog: the sniper rifle is the Barrett .50 cal of the pack, SM_Wep_Sniper_Heavy_01 (the owner, 2026-10-20)", RangedWeapon.Has(RangedWeapon.Kind.Sniper) && RangedWeapon.PrefabOf(RangedWeapon.Kind.Sniper).name == "SM_Wep_Sniper_Heavy_01");
            GunCheck("catalog: the red dot sight is in the weapon catalog, with a place on each gun that carries one (the SMG, the rifle, the shotgun) and none on the others",
                RangedWeapon.Catalog.redDot != null && RangedWeapon.Catalog.Gun((int)RangedWeapon.Kind.Smg).sightAt != Vector3.zero && RangedWeapon.Catalog.Gun((int)RangedWeapon.Kind.Rifle).sightAt != Vector3.zero && RangedWeapon.Catalog.Gun((int)RangedWeapon.Kind.Shotgun).sightAt != Vector3.zero
                && RangedWeapon.Catalog.Gun((int)RangedWeapon.Kind.Sniper).sightAt == Vector3.zero && RangedWeapon.Catalog.Gun((int)RangedWeapon.Kind.Pew).sightAt == Vector3.zero);
            var ac = GameAudio.Catalog;
'@
Rep 'shots0 = GunSounds.ShotsPlayed, sf0 = gc.ShotsFired;' 'shots0 = GunSounds.ShotsPlayed, sf0 = gc.ShotsFired, silenced0 = GunSounds.SilencedShotsPlayed;'
Rep '                else GunCheck($"{tag} the gun lying on the floor has no silencer either", item.transform.Find("Silencer") == null);' @'
                else GunCheck($"{tag} the gun lying on the floor has no silencer either", item.transform.Find("Silencer") == null);
                bool wantDot = RangedWeapon.TakesRedDot(spec.weaponClass);
                var redDot = held.Grip.Find("RedDot");
                GunCheck($"{tag} {(wantDot ? "it has a red dot sight on top of the body with its red dot lit (the owner, 2026-10-20), on the gun in the hand and on the gun lying on the floor" : "it has no red dot sight")}",
                    (redDot != null) == wantDot && (item.transform.Find("RedDot") != null) == wantDot && (!wantDot || (redDot.Find("Dot") != null && Vector3.Dot(redDot.position - held.Grip.position, held.Grip.up) > 0f)));
'@
Rep '                GunCheck($"{tag} the guards hear it from {gun.NoiseRadius} m", noiseHeard.Any(n => n.Source == player.gameObject && Mathf.Approximately(n.Loudness, gun.NoiseRadius)));' @'
                GunCheck($"{tag} {(should ? "the shot is the suppressed one made for guns with a silencer on (the owner, 2026-10-20)" : "the shot is the loud recording: this gun is never silenced")}", GunSounds.SilencedShotsPlayed - silenced0 == (should ? 1 : 0), $"(silenced shots {GunSounds.SilencedShotsPlayed - silenced0})");
                GunCheck($"{tag} the guards hear it from {gun.NoiseRadius} m", noiseHeard.Any(n => n.Source == player.gameObject && Mathf.Approximately(n.Loudness, gun.NoiseRadius)));
'@
Save-Src
Write-Output 'round 12 batch G46 patched'

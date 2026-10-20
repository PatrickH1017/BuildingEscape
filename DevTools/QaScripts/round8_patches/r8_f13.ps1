# Round 8, batch F13 (the owner, 2026-10-18, three more messages about the silencer):
#  - "shotguns and snipers should not be silenced either" and "Silencers should be on pistols, sub machines, and rifles": the rule is a property of the weapon CLASS (RangedWeapon.WeaponClass / TakesSilencer),
#    so a gun added later (the game has only the pew today) gets it right without anybody remembering: the pew is a pistol, so it takes one; a shotgun or a sniper rifle never is silenced, whatever is asked.
#  - his photographs of suppressed rifles "display where the silencer should be on the weapons ... the last point the bullet should exit from is the silencer (basically making it an extension of the existing barrel
#    visually)": the silencer is on the end of the barrel, in line with it (batch F12 did that), the shot's last point is its front face (the Muzzle transform), and now the empty case is thrown out of the slide's
#    ejection port (a "Port" transform on the held gun) instead of from just behind the muzzle - which, with a silencer on, would have been its far end.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

# ================================================================ RangedWeapon: the weapon class decides whether a silencer fits; the ejection port
Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
        public enum Kind { Pew }
'@ @'
        public enum Kind { Pew }

        /// <summary>What kind of gun a weapon is (the owner, 2026-10-18: "Silencers should be on pistols, sub machines, and rifles" and "shotguns and snipers should not be silenced either").</summary>
        public enum WeaponClass { Pistol, SubMachineGun, Rifle, Shotgun, Sniper }

        /// <summary>The rule for silencers: a pistol, a sub machine gun and a rifle can take one; a shotgun and a sniper rifle never can - their shots stay loud.</summary>
        public static bool TakesSilencer(WeaponClass c) => c == WeaponClass.Pistol || c == WeaponClass.SubMachineGun || c == WeaponClass.Rifle;

        /// <summary>Is a gun silenced: only when a silencer was put on it and one fits it, whatever is asked.</summary>
        public static bool IsSilenced(Spec spec, bool silencerPutOn) => silencerPutOn && spec.CanSilence;
'@
Rep @'
            public float silencedNoise, silencerLength;
            public int magazine;
'@ @'
            public float silencedNoise, silencerLength;
            /// <summary>What kind of gun it is: it decides whether a silencer fits (<see cref="RangedWeapon.TakesSilencer"/>).</summary>
            public WeaponClass weaponClass;
            /// <summary>Does a silencer fit this gun? Pistols, sub machine guns and rifles take one; shotguns and sniper rifles never do.</summary>
            public bool CanSilence => TakesSilencer(weaponClass);
            public int magazine;
'@
Rep @'
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f, silencedNoise = 12f, silencerLength = 0.16f,
'@ @'
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f, silencedNoise = 12f, silencerLength = 0.16f, weaponClass = WeaponClass.Pistol,
'@
Rep @'
        public bool silenced = true;
'@ @'
        public bool silenced
        {
            get => IsSilenced(Data, silencerOn);
            set => silencerOn = value;
        }
        bool silencerOn = true;
'@
Rep @'
            /// <summary>The front of the barrel (where a shot of the bare gun leaves) and the middle of the grip.</summary>
            public Vector3 muzzle, grip;
'@ @'
            /// <summary>The front of the barrel (where a shot of the bare gun leaves), the middle of the grip, and the ejection port on the right of the slide (where the empty case is thrown out).</summary>
            public Vector3 muzzle, grip, port;
'@
Rep @'
            l.grip = new Vector3(b.min.x + 0.26f * b.size.x, b.center.y - 0.18f * b.size.y, b.center.z);
            l.fullBounds = b;
            if (cat.silencer && cat.pewMuzzleFace != Vector3.zero && WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sil))
'@ @'
            l.grip = new Vector3(b.min.x + 0.26f * b.size.x, b.center.y - 0.18f * b.size.y, b.center.z);
            l.port = new Vector3(sb.center.x - 0.05f * sb.size.x, sb.center.y + 0.1f * sb.size.y, sb.min.z);       // (the case leaves the slide's right side, a little behind its middle; the right of the gun is the game frame's -z)
            l.fullBounds = b;
            if (spec.CanSilence && cat.silencer && cat.pewMuzzleFace != Vector3.zero && WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sil))
'@
Rep @'
            if (lay.hasSilencer) AttachSilencer(root.transform, model.transform.localPosition, lying, lay);
'@ @'
            if (!spec.CanSilence) return root;                                                // (a shotgun or a sniper rifle never has a silencer on it)
            if (lay.hasSilencer) AttachSilencer(root.transform, model.transform.localPosition, lying, lay);
'@
Rep @'
            var packTurn = Quaternion.Euler(Catalog.pewTurn);

            var root = new GameObject(spec.name + "_Held");
'@ @'
            var packTurn = Quaternion.Euler(Catalog.pewTurn);
            silenced = IsSilenced(spec, silenced);                                             // (only a gun a silencer fits can wear one)

            var root = new GameObject(spec.name + "_Held");
'@
Rep @'
            muzzle = m;
            return root;
'@ @'
            // the empty case leaves the slide's ejection port, not the muzzle (with a silencer on, the muzzle is the far end of the gun)
            var port = new GameObject("Port").transform;
            port.SetParent(grip, false);
            port.localPosition = model.transform.localPosition + turn * (lay.port * lay.scale);
            port.localRotation = Quaternion.identity;
            muzzle = m;
            return root;
'@
Save-Src

# ================================================================ HeldGun: the port; GunFx: the case starts there; GunController passes it
Open-Src "$Src\Guns\HeldGun.cs"
Rep @'
        public Transform Muzzle { get; private set; }
'@ @'
        public Transform Muzzle { get; private set; }
        /// <summary>Where the empty case leaves the gun: the ejection port on the right of the slide (null when the model has none).</summary>
        public Transform Port { get; private set; }
        public Vector3? PortPosition => Port ? Port.position : (Vector3?)null;
'@
Rep @'
            held.Muzzle = muzzle;
'@ @'
            held.Muzzle = muzzle;
            held.Port = grip.Find("Port");
'@
Save-Src

Open-Src "$Src\Guns\GunFx.cs"
Rep @'
        public void Shot(Vector3 muzzle, Vector3 end, bool impact, Vector3 right, bool silenced = false)
'@ @'
        public void Shot(Vector3 muzzle, Vector3 end, bool impact, Vector3 right, bool silenced = false, Vector3? casingFrom = null)
'@
Rep @'
        /// (<paramref name="impact"/>), and a case thrown out to the right of the gun.
'@ @'
        /// (<paramref name="impact"/>), and a case thrown out to the right of the gun - from <paramref name="casingFrom"/>, the slide's ejection port, when the gun has one (with a silencer on, the muzzle is the far end of it).
'@
Rep @'
                c.t.position = muzzle - (end - muzzle).normalized * 0.05f;
'@ @'
                c.t.position = casingFrom ?? (muzzle - (end - muzzle).normalized * 0.05f);
'@
Save-Src

Open-Src "$Src\Guns\GunController.cs"
Rep @'
            GunFx.Get().Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced);
'@ @'
            GunFx.Get().Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null);
'@
Rep @'
            GunFx.Get().Shot(muzzle, imp.point, imp.hit, held ? held.Grip.right : transform.right, gun.silenced);
'@ @'
            GunFx.Get().Shot(muzzle, imp.point, imp.hit, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null);
'@
Save-Src

# ================================================================ tests: the rule, the silencer on the barrel (from the models' own vertices), the shot's last point, the case's port; the look scenario shows the silencer
Open-Src "$Ed\NetDev\NetDevR7Test.cs"
Rep @'
            Check("silencer: the shot and its tail are quieter (the flash is smaller, in GunFx)", GunSounds.SilencedShotVolume < 0.6f && GunSounds.SilencedTailVolume < GunSounds.SilencedShotVolume);
'@ @'
            Check("silencer: the shot and its tail are quieter (the flash is smaller, in GunFx)", GunSounds.SilencedShotVolume < 0.6f && GunSounds.SilencedTailVolume < GunSounds.SilencedShotVolume);
            Check("silencer: pistols, sub machine guns and rifles take one; shotguns and sniper rifles never do and are never silenced, whatever is asked (the owner, 2026-10-18)",
                RangedWeapon.TakesSilencer(RangedWeapon.WeaponClass.Pistol) && RangedWeapon.TakesSilencer(RangedWeapon.WeaponClass.SubMachineGun) && RangedWeapon.TakesSilencer(RangedWeapon.WeaponClass.Rifle)
                && !RangedWeapon.TakesSilencer(RangedWeapon.WeaponClass.Shotgun) && !RangedWeapon.TakesSilencer(RangedWeapon.WeaponClass.Sniper)
                && pewSpec.weaponClass == RangedWeapon.WeaponClass.Pistol && pewSpec.CanSilence && RangedWeapon.IsSilenced(pewSpec, true) && !RangedWeapon.IsSilenced(pewSpec, false)
                && RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.SubMachineGun }, true) && RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Rifle }, true)
                && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Shotgun }, true) && !RangedWeapon.IsSilenced(new RangedWeapon.Spec { weaponClass = RangedWeapon.WeaponClass.Sniper }, true));
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevPewLook.cs"
Rep @'
                var held = anims[i] ? HeldGun.Attach(anims[i], RangedWeapon.Kind.Pew) : null;
'@ @'
                var held = anims[i] ? HeldGun.Attach(anims[i], RangedWeapon.Kind.Pew, true) : null;      // (silenced, like the player's own)
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevPew.cs"
Rep @'
        IEnumerator PewScenario()
'@ @'
        // ---- where the silencer sits (the owner, 2026-10-18: "make sure the silencer is on the exit barrel of the pew", with photographs of suppressed rifles: it is an extension of the barrel, and the bullet's last point is its front)
        // Measured on the live objects from the models' own vertices (read here, in the editor) and not from the numbers the weapon catalog stores, so that a wrong stored number is caught.
        static float silencerFrontZ = float.NaN, silencerSeatZ = float.NaN;

        static void ReadSilencerMesh()
        {
            if (!float.IsNaN(silencerFrontZ)) return;
            float front = float.MinValue, seat = float.MaxValue;
            foreach (var v in RangedWeapon.Catalog.silencer.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                front = Mathf.Max(front, v.z);
                if (new Vector2(v.x, v.y).sqrMagnitude < 2.5e-7f && v.z < seat) seat = v.z;      // a vertex on the axis: the bottom of a recess
            }
            silencerFrontZ = front; silencerSeatZ = seat;
        }

        /// <summary>The middle of the silencer's front face, in the world: the last point of the barrel.</summary>
        static Vector3 SilencerFront(Transform silencer)
        {
            ReadSilencerMesh();
            return silencer.TransformPoint(new Vector3(0f, 0f, silencerFrontZ));
        }

        /// <summary>Is the silencer on the barrel: its rear recess over the end of the barrel (the front-most ring of the pistol's body mesh), its length axis along the barrel, and - given a muzzle - the muzzle on its front face.</summary>
        static bool SilencerOnBarrel(Transform model, Transform silencer, Transform muzzle, out string why)
        {
            why = "(no model or silencer)";
            if (!model || !silencer) return false;
            ReadSilencerMesh();
            var vs = RangedWeapon.Catalog.pew.GetComponent<MeshFilter>().sharedMesh.vertices;
            float front = float.MinValue;
            foreach (var v in vs) front = Mathf.Max(front, v.z);
            Bounds ring = default; bool any = false;
            foreach (var v in vs)
            {
                if (v.z < front - 0.0005f) continue;
                if (!any) { ring = new Bounds(v, Vector3.zero); any = true; } else ring.Encapsulate(v);
            }
            var tip = model.TransformPoint(new Vector3(ring.center.x, ring.center.y, front));
            var seat = silencer.TransformPoint(new Vector3(0f, 0f, silencerSeatZ));
            float off = Vector3.Distance(tip, seat);
            float angle = Vector3.Angle(silencer.TransformDirection(Vector3.forward), model.TransformDirection(Vector3.forward));
            float muzzleOff = muzzle ? Vector3.Distance(muzzle.position, SilencerFront(silencer)) : 0f;
            why = $"(end of the barrel to the bottom of the recess {off * 1000f:0.0} mm, axis off by {angle:0.00} degrees{(muzzle ? $", the muzzle {muzzleOff * 1000f:0.0} mm from the silencer's front face" : "")})";
            return any && off < 0.002f && angle < 0.5f && muzzleOff < 0.003f;
        }

        IEnumerator PewScenario()
'@
Rep @'
            PewCheck("the pew lying on the floor has the silencer on too", pewItem != null && pewItem.transform.Find("Silencer") != null);
'@ @'
            PewCheck("the pew lying on the floor has the silencer on too", pewItem != null && pewItem.transform.Find("Silencer") != null);
            {
                bool onBarrel = false; string whyBarrel = "";
                try { onBarrel = SilencerOnBarrel(gc.Held.Grip.Find("Pew_Model"), gc.Held.Grip.Find("Silencer"), gc.Held.Muzzle, out whyBarrel); } catch (System.Exception e) { whyBarrel = "(" + e.Message + ")"; }
                PewCheck("the silencer is an extension of the barrel: its recess is over the end of the barrel, on the barrel's own line, and the muzzle is the middle of its front face (the owner, 2026-10-18)", onBarrel, whyBarrel);
                bool lyingOk = false; string whyLying = "";
                try { lyingOk = SilencerOnBarrel(pewItem.transform.Find("Pew_Model"), pewItem.transform.Find("Silencer"), null, out whyLying); } catch (System.Exception e) { whyLying = "(" + e.Message + ")"; }
                PewCheck("the same on the pew lying on the floor", lyingOk, whyLying);
                var port = gc.Held.Port;
                PewCheck("the empty case leaves the slide's ejection port: near the grip and far behind the muzzle (with the silencer on, the muzzle is the far end of the gun)",
                    port != null && Vector3.Distance(port.position, gc.Held.Grip.position) < 0.1f && Vector3.Distance(port.position, gc.Held.Muzzle.position) > 0.25f,
                    port ? $"(port {Vector3.Distance(port.position, gc.Held.Grip.position):0.000} m from the grip, {Vector3.Distance(port.position, gc.Held.Muzzle.position):0.000} m from the muzzle)" : "(no port)");
            }
'@
Rep @'
                bool fired1 = gc.Fire();
'@ @'
                bool fired1 = gc.Fire();
                var silencerFrontNow = SilencerFront(gc.Held.Grip.Find("Silencer"));            // (this frame: the arm has not kicked yet)
'@
Rep @'
                PewCheck("the first shot leaves the gun", fired1 && pewGun.Magazine.Rounds == 7);
'@ @'
                PewCheck("the first shot leaves the gun", fired1 && pewGun.Magazine.Rounds == 7);
                PewCheck("the bullet starts at the front of the silencer: the last point of the barrel (the owner, 2026-10-18)", fired1 && Vector3.Distance(gc.LastMuzzle, silencerFrontNow) < 0.01f,
                    $"(bullet from {gc.LastMuzzle}, silencer front {silencerFrontNow}, {Vector3.Distance(gc.LastMuzzle, silencerFrontNow) * 1000f:0.0} mm apart)");
'@
Save-Src
Write-Output 'round 8 batch F13 (silencer by weapon class, shot and case exits, tests) patched'

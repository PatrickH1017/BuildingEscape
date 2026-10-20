# Round 8, batch F14: the Synty meshes cannot be read in Play mode (isReadable is false: "Not allowed to access vertices on mesh ..."), so the pew test must not read vertices.
# The test now uses the models' boxes (Renderer.bounds, what a build can know too) and the editor-measured numbers in the catalog:
#  - the placement checks apply the catalog's barrel end / recess seat to the LIVE objects (the held gun and the lying item), so a wrong frame, turn or scale in LayoutOf / AttachSilencer / BuildHeld is caught;
#  - one new check says the catalog's numbers agree with the models' boxes (the barrel's end is the front of the pistol, on the slide's upper half and centred; the silencer's recess is a few millimetres deep);
#  - the ejection port is "near the grip" within 16 cm (the palm sits on the grip below the slide: 11 cm from the port).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
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

Open-Src "$Ed\NetDev\NetDevPew.cs"
RepBetween '        static float silencerFrontZ = float.NaN, silencerSeatZ = float.NaN;' '        IEnumerator PewScenario()' @'
        static float silencerFrontZ = float.NaN;

        /// <summary>The silencer's own z of its front face, from its box (a build cannot read a mesh's vertices, and neither can a test in Play mode).</summary>
        static float SilencerFrontLocalZ()
        {
            if (float.IsNaN(silencerFrontZ) && WeaponCatalog.Measure(RangedWeapon.Catalog.silencer, Quaternion.identity, null, out var b)) silencerFrontZ = b.max.z;
            return silencerFrontZ;
        }

        /// <summary>The middle of the silencer's front face, in the world: the last point of the barrel.</summary>
        static Vector3 SilencerFront(Transform silencer) => silencer.TransformPoint(new Vector3(0f, 0f, SilencerFrontLocalZ()));

        /// <summary>The catalog's numbers agree with the models' boxes: the barrel's end is the front of the pistol, centred and on the slide's upper half; the silencer's rear recess is a few millimetres deep and its axis is the barrel's.</summary>
        static bool CatalogMeasurementsAgree(out string why)
        {
            var cat = RangedWeapon.Catalog;
            why = "(no models)";
            if (!WeaponCatalog.Measure(cat.pew, Quaternion.identity, null, out var pb) || !WeaponCatalog.Measure(cat.silencer, Quaternion.identity, null, out var sb)) return false;
            var face = cat.pewMuzzleFace;
            float seat = face.z - cat.silencerAt.z;                 // the silencer's own z of the bottom of its rear recess
            bool atFront = Mathf.Abs(face.z - pb.max.z) < 0.002f;
            bool centred = Mathf.Abs(face.x) < 0.002f && face.y > pb.center.y && face.y < pb.max.y;
            bool recess = seat - sb.min.z > 0.002f && seat - sb.min.z < 0.02f;
            bool lined = Mathf.Abs(cat.silencerAt.x - face.x) < 0.001f && Mathf.Abs(cat.silencerAt.y - face.y) < 0.001f && cat.silencerTurn == Vector3.zero;
            why = $"(barrel end z {face.z:0.0000} against the pistol's front {pb.max.z:0.0000}; its line ({face.x:0.0000}, {face.y:0.0000}); the recess is {(seat - sb.min.z) * 1000f:0.0} mm deep)";
            return atFront && centred && recess && lined;
        }

        /// <summary>Is the silencer on the barrel: its rear recess over the end of the barrel (the catalog's measured muzzle face, on the gun as it is placed), its length axis along the barrel, and - given a muzzle - the muzzle on its front face.</summary>
        static bool SilencerOnBarrel(Transform model, Transform silencer, Transform muzzle, out string why)
        {
            why = "(no model or silencer)";
            if (!model || !silencer) return false;
            var cat = RangedWeapon.Catalog;
            var tip = model.TransformPoint(cat.pewMuzzleFace);                                                  // the middle of the barrel's end
            var seat = silencer.TransformPoint(new Vector3(0f, 0f, cat.pewMuzzleFace.z - cat.silencerAt.z));    // the bottom of the silencer's rear recess
            float off = Vector3.Distance(tip, seat);
            float angle = Vector3.Angle(silencer.TransformDirection(Vector3.forward), model.TransformDirection(Vector3.forward));
            float muzzleOff = muzzle ? Vector3.Distance(muzzle.position, SilencerFront(silencer)) : 0f;
            why = $"(end of the barrel to the bottom of the recess {off * 1000f:0.0} mm, axis off by {angle:0.00} degrees{(muzzle ? $", the muzzle {muzzleOff * 1000f:0.0} mm from the silencer's front face" : "")})";
            return off < 0.002f && angle < 0.5f && muzzleOff < 0.003f;
        }

'@
Rep @'
                PewCheck("the same on the pew lying on the floor", lyingOk, whyLying);
'@ @'
                PewCheck("the same on the pew lying on the floor", lyingOk, whyLying);
                bool agree = false; string whyAgree = "";
                try { agree = CatalogMeasurementsAgree(out whyAgree); } catch (System.Exception e) { whyAgree = "(" + e.Message + ")"; }
                PewCheck("the catalog's measured barrel end and silencer seat agree with the models' boxes", agree, whyAgree);
'@
Rep @'
                    port != null && Vector3.Distance(port.position, gc.Held.Grip.position) < 0.1f && Vector3.Distance(port.position, gc.Held.Muzzle.position) > 0.25f,
'@ @'
                    port != null && Vector3.Distance(port.position, gc.Held.Grip.position) < 0.16f && Vector3.Distance(port.position, gc.Held.Muzzle.position) > 0.25f,
'@
Save-Src
Write-Output 'round 8 batch F14 (pew test without mesh reads) patched'

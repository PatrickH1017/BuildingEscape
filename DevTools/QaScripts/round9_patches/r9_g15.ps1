# Round 9, batch G15 (the four guns): a long gun lying slanted across a safe is aimed at by its middle (Pickup.aimBox): the world box round a slanted gun covers the whole diagonal and hid the watch beside it from the crosshair.
#  + the reload-sound count of the Guns scenario is taken before the trigger is held down (the reload of a short magazine starts before the window ends)
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

Open-Src "$Src\Pickup.cs"
Rep @'
        /// <summary>The world box around all of this item's solid colliders (what the crosshair aims at). False if it has none.</summary>
        public bool TryGetBounds(out Bounds box)
        {
            box = default;
            bool any = false;
'@ @'
        /// <summary>
        /// When set (a long gun, which lies slanted across a safe), the crosshair aims at this box, in the item's own space, instead of the box round all its colliders: the world box round a slanted gun covers the whole
        /// diagonal of the hollow and would hide what lies beside it (the owner, 2026-10-18: every item of a safe is aimed at on its own).
        /// </summary>
        public Bounds? aimBox;

        /// <summary>The world box around all of this item's solid colliders (what the crosshair aims at), or round its <see cref="aimBox"/>. False if it has none.</summary>
        public bool TryGetBounds(out Bounds box)
        {
            box = default;
            if (aimBox.HasValue)
            {
                var ab = aimBox.Value;
                box = new Bounds(transform.TransformPoint(ab.center + new Vector3(ab.extents.x, ab.extents.y, ab.extents.z)), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    var corner = ab.center + Vector3.Scale(ab.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    box.Encapsulate(transform.TransformPoint(corner));
                }
                return true;
            }
            bool any = false;
'@
Save-Src

Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
            p.weight = spec.weight; p.label = spec.name;
            var rw = root.AddComponent<RangedWeapon>();
'@ @'
            p.weight = spec.weight; p.label = spec.name;
            if (spec.hold == HoldStyle.TwoHand) p.aimBox = new Bounds(Vector3.zero, new Vector3(Mathf.Min(size.x, 0.32f), Mathf.Max(size.z, 0.05f), Mathf.Min(size.y, 0.10f)));      // (a long gun is aimed at by its middle: the receiver, the grip)
            var rw = root.AddComponent<RangedWeapon>();
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevGuns.cs"
Rep @'
                // ---- the rate of fire: the trigger held down for a few seconds
                yield return new WaitForSeconds(spec.fireInterval + 0.1f);
'@ @'
                // ---- the rate of fire: the trigger held down for a few seconds
                int steps0 = GunSounds.ReloadStepsPlayed;                      // (the reload of a short magazine can start before the window is over)
                yield return new WaitForSeconds(spec.fireInterval + 0.1f);
'@
Rep @'
                int steps0 = GunSounds.ReloadStepsPlayed;
                yield return new WaitForSeconds(0.6f);
'@ @'
                yield return new WaitForSeconds(0.6f);
'@
Save-Src

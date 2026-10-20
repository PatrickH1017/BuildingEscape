# Round 9, batch G14 (the four guns): fixes found by the first full run.
#  - a long gun's pick-up box is as wide as its body only (not its scope and magazine): in a safe it must not cover the clip or the watch lying beside it
#  - Outpost scenario: the clip refused at three clips and taken with room; a slanted long gun in a safe; any gun can be picked up from a safe; every item that is not aimed at is named in the log
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
            bc.size = new Vector3(size.x, Mathf.Max(size.z, 0.03f), size.y);                  // (the side view's height is now along z)
'@ @'
            // (the side view's height is now along z; a long gun is picked up by its body: the box is no wider than that, not as wide as its scope and its magazine, so that in a safe it does not cover what lies beside it)
            bc.size = new Vector3(size.x, Mathf.Max(size.z, 0.03f), spec.hold == HoldStyle.TwoHand ? Mathf.Min(size.y, 0.10f) : size.y);
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevOutpost.cs"
Rep @'
                                wsBarrels &= Vector3.Dot(wsGun.transform.right, wsS.transform.forward) > 0.95f;
'@ @'
                                var wsLay = RangedWeapon.LayoutOf(wsGun.kind);
                                bool wsSlanted = wsLay.fullBounds.size.x * wsLay.scale > 0.46f;                // (a long gun lies slanted across the hollow, its barrel out through the open door)
                                wsBarrels &= Vector3.Dot(wsGun.transform.right, wsS.transform.forward) > (wsSlanted ? 0.55f : 0.95f);
'@
Rep @'
                                if (xAim.Kind == AimKind.Pickup && xAim.Pickup == xItem) wsAimed++;
'@ @'
                                if (xAim.Kind == AimKind.Pickup && xAim.Pickup == xItem) wsAimed++;
                                else Log($"[Outpost] not aimed at: {xItem.Label} at {xl} in safe {xSafe.Index}; the crosshair found {xAim.Kind} {(xAim.Pickup ? xAim.Pickup.Label : "-")}");
'@
Rep @'
                        foreach (var wsP in wsFound) if (wsP.Label == "Pew") { wsPew = wsP; break; }
                        OutpostCheck($"{wsTag} a pistol of the open safe can be picked up", wsPew != null);
'@ @'
                        foreach (var wsP in wsFound) if (wsP.GetComponent<RangedWeapon>() != null) { wsPew = wsP; break; }
                        OutpostCheck($"{wsTag} a gun of the open safe can be picked up", wsPew != null);
'@
Rep @'
                            int tReserve0 = tGun != null ? tGun.Magazine.Reserve : -1;
'@ @'
                            // (the owner, 2026-10-19: three clips at most) a pistol with its clip and two spare ones is full: a clip is refused and stays where it lies; with a spare clip used up there is room for one
                            bool tFullRefused = tGun != null && tGun.Magazine.Room == 0 && !pack.TryStore(tClip);
                            yield return null;
                            OutpostCheck($"{wsTag} with the pistol and its two spare clips (three clips) the clip is refused too and stays where it lies", tFullRefused && tClip != null && tClip.isActiveAndEnabled);
                            if (tGun != null) tGun.Magazine.SetReserve(8);
                            int tReserve0 = tGun != null ? tGun.Magazine.Reserve : -1;
'@
Save-Src

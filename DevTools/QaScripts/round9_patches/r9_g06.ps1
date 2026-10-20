# Round 9, batch G06: the GunItems pictures one gun at a time (the guns in a row hid each other from the side).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

Open-Src "$Ed\NetDev\NetDevGunSheet.cs"
Rep @'
            ShotCam(cam, "gunitems_top");
            for (int i = 0; i < guns.Count; i++)
            {
                var g = guns[i];
                var k = kinds[i];
                var spec = RangedWeapon.Specs[(int)k];
'@ @'
            ShotCam(cam, "gunitems_top");
            foreach (var g0 in guns) Destroy(g0);
            yield return null;
            for (int i = 0; i < kinds.Length; i++)
            {
                var k = kinds[i];
                if (!RangedWeapon.Has(k)) continue;
                var g = RangedWeapon.Create(k, at + new Vector3(0f, 0.03f, 0f), root.transform, 0f);       // (alone, so that nothing hides it)
                if (g.TryGetComponent(out Rigidbody rb1)) rb1.isKinematic = true;
                yield return null;
                var spec = RangedWeapon.Specs[(int)k];
'@
Rep @'
                    ShotCam(cam, $"gunitems_{k}_end");
                }
            }
'@ @'
                    ShotCam(cam, $"gunitems_{k}_end");
                }
                Destroy(g);
                yield return null;
            }
'@
Save-Src

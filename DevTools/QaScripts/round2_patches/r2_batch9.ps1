# Round 2, batch 9: a picture of the outpost computer in the Looks scenario. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep '                LookCheck($"{f} pictures taken", shots >= 5, $"({shots})");' @'
                // the outpost's computer, from where the player stands to use it, and the post-it on its monitor
                var computer = FindFirstObjectByType<ComputerProp>();
                LookCheck($"{f} the outpost has its computer", computer != null);
                if (computer != null)
                {
                    var front = computer.transform.rotation * Vector3.forward;
                    eye.transform.position = computer.transform.position + front * 0.85f + Vector3.up * 0.62f;
                    eye.transform.LookAt(computer.transform.position + Vector3.up * 0.26f + front * -0.1f);
                    yield return null;
                    ShotCam(eye, $"look{floor}_{shots++}_computer");
                }
                LookCheck($"{f} pictures taken", shots >= 5, $"({shots})");
'@
Save-Src
Write-Output 'batch 9 patched'

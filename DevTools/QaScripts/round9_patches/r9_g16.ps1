# Round 9, batch G16 (the four guns): the built-player autopilot (two built players over the loopback) fires the client's pistol over the network: the shot carries its seed, the host accepts it and shows it (the changed protocol, "21").
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

Open-Src "$Src\QaAutopilot.cs"
Rep @'
                    Check("client: the pistol is in my pack (the host agreed to my E on it)", cGun != null);
'@ @'
                    Check("client: the pistol is in my pack (the host agreed to my E on it)", cGun != null);
                    if (cGun != null)
                    {
                        // a shot over the network (the owner's guns, 2026-10-19): the shot carries its seed, the host traces the bullet, tells everybody else and the guards hear it
                        var cGc = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<GunController>() : null;
                        yield return Until(() => cGc != null && cGc.Gun == cGun, 20f, "the pistol in my hand");
                        yield return Wait(0.6f);
                        int shotsBefore = cGc != null ? cGc.ShotsFired : 0;
                        bool cFired = cGc != null && cGc.Fire();
                        Check("client: the pistol is in my hand and fires - the shot goes to the host", cFired && cGc.ShotsFired == shotsBefore + 1, $"(fired {cFired})");
                    }
'@
Rep @'
                if (hs.WatchCount > 0 && rPack != null)
                {
                    yield return Until(() => rPack.Items.Any(it => it && it.Label == "Gold Watch"), 60f, "the client's gold watch");
                    Check("host: the client's gold watch is in his pack here too, on its own", rPack.Items.Any(it => it && it.Label == "Gold Watch"));
                }
            }
        }
'@ @'
                if (hs.WatchCount > 0 && rPack != null)
                {
                    yield return Until(() => rPack.Items.Any(it => it && it.Label == "Gold Watch"), 60f, "the client's gold watch");
                    Check("host: the client's gold watch is in his pack here too, on its own", rPack.Items.Any(it => it && it.Label == "Gold Watch"));
                }
                // the client fired his pistol: the host accepted the shot and shows it from his gun
                var rGc = remote != null ? remote.GetComponent<GunController>() : null;
                yield return Until(() => RunSync.Instance != null && RunSync.Instance.GunShotsAccepted >= 1, 60f, "the client's shot");
                yield return Wait(0.5f);
                Check("host: the client's shot arrived and was accepted, and is shown here from the pistol in his hand", RunSync.Instance != null && RunSync.Instance.GunShotsAccepted >= 1 && rGc != null && rGc.ShotsSeen >= 1, $"(accepted {(RunSync.Instance != null ? RunSync.Instance.GunShotsAccepted : -1)}, shown {(rGc != null ? rGc.ShotsSeen : -1)})");
            }
        }
'@
Save-Src

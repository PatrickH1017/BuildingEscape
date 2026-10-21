# Round 12, batch G43b: the Toon line-up also takes a close-up of a Synty security officer (the guards of the game) standing dazed (the Blink stunned loop), to compare with the owner's picture.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevToon.cs'
Rep '            ToonCheck("snaps out of the daze to idle", w[0] > 0.8f && w[6] < 0.1f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");' @'
            ToonCheck("snaps out of the daze to idle", w[0] > 0.8f && w[6] < 0.1f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            // a Synty security officer (the guards of the game) standing dazed, close up
            if (byId.TryGetValue("syn_security_male_01", out var securityAnim) && securityAnim.HasStun)
            {
                var lineupAt = cam.transform.position; var lineupRot = cam.transform.rotation; float lineupFov = cam.fieldOfView;
                for (float t = 0; t < 1.0f; t += Time.deltaTime) { securityAnim.Stun(); yield return null; }
                var securityHost = securityAnim.transform.parent;
                cam.fieldOfView = 17f;
                cam.transform.position = securityHost.position + new Vector3(0.1f, 0.5f, -4.0f);
                cam.transform.LookAt(securityHost.position + new Vector3(0f, 0.15f, 0f));
                yield return null;
                ShotCam(cam, "toon_security_stunned");
                var sw = securityAnim.DebugWeights();
                ToonCheck("the Synty security officer stands dazed too (the dazed slot, not the fall)", sw[6] > 0.8f && sw[3] < 0.1f, $"(weights {string.Join(",", sw.Select(x => x.ToString("0.00")))})");
                for (float t = 0; t < 2.5f; t += Time.deltaTime) { securityAnim.Recover(); securityAnim.Locomote(false); yield return null; }
                cam.transform.position = lineupAt; cam.transform.rotation = lineupRot; cam.fieldOfView = lineupFov;
            }
'@
Save-Src
Write-Output 'round 12 batch G43b patched'

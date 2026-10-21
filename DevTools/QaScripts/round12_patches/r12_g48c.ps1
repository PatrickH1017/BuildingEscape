# Round 12, batch G48c: the first-person scenario also photographs the body from the side (a second camera) while the camera is at the eyes, to see the arms shader's cut: only the forearms and hands of the body should be there.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevFpLook.cs'
Rep '                FpLookCheck($"{kind}: nothing of the body but its arms is drawn"' @"
                if (kind == RangedWeapon.Kind.Smg || kind == RangedWeapon.Kind.Sniper)
                {
                    var side = new GameObject("FpSideCam").AddComponent<Camera>();
                    side.clearFlags = CameraClearFlags.SolidColor; side.backgroundColor = new Color(0.55f, 0.66f, 0.82f); side.enabled = false; side.fieldOfView = 55f;
                    side.transform.position = camNow.transform.position + camNow.transform.right * 1.1f + camNow.transform.forward * 0.45f + camNow.transform.up * 0.15f;
                    side.transform.LookAt(camNow.transform.position + camNow.transform.forward * 0.35f - camNow.transform.up * 0.2f);
                    ShotCam(side, ""fplook_side_"" + kind);
                    side.transform.position = camNow.transform.position + camNow.transform.right * -0.9f + camNow.transform.forward * 0.8f + camNow.transform.up * 0.2f;
                    side.transform.LookAt(camNow.transform.position + camNow.transform.forward * 0.35f - camNow.transform.up * 0.2f);
                    ShotCam(side, ""fplook_leftside_"" + kind);
                    Destroy(side.gameObject);
                }
                FpLookCheck($"{kind}: nothing of the body but its arms is drawn"
"@
Save-Src

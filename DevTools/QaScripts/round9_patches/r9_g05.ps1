# Round 9, batch G05: a picture scenario of the five guns as items (GunItems): lying in a row, each from the side, the silenced ones end-on along the barrel.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

Open-Src "$Ed\NetDev\NetDevGunSheet.cs"
Rep @'
            Log("[GunSheet] DONE");
            Quit(0);
        }
    }
}
'@ @'
            Log("[GunSheet] DONE");
            Quit(0);
        }

        /// <summary>
        /// The five guns as the game makes them (<see cref="RangedWeapon.Create"/>), lying on the ground in a row: from above, each from the side, and the ones that take a silencer end-on along the barrel (the silencer must sit on the
        /// barrel's own line, in the middle). Needs graphics: <c>ui.ps1 -Methods GunItems</c>. Pictures: %TEMP%\be_shot_gunitems_*.png.
        /// </summary>
        IEnumerator GunItemsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("GunItems");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(8f, 0.1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.42f, 0.44f, 0.48f) };
            var camGo = new GameObject("GunItemsCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var at = root.transform.position;
            var kinds = (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind));
            var guns = new System.Collections.Generic.List<GameObject>();
            float z = -1.5f;
            foreach (var k in kinds)
            {
                var g = RangedWeapon.Has(k) ? RangedWeapon.Create(k, at + new Vector3(0f, 0.03f, z), root.transform, 0f) : null;
                Log($"[GunItems] {k}: {(g ? "made" : "MISSING")}");
                if (g)
                {
                    if (g.TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
                    guns.Add(g);
                }
                z += 0.75f;
            }
            yield return null;
            cam.fieldOfView = 36f;
            cam.transform.position = at + new Vector3(0f, 5.2f, 0.0f);
            cam.transform.LookAt(at + new Vector3(0f, 0f, 0.0f), Vector3.forward);               // (from above, +x to the right: the barrels point right, the first kind is at the bottom)
            yield return null;
            ShotCam(cam, "gunitems_top");
            for (int i = 0; i < guns.Count; i++)
            {
                var g = guns[i];
                var k = kinds[i];
                var spec = RangedWeapon.Specs[(int)k];
                var lay = RangedWeapon.LayoutOf(k);
                float len = lay.fullBounds.size.x * lay.scale;
                // from the side, low: the whole gun in the picture
                cam.fieldOfView = 30f;
                cam.transform.position = g.transform.position + new Vector3(0f, 0.16f, -Mathf.Max(1.0f, len * 1.9f));
                cam.transform.LookAt(g.transform.position + new Vector3(0f, 0.02f, 0f));
                yield return null;
                ShotCam(cam, $"gunitems_{k}_side");
                var sil = g.transform.Find("Silencer");
                Log($"[GunItems] {k}: class {spec.weaponClass}, takes a silencer {spec.CanSilence}, silencer on the item {(sil ? "yes" : "no")}, scale {lay.scale:0.000}, length {len:0.000} m, foregrip {lay.hasForegrip}");
                if (sil)
                {
                    cam.fieldOfView = 24f;
                    cam.transform.position = sil.position + sil.forward * 1.0f + Vector3.up * 0.01f;
                    cam.transform.LookAt(sil.position);
                    yield return null;
                    ShotCam(cam, $"gunitems_{k}_end");
                }
            }
            Log("[GunItems] DONE");
            Quit(0);
        }
    }
}
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void GunSheet()
        {
            SessionState.SetString("netdev.mode", "gunsheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@ @'
        public static void GunSheet()
        {
            SessionState.SetString("netdev.mode", "gunsheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunItems()
        {
            SessionState.SetString("netdev.mode", "gunitems");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
Rep @'
                case "gunsheet": yield return GunSheetScenario(); break;
'@ @'
                case "gunsheet": yield return GunSheetScenario(); break;
                case "gunitems": yield return GunItemsScenario(); break;
'@
Rep 'mode != "gunsheet" && mode != "ui"' 'mode != "gunsheet" && mode != "gunitems" && mode != "ui"'
Save-Src

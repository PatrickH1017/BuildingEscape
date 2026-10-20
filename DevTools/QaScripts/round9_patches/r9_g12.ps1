# Round 9, batch G12: a picture scenario of the weapon safe holding each of the five guns (GunSafes): open, with its clip, its cash and a gold watch, from the front and from above.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

$draft = @'

        /// <summary>
        /// A weapon safe holding each of the five guns in turn (the owner's ladder of unlocks, 2026-10-19), open, with its spare clip, a bit of cash and a gold watch: from the front and from above. A long gun is longer than the hollow is deep:
        /// it lies slanted across it with its barrel out through the open door. Needs graphics: <c>ui.ps1 -Methods GunSafes</c>. Pictures: %TEMP%\be_shot_gunsafes_{kind}_{front|top}.png.
        /// </summary>
        IEnumerator GunSafesScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var root = new GameObject("GunSafes");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            Material Flat(Color c) => new Material(Shader.Find("Standard")) { color = c };
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(8f, 0.1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.42f, 0.44f, 0.48f));
            var camGo = new GameObject("GunSafesCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;
            var mats = new FloorSafe.Mats { steel = Flat(new Color(0.5f, 0.52f, 0.55f)), dark = Flat(new Color(0.12f, 0.12f, 0.13f)), brass = Flat(new Color(0.8f, 0.65f, 0.25f)), lining = Flat(new Color(0.2f, 0.2f, 0.22f)), led = Flat(new Color(0.2f, 1f, 0.3f)) };
            var gen = FindFirstObjectByType<FloorGenerator>();
            var build = typeof(FloorGenerator).GetMethod("BuildValuable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var at = root.transform.position;

            foreach (var kind in (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind)))
            {
                if (!RangedWeapon.Has(kind)) continue;
                var holder = new GameObject("Safe_" + kind).transform;
                holder.SetParent(root.transform, false);
                var safe = FloorSafe.Build(holder, 0f, at.z + 2.6f, 180f, 7, 0, 1234, mats);
                safe.FillWithGuns(holder, new[] { SafeRules.GunItem(kind) });
                safe.FillWithExtras(holder, 450, true, Flat(new Color(0.35f, 0.62f, 0.35f)), Flat(new Color(0.9f, 0.9f, 0.85f)), Flat(new Color(0.15f, 0.15f, 0.17f)), Flat(new Color(0.8f, 0.65f, 0.25f)), kind);
                if (gen && build != null)
                {
                    var watchAt = safe.WatchSpot(out float watchYaw);
                    safe.AddWatch((GameObject)build.Invoke(gen, new object[] { ValuableKind.Watch, SafeRules.SideWatchValue(7), watchAt, watchYaw, holder }));
                }
                safe.ApplyOpen(true);
                yield return new WaitForSeconds(1.4f);
                var st = safe.transform;
                var gunObj = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None);
                int guns = 0; foreach (var g in gunObj) if (g && g.isActiveAndEnabled && g.transform.IsChildOf(holder)) guns++;
                Log($"[GunSafes] {kind}: {guns} gun(s) lying in the open safe, {FindObjectsByType<AmmoClip>(FindObjectsSortMode.None).Length} clip(s) about");
                cam.fieldOfView = 38f;
                cam.transform.position = st.TransformPoint(new Vector3(0.85f, 1.25f, 1.45f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.30f, 0.1f)));
                yield return null;
                ShotCam(cam, $"gunsafes_{kind}_front");
                cam.fieldOfView = 34f;
                cam.transform.position = st.TransformPoint(new Vector3(0.0f, 1.9f, 0.25f));
                cam.transform.LookAt(st.TransformPoint(new Vector3(0f, 0.1f, 0.0f)), st.forward);
                yield return null;
                ShotCam(cam, $"gunsafes_{kind}_top");
                Destroy(holder.gameObject);
                yield return null;
            }
            Log("[GunSafes] DONE");
            Quit(0);
        }
    }
}
'@
Open-Src "$Ed\NetDev\NetDevGunSheet.cs"
$last = $script:s.LastIndexOf("    }`n}")
if ($last -lt 0) { $last = $script:s.LastIndexOf("    }`r`n}") }
if ($last -lt 0) { throw 'end of class not found' }
if ($script:s.Contains('GunSafesScenario')) { throw 'already there' }
$method = $draft.Replace("`r`n", "`n")
$method = $method.Substring(0, $method.Length - "    }`n}`n".Length)       # (the class and namespace closers are already in the file)
$script:s = $script:s.Substring(0, $last) + ($method.TrimStart("`n").Replace("`n", $script:eol)) + $script:s.Substring($last)
Save-Src

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void GunLook()
        {
'@ @'
        public static void GunSafes()
        {
            SessionState.SetString("netdev.mode", "gunsafes");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunLook()
        {
'@
Rep @'
                case "gunlook": yield return GunLookScenario(); break;
'@ @'
                case "gunlook": yield return GunLookScenario(); break;
                case "gunsafes": yield return GunSafesScenario(); break;
'@
Rep 'mode != "gunlook" && mode != "ui"' 'mode != "gunlook" && mode != "gunsafes" && mode != "ui"'
Save-Src

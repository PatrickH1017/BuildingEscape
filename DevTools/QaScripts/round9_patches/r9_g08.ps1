# Round 9, batch G08 (the four guns): the hold. A long gun is held in both hands (HeldGun: a two-bone solver for both arms, the gun scaled to the holder's arm, pulled back to the chest until the left hand reaches).
#  - CharacterAnimator.LeftHand; RangedWeapon.BuildHeld takes the body scale; HeldGun rewritten
#  - a picture scenario GunLook: every gun in the hands of a toon person, a Synty person and a Party character, from behind, the side and the front, aiming level, up and down
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Src\Guns\HeldGun.cs" 'C:\bqs\gun_draft\HeldGun.cs'

Open-Src "$Src\CharacterAnimator.cs"
Rep @'
        public Transform RightHand => clipMode && anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
'@ @'
        public Transform RightHand => clipMode && anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        /// <summary>The left hand bone of a humanoid rig (the hand that holds the front of a long gun), or null for the Party rig.</summary>
        public Transform LeftHand => clipMode && anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;
'@
Save-Src

Open-Src "$Src\Guns\RangedWeapon.cs"
Rep 'public static GameObject BuildHeld(Kind k, Transform hand, out Transform grip, out Transform muzzle, bool silenced = false)' 'public static GameObject BuildHeld(Kind k, Transform hand, out Transform grip, out Transform muzzle, bool silenced = false, float bodyScale = 1f)'
Rep 'grip.localScale = Vector3.one * inv;' 'grip.localScale = Vector3.one * inv * bodyScale;                                  // (a long gun is scaled to the arms that hold it: HeldGun)'
Save-Src

# ---- the picture scenario
$draft = @'
        /// <summary>
        /// Every gun in the hands of a toon person, a Synty office person and a Party character (needs graphics: <c>ui.ps1 -Methods GunLook</c>): from behind (the way the game's camera sees them), from the side and from the front,
        /// aiming level, then up and down. For tuning the two-handed hold. Pictures: %TEMP%\be_shot_gunlook_{kind}_{who}_{view}.png.
        /// </summary>
        IEnumerator GunLookScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var cat = CharacterCatalog.Instance;
            var root = new GameObject("GunLookLineup");
            root.transform.position = new Vector3(0f, 0f, 90f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.transform.SetParent(root.transform, false);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            ground.transform.localScale = new Vector3(60f, 0.1f, 20f);
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.38f, 0.42f) };

            var whoNames = new[] { "toon", "synty", "party" };
            var lookIds = new[] { "male_a", "syn_boss_male_01", null };
            var xs = new[] { -4f, 0f, 5f };
            var anims = new System.Collections.Generic.List<CharacterAnimator>();
            var bodies = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < whoNames.Length; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = "Host_" + whoNames[i];
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3(xs[i], 0f, 0f);
                host.transform.localRotation = Quaternion.identity;                          // facing +z, away from the camera at -z: the way the game's camera sees a player
                Object.Destroy(host.GetComponent<Collider>());
                CharacterLook look;
                if (lookIds[i] != null) look = new CharacterLook(lookIds[i], Color.white, 1, null, 1f, 1f, lookIds[i]);
                else look = PartyCharacterFactory.PlayerLooks[0];
                anims.Add(PartyCharacterFactory.Attach(host, look));
                bodies.Add(host);
            }
            foreach (var an in anims) if (an) { var ar = an.GetComponent<Animator>(); if (ar) ar.cullingMode = AnimatorCullingMode.AlwaysAnimate; foreach (var smr in an.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; }
            yield return new WaitForSeconds(1.0f);
            var camGo = new GameObject("GunLookCam");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.enabled = false;

            var kinds = (RangedWeapon.Kind[])System.Enum.GetValues(typeof(RangedWeapon.Kind));
            var aims = new[] { ("level", Vector3.forward), ("up", new Vector3(0f, 0.34f, 0.94f)), ("down", new Vector3(0f, -0.26f, 0.97f)) };
            foreach (var k in kinds)
            {
                if (!RangedWeapon.Has(k)) continue;
                var spec = RangedWeapon.Specs[(int)k];
                var helds = new System.Collections.Generic.List<HeldGun>();
                for (int i = 0; i < anims.Count; i++)
                {
                    var held = anims[i] ? HeldGun.Attach(anims[i], k, spec.CanSilence) : null;
                    helds.Add(held);
                    Log($"[GunLook] {k} on {whoNames[i]}: {(held != null ? held.ArmInfo() + ", body scale " + held.BodyScale.ToString("0.00") + ", two-handed " + held.TwoHanded : "NO HAND")}");
                }
                foreach (var (label, dir) in aims)
                {
                    foreach (var h in helds) if (h) { h.Raise = 1f; h.AimDirection = dir; }
                    yield return new WaitForSeconds(0.9f);
                    for (int i = 0; i < anims.Count; i++)
                    {
                        if (!anims[i] || helds[i] == null) continue;
                        var rs = anims[i].GetComponentsInChildren<SkinnedMeshRenderer>();
                        var b = rs.Length > 0 ? rs[0].bounds : new Bounds(bodies[i].transform.position, Vector3.one);
                        for (int r = 1; r < rs.Length; r++) b.Encapsulate(rs[r].bounds);
                        float s = Mathf.Max(0.6f, b.size.y);
                        var c = b.center;
                        cam.fieldOfView = 32f;
                        // from behind and to the right (the game's camera), then the side (the right), then the front
                        var views = label == "level"
                            ? new[] { ("behind", new Vector3(0.40f * s, 0.10f * s, -1.9f * s), new Vector3(0f, 0.05f * s, 0.30f * s)),
                                      ("side", new Vector3(2.0f * s, 0.05f * s, 0.15f * s), new Vector3(0f, 0.0f, 0.30f * s)),
                                      ("front", new Vector3(0.9f * s, 0.15f * s, 1.9f * s), new Vector3(0f, 0.0f, 0.15f * s)) }
                            : new[] { ("behind", new Vector3(0.40f * s, 0.10f * s, -1.9f * s), new Vector3(0f, 0.05f * s, 0.30f * s)) };
                        foreach (var (vname, offs, look) in views)
                        {
                            cam.transform.position = c + offs;
                            cam.transform.LookAt(c + look);
                            yield return null;
                            ShotCam(cam, $"gunlook_{k}_{whoNames[i]}_{vname}_{label}");
                        }
                        if (label == "level") Log($"[GunLook] {k} on {whoNames[i]}: left hand {helds[i].LeftHandGap:0.000} m from the foregrip (two-handed {helds[i].TwoHanded})");
                    }
                }
                foreach (var h in helds) if (h) h.Remove();
                yield return null;
            }
            Log("[GunLook] DONE");
            Quit(0);
        }
    }
}
'@
Open-Src "$Ed\NetDev\NetDevGunSheet.cs"
$tail = @'
            Log("[GunItems] DONE");
            Quit(0);
        }
    }
}
'@
$tailRegex = [regex]::Escape($tail.Replace("`r`n", "`n")).Replace("\n", "\r?\n")
if (([regex]::Matches($script:s, $tailRegex)).Count -ne 1) { throw 'the end of the GunItems scenario was not found exactly once' }
$replacement = ($tail.Replace("`r`n", "`n").Replace("        }\n    }\n}\n", "        }\n`n" + $draft.Replace("`r`n", "`n") + "`n")).Replace("`n", $script:eol)
$script:s = [regex]::Replace($script:s, $tailRegex, { param($m) $replacement })
Save-Src

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void GunItems()
        {
'@ @'
        public static void GunLook()
        {
            SessionState.SetString("netdev.mode", "gunlook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunItems()
        {
'@
Rep @'
                case "gunitems": yield return GunItemsScenario(); break;
'@ @'
                case "gunitems": yield return GunItemsScenario(); break;
                case "gunlook": yield return GunLookScenario(); break;
'@
Rep 'mode != "gunitems" && mode != "ui"' 'mode != "gunitems" && mode != "gunlook" && mode != "ui"'
Save-Src

# Round 12, batch G47 (the owner, 2026-10-20): "When in first person with a weapon the character is somewhat visible blocking the screen (reference image for how it should look more like attached)".
# The picture: a first-person shooter - the gun, the sight in the middle of the screen, a forearm reaching in from the side and nothing else of the body.
# Why the body was in the picture: first person hid the body parts that are not "in the hands", and a held gun was found by looking for a HeldGun component up the parents of each renderer - but HeldGun sits on the character's MODEL root, so with a gun in
# the hands every part of the body counted as part of the gun and stayed visible (a held bat or crowbar, on the other hand, was hidden: its held model has no MeleeWeapon component). Now:
#  (1) the gun or tool in the hands is found by name (the held models are called X_Held, RangedWeapon / MeleeWeapon.BuildHeld) and stays whole;
#  (2) the rest of the body is out of the picture but for the two forearms and hands (the arms), drawn by a small shader that throws away everything farther than a hand's width from the forearms (the body is one skinned mesh
#      that a build cannot cut up): Resources/Shaders/FirstPersonArms.shader, the arm bones handed in just before the picture is drawn; a rig without arms (the Party character) is hidden whole as before; the shadow is still the whole body's.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

Open-Src "$A\CameraFollow.cs"
RepRegion '        /// <summary>The body goes out of the picture in first person (its meshes only cast their shadow; what is in the hands stays) and comes back as it was.</summary>' '            bodyHidden = true;' @'
        // ---- what is seen of the player's own body in first person (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen": the gun and a forearm, nothing else)
        const string ArmsShaderName = "BuildingEscape/FirstPersonArms";
        const float ArmReach = 0.105f;                                   // how far from a forearm's line the body is still drawn (m): the arm's own thickness and a little more
        static Shader armsShader; static bool armsShaderLooked;
        readonly Dictionary<Renderer, Material[]> armsOriginal = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<Material, Material> armsMade = new Dictionary<Material, Material>();
        Transform armRU, armRE, armRH, armLU, armLE, armLH;              // the right and the left upper arm, elbow and wrist
        /// <summary>First person with a rig that has arms: the body is drawn as just its two forearms and hands (the shader throws the rest away), with the gun or the tool in the hands whole.</summary>
        public bool ArmsShown { get; private set; }

        void OnEnable() { Application.onBeforeRender += PushArms; }
        void OnDisable() { Application.onBeforeRender -= PushArms; }
        void OnDestroy() { foreach (var m in armsMade.Values) if (m) Destroy(m); armsMade.Clear(); }

        /// <summary>The bones of the two arms of a humanoid body; false for a rig without them (the Party character), which is then out of the picture altogether.</summary>
        bool FindArms(Transform body)
        {
            var an = body.GetComponentInChildren<Animator>();
            if (!an || !an.isHuman) return false;
            armRU = an.GetBoneTransform(HumanBodyBones.RightUpperArm); armRE = an.GetBoneTransform(HumanBodyBones.RightLowerArm); armRH = an.GetBoneTransform(HumanBodyBones.RightHand);
            armLU = an.GetBoneTransform(HumanBodyBones.LeftUpperArm); armLE = an.GetBoneTransform(HumanBodyBones.LeftLowerArm); armLH = an.GetBoneTransform(HumanBodyBones.LeftHand);
            return armRU && armRE && armRH && armLU && armLE && armLH;
        }

        /// <summary>The gun or the tool in the hands (<c>X_Held</c>, built by RangedWeapon.BuildHeld / MeleeWeapon.BuildHeld) stays whole in the picture.</summary>
        public static bool IsHeldModel(Transform t, Transform body)
        {
            for (var p = t; p && p != body; p = p.parent) if (p.name.EndsWith("_Held")) return true;
            return false;
        }

        /// <summary>A body mesh drawn with Standard materials (the characters' colour atlas): the kind the arms shader can take over.</summary>
        static bool IsSkin(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer || r is MeshRenderer) || !r.enabled) return false;
            var ms = r.sharedMaterials;
            if (ms.Length == 0) return false;
            foreach (var m in ms) if (!m || !m.shader || m.shader.name != "Standard") return false;
            return true;
        }

        Material[] ArmMaterialsFor(Material[] source)
        {
            var made = new Material[source.Length];
            for (int i = 0; i < made.Length; i++)
            {
                var m = source[i];
                if (!armsMade.TryGetValue(m, out var a) || !a)
                {
                    a = new Material(armsShader) { name = "Arms " + m.name, mainTexture = m.mainTexture, color = m.color, hideFlags = HideFlags.HideAndDontSave };
                    if (a.HasProperty("_Glossiness") && m.HasProperty("_Glossiness")) a.SetFloat("_Glossiness", m.GetFloat("_Glossiness"));
                    armsMade[m] = a;
                }
                made[i] = a;
            }
            return made;
        }

        /// <summary>Hands the two forearms to the arms shader just before the picture is drawn (after the gun's arm solver has put them where they are).</summary>
        void PushArms()
        {
            if (!ArmsShown || !armRE || !armRH || !armLE || !armLH) return;
            SetArm("_BEArmRA", "_BEArmRB", armRU, armRE, armRH);
            SetArm("_BEArmLA", "_BEArmLB", armLU, armLE, armLH);
            Shader.SetGlobalFloat("_BEArmReach", ArmReach);
        }

        static void SetArm(string idA, string idB, Transform upper, Transform elbow, Transform hand)
        {
            var dir = hand.position - elbow.position;
            float len = dir.magnitude;
            var tip = len > 0.001f ? hand.position + dir / len * 0.11f : hand.position;                    // (past the wrist: the fingers)
            var start = upper ? Vector3.Lerp(elbow.position, upper.position, 0.30f) : elbow.position;       // (a little of the upper arm: the cut stays out of the picture)
            Shader.SetGlobalVector(idA, start);
            Shader.SetGlobalVector(idB, tip);
        }

        /// <summary>In first person only the two arms are in the picture (the body casts its shadow still) with what is in the hands; in third person the body comes back as it was.</summary>
        void ShowBody(bool show)
        {
            var body = spectate ? spectate : target;
            if (!body) return;
            if (show)
            {
                if (!bodyHidden) return;
                foreach (var r in hiddenBody)
                {
                    if (!r) continue;
                    if (bodyModes.TryGetValue(r, out var mode)) r.shadowCastingMode = mode;
                    if (armsOriginal.TryGetValue(r, out var mats)) r.sharedMaterials = mats;
                }
                hiddenBody.Clear(); bodyModes.Clear(); armsOriginal.Clear();
                bodyHidden = false; ArmsShown = false;
                return;
            }
            if (bodyHidden && Time.unscaledTime < nextBodyScan) return;
            nextBodyScan = Time.unscaledTime + 0.5f;                                  // (a hat or another look can come later: looked at again twice a second)
            if (!armsShaderLooked) { armsShaderLooked = true; armsShader = Shader.Find(ArmsShaderName); }
            bool arms = armsShader != null && FindArms(body);
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || bodyModes.ContainsKey(r) || IsHeldModel(r.transform, body)) continue;
                bodyModes[r] = r.shadowCastingMode;
                hiddenBody.Add(r);
                if (arms && IsSkin(r)) { armsOriginal[r] = r.sharedMaterials; r.sharedMaterials = ArmMaterialsFor(r.sharedMaterials); }
                else r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
            ArmsShown = arms;
            bodyHidden = true;
'@
Save-Src

Open-Src "$E\NetDev\NetDevRound12.cs"
RepRegion '                var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && r.GetComponentInParent<HeldGun>() == null && r.GetComponentInParent<MeleeWeapon>() == null).ToList();' 'R12Check("P again: back to third person, the body is back as it was", !rig.IsFirstPerson && parts.All(r => !r || r.shadowCastingMode == original[r]));' @'
                var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && !CameraFollow.IsHeldModel(r.transform, body)).ToList();
                var heldParts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && CameraFollow.IsHeldModel(r.transform, body)).ToList();
                var original = new Dictionary<Renderer, ShadowCastingMode>();
                var originalMats = new Dictionary<Renderer, Material[]>();
                foreach (var r in parts) { original[r] = r.shadowCastingMode; originalMats[r] = r.sharedMaterials; }
                const string ArmsShader = "BuildingEscape/FirstPersonArms";
                bool IsArms(Renderer r) => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader && m.shader.name == ArmsShader);
                rig.ToggleView(); yield return new WaitForSeconds(0.9f);
                float eyes = Vector3.Distance(rig.transform.position, body.position + Vector3.up * rig.eyeHeight);
                bool armsOnly = parts.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || (rig.ArmsShown && IsArms(r))) && (!rig.ArmsShown || parts.Any(IsArms));
                R12Check("P: first person - the camera is at the eyes and the body is out of the picture but for its two forearms (the arms shader on the body's meshes, the whole body still casts its shadow); what is in the hands stays whole (the owner, 2026-10-20)",
                    rig.IsFirstPerson && eyes < 0.2f && parts.Count > 0 && armsOnly && heldParts.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !IsArms(r)), $"(eyes {eyes:0.00} m off, arms shown {rig.ArmsShown}, {parts.Count} body parts, {heldParts.Count} parts in the hands)");
                if (gfx) ShotCam(Camera.main, "round12_first_person");

                // with a gun in the hands: the gun is whole, the body is only its arms (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen")
                var smg = RangedWeapon.Create(RangedWeapon.Kind.Smg, player.transform.position + Vector3.up * 0.5f, null, 0f, 2).GetComponent<Pickup>();
                pack.TryStore(smg); yield return new WaitForSeconds(0.3f);
                pack.SelectSlot(2); yield return new WaitForSeconds(1.2f);
                var gunNow = player.GetComponent<GunController>();
                var heldGun = gunNow ? gunNow.Held : null;
                var gunParts = heldGun ? heldGun.Grip.GetComponentsInChildren<Renderer>(true).ToList() : new List<Renderer>();
                var bodyNow = body.GetComponentsInChildren<Renderer>(true).Where(r => r && !CameraFollow.IsHeldModel(r.transform, body) && r.enabled && r.gameObject.activeInHierarchy).ToList();
                R12Check("first person with the SMG in the hands: the gun is whole (every part drawn, none of it the arms shader), every other part of the body is hidden or only its arms - the character no longer blocks the screen",
                    pack.Equipped == smg && heldGun != null && gunParts.Count > 3 && gunParts.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !IsArms(r)) && bodyNow.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || IsArms(r)),
                    $"(gun parts {gunParts.Count}, other parts {bodyNow.Count}, arms shown {rig.ArmsShown}, equipped {(pack.Equipped ? pack.Equipped.Label : "nothing")})");
                if (gfx) ShotCam(Camera.main, "round12_first_person_gun");
                pack.SelectSlot(0); yield return new WaitForSeconds(0.4f);
                rig.ToggleView(); yield return new WaitForSeconds(0.9f);
                R12Check("P again: back to third person, the body is back as it was (its shadow mode and its own materials)", !rig.IsFirstPerson && parts.All(r => !r || (r.shadowCastingMode == original[r] && r.sharedMaterials.SequenceEqual(originalMats[r]))));
'@
Save-Src
Write-Output 'round 12 batch G47 patched'

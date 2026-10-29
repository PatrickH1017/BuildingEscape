# The owner's restraint, drag and rescue clips, stage 1 (2026-10-28): a full-body clip layer in the animator, the pinned player tied up and lying hogtied, a teammate dragging a hogtied player by the head and an eliminated officer by the foot. New files (Scripts\Restraint\
# RestraintRules, RestraintClips, ZipTies, RestraintVisual) and the changes to the files that exist: CharacterAnimator (the layer), WobblyPlayer (a pinned body no longer tips over: its capsule lies down instead, so the clip is the body's picture), HeldGun and
# MeleeController (the hands are tied: the weapon in them is put away), FirstPersonMelee, GuardVisual (an eliminated officer's body can be dragged by the foot), WeaponAnimationSetup (the 13 files).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the Restraint scripts' } else {
  New-Item -ItemType Directory -Force ($A + 'Scripts\Restraint') | Out-Null
  foreach ($f in 'RestraintRules.cs', 'RestraintClips.cs', 'ZipTies.cs', 'RestraintVisual.cs') { Copy-Item (Join-Path $here "restraint\$f") ($A + "Scripts\Restraint\$f") -Force }
  Write-Output 'copied the Restraint scripts'
}

# ---------------------------------------------------------------- CharacterAnimator: the full-body layer
Open-Src ($A + 'Scripts\CharacterAnimator.cs')
Rep '            layers = AnimationLayerMixerPlayable.Create(graph, 4);' '            layers = AnimationLayerMixerPlayable.Create(graph, 5);          // (4: a full-body clip, see PlayBody)'
Rep '        float gunWeight, reloadWeight, meleeWeight;' @'
        float gunWeight, reloadWeight, meleeWeight;
        AnimationClipPlayable pBody;
        AnimationClip bodyClip;
        float bodyWeight;
        bool bodyOn;
'@
Rep '            if (layersOn) FadeWeapons();                                           // (nobody holds the gun or the weapon any more: it was put away)' @'
            if (layersOn) FadeWeapons();                                           // (nobody holds the gun or the weapon any more: it was put away)
            if (layersOn) StepBody();
'@
Rep '        /// <summary>Nobody asked for a gun''s or a weapon''s pose last frame (it was put away): its layers fade out.</summary>' @'
        // ---- a full-body clip (the owner's restraint, drag and rescue clips, 2026-10-28): the top layer, no mask, over everything the character was doing. Whoever plays it sets its time every frame (SetBodyTime: the clip does not run by itself, so that two
        // characters that play the two halves of one scene can be held at the same moment).
        /// <summary>This rig can play a full-body clip (a humanoid in clip mode).</summary>
        public bool CanPlayBody => CanPlayWeapons;
        /// <summary>A full-body clip is playing or fading (the weapons in the hands are put away meanwhile).</summary>
        public bool BodyActive => layersOn && (bodyOn || bodyWeight > 0.001f);
        /// <summary>How much of the body the full-body clip has this frame (0..1).</summary>
        public float BodyWeightNow => layersOn ? bodyWeight : 0f;
        /// <summary>The full-body clip now playing (null: none).</summary>
        public AnimationClip BodyClipNow => bodyOn ? bodyClip : null;

        /// <summary>Plays <paramref name="clip"/> over the whole body (it fades in); false when this rig cannot.</summary>
        public bool PlayBody(AnimationClip clip)
        {
            if (!clip || !EnsureLayers()) return false;
            if (!pBody.IsValid() || bodyClip != clip)
            {
                pBody = Swap(pBody, clip, layers, 4);
                bodyClip = clip;
                layers.SetInputWeight(4, bodyWeight);
            }
            bodyOn = true;
            return true;
        }

        /// <summary>The time (s) of the full-body clip.</summary>
        public void SetBodyTime(float t) { if (pBody.IsValid()) pBody.SetTime(t); }

        /// <summary>The full-body clip fades out and the character goes back to what he was doing.</summary>
        public void StopBody() { bodyOn = false; }

        void StepBody()
        {
            if (!pBody.IsValid()) { bodyWeight = 0f; return; }
            bodyWeight = Mathf.MoveTowards(bodyWeight, bodyOn ? 1f : 0f, Time.deltaTime / 0.15f);
            layers.SetInputWeight(4, bodyWeight);
            if (!bodyOn && bodyWeight <= 0f) { pBody = Swap(pBody, null, layers, 4); bodyClip = null; }
        }

        /// <summary>Nobody asked for a gun's or a weapon's pose last frame (it was put away): its layers fade out.</summary>
'@
Save-Src

# ---------------------------------------------------------------- WobblyPlayer: a pinned or busted body lies on the floor as a capsule laid down, the clip is its picture
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep '            if (capsule) { standHeight = capsule.height; standCenter = capsule.center; }' '            if (capsule) { standHeight = capsule.height; standCenter = capsule.center; standDirection = capsule.direction; }'
Rep '        bool crouchApplied;' @'
        bool crouchApplied;
        int standDirection = 1;
        bool lying;
        RigidbodyConstraints standConstraints;
'@
Rep '            if (!GetComponent<HeldWalkie>()) gameObject.AddComponent<HeldWalkie>();                              // (the walkie talkie he holds out: the same)' @'
            if (!GetComponent<HeldWalkie>()) gameObject.AddComponent<HeldWalkie>();                              // (the walkie talkie he holds out: the same)
            if (!GetComponent<RestraintVisual>()) gameObject.AddComponent<RestraintVisual>();                    // (tied up, dragged, freed: the owner's restraint clips, 2026-10-28)
'@
Rep '            if (!capsule || crouching == crouchApplied) return;' @'
            if (lying) { crouchApplied = false; IsCrouching = false; return; }                // (a body on the floor keeps its laid-down capsule)
            if (!capsule || crouching == crouchApplied) return;
'@
Rep '                if (was == CustodyState.Free && rb && !rb.isKinematic)
                {
                    // taken down: tipped over onto the ground (only the machine that owns the body moves it)
                    rb.AddForce(-transform.forward * 1.5f, ForceMode.VelocityChange);
                    rb.AddTorque(transform.right * 5f, ForceMode.VelocityChange);
                    NoiseBus.Emit(transform.position, 8f, gameObject);
                }' @'
                if (was == CustodyState.Free)
                {
                    // taken down. A character who can play the owner's restraint clips (2026-10-28) is shown lying by the clip: the capsule is laid down on the floor and the body stays as it is. Any other is tipped over onto the ground as before
                    // (only the machine that owns the body moves it)
                    bool shown = RestraintShown();
                    if (shown) SetLying(true);
                    if (rb && !rb.isKinematic)
                    {
                        if (!shown)
                        {
                            rb.AddForce(-transform.forward * 1.5f, ForceMode.VelocityChange);
                            rb.AddTorque(transform.right * 5f, ForceMode.VelocityChange);
                        }
                        NoiseBus.Emit(transform.position, 8f, gameObject);
                    }
                }
'@
Rep '                PinnedBy = null;
                if (rb && !rb.isKinematic) rb.AddForce(Vector3.up * 2f, ForceMode.VelocityChange); // gets back up' @'
                PinnedBy = null;
                SetLying(false);
                if (rb && !rb.isKinematic) rb.AddForce(Vector3.up * 2f, ForceMode.VelocityChange); // gets back up
'@
Rep '            if (Vector3.Dot(transform.up, Vector3.up) > 0.6f) rb.AddTorque(transform.right * 3f, ForceMode.Acceleration);' '            if (!lying && Vector3.Dot(transform.up, Vector3.up) > 0.6f) rb.AddTorque(transform.right * 3f, ForceMode.Acceleration);'
Rep '        /// <summary>Pinned / busted: the body lies where it fell. No walking, no righting torque; it just settles.</summary>' @'
        /// <summary>The restraint clips can show this body (a humanoid in clip mode, the clips in the build): a pinned player is not tipped over, the clip lies him down.</summary>
        bool RestraintShown()
        {
            var v = GetComponent<PlayerVisual>();
            return v && v.Anim && v.Anim.CanPlayBody && RestraintClips.Get(RestraintClips.Hogtied) != null;
        }

        /// <summary>
        /// The capsule laid down on the floor (along the body's forward, its bottom where the feet were) and the body kept from turning over, or standing again. The body's own pivot stays where it is: the clip draws the character on the floor under it
        /// (<see cref="RestraintVisual"/>).
        /// </summary>
        void SetLying(bool on)
        {
            if (lying == on) return;
            lying = on;
            if (rb)
            {
                if (on) { standConstraints = rb.constraints; rb.constraints = RigidbodyConstraints.FreezeRotation; }
                else rb.constraints = standConstraints;
            }
            if (!capsule) return;
            if (on)
            {
                capsule.direction = 2;
                capsule.height = standHeight;
                capsule.center = new Vector3(standCenter.x, standCenter.y - standHeight * 0.5f + capsule.radius, standCenter.z);
            }
            else
            {
                capsule.direction = standDirection;
                capsule.height = standHeight;
                capsule.center = standCenter;
                crouchApplied = false;
            }
        }

        /// <summary>The body is shown lying by a restraint clip (for the tests).</summary>
        public bool IsLying => lying;

        /// <summary>Pinned / busted: the body lies where it fell. No walking, no righting torque; it just settles.</summary>
'@
Save-Src

# ---------------------------------------------------------------- the hands are tied: the weapon in them is put away
Open-Src ($A + 'Scripts\Guns\HeldGun.cs')
Rep '            if (!animated || !Grip) return;
            raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime / Mathf.Max(0.05f, raiseSeconds));' @'
            if (Grip && character && character.BodyActive)              // (the owner's restraint clips: the hands are tied, the gun is put away while one plays)
            {
                if (!putAway) { putAway = true; Grip.gameObject.SetActive(false); }
                return;
            }
            if (putAway && Grip) { putAway = false; Grip.gameObject.SetActive(true); }
            if (!animated || !Grip) return;
            raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime / Mathf.Max(0.05f, raiseSeconds));
'@
Rep '        void LateUpdate()
        {
            if (!Grip || !hasArm) return;' @'
        bool putAway;

        void LateUpdate()
        {
            if (!Grip || !hasArm || putAway) return;
'@
Save-Src
Open-Src ($A + 'Scripts\MeleeController.cs')
Rep '        void PoseMelee()
        {
            if (meleeClips == null || !heldModel) return;' @'
        void PoseMelee()
        {
            if (heldModel)
            {
                var a0 = vis ? vis.Anim : null;
                bool away = a0 && a0.BodyActive;                                       // (the owner's restraint clips: the hands are tied, the weapon is put away while one plays)
                if (heldModel.activeSelf == away) heldModel.SetActive(!away);
            }
            if (meleeClips == null || !heldModel) return;
'@
Save-Src
Open-Src ($A + 'Scripts\FirstPersonMelee.cs')
Rep '            if (!weapon || !upper || !fore || !hand) return;' '            if (!weapon || !upper || !fore || !hand || (anim && anim.BodyActive)) return;'
Save-Src

# ---------------------------------------------------------------- an officer's body, eliminated, can be dragged by the foot: the restraint visual shows it
Open-Src ($A + 'Scripts\GuardVisual.cs')
Rep '            if (!GetComponent<GuardBubble>()) gameObject.AddComponent<GuardBubble>();   // the occasional comic-book curse' @'
            if (!GetComponent<GuardBubble>()) gameObject.AddComponent<GuardBubble>();   // the occasional comic-book curse
            if (!GetComponent<RestraintVisual>()) gameObject.AddComponent<RestraintVisual>();   // (the owner's drag-by-the-foot clip for his body, 2026-10-28)
'@
Save-Src

# ---------------------------------------------------------------- the setup: the 13 files are humanoid clips named like their files
Open-Src ($A + 'Scripts\Restraint\RestraintClips.cs')
Rep '        /// <summary>The clips that loop (the others play once).</summary>' @'
        /// <summary>The frames of a clip's Blender file (1 .. n).</summary>
        public static int Frames(string name)
        {
            if (name == TieUp) return RestraintRules.TieUpFrames;
            if (name == Hogtied) return RestraintRules.HogtiedFrames;
            if (name == GuardShotGuard || name == GuardShotDragged) return RestraintRules.GuardShotFrames;
            if (name == FreeUpRescuer || name == FreeUpFreed) return RestraintRules.FreeUpFrames;
            return RestraintRules.DragFrames;
        }

        /// <summary>The clips that loop (the others play once).</summary>
'@
Save-Src
Open-Src ($A + 'Editor\WeaponAnimationSetup.cs')
Rep '        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]' @'
        // the owner's restraint, drag and rescue files (2026-10-28): each ONE clip of the whole take, named like its file; the dragging ones loop
        static IEnumerable<KeyValuePair<string, Cut[]>> RestraintCuts()
        {
            foreach (var n in RestraintClips.All)
                yield return new KeyValuePair<string, Cut[]>(n, new[] { new Cut(n, 1, RestraintClips.Frames(n), RestraintClips.Loops(n)) });
        }

        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]
'@
Rep '            foreach (var pair in Cuts.Concat(StanceCuts()).Concat(MeleeStanceCuts()))' '            foreach (var pair in Cuts.Concat(StanceCuts()).Concat(MeleeStanceCuts()).Concat(RestraintCuts()))'
Save-Src
"restraint 1 patched"

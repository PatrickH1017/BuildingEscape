# T12 (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable(in order to hide them)"): the body of an eliminated guard is taken hold of with the grab key (Q), trails behind the player and does not sink away while dragged.
# (Players' bodies - busted players - are NOT done: see DEVELOPER_NOTES 2.63 T12.) Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't12_new\DraggableBodyRules.cs') ($A + 'Scripts\DraggableBodyRules.cs')
Put-Crlf (Join-Path $here 't12_new\DraggableBody.cs') ($A + 'Scripts\DraggableBody.cs')
Put-Crlf (Join-Path $here 't12_new\RunSync.Bodies.cs') ($A + 'Scripts\Net\RunSync.Bodies.cs')
Put-Crlf (Join-Path $here 't12_new\NetDevAsks1023FTest.cs') ($A + 'Editor\NetDev\NetDevAsks1023FTest.cs')
Put-Crlf (Join-Path $here 't12_new\NetDevAsks1023F.cs') ($A + 'Editor\NetDev\NetDevAsks1023F.cs')

# ---- the player drags slower
$SC = $A + 'Scripts\WobblyPlayer.cs'; Open-Src $SC
Rep '        public Vector3? VelocityOverride { get; set; }' @'
        public Vector3? VelocityOverride { get; set; }
        /// <summary>The share of his walking speed this player keeps while he drags a body (1 = none; <see cref="DraggableBody"/> sets it to <see cref="DraggableBodyRules.DraggerSpeedShare"/>).</summary>
        public float DragFactor { get; set; } = 1f;
'@.TrimEnd()
Rep '                float gait = IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f;' '                float gait = (IsSprinting ? sprintMultiplier : IsCrouching ? crouchSpeedMultiplier : 1f) * DragFactor;'
Save-Src

# ---- the dead guard gets the draggable body next to his corpse timer
$SC = $A + 'Scripts\GuardAI.cs'; Open-Src $SC
Rep '        GuardCorpse corpse;' ('        GuardCorpse corpse;' + "`n" + '        DraggableBody draggable;')
Rep '            if (!IsBoss && corpse == null) corpse = gameObject.AddComponent<GuardCorpse>();' ('            if (!IsBoss && corpse == null) corpse = gameObject.AddComponent<GuardCorpse>();' + "`n" + '            if (!IsBoss && draggable == null) draggable = gameObject.AddComponent<DraggableBody>();          // (the owner, 2026-10-23: a body can be dragged away to hide it)')
Save-Src

# ---- a body that is dragged does not sink away
$SC = $A + 'Scripts\GuardCorpse.cs'; Open-Src $SC
Rep '        bool began;' @'
        bool began;
        DraggableBody drag;

        /// <summary>The body has begun to sink: it can no longer be taken hold of.</summary>
        public bool Sinking => began;
'@.TrimEnd()
Rep '            since += Time.deltaTime;' @'
            if (drag == null) drag = GetComponent<DraggableBody>();
            if (drag == null || !drag.IsDragged) since += Time.deltaTime;                  // (a body that is dragged does not sink away under the hands that drag it: the owner, 2026-10-23)
'@.TrimEnd()
Save-Src

# ---- the grab key takes hold of a body
$SC = $A + 'Scripts\PlayerGrab.cs'; Open-Src $SC
Rep '            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else TryGrab(); }' '            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else if (Dragging != null) AskDropBody(); else TryGrab(); }'
Rep '            if (p) p.TryGetComponent(out rb);' ('            if (p) p.TryGetComponent(out rb);' + "`n" + '            if (!rb && !p && TryGrabBody()) return;                     // (nothing to pick up: perhaps a body to drag)')
Rep '        void OnDisable() { Release(false); }' @'
        // ------------------------------------------------------------------ dragging a body (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)")

        DraggableBody dragging;

        /// <summary>The body of an eliminated guard this player drags, or null (<see cref="DraggableBody"/>).</summary>
        public DraggableBody Dragging => dragging != null && wob != null && dragging.Dragger == wob ? dragging : null;

        /// <summary>The grab key with nothing to pick up: take hold of the nearest body in reach, if there is one (a networked run asks the host). True when there was one.</summary>
        public bool TryGrabBody()
        {
            if (!wob) TryGetComponent(out wob);
            if (!wob || wob.InCustody || Held || Dragging != null) return false;
            var body = DraggableBody.Nearest(transform, reach);
            if (body == null) return false;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragBody(body.Guard);
            else ApplyDragBody(body);
            return true;
        }

        void AskDropBody()
        {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDropBody();
            else ApplyDropBody();
        }

        /// <summary>This player now drags the body (offline: at once; a networked run: the host said so, every machine applies it).</summary>
        public void ApplyDragBody(DraggableBody body)
        {
            if (!body || Dragging != null) return;
            if (!wob) TryGetComponent(out wob);
            if (!wob) return;
            dragging = body;
            body.Begin(wob);
        }

        /// <summary>This player lets go of the body he drags.</summary>
        public void ApplyDropBody()
        {
            if (dragging != null) dragging.End();
            dragging = null;
        }

        void OnDisable() { Release(false); ApplyDropBody(); }
'@.TrimEnd()
Save-Src

# ---- three new RPCs (RunSync.Bodies) and the bullets / walkie changes of today: players on different builds must not join each other
$SC = $A + 'Scripts\Net\NetConst.cs'; Open-Src $SC
RepRx 'ProtocolVersion = "27";   // 27:' 'ProtocolVersion = "28";   // 28: (2026-10-23) dragging the body of an eliminated guard (three RPCs, RunSync.Bodies), bullets instead of clips and the bullets a guard drops (taken by running over them), the walkie talkie at the Vendor (the catalog has one entry more), the replacement guard and the dev terminal; 27:'
Save-Src

# ---- the scenario and the pure test are registered
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void Asks1023D()' ('        public static void Asks1023F()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "asks1023f");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void Asks1023D()')
Rep '&& mode != "asks1023e")' '&& mode != "asks1023e" && mode != "asks1023f")'
Rep '                case "asks1023d": yield return Asks1023DScenario(); break;' ('                case "asks1023d": yield return Asks1023DScenario(); break;' + "`n" + '                case "asks1023f": yield return Asks1023FScenario(); break;')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023DTest();' ('            Asks1023DTest();' + "`n" + '            Asks1023FTest();')
Save-Src
"T12 patched"

# The owner's restraint, drag and rescue clips, stages 2 and 3 (2026-10-28): an officer drags the arrested captive by the head to the guards' outpost, the captive struggles (a held button: the officer's drag slows, a stamina, a shove),
# a hit officer lets go of him (the drop), all over the network.
#   GuardAI (now partial; GuardAI.Drag.cs): the Dragging state, BeginDrag / UpdateDragging / DropCaptive (a hit, a stun or a knock-out lets the captive go)
#   HeistManager.BustPlayer: the officer who held him starts to drag him
#   WobblyPlayer: DraggedByGuard, the struggle button and its numbers, the drop, the captive follows the officer
#   NetPlayer: the officer, the struggle and the drop are replicated; PlayerGrab / RunSync.Bodies: a teammate cannot take a captive an officer drags
#   RestraintVisual: the officer's and the captive's clips
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the restraint sources' } else {
  Copy-Item (Join-Path $here 'restraint\RestraintRules.cs') ($A + 'Scripts\Restraint\RestraintRules.cs') -Force
  Copy-Item (Join-Path $here 'restraint\RestraintVisual.cs') ($A + 'Scripts\Restraint\RestraintVisual.cs') -Force
  Copy-Item (Join-Path $here 'restraint\GuardAI.Drag.cs') ($A + 'Scripts\Restraint\GuardAI.Drag.cs') -Force
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintRules.cs') ($A + 'Editor\NetDev\NetDevRestraintRules.cs') -Force
  Write-Output 'copied RestraintRules, RestraintVisual, GuardAI.Drag, NetDevRestraintRules'
}

# ---------------------------------------------------------------- GuardAI
Open-Src ($A + 'Scripts\GuardAI.cs')
Rep '    public enum GuardState { Patrol, Investigate, Chase, Stunned, Calling, Pinning }' '    public enum GuardState { Patrol, Investigate, Chase, Stunned, Calling, Pinning, Dragging }          // (Dragging: he drags an arrested player by the head to the outpost, GuardAI.Drag.cs)'
Rep '    public class GuardAI : MonoBehaviour' '    public partial class GuardAI : MonoBehaviour'
Rep '            if (State == GuardState.Stunned || State == GuardState.Chase || State == GuardState.Calling || State == GuardState.Pinning) return;' '            if (State == GuardState.Stunned || State == GuardState.Chase || State == GuardState.Calling || State == GuardState.Pinning || State == GuardState.Dragging) return;'
Rep '            if (IsPuppet || Dead || State == GuardState.Pinning || State == GuardState.Stunned || State == GuardState.Chase || !agent || !agent.isOnNavMesh) return;' '            if (IsPuppet || Dead || State == GuardState.Pinning || State == GuardState.Dragging || State == GuardState.Stunned || State == GuardState.Chase || !agent || !agent.isOnNavMesh) return;'
Rep '                case GuardState.Pinning: UpdatePinning(); break;' @'
                case GuardState.Pinning: UpdatePinning(); break;
                case GuardState.Dragging: UpdateDragging(); break;
'@
Rep '            if (State != GuardState.Chase && State != GuardState.Calling && State != GuardState.Pinning) ScanForPlayers();' '            if (State != GuardState.Chase && State != GuardState.Calling && State != GuardState.Pinning && State != GuardState.Dragging) ScanForPlayers();'
Rep '        public void ReleaseHold(bool freeThem = false)
        {' @'
        public void ReleaseHold(bool freeThem = false)
        {
            if (Dragging != null) DropCaptive();             // (hit, stunned or knocked out while he drags a captive: he lets go of him, GuardAI.Drag.cs)
'@
Save-Src

# ---------------------------------------------------------------- the dev overlay names the new state
Open-Src ($A + 'Scripts\Dev\DevOverlayRules.cs')
Rep '                case GuardState.Pinning: return "PINNING";' @'
                case GuardState.Pinning: return "PINNING";
                case GuardState.Dragging: return "DRAGGING";
'@
Save-Src
Open-Src ($A + 'Scripts\Dev\DevSpectatorView.cs')
Rep '                case GuardState.Chase: case GuardState.Pinning: return HudTheme.Danger;' '                case GuardState.Chase: case GuardState.Pinning: case GuardState.Dragging: return HudTheme.Danger;'
Save-Src

# ---------------------------------------------------------------- HeistManager: the officer who held him starts to drag him
Open-Src ($A + 'Scripts\HeistManager.cs')
Rep '            if (guard) guard.ReleaseHold();' '            if (guard && !guard.BeginDrag(p)) guard.ReleaseHold();           // (an officer who can drags him by the head to the outpost, GuardAI.Drag.cs)'
Save-Src

# ---------------------------------------------------------------- WobblyPlayer: dragged by an officer, the struggle, the drop
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep '        public WobblyPlayer DraggedBy { get; set; }' @'
        public WobblyPlayer DraggedBy { get; set; }
        /// <summary>The officer who drags this arrested player by the head to the guards outpost (the owner's restraint clips, 2026-10-28), or null. The host's officer sets it; every machine knows it (<c>NetPlayer</c> carries it).</summary>
        public GuardAI DraggedByGuard { get; set; }
        /// <summary>The struggle button (Space) is held while an officer drags him (the owner's machine reads the key; the host reads it from <c>NetPlayer</c>).</summary>
        public bool StruggleHeld { get; set; }
        /// <summary>He is struggling now (the host decides: the button is held and he has stamina): the officer's drag is slow, the clip shows it.</summary>
        public bool StruggleActive { get; set; }
        /// <summary>The struggle stamina, 0 (empty) .. 1 (full): the host's.</summary>
        public float StruggleStamina { get; set; } = 1f;
        /// <summary>How many times the officer has shoved him (the host counts; a change is a shove on every machine).</summary>
        public int ShoveCount { get; set; }
        /// <summary>How many times an officer has had to let go of this player because he was hit, the officer who did, and when (every machine: the drop clip runs from that moment).</summary>
        public int DropCount { get; private set; }
        public GuardAI DroppedBy { get; private set; }
        public float DropTime { get; private set; } = -999f;
        /// <summary>The officer let go of this player because he was hit (the host calls it; the other machines when <c>NetPlayer</c> carries the news).</summary>
        public void NoteDrop(GuardAI by, int count) { DropCount = count; DroppedBy = by; DropTime = Time.time; }
'@
Rep '        [System.NonSerialized] public bool testSprint, testCrouch;' '        [System.NonSerialized] public bool testSprint, testCrouch, testStruggle;'
Rep '            if (IsStumbling && Time.time > stumbleUntil) IsStumbling = false;' @'
            // the struggle (the owner's restraint clips, 2026-10-28): an officer drags him by the head, he holds Space to dig his heels in
            bool struggleKey = Input.GetKey(KeyCode.Space);
#if UNITY_EDITOR
            struggleKey |= testStruggle;
#endif
            StruggleHeld = struggleKey && Custody == CustodyState.Busted && DraggedByGuard != null && !OutpostUi.Open && !inputLocked;
            if (IsStumbling && Time.time > stumbleUntil) IsStumbling = false;
'@
Rep '            var by = DraggedBy;
            if (!by) { dragTrailLive = false; return; }
            if (Custody != CustodyState.Busted) { DraggedBy = null; dragTrailLive = false; return; }
            var p = by.transform.position;' @'
            if (Custody != CustodyState.Busted) { DraggedBy = null; DraggedByGuard = null; dragTrailLive = false; return; }
            Transform by = DraggedByGuard ? DraggedByGuard.transform : DraggedBy ? DraggedBy.transform : null;          // (a teammate, or an officer: the owner's restraint clips)
            if (!by) { dragTrailLive = false; return; }
            var p = by.position;
'@
Save-Src

# ---------------------------------------------------------------- NetPlayer: the officer, the struggle and the drop on every machine
Open-Src ($A + 'Scripts\Net\NetPlayer.cs')
Rep '        readonly NetworkVariable<byte> bustedLevel = new(0);' @'
        readonly NetworkVariable<byte> bustedLevel = new(0);

        // The officer who drags this arrested player to the outpost (a guards net id, -1 none), the struggle and the drop (the owner's restraint clips, 2026-10-28): the host decides and publishes, every machine shows it; the button is the owners.
        readonly NetworkVariable<int> draggedByGuard = new(-1);
        readonly NetworkVariable<bool> struggleActive = new(false);
        readonly NetworkVariable<byte> struggleStamina = new((byte)100);
        readonly NetworkVariable<byte> shoveCount = new((byte)0);
        readonly NetworkVariable<byte> dropCount = new((byte)0);
        readonly NetworkVariable<int> droppedBy = new(-1);
        readonly NetworkVariable<bool> struggleHeld = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        int lastDrop;
'@
Rep '            All.Add(this);' @'
            All.Add(this);
            lastDrop = dropCount.Value;
'@
Rep '        void Update()
        {
            if (!IsSpawned || wobbly == null) return;
            if (!IsServer) wobbly.ApplyCustody((CustodyState)custody.Value, pinSecondsLeft.Value, bustedLevel.Value);
            if (IsOwner) { hidden.Value = wobbly.IsHidden; crouched.Value = wobbly.IsCrouching; aiming.Value = wobbly.IsAiming; return; }' @'
        /// <summary>Host: this player is dragged by this officer to the outpost, or (null) no longer.</summary>
        public void HostSetDragger(GuardAI g) { if (IsServer) draggedByGuard.Value = g ? g.NetId : -1; }

        /// <summary>Host: the struggle of the player an officer drags: struggling now, the stamina (0..1) and how many times the officer has shoved him.</summary>
        public void HostSetStruggle(bool active, float stamina01, int shoves)
        {
            if (!IsServer) return;
            struggleActive.Value = active;
            struggleStamina.Value = (byte)Mathf.Clamp(Mathf.RoundToInt(stamina01 * 100f), 0, 100);
            shoveCount.Value = (byte)(shoves & 255);
        }

        /// <summary>Host: this officer was hit and let go of the player (the drop is shown on every machine).</summary>
        public void HostSetDrop(GuardAI g, int count)
        {
            if (!IsServer) return;
            droppedBy.Value = g ? g.NetId : -1;
            dropCount.Value = (byte)(count & 255);
        }

        void Update()
        {
            if (!IsSpawned || wobbly == null) return;
            if (!IsServer)
            {
                wobbly.ApplyCustody((CustodyState)custody.Value, pinSecondsLeft.Value, bustedLevel.Value);
                wobbly.DraggedByGuard = draggedByGuard.Value >= 0 ? NetIds.Get<GuardAI>(draggedByGuard.Value) : null;
                wobbly.StruggleActive = struggleActive.Value; wobbly.StruggleStamina = struggleStamina.Value / 100f; wobbly.ShoveCount = shoveCount.Value;
                if (dropCount.Value != lastDrop) { lastDrop = dropCount.Value; wobbly.NoteDrop(droppedBy.Value >= 0 ? NetIds.Get<GuardAI>(droppedBy.Value) : null, lastDrop); }
            }
            if (IsOwner) { hidden.Value = wobbly.IsHidden; crouched.Value = wobbly.IsCrouching; aiming.Value = wobbly.IsAiming; struggleHeld.Value = wobbly.StruggleHeld; return; }
            wobbly.StruggleHeld = struggleHeld.Value;
'@
Save-Src

# ---------------------------------------------------------------- a teammate cannot take hold of a captive an officer drags
Open-Src ($A + 'Scripts\PlayerGrab.cs')
Rep '                if (!p || p == wob || p.Custody != CustodyState.Busted || p.DraggedBy != null) continue;' '                if (!p || p == wob || p.Custody != CustodyState.Busted || p.DraggedBy != null || p.DraggedByGuard != null) continue;'
Save-Src
Open-Src ($A + 'Scripts\Net\RunSync.Bodies.cs')
Rep 'vw.Custody != CustodyState.Busted || vw.DraggedBy != null) return;' 'vw.Custody != CustodyState.Busted || vw.DraggedBy != null || vw.DraggedByGuard != null) return;'
Save-Src

# ---------------------------------------------------------------- the protocol: new variables, a new guard state
Open-Src ($A + 'Scripts\Net\NetConst.cs')
Rep 'ProtocolVersion = "30";   // 30:' 'ProtocolVersion = "31";   // 31: (2026-10-28) the restraint, drag and rescue clips (an officer drags the arrested player by the head to the outpost: the guard state Dragging, the dragging officer, the struggle and the drop are published by the host; the struggle button is the owners). 30:'
Save-Src

# ---------------------------------------------------------------- the pure tests join PolicyTest
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            BulletDamageRulesTest();' @'
            BulletDamageRulesTest();
            RestraintRulesTest();
'@
Save-Src
"restraint 2 patched"

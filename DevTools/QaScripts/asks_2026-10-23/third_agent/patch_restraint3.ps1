# The owner's restraint, drag and rescue clips, stages 4 and 5 (2026-10-28): the rescue (a teammate holds E beside a hogtied captive; the free-up clip runs as the hold does; the ties come off; he is on his feet with the pin immunity) and the feedback
# (generated placeholder sounds, drag marks on the floor, the HUD's placeholder lines). Needs patch_restraint2.ps1 applied first.
#   SoundBank: DragScuff, StruggleGrunt, Shove, ZipRatchet (RestraintSound)
#   WobblyPlayer: FreeProgress, RescueTarget, RescuedFrame; the PlayerRescue component
#   NetPlayer: the progress (host) and the rescuer's target (owner) on every machine
#   HeistManager.FreeCaptive; CustodyHud: the lines for the captive
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the restraint sources' } else {
  foreach ($f in 'RestraintRules.cs','RestraintVisual.cs','GuardAI.Drag.cs','RestraintSound.cs','DragMarks.cs','PlayerRescue.cs') { Copy-Item (Join-Path $here ('restraint\' + $f)) ($A + 'Scripts\Restraint\' + $f) -Force }
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintRules.cs') ($A + 'Editor\NetDev\NetDevRestraintRules.cs') -Force
  Write-Output 'copied the restraint sources'
}

# ---------------------------------------------------------------- the sounds
Open-Src ($A + 'Scripts\Audio\SoundBank.cs')
Rep 'HitMarker, HitHead, HitKill }' 'HitMarker, HitHead, HitKill, DragScuff, StruggleGrunt, Shove, ZipRatchet }          // (from DragScuff: the restraint clips, 2026-10-28: generated placeholders, RestraintSound)'
Rep 'OutpostSound.HitVariants, OutpostSound.HitVariants, OutpostSound.HitVariants,' 'OutpostSound.HitVariants, OutpostSound.HitVariants, OutpostSound.HitVariants, RestraintSound.ScuffVariants, RestraintSound.StrainVariants, RestraintSound.ShoveVariants, RestraintSound.RatchetVariants,'
Rep '                case SoundId.HitKill: return OutpostSound.HitKill();' @'
                case SoundId.HitKill: return OutpostSound.HitKill();
                case SoundId.DragScuff: return RestraintSound.Scuff(variant);
                case SoundId.StruggleGrunt: return RestraintSound.Strain(variant);
                case SoundId.Shove: return RestraintSound.Shove(variant);
                case SoundId.ZipRatchet: return RestraintSound.Ratchet(variant);
'@
Save-Src

# ---------------------------------------------------------------- WobblyPlayer: the freeing
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep '        public void NoteDrop(GuardAI by, int count) { DropCount = count; DroppedBy = by; DropTime = Time.time; }' @'
        public void NoteDrop(GuardAI by, int count) { DropCount = count; DroppedBy = by; DropTime = Time.time; }
        /// <summary>How far a teammate has got in freeing this busted player, 0 .. 1 (the host counts it, PlayerRescue; every machine knows it).</summary>
        public float FreeProgress { get; set; }
        /// <summary>The busted player this one is freeing now (holding E beside him), or null: his own machine decides, every machine knows it.</summary>
        public WobblyPlayer RescueTarget { get; set; }
        /// <summary>The frame a teammate last worked on this player (the host: a captive nobody works on winds back).</summary>
        public int RescuedFrame { get; set; } = -999;
'@
Rep 'if (!GetComponent<RestraintVisual>()) gameObject.AddComponent<RestraintVisual>();' @'
if (!GetComponent<RestraintVisual>()) gameObject.AddComponent<RestraintVisual>();
            if (!GetComponent<PlayerRescue>()) gameObject.AddComponent<PlayerRescue>();                          // (a teammate frees him: hold E beside him)
'@
Save-Src

# ---------------------------------------------------------------- NetPlayer: the progress and the rescuer's target
Open-Src ($A + 'Scripts\Net\NetPlayer.cs')
Rep '        int lastDrop;' @'
        int lastDrop;
        // The freeing (the owner's restraint clips): how far a teammate has got (the host), and whom this player is freeing (his own machine: a slot, -1 none).
        readonly NetworkVariable<byte> freeProgress = new((byte)0);
        readonly NetworkVariable<int> rescueSlot = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
'@
Rep '            dropCount.Value = (byte)(count & 255);
        }' @'
            dropCount.Value = (byte)(count & 255);
        }

        /// <summary>Host: how far the teammates have got in freeing this busted player (0..1).</summary>
        public void HostSetFreeProgress(float progress01)
        {
            if (!IsServer) return;
            freeProgress.Value = (byte)Mathf.Clamp(Mathf.RoundToInt(progress01 * 100f), 0, 100);
        }

        static int SlotOrNone(WobblyPlayer p) => p && p.TryGetComponent(out NetPlayer np) && np.IsSpawned ? np.Slot : -1;
'@
Rep '                if (dropCount.Value != lastDrop) { lastDrop = dropCount.Value; wobbly.NoteDrop(droppedBy.Value >= 0 ? NetIds.Get<GuardAI>(droppedBy.Value) : null, lastDrop); }' @'
                if (dropCount.Value != lastDrop) { lastDrop = dropCount.Value; wobbly.NoteDrop(droppedBy.Value >= 0 ? NetIds.Get<GuardAI>(droppedBy.Value) : null, lastDrop); }
                wobbly.FreeProgress = freeProgress.Value / 100f;
'@
Rep 'struggleHeld.Value = wobbly.StruggleHeld; return; }' 'struggleHeld.Value = wobbly.StruggleHeld; rescueSlot.Value = SlotOrNone(wobbly.RescueTarget); return; }'
Rep '            wobbly.StruggleHeld = struggleHeld.Value;' @'
            wobbly.StruggleHeld = struggleHeld.Value;
            { var rp = rescueSlot.Value >= 0 ? BySlot(rescueSlot.Value) : null; wobbly.RescueTarget = rp ? rp.GetComponent<WobblyPlayer>() : null; }
'@
Save-Src

# ---------------------------------------------------------------- HeistManager: freeing a captive
Open-Src ($A + 'Scripts\HeistManager.cs')
Rep '        /// <summary>Vendor buy-back: the busted player is out (host). The caller already took the money.</summary>' @'
        /// <summary>
        /// A teammate cut a busted player free (the owner's restraint clips, 2026-10-28: he held E beside him for <see cref="RestraintRules.FreeSeconds"/>): the captive is on his feet again with the same few seconds in which nobody can pin him as after a break-free.
        /// Host / offline. The vendor's buy-back is unchanged.
        /// </summary>
        public bool FreeCaptive(WobblyPlayer p, WobblyPlayer by)
        {
            if (!p || !Net.NetRole.Simulates || p.Custody != CustodyState.Busted) return false;
            p.PinImmuneUntil = Time.time + CustodyRules.PinImmunitySeconds;
            p.FreeProgress = 0f;
            var np = p.GetComponent<Net.NetPlayer>();
            if (np && np.IsSpawned) np.HostSetFreeProgress(0f);
            SetCustody(p, CustodyState.Free, 0f, 0);
            Announce($"{Who(p, "was", "were")} freed");          // (PLACEHOLDER wording: the owner writes the words)
            return true;
        }

        /// <summary>Vendor buy-back: the busted player is out (host). The caller already took the money.</summary>
'@
Save-Src

# ---------------------------------------------------------------- CustodyHud: the lines for the captive (placeholders)
Open-Src ($A + 'Scripts\CustodyHud.cs')
Rep '                HudWidgets.CustodyMine(false, 0, 0f, $"Your team can buy you back from the vendor at the Loading Docks (${price})", price);' @'
                string help = p.DraggedByGuard != null ? "Hold SPACE to struggle" : p.FreeProgress > RestraintRules.FreeShownFrom ? "A teammate is freeing you" : $"Your team can buy you back from the vendor at the Loading Docks (${price})";          // (PLACEHOLDER wording for the first two: the owner writes the words)
                HudWidgets.CustodyMine(false, 0, 0f, help, price);
                if (p.DraggedByGuard != null)
                {
                    float s = Screen.height / 1080f;
                    HudWidgets.ProgressRing(new Vector2(Screen.width * 0.5f, Screen.height * 0.32f + 190f * s), 84f * s, 7f * s, p.StruggleStamina, HudTheme.Quota, HudTheme.Dim);          // (the struggle stamina)
                }
'@
Save-Src
"restraint 3 patched"

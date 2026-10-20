using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The guard outpost of the floor that is on show, as a whole: the locked front door and its keypad, the alarm, and the floor's
    /// safe. FloorGenerator builds the pieces and hands them over; this keeps the state (is the lock open, is the alarm going, is the
    /// safe open), does what the hack decides, and makes the keypad light, the siren and the flashing lamp follow the state.
    ///
    /// The host (or the offline game) decides: a finished hack is a request (<see cref="SubmitHack"/>); a won one opens the lock, a lost
    /// one sets the alarm off, which calls backup exactly as a guard who spots a player does (<see cref="HeistManager.CallBackupAt"/>):
    /// two officers at once and another every half minute until the floor ends (the alarm panel with the button that called them off, <see cref="AlarmPanelProp"/>, is not built any more: the owner
    /// had the button taken out on 2026-10-17; its code is still here in case it comes back). The outpost's door is locked by the hack (officers still open it: it is their outpost) and, as a hard lock that
    /// officers cannot open either, only with a code at the security terminal (<see cref="LockByCode"/>); shutting the door locks nothing. The state travels to the other machines as three bits
    /// (<see cref="Bits"/>, mirrored by RunSync), so everyone sees the same lock, hears the same siren and finds the same safe open.
    /// </summary>
    public sealed class OutpostController : MonoBehaviour
    {
        /// <summary>How long the siren sounds, and how long the lock stays jammed (a hack cannot be started meanwhile).</summary>
        public const float AlarmSeconds = 14f;
        /// <summary>A player farther from the keypad than this loses the hack in progress.</summary>
        public const float HackReach = 4.2f;
        /// <summary>The host accepts a hack result from a player this close to the keypad (the player's position is a moment old).</summary>
        public const float HostReach = 6.5f;
        /// <summary>The host accepts a press on the alarm panel from a player this close to it (the player's position is a moment old).</summary>
        public const float PanelHostReach = 5.5f;

        const int UnlockedBit = 1, AlarmBit = 2, SafeBit = 4, GunSafeBit = 8, MoreSafeBits = 16;      // (the first safe of a floor has the bit 4; the second, third and fourth have 16, 32 and 64)

        /// <summary>The outpost of the floor that is on show (null on a floor without one).</summary>
        public static OutpostController Active { get; private set; }

        // ---- what the builder fills in
        public int Floor;
        public int RunSeed;
        /// <summary>The middle of the keypad and the way it faces (out of the room, into the corridor).</summary>
        public Vector3 KeypadPosition;
        public Vector3 Outward = Vector3.forward;
        /// <summary>The locked front door and its place in the DoorManager's list.</summary>
        public SlidingDoor Door;
        public int DoorIndex = -1;
        /// <summary>The floor's safes, lowest number first (empty when it has none; floors 3 and up have several).</summary>
        public readonly List<FloorSafe> Safes = new List<FloorSafe>();
        /// <summary>The floor's first safe, if it has one.</summary>
        public FloorSafe Safe => Safes.Count > 0 ? Safes[0] : null;
        /// <summary>The lit parts of the keypad (they change colour with the state) and the lamp over the door.</summary>
        public Renderer[] LedRenderers;
        public Renderer BeaconRenderer;
        public Light BeaconLight;
        /// <summary>The middle of the alarm panel inside the room, and its lamps (green: all quiet, red: alarm or backup going).</summary>
        public Vector3 PanelPosition;
        public Renderer[] PanelLamps;

        /// <summary>The epoch of the floor this belongs to (the network state is tagged with it, so a floor never takes the last floor's state).</summary>
        public byte BuiltEpoch { get; private set; }

        // ---- state
        /// <summary>The gun safe on the desk (floor 3), or null: the pistol is inside until it is hacked open (<see cref="GunSafeProp"/>).</summary>
        public GunSafeProp GunSafe;
        public bool Unlocked { get; private set; }
        public bool AlarmOn { get; private set; }
        /// <summary>The lock is jammed for as long as the alarm sounds.</summary>
        public bool Jammed => AlarmOn;

        float bornAt, alarmUntil, nextSiren, panelFlashUntil;
        int panelKey = -1;
        MaterialPropertyBlock block;
        static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");

        static readonly Color LedLocked = new Color(1f, 0.12f, 0.08f), LedOpen = new Color(0.15f, 1f, 0.25f), LedOff = new Color(0.12f, 0.02f, 0.02f);
        static readonly Color PanelQuiet = new Color(0.12f, 0.75f, 0.2f), PanelAlert = new Color(1f, 0.12f, 0.06f), PanelDone = new Color(0.45f, 1f, 0.55f);

        /// <summary>The state as bits (lock open, alarm going, a gun safe open, and one bit for each floor safe that is open) for the other machines.</summary>
        public byte Bits
        {
            get
            {
                int bits = (Unlocked ? UnlockedBit : 0) | (AlarmOn ? AlarmBit : 0) | (GunSafe != null && GunSafe.IsOpen ? GunSafeBit : 0);
                for (int i = 0; i < Safes.Count; i++) if (Safes[i] && Safes[i].IsOpen) bits |= SafeBitOf(Safes[i].Index);
                return (byte)bits;
            }
        }

        /// <summary>The bit that says safe number <paramref name="index"/> is open: the first has its own, the next three the high ones (0: no bit for a fifth safe, there is none).</summary>
        static int SafeBitOf(int index) => index <= 0 ? SafeBit : index <= 3 ? MoreSafeBits << (index - 1) : 0;

        /// <summary>The line under the reticle (and the door's prompt) while the lock is shut; null when it is open.</summary>
        public string LockPrompt => Unlocked ? null : AlarmOn ? "The lock is jammed - the alarm is going off" : "[E] Hack the lock";

        void Awake()
        {
            Active = this;
            bornAt = Time.time;
            BuiltEpoch = Net.RunSync.Epoch;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        /// <summary>Called by the builder once the pieces are in: puts them in their first state.</summary>
        public void Begin()
        {
            block = new MaterialPropertyBlock();
            if (Door != null) Door.Locked = true;
            UpdateVisuals(0f);
            UpdatePanel(0f);
        }

        // ------------------------------------------------------------------ the local player works the lock

        /// <summary>The local player pressed E on the keypad or on the door: start the hack (unless the lock is open or jammed).</summary>
        public void UseLockedDoor(WobblyPlayer who)
        {
            if (!who || Unlocked) return;
            if (AlarmOn) { Backpack.NoticeToLocal("The lock is jammed - wait for the alarm to stop"); return; }
            HackPanel.Begin(this, who);
        }

        /// <summary>The local player's hack ended: offline it is settled here, in a networked run the host settles it.</summary>
        public void SubmitHack(bool success)
        {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestHack(success);
            else HostHackResult(success);
        }

        /// <summary>Host check: is this player close enough to the keypad to have hacked it?</summary>
        public bool InHackReach(Vector3 playerPosition) =>
            (playerPosition - KeypadPosition).sqrMagnitude <= HostReach * HostReach;

        /// <summary>Host (or offline): a hack finished. A won one opens the lock; a lost one sets off the alarm (once; not while it is going).</summary>
        public void HostHackResult(bool success)
        {
            if (!Net.NetRole.Simulates || Unlocked) return;
            if (success) { SetUnlocked(); return; }
            if (AlarmOn) return;
            alarmUntil = Time.time + AlarmSeconds;
            SetAlarm(true);
            HeistManager.Instance?.CallBackupAt(KeypadPosition, "ALARM TRIPPED - BACKUP CALLED");
        }

        /// <summary>Host (or offline): something other than the lock went wrong at <paramref name="at"/> (the gun safe's hack hit a wall): the siren sounds and backup is called, exactly as for a failed lock hack.</summary>
        public void HostAlarmAt(Vector3 at)
        {
            if (!Net.NetRole.Simulates || AlarmOn) return;
            alarmUntil = Time.time + AlarmSeconds;
            SetAlarm(true);
            HeistManager.Instance?.CallBackupAt(at, "ALARM TRIPPED - BACKUP CALLED");
        }

        // ------------------------------------------------------------------ the alarm panel inside the room

        /// <summary>Is there anything for the alarm panel to switch off: the siren, or the backup that is on its way (called by the siren or by an officer)?</summary>
        public bool AlertOn
        {
            get
            {
                if (AlarmOn) return true;
                var hm = HeistManager.Instance;
                return hm != null && hm.Phase == HeistPhase.Heist && hm.BackupActive;
            }
        }

        /// <summary>The line under the reticle when the crosshair is on the panel.</summary>
        public string PanelPrompt => AlertOn ? "[E] Shut off the alarm and call off the backup" : "Alarm panel - all quiet";

        /// <summary>The local player pressed E on the panel's button.</summary>
        public void UsePanel(WobblyPlayer who)
        {
            if (!who) return;
            if (!AlertOn) { Backpack.NoticeToLocal("Nothing to switch off - the alarm is quiet"); return; }
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestStandDown();
            else if (HostStandDown()) PlayStandDown();
        }

        /// <summary>Host check: is this player close enough to the panel to have pressed it?</summary>
        public bool InPanelReach(Vector3 playerPosition) =>
            PanelLamps != null && PanelLamps.Length > 0 &&                           // (no panel is built any more: there is nothing to reach, so the host never accepts a press, wherever the player stands)
            (playerPosition - PanelPosition).sqrMagnitude <= PanelHostReach * PanelHostReach;

        /// <summary>
        /// Host (or offline): the button was pressed. The siren stops and the lock is free again, and the backup is called off
        /// (<see cref="HeistManager.StandDown"/>: no more officers are called, whoever started it). Returns false when there was nothing going.
        /// </summary>
        public bool HostStandDown()
        {
            if (!Net.NetRole.Simulates) return false;
            bool hadAlarm = AlarmOn;
            var hm = HeistManager.Instance;
            bool hadBackup = hm != null && hm.StandDown();
            if (hadAlarm) { alarmUntil = 0f; SetAlarm(false); }
            return hadAlarm || hadBackup;
        }

        /// <summary>On every machine, when the button was pressed: a relay clack and the siren dying away, and the panel's lamps flash.</summary>
        public void PlayStandDown()
        {
            panelFlashUntil = Time.time + 1.5f;
            GameAudio.PlaySound(SoundId.AlarmOff, PanelPosition, 1f, 1f, true, 32f, 3f);
            UpdatePanel(Time.time);
        }

        /// <summary>The panel's lamps: green while all is quiet, flashing red while the alarm or the backup is going, bright green a moment after it was switched off.</summary>
        void UpdatePanel(float now)
        {
            if (PanelLamps == null || PanelLamps.Length == 0) return;
            int state = now < panelFlashUntil ? 2 : AlertOn ? 1 : 0;
            int key = state == 1 ? 10 + ((int)(now * 6f) & 1) : state;
            if (key == panelKey) return;
            panelKey = key;
            Color c = state == 0 ? PanelQuiet : state == 2 ? PanelDone : (key & 1) == 0 ? PanelAlert : LedOff;
            for (int i = 0; i < PanelLamps.Length; i++) SetEmission(PanelLamps[i], c);
        }

        // ------------------------------------------------------------------ state

        bool Quiet => Time.time - bornAt < 2.5f;        // (state that was already there when this machine built the floor: no sounds)

        void SetUnlocked()
        {
            if (Unlocked) return;
            Unlocked = true;
            if (Door != null)
            {
                Door.Locked = false;
                Door.SetComputerLocked(false, Time.time);
                if (Quiet) Door.SnapLatched(); else Door.SetOpen(true, Time.time);
            }
            if (AlarmOn) { AlarmOn = false; UpdateBeacon(); }
            if (!Quiet) GameAudio.PlaySound(SoundId.HackWin, KeypadPosition, 0.9f, 1f, true, 32f, 3f);
            UpdateVisuals(Time.time);
        }

        void SetAlarm(bool on)
        {
            if (AlarmOn == on) return;
            AlarmOn = on;
            nextSiren = 0f;
            if (on && !Quiet) GameAudio.PlaySound(SoundId.HackFail, KeypadPosition, 1f, 1f, true, 40f, 3f);
            UpdateBeacon();
            UpdateVisuals(Time.time);
        }

        /// <summary>Is this spot inside the outpost room (on the far side of the keypad's wall)?</summary>
        public bool IsInside(Vector3 p)
        {
            if (HasRoomBounds) return RoomBounds.Contains(p.x, p.z);                 // (the inside of the room: the walls, and so the doorway, stand outside it)
            return Vector3.Dot(p - KeypadPosition, Outward) < -0.5f && (p - KeypadPosition).sqrMagnitude < 144f;      // (no room known: the room's side of the wall, within 12 m)
        }

        /// <summary>The inside of the outpost room (world rectangle), set by the builder.</summary>
        public Rc RoomBounds;
        public bool HasRoomBounds;

        /// <summary>Is this spot inside the room, or within <paramref name="slack"/> metres of it (the host sees a player where he stood a moment ago)?</summary>
        public bool IsInsideNear(Vector3 p, float slack = 1.5f)
        {
            if (HasRoomBounds) return RoomBounds.Grow(slack).Contains(p.x, p.z);
            return IsInside(p);
        }

        /// <summary>
        /// A player locked the outpost's door with a code (the security terminal's LOCK, typed at the computer inside the room): the lock is shut again and it is a hard one, so the officers cannot open the
        /// door either; somebody has to hack the keypad from the corridor, or undo it from inside (E on the door, or UNLOCK at the terminal). Shutting the door locks nothing: the officers open a shut door
        /// that nobody has locked (the owner, 2026-10-18: "guards should be able to open the outpost door as long as it is not locked from the inside by a player ... you have to lock the door with a code").
        /// </summary>
        public void LockByCode()
        {
            if (!Unlocked && Door != null && Door.ComputerLocked) return;           // (it is locked this way already: no second clang)
            Unlocked = false;
            if (Door != null)
            {
                Door.Locked = true;
                Door.SetComputerLocked(true, Time.time);
                if (!Quiet) GameAudio.PlaySound(SoundId.MetalLock, Door.Centre + Vector3.up, 0.6f, 1.1f, true, 24f, 3f);
            }
            UpdateVisuals(Time.time);
        }

        /// <summary>
        /// Host (or offline): the terminal's LOCK / UNLOCK for the outpost's own door. UNLOCK only undoes a lock set by a code, and opens the door as a hack and E from inside do (the clients follow the host
        /// through the state bits, and a rising Unlocked bit opens the door on their side: the host must have it open too). Clients get the new state with the run state (<see cref="NetApply"/>).
        /// </summary>
        public void HostLockByCode(bool locked)
        {
            if (!Net.NetRole.Simulates) return;
            if (locked) LockByCode();
            else if (!Unlocked && Door != null && Door.ComputerLocked) UnlockByCode(true);
        }

        /// <summary>A player inside works the locked door with E: the lock opens and so does the door.</summary>
        public void UnlockFromInside() => UnlockByCode(true);

        /// <summary>The lock is undone (E on the door from inside, or UNLOCK at the terminal); <paramref name="open"/> slides the door open as well.</summary>
        void UnlockByCode(bool open)
        {
            if (Unlocked) return;
            Unlocked = true;
            if (Door != null)
            {
                Door.Locked = false;
                Door.SetComputerLocked(false, Time.time);
                if (open) Door.SetOpen(true, Time.time);
            }
            UpdateVisuals(Time.time);
        }

        /// <summary>Client: take the host's state (a bit for each of: the lock is open, the alarm is going, the safe is open).</summary>
        public void NetApply(byte bits)
        {
            if ((bits & UnlockedBit) != 0) SetUnlocked();
            else if (Unlocked) LockByCode();                                        // (the host locked it with a code)
            if (!Unlocked && ((bits & AlarmBit) != 0) != AlarmOn) SetAlarm((bits & AlarmBit) != 0);
            for (int i = 0; i < Safes.Count; i++)
            {
                var safe = Safes[i];
                int bit = safe ? SafeBitOf(safe.Index) : 0;
                if (bit != 0 && (bits & bit) != 0 && !safe.IsOpen) safe.ApplyOpen(Quiet);
            }
            if ((bits & GunSafeBit) != 0 && GunSafe != null && !GunSafe.IsOpen) GunSafe.ApplyOpen(true);
        }

        // ------------------------------------------------------------------ looks and sounds

        void Update()
        {
            float now = Time.time;
            if (AlarmOn)
            {
                if (Net.NetRole.Simulates && now >= alarmUntil) SetAlarm(false);
                else if (now >= nextSiren)
                {
                    nextSiren = now + OutpostSound.AlarmSeconds - 0.05f;
                    GameAudio.PlaySound(SoundId.Alarm, KeypadPosition + Vector3.up * 1.2f, 0.9f, 1f, true, 70f, 6f);
                }
            }
            if (AlarmOn) UpdateVisuals(now);
            UpdatePanel(now);
        }

        void UpdateBeacon()
        {
            if (BeaconLight) BeaconLight.enabled = AlarmOn;
            if (!AlarmOn) SetEmission(BeaconRenderer, new Color(0.25f, 0.02f, 0.02f));
        }

        void UpdateVisuals(float now)
        {
            Color led;
            if (Unlocked) led = LedOpen;
            else if (AlarmOn) led = (int)(now * 8f) % 2 == 0 ? LedLocked : LedOff;       // flashes
            else led = LedLocked * 0.8f;
            if (LedRenderers != null)
                for (int i = 0; i < LedRenderers.Length; i++) SetEmission(LedRenderers[i], led);
            if (AlarmOn)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 9f);
                if (BeaconLight) BeaconLight.intensity = 0.8f + 2.2f * pulse;
                SetEmission(BeaconRenderer, new Color(1f, 0.1f, 0.05f) * (0.35f + 0.65f * pulse));
            }
        }

        /// <summary>Colours one renderer's glow without touching its (shared) material.</summary>
        void SetEmission(Renderer r, Color colour)
        {
            if (!r) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, colour);
            block.SetColor(EmissionId, colour * 1.8f);
            r.SetPropertyBlock(block);
        }
    }
}

using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    public partial class RunSync
    {
        // Mirror of HeistManager + elevator flags. The host writes, clients read.
        readonly NetworkVariable<int> vPhase = new(), vFloor = new(), vTotal = new(), vQuota = new(), vBanked = new(),
            vLost = new(), vCaught = new(), vRunTotal = new(), vCash = new(), vSurplusPaid = new(), vResume = new(), vTimeLeft = new();
        readonly NetworkVariable<int> vBackupIn = new(-1);   // seconds until the next backup officer, -1 while none has been called
        readonly NetworkVariable<bool> vBackupCalled = new(), vAboard = new(), vSealing = new(), vBigAlarm = new();      // (the Big Alarm: the owner, 2026-10-24)
        // service elevator panel, floor 12 back door
        readonly NetworkVariable<bool> vGoingUp = new(true), vBackOpen = new();
        // the doors backup comes out of: bit 0 the guards' elevator, bit 1 and 2 the two stairwells
        readonly NetworkVariable<byte> vEntriesOpen = new();
        readonly NetworkVariable<int> vDestination = new(-1);
        // the guns the party has found and brought back (RunUnlocks): the host decides, everybody's dealer reads it
        readonly NetworkVariable<int> vUnlocks = new();
        // The epoch of the floor the host has finished building. Until it matches this machine's epoch the
        // mirrored values still belong to the previous floor, so they are not applied.
        readonly NetworkVariable<byte> vReadyEpoch = new();

        /// <summary>Host: the new floor is built and the run state describes it.</summary>
        public void MarkFloorReady()
        {
            if (IsServer) vReadyEpoch.Value = Epoch;
        }

        void HostPublishState()
        {
            var hm = HeistManager.Instance;
            if (hm == null) return;
            vPhase.Value = (int)hm.Phase;
            vFloor.Value = hm.Floor;
            vTotal.Value = hm.TotalFloors;
            vQuota.Value = hm.Quota;
            vBanked.Value = hm.Banked;
            vLost.Value = hm.Lost;
            vCaught.Value = hm.Caught;
            vRunTotal.Value = hm.RunTotal;
            vCash.Value = hm.Cash;
            vSurplusPaid.Value = hm.SurplusPaid;
            vTimeLeft.Value = Mathf.CeilToInt(Mathf.Max(0f, hm.TimeLeft)); // changes once a second
            vBackupCalled.Value = hm.BackupCalled;
            vBackupIn.Value = hm.BackupIn;
            vBigAlarm.Value = hm.BigAlarmActive;
            vAboard.Value = ElevatorExtraction.EveryoneAboard;
            vSealing.Value = ElevatorExtraction.Sealing;
            vGoingUp.Value = ElevatorExtraction.GoingUp;
            vDestination.Value = ElevatorExtraction.Destination;
            vBackOpen.Value = ElevatorExtraction.BackDoorOpen;
            vUnlocks.Value = RunUnlocks.Mask;
            var gen0 = TowerManager.Instance != null ? TowerManager.Instance.generator : null;
            vEntriesOpen.Value = gen0 != null ? gen0.EntryDoorMask : (byte)0;
            var tm = TowerManager.Instance;
            if (tm != null) vResume.Value = tm.ResumeLevel;
            HostPublishOutpost();
            HostPublishPa();
        }

        void ClientApplyState()
        {
            RunUnlocks.NetSet(vUnlocks.Value);                    // (not tied to a floor: what was brought back stays unlocked)
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            if (hm == null || (tm != null && tm.Loading)) return; // a floor is being built; the next update is the right one
            if (vReadyEpoch.Value != Epoch) return;               // the host has not finished the same floor yet
            hm.ApplyNet((HeistPhase)vPhase.Value, vFloor.Value, vTotal.Value, vQuota.Value, vBanked.Value, vLost.Value,
                        vCaught.Value, vRunTotal.Value, vCash.Value, vSurplusPaid.Value, vTimeLeft.Value, vBackupCalled.Value, vBackupIn.Value, vBigAlarm.Value);
            ElevatorExtraction.NetSetFlags(vAboard.Value, vSealing.Value);
            ElevatorExtraction.NetSetPanel(vGoingUp.Value, vDestination.Value, vBackOpen.Value);
            if (tm != null && tm.generator != null) tm.generator.NetEntryDoors(vEntriesOpen.Value);
            if (tm != null) tm.NetSetResume(vResume.Value);
            ClientApplyOutpost();
            ClientApplyPa();
        }
    }
}

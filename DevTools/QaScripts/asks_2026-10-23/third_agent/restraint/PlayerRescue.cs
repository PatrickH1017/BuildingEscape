using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A teammate frees a hogtied captive (the owner's restraint, drag and rescue clips, 2026-10-28): he holds E beside a busted player who lies there (not dragged by an officer or a teammate, not being dropped by one) for
    /// <see cref="RestraintRules.FreeSeconds"/>. The clip (Free_Hogtied) runs as the hold does, so the ties come off one by one and a let-go winds it back; at the end the captive is on his feet with the same few seconds of pin immunity as after a break-free
    /// (<see cref="HeistManager.FreeCaptive"/>). The buy-back at the vendor is unchanged.
    /// The rescuer's own machine reads the button and says whom he works on (<see cref="WobblyPlayer.RescueTarget"/>, carried by <c>NetPlayer</c>); the host (or the offline game) counts the progress
    /// (<see cref="WobblyPlayer.FreeProgress"/>, carried back the same way). An item, a door or the dealer under the crosshair takes E first. The rescuer is put beside the captive when he starts and cannot walk while he holds.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class PlayerRescue : MonoBehaviour
    {
        /// <summary>Tests: the owner holds E (the key itself is read by the machine that owns the body).</summary>
        public static bool TestHold;

        WobblyPlayer wob;
        PlayerGrab grab;
        Net.NetPlayer net;
        bool lockedByMe, snapped;
        int lastPublished = -1;

        /// <summary>A captive within reach whom E would free (the owner's machine; for the prompt and the tests).</summary>
        public WobblyPlayer Candidate { get; private set; }
        /// <summary>This player may free somebody now, and is doing it (the owner's machine; for the tests).</summary>
        public bool Able { get; private set; }
        public bool Working { get; private set; }

        void Awake() { TryGetComponent(out wob); TryGetComponent(out grab); TryGetComponent(out net); }

        /// <summary>The nearest busted player this one could free now: within reach, lying (nobody drags him, no officer was just hit by a shot that dropped him).</summary>
        public static WobblyPlayer FindCaptive(WobblyPlayer me)
        {
            WobblyPlayer best = null;
            float bestD = RestraintRules.FreeReach;
            var all = WobblyPlayerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (!p || p == me || !CanBeFreed(p)) continue;
                var d = p.transform.position - me.transform.position; d.y = 0f;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = p; }
            }
            return best;
        }

        /// <summary>Busted, lying, not dragged by an officer or a teammate, not in the middle of a drop.</summary>
        public static bool CanBeFreed(WobblyPlayer p) =>
            p != null && p.Custody == CustodyState.Busted && p.DraggedBy == null && p.DraggedByGuard == null && Time.time - p.DropTime >= RestraintRules.DropSeconds;

        void Update()
        {
            if (wob == null) return;
            bool mine = Net.NetPlayer.IsLocalPlayer(wob);
            if (mine) ReadInput();
            if (Net.NetRole.Simulates) HostStep(Time.deltaTime);
        }

        void ReadInput()
        {
            Candidate = null;
            bool able = wob.Custody == CustodyState.Free && !wob.IsHidden && !wob.IsStumbling && !Crosshair.MenuOpen && (grab == null || (!grab.Held && !grab.IsDragging));
            if (able) Candidate = FindCaptive(wob);
            bool key = Input.GetKey(KeyCode.E) || TestHold;
            bool aimFree = TestHold || Crosshair.Aim.Kind == AimKind.None;               // (an item, a door or the dealer under the crosshair takes E first)
            bool working = able && Candidate != null && key && (aimFree || wob.RescueTarget == Candidate);
            Able = able; Working = working;
            if (working)
            {
                if (!snapped) { snapped = true; Beside(Candidate); }
                wob.RescueTarget = Candidate;
                if (!wob.inputLocked) { wob.inputLocked = true; lockedByMe = true; }
            }
            else
            {
                snapped = false;
                wob.RescueTarget = null;
                if (lockedByMe) { wob.inputLocked = false; lockedByMe = false; }
            }
        }

        /// <summary>The rescuer starts beside the captive, on his right (the clip kneels him there), so the camera is where the clip is.</summary>
        void Beside(WobblyPlayer captive)
        {
            var t = captive.transform;
            var yaw = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            var spot = t.position + yaw * new Vector3(RestraintRules.RescuerStandOff, 0f, -RestraintRules.RescuerBack);
            if (net && net.IsSpawned) net.PlaceAt(spot, yaw); else wob.TeleportTo(spot, yaw);
        }

        /// <summary>The host's (and the offline game's) count of the freeing: the rescuer's seconds go into his captive, a captive nobody works on winds back, and a full hold frees him.</summary>
        void HostStep(float dt)
        {
            var t = wob.RescueTarget;
            if (t != null)
            {
                bool ok = wob.Custody == CustodyState.Free && CanBeFreed(t);
                var d = t.transform.position - wob.transform.position; d.y = 0f;
                ok &= d.magnitude <= RestraintRules.FreeReach + 0.9f;
                if (ok)
                {
                    t.FreeProgress = RestraintRules.FreeStep(t.FreeProgress, dt, true);
                    t.RescuedFrame = Time.frameCount;
                    Publish(t);
                    if (t.FreeProgress >= 1f) HeistManager.Instance?.FreeCaptive(t, wob);
                }
            }
            if (wob.Custody != CustodyState.Busted) { if (wob.FreeProgress != 0f) { wob.FreeProgress = 0f; Publish(wob); } return; }
            if (wob.FreeProgress > 0f && Time.frameCount - wob.RescuedFrame > 1)
            {
                wob.FreeProgress = RestraintRules.FreeStep(wob.FreeProgress, dt, false);                // (nobody holds: it winds back)
                Publish(wob);
            }
        }

        void Publish(WobblyPlayer p)
        {
            var np = p.GetComponent<Net.NetPlayer>();
            if (np && np.IsSpawned) np.HostSetFreeProgress(p.FreeProgress);
        }

        void OnDisable()
        {
            if (wob != null && lockedByMe) { wob.inputLocked = false; lockedByMe = false; }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || wob == null || !Net.NetPlayer.IsLocalPlayer(wob) || Candidate == null) return;
            if (Crosshair.MenuOpen || wob.Custody != CustodyState.Free) return;
            float s = Screen.height / 1080f;
            float y = Screen.height * 0.62f;
            if (wob.RescueTarget == Candidate && Candidate.FreeProgress > RestraintRules.FreeShownFrom)
                HudWidgets.ProgressRing(new Vector2(Screen.width * 0.5f, y), 96f * s, 8f * s, Candidate.FreeProgress, HudTheme.Quota, HudTheme.Dim);
            else
                HudWidgets.Prompt("Hold E to free", y);                                                  // PLACEHOLDER wording: the owner writes the words
        }
    }
}

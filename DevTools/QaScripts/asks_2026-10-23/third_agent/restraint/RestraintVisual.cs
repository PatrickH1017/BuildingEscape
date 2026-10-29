using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows a player or an officer's body in the owner's restraint, drag and rescue clips (2026-10-28), on every machine, from the state the game already keeps and replicates: a pinned player is tied up (<see cref="RestraintClips.TieUp"/>, once) and then lies
    /// hogtied (the loop); a hogtied teammate dragged by the head, an eliminated officer dragged by the foot: the dragger and the dragged play the two halves of one scene (the dragged body is put where the dragger's model is, and plays the clip at the dragger's time, so
    /// the hand and what it holds meet as they do in Blender); the officer's own drag, the struggle, the drop and the free-up are the later stages of the same component. The clips are full-body: <see cref="CharacterAnimator.PlayBody"/> puts them over everything the
    /// character was doing, the weapons in his hands are put away meanwhile, and the zip ties (<see cref="ZipTies"/>) come on and off by the clip's time. A character who cannot play them (a look that is not a humanoid in clip mode, or a clip missing from the build)
    /// is shown the old way (knocked down).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed class RestraintVisual : MonoBehaviour
    {
        public enum Look
        {
            None, TieUp, Hogtied,
            DragHeadDragger, DragHeadDragged, DragFootDragger, DragFootDragged,
            GuardDragGuard, GuardDragDragged, GuardDragStruggleDragged, GuardShotGuard, GuardShotDragged, FreeUpRescuer, FreeUpFreed,
        }

        /// <summary>Every restraint visual on this machine (for the tests).</summary>
        public static readonly List<RestraintVisual> All = new List<RestraintVisual>();

        WobblyPlayer wob;
        GuardAI guard;
        PlayerGrab grab;
        DraggableBody body;
        Rigidbody rb;
        CapsuleCollider capsule;
        PlayerVisual pv;
        GuardVisual gv;
        CharacterAnimator anim;
        Vector3 restPos;
        Quaternion restRot;
        bool restSaved;
        float tieStart = -999f, lookStarted, clock;
        Look look;
        AnimationClip clip;
        ZipTies ties;
        // ---- what it sounds and looks like (placeholders: RestraintSound, DragMarks)
        float nextScuff, nextStrain;
        int lastShove = -1;
        bool tiesSeen, prevW, prevK, prevL;
        Vector3 lastMarkAt;
        bool haveMark;
        /// <summary>How many scuffs, strains, shoves and ratchets this body made (for the tests).</summary>
        public int Scuffs { get; private set; }
        public int Strains { get; private set; }
        public int Shoves { get; private set; }
        public int Ratchets { get; private set; }

        /// <summary>What this body shows now.</summary>
        public Look Current => look;
        /// <summary>The character's animation driver (null while he has none).</summary>
        public CharacterAnimator Anim => anim;
        /// <summary>The clip's time (s) this frame: a dragger's clock, which the dragged body takes.</summary>
        public float PairTime { get; private set; }
        /// <summary>Where the model is held (for the tests).</summary>
        public Transform ModelRoot => anim ? anim.transform : null;
        public AnimationClip ClipNow => clip;
        public ZipTies Ties => ties;

        public static string NameOf(Look l)
        {
            switch (l)
            {
                case Look.TieUp: return RestraintClips.TieUp;
                case Look.Hogtied: return RestraintClips.Hogtied;
                case Look.DragHeadDragger: return RestraintClips.DragHeadDragger;
                case Look.DragHeadDragged: return RestraintClips.DragHeadDragged;
                case Look.DragFootDragger: return RestraintClips.DragFootDragger;
                case Look.DragFootDragged: return RestraintClips.DragFootDragged;
                case Look.GuardDragGuard: return RestraintClips.GuardDragGuard;
                case Look.GuardDragDragged: return RestraintClips.GuardDragDragged;
                case Look.GuardDragStruggleDragged: return RestraintClips.GuardDragStruggleDragged;
                case Look.GuardShotGuard: return RestraintClips.GuardShotGuard;
                case Look.GuardShotDragged: return RestraintClips.GuardShotDragged;
                case Look.FreeUpRescuer: return RestraintClips.FreeUpRescuer;
                case Look.FreeUpFreed: return RestraintClips.FreeUpFreed;
                default: return null;
            }
        }

        static bool IsDragger(Look l) => l == Look.DragHeadDragger || l == Look.DragFootDragger || l == Look.GuardDragGuard;
        static bool IsDragged(Look l) => l == Look.DragHeadDragged || l == Look.DragFootDragged || l == Look.GuardDragDragged || l == Look.GuardDragStruggleDragged || l == Look.GuardShotDragged || l == Look.FreeUpRescuer;
        static bool LyingSolo(Look l) => l == Look.TieUp || l == Look.Hogtied || l == Look.FreeUpFreed;

        void Awake()
        {
            TryGetComponent(out wob); TryGetComponent(out guard); TryGetComponent(out grab); TryGetComponent(out body);
            TryGetComponent(out rb); TryGetComponent(out capsule); TryGetComponent(out pv); TryGetComponent(out gv);
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (wob) wob.CustodyChanged += OnCustody;
        }

        void OnDisable()
        {
            All.Remove(this);
            if (wob) wob.CustodyChanged -= OnCustody;
            Stop();
        }

        void OnCustody(CustodyState s) { if (s == CustodyState.Pinned) tieStart = Time.time; }

        /// <summary>What this body should be showing this frame.</summary>
        Look Compute()
        {
            if (wob != null)
            {
                if (wob.InCustody)
                {
                    if (wob.Custody == CustodyState.Busted)
                    {
                        if (wob.DraggedByGuard != null) return wob.StruggleActive ? Look.GuardDragStruggleDragged : Look.GuardDragDragged;          // (an officer drags him by the head to the outpost)
                        if (wob.DraggedBy != null) return Look.DragHeadDragged;
                        if (wob.DroppedBy != null && Time.time - wob.DropTime < RestraintRules.DropSeconds) return Look.GuardShotDragged;           // (the officer was hit: he let go of him)
                        if (wob.FreeProgress > RestraintRules.FreeShownFrom) return Look.FreeUpFreed;                                                // (a teammate holds the button beside him: the ties come off)
                    }
                    if (wob.Custody == CustodyState.Pinned && Time.time - tieStart < RestraintRules.Seconds(RestraintRules.TieUpFrames)) return Look.TieUp;
                    return Look.Hogtied;
                }
                var rt = wob.RescueTarget;
                if (rt != null && rt.Custody == CustodyState.Busted && rt.FreeProgress > RestraintRules.FreeShownFrom) return Look.FreeUpRescuer;          // (he kneels beside the captive and cuts the ties)
                if (grab != null)
                {
                    if (grab.DraggingPlayer != null) return Look.DragHeadDragger;
                    if (grab.Dragging != null) return Look.DragFootDragger;
                }
                return Look.None;
            }
            if (guard != null)
            {
                if (!body) TryGetComponent(out body);
                if (guard.Dead) return body != null && body.IsDragged ? Look.DragFootDragged : Look.None;
                if (Captive(false) != null) return Look.GuardDragGuard;
                if (Captive(true) != null) return Look.GuardShotGuard;
            }
            return Look.None;
        }

        /// <summary>The player this officer drags by the head (<paramref name="dropped"/> false), or the one he let go of a moment ago when he was hit (true; the drop clip is still running), found on every machine through what the player carries.</summary>
        WobblyPlayer Captive(bool dropped)
        {
            var all = WobblyPlayerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (!p || p.Custody != CustodyState.Busted) continue;
                if (!dropped) { if (p.DraggedByGuard == guard) return p; }
                else if (p.DroppedBy == guard && Time.time - p.DropTime < RestraintRules.DropSeconds) return p;
            }
            return null;
        }

        void Update()
        {
            var a = pv ? pv.Anim : gv ? gv.Anim : null;
            if (a != anim) { Stop(); anim = a; restSaved = false; ties = null; }
            if (!anim || !anim.CanPlayBody) return;
            if (wob != null) NoticeShove();
            var want = Compute();
            if (want != look) Begin(want);
            if (look == Look.None || !clip) return;
            Tick();
        }

        void Begin(Look want)
        {
            var was = look;
            if (want == Look.None) { Stop(); return; }
            var c = RestraintClips.Get(NameOf(want));
            if (!c) { Stop(); return; }                                        // (not in the build: the old knocked-down pose)
            look = want; clip = c;
            if (!restSaved) { restPos = anim.transform.localPosition; restRot = anim.transform.localRotation; restSaved = true; }
            anim.PlayBody(c);
            lookStarted = Time.time;
            if (was == Look.None && wob != null && Net.NetPlayer.IsLocalPlayer(wob) && (LyingSolo(want) || want == Look.FreeUpRescuer))
            {
                var rig = CameraFollow.Current;
                if (rig) rig.SetFirstPerson(false);                            // (the view of a body on the floor is the third person's)
            }
        }

        /// <summary>No clip: the character goes back to his own animation and his own place.</summary>
        void Stop()
        {
            look = Look.None; clip = null;
            if (anim)
            {
                anim.StopBody();
                if (restSaved && anim.transform) { anim.transform.localPosition = restPos; anim.transform.localRotation = restRot; }
            }
            if (ties) ties.Set(false, false, false);
            tiesSeen = false; haveMark = false;
        }

        /// <summary>The officer shoved the captive: a thud, and a grunt (the host counts the shoves; a change is one, but not the first sight of the number).</summary>
        void NoticeShove()
        {
            int n = wob.ShoveCount;
            if (lastShove >= 0 && n != lastShove)
            {
                Shoves++;
                var at = anim.transform.position + Vector3.up * 0.3f;
                GameAudio.PlaySound(SoundId.Shove, at, 0.9f, Random.Range(0.92f, 1.06f), true, RestraintRules.HearRange, 2f);
                GameAudio.PlaySound(SoundId.StruggleGrunt, at, 0.7f, 0.85f, true, RestraintRules.HearRange, 2f);
            }
            lastShove = n;
        }

        /// <summary>The sounds and the marks of the dragged body and the struggle: a scuff and a floor mark every step of the drag, the captive's strain every second while he struggles (no noise reaches the officers: nothing here makes a <c>NoiseEvent</c>).</summary>
        void Feel()
        {
            if (!anim) return;
            var at = anim.transform.position + Vector3.up * 0.2f;
            if (IsDragged(look) && look != Look.GuardShotDragged && look != Look.FreeUpRescuer)
            {
                var p = Partner();
                float rate = RestraintRules.DragRate(p ? p.GroundSpeed() : 0f);
                if (rate > 0f && Time.time >= nextScuff)
                {
                    nextScuff = Time.time + 0.6f / rate;
                    Scuffs++;
                    GameAudio.PlaySound(SoundId.DragScuff, at, 0.55f, Random.Range(0.9f, 1.1f), true, RestraintRules.HearRange, 2f);
                    var floorAt = anim.transform.position + anim.transform.forward * -0.4f;
                    var along = haveMark ? floorAt - lastMarkAt : anim.transform.forward;
                    if (!haveMark || along.sqrMagnitude > 0.09f) { DragMarks.Add(floorAt, along); lastMarkAt = floorAt; haveMark = true; }
                }
            }
            if (look == Look.GuardDragStruggleDragged && Time.time >= nextStrain)
            {
                nextStrain = Time.time + 0.95f;
                Strains++;
                GameAudio.PlaySound(SoundId.StruggleGrunt, at, 0.8f, Random.Range(0.92f, 1.08f), true, RestraintRules.HearRange, 2f);
            }
        }

        float GroundSpeed()
        {
            if (guard != null) return guard.CurrentSpeed;                      // (the agent's speed; a puppet has the host's)
            Vector3 v = wob && wob.VelocityOverride.HasValue ? wob.VelocityOverride.Value : rb && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;
            v.y = 0f;
            return v.magnitude;
        }

        RestraintVisual Partner()
        {
            if (wob != null && wob.RescueTarget != null) return wob.RescueTarget.GetComponent<RestraintVisual>();
            if (wob != null && wob.DraggedByGuard != null) return wob.DraggedByGuard.GetComponent<RestraintVisual>();
            if (wob != null && wob.DroppedBy != null) return wob.DroppedBy.GetComponent<RestraintVisual>();
            WobblyPlayer p = null;
            if (wob != null) p = wob.DraggedBy;
            else if (body != null) p = body.Dragger;
            return p ? p.GetComponent<RestraintVisual>() : null;
        }

        void Tick()
        {
            float len = clip.length;
            float t;
            switch (look)
            {
                case Look.TieUp:
                    t = Mathf.Min(Time.time - tieStart, len);
                    break;
                case Look.DragHeadDragger:
                case Look.DragFootDragger:
                case Look.GuardDragGuard:
                    clock += Time.deltaTime * RestraintRules.DragRate(GroundSpeed());
                    t = RestraintRules.LoopTime(clock, len);
                    PairTime = t;
                    break;
                case Look.FreeUpFreed:
                    t = Mathf.Clamp01(wob.FreeProgress) * len;                                                  // (the clip runs as the hold does: let go and it winds back)
                    break;
                case Look.FreeUpRescuer:
                    t = wob.RescueTarget != null ? Mathf.Clamp01(wob.RescueTarget.FreeProgress) * len : 0f;
                    break;
                case Look.GuardShotGuard:
                    var cp = Captive(true);
                    t = Mathf.Clamp(cp ? Time.time - cp.DropTime : Time.time - lookStarted, 0f, len);          // (both halves of the drop run on the clock of the hit)
                    break;
                case Look.GuardShotDragged:
                    t = Mathf.Clamp(Time.time - wob.DropTime, 0f, len);
                    break;
                case Look.DragHeadDragged:
                case Look.DragFootDragged:
                case Look.GuardDragDragged:
                case Look.GuardDragStruggleDragged:
                    var p = Partner();
                    t = p && p.PairTime >= 0f && IsDragger(p.look) ? p.PairTime : RestraintRules.LoopTime(Time.time - lookStarted, len);
                    break;
                default:
                    t = RestraintRules.LoopTime(Time.time - lookStarted, len);
                    break;
            }
            anim.SetBodyTime(t);
            UpdateTies(t);
            Feel();
        }

        /// <summary>The zip ties of this frame.</summary>
        void UpdateTies(float t)
        {
            bool w = false, k = false, l = false;
            switch (look)
            {
                case Look.TieUp: w = RestraintRules.TieUpWrists(t); k = RestraintRules.TieUpAnkles(t); l = RestraintRules.TieUpLink(t); break;
                case Look.Hogtied:
                case Look.DragHeadDragged:
                case Look.GuardDragDragged:
                case Look.GuardDragStruggleDragged:
                case Look.GuardShotDragged: w = k = l = true; break;
                case Look.FreeUpFreed: w = RestraintRules.FreeUpWrists(t); k = RestraintRules.FreeUpAnkles(t); l = RestraintRules.FreeUpLink(t); break;
            }
            if (!ties && (w || k || l)) ties = ZipTies.On(anim.GetComponent<Animator>());
            if (ties) ties.Set(w, k, l);
            if (tiesSeen && (w != prevW || k != prevK || l != prevL))
            {
                Ratchets++;
                GameAudio.PlaySound(SoundId.ZipRatchet, anim.transform.position + Vector3.up * 0.25f, 0.7f, Random.Range(0.95f, 1.05f), true, RestraintRules.HearRange, 2f);
            }
            prevW = w; prevK = k; prevL = l; tiesSeen = true;
        }

        void LateUpdate()
        {
            if (look == Look.None || !anim || !clip) return;
            var t = anim.transform;
            if (LyingSolo(look) || IsDragged(look))
            {
                Vector3 pos; Quaternion rot;
                if (IsDragged(look))
                {
                    var p = Partner();
                    if (!p) return;
                    p.ModelPose(out pos, out rot);                                   // (the partner's pose this frame, whatever order the two late updates run in)
                }
                else ModelPose(out pos, out rot);
                t.SetPositionAndRotation(pos, rot);
            }
            else if (restSaved)
            {
                t.localPosition = restPos; t.localRotation = restRot;                // (a dragger walks where he is)
            }
        }

        /// <summary>Where this body's model is held this frame: on the floor under a lying body, else where the model stands (the clips do not move it).</summary>
        void ModelPose(out Vector3 pos, out Quaternion rot)
        {
            if (LyingSolo(look))
            {
                float floorY = capsule && capsule.enabled ? capsule.bounds.min.y : transform.position.y - 1f;
                pos = new Vector3(transform.position.x, floorY, transform.position.z);
                rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                return;
            }
            var parent = anim.transform.parent;
            if (restSaved && parent) { pos = parent.TransformPoint(restPos); rot = parent.rotation * restRot; return; }
            pos = anim.transform.position; rot = anim.transform.rotation;
        }
    }
}

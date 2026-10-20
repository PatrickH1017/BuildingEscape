using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The elevator car's sliding doors: each door is two panels that part into the wall. Open while you are
    /// on a floor, closed (and locked) once the quota is met and the team is riding down. Opening and closing make a
    /// sound (SoundBank), heard by whoever is near.
    /// </summary>
    public class ElevatorDoor : MonoBehaviour
    {
        public float speed = 1.6f; // 1 = fully open per second (the doors CLOSE this fast; opening takes OpenSeconds)
        /// <summary>How long the doors take to open (the owner, 2026-10-17 night: "elevator door opening should also take 2 seconds").</summary>
        public const float OpenSeconds = 2f;

        /// <summary>How far open (0..1) a door is after <paramref name="dt"/> seconds of moving from <paramref name="k"/> towards <paramref name="target"/>: opening takes <see cref="OpenSeconds"/>, closing runs at <paramref name="closeSpeed"/> per second.</summary>
        public static float Advance(float k, float target, float dt, float closeSpeed) => Mathf.MoveTowards(k, target, (target > k ? 1f / OpenSeconds : closeSpeed) * dt);
        [Tooltip("Also ring the arrival chime when the doors open (the guest elevator that brings backup).")]
        public bool chimeOnOpen;

        struct Pair { public Transform a, b; public Vector3 closedA, closedB, openA, openB; }
        readonly List<Pair> pairs = new();
        float k = 1f, target = 1f;

        public bool IsOpen => target > 0.5f;
        public bool FullyClosed => target <= 0.5f && k <= 0.001f;

        /// <summary>Adds one door (two panels); it starts open. Call once per doorway.</summary>
        public void Setup(Transform panelA, Transform panelB, Vector3 slideA, Vector3 slideB)
        {
            var p = new Pair
            {
                a = panelA, b = panelB,
                closedA = panelA.position, closedB = panelB.position,
                openA = panelA.position + slideA, openB = panelB.position + slideB
            };
            pairs.Add(p);
            k = target = 1f;
            Apply();
        }

        /// <summary>Opens or closes the door. Safe to call every frame: the sound only plays when the direction changes.</summary>
        public void SetOpen(bool open)
        {
            float t = open ? 1f : 0f;
            if (!Mathf.Approximately(t, target)) PlaySlide(open);
            target = t;
        }

        void PlaySlide(bool open)
        {
            if (!Application.isPlaying || pairs.Count == 0) return;
            var pos = (pairs[0].closedA + pairs[0].closedB) * 0.5f + Vector3.up * 1.2f;   // the middle of the doorway
            GameAudio.PlaySound(open ? SoundId.DoorOpen : SoundId.DoorClose, pos, 0.9f, 0.88f, true, 35f, 3f);
            if (open && chimeOnOpen) GameAudio.PlaySound(SoundId.ElevatorDing, pos, 0.8f, 1f, true, 40f, 4f);
        }

        /// <summary>Jump straight to closed / open (used right after the NavMesh is baked with the doors open).</summary>
        public void Snap(bool open)
        {
            target = k = open ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            if (Mathf.Approximately(k, target)) return;      // at rest: nothing to move
            k = Advance(k, target, Time.deltaTime, speed);
            Apply();
        }

        void Apply()
        {
            foreach (var p in pairs)
            {
                if (!p.a || !p.b) continue;
                p.a.position = Vector3.Lerp(p.closedA, p.openA, k);
                p.b.position = Vector3.Lerp(p.closedB, p.openB, k);
            }
        }
    }
}

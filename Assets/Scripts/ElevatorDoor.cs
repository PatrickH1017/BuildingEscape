using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The elevator car's sliding doors: each door is two panels that part into the wall. Open while you are
    /// on a floor, closed (and locked) once the quota is met and the team is riding down.
    /// </summary>
    public class ElevatorDoor : MonoBehaviour
    {
        public float speed = 1.6f; // 1 = fully open per second

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

        public void SetOpen(bool open) { target = open ? 1f : 0f; }

        /// <summary>Jump straight to closed / open (used right after the NavMesh is baked with the doors open).</summary>
        public void Snap(bool open)
        {
            target = k = open ? 1f : 0f;
            Apply();
        }

        void Update()
        {
            k = Mathf.MoveTowards(k, target, speed * Time.deltaTime);
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

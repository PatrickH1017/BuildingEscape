using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Hides the furniture of rooms that are far from the camera (the walls stay), so a 5,000 prop floor stays light.
    /// </summary>
    public sealed class PropCuller : MonoBehaviour
    {
        struct Group { public Vector3 centre; public Renderer[] rs; public bool on; }

        Group[] groups = new Group[0];
        float distance;
        Camera cam;
        float next;

        public void Setup(List<Transform> roots, float maxDistance)
        {
            distance = maxDistance;
            var list = new List<Group>();
            foreach (var t in roots)
            {
                if (!t) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                list.Add(new Group { centre = b.center, rs = rs, on = true });
            }
            groups = list.ToArray();
        }

        void Update()
        {
            if (Time.unscaledTime < next || groups.Length == 0) return;
            next = Time.unscaledTime + 0.35f;
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
            if (!cam) return;
            var p = cam.transform.position;
            for (int i = 0; i < groups.Length; i++)
            {
                float dx = groups[i].centre.x - p.x, dz = groups[i].centre.z - p.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                bool want = groups[i].on ? d < distance + 8f : d < distance; // a little hysteresis
                if (want == groups[i].on) continue;
                groups[i].on = want;
                var rs = groups[i].rs;
                for (int k = 0; k < rs.Length; k++) if (rs[k]) rs[k].enabled = want;
            }
        }
    }
}

using UnityEngine;
namespace BuildingEscape { public static class HeldGunCopy {
        public static void SolveArm(Transform up, Transform fo, Transform ha, Vector3 target, Vector3 pole, float weight)
        {
            var s = up.position;
            float l1 = (fo.position - s).magnitude, l2 = (ha.position - fo.position).magnitude;
            var to = target - s;
            if (l1 < 1e-4f || l2 < 1e-4f || to.sqrMagnitude < 1e-8f) return;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
            var dir = to.normalized;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            var perp = Vector3.ProjectOnPlane(pole, dir);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.down, dir);
            perp.Normalize();
            var elbow = s + dir * (l1 * cosA) + perp * (l1 * sinA);
            var wrist = s + dir * d;
            up.rotation = Quaternion.Slerp(up.rotation, Quaternion.FromToRotation(fo.position - s, elbow - s) * up.rotation, weight);
            fo.rotation = Quaternion.Slerp(fo.rotation, Quaternion.FromToRotation(ha.position - fo.position, wrist - fo.position) * fo.rotation, weight);
        }
} }

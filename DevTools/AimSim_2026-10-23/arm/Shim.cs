// A small stand-in for the parts of UnityEngine that SightsAimLayer, AnimToolkitLib and ReloadMotionRules use, with Unity's own conventions
// (left-handed, Quaternion.Euler in z-x-y order, AngleAxis, LookRotation, FromToRotation, a Transform hierarchy with local/world position and rotation).
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public static class Mathf
    {
        public const float Rad2Deg = 57.29578f, Deg2Rad = 0.0174532924f, PI = 3.14159265f;
        public static float Abs(float v) => Math.Abs(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), 1e-8f * 8f);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
    }
    public static class Time { public static float deltaTime = 0.016f; }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public static Vector3 Normalize(Vector3 v) => v.normalized;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float s = Dot(n, n); if (s < 1e-15f) return v; return v - n * (Dot(v, n) / s); }
        public static float Angle(Vector3 a, Vector3 b) { float d = Dot(a.normalized, b.normalized); return (float)Math.Acos(Math.Max(-1f, Math.Min(1f, d))) * Mathf.Rad2Deg; }
        public static Vector3 ClampMagnitude(Vector3 v, float m) => v.sqrMagnitude > m * m ? v.normalized * m : v;
        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f, xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2, wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3((1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z, (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z, (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);      // (unit quaternions)
        static Quaternion Norm(Quaternion q) { float m = Mathf.Sqrt(Dot(q, q)); return m < 1e-12f ? identity : new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m); }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t); if (Dot(a, b) < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return Norm(new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t));
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t); float d = Dot(a, b); if (d < 0f) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            if (d > 0.9995f) return Lerp(a, b, t);
            float th = (float)Math.Acos(d), s = (float)Math.Sin(th), wa = (float)Math.Sin((1 - t) * th) / s, wb = (float)Math.Sin(t * th) / s;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }
        public static Quaternion AngleAxis(float deg, Vector3 axis)
        {
            axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);
        public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);      // (Unity: z first, then x, then y)
        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            from = from.normalized; to = to.normalized; float d = Vector3.Dot(from, to);
            if (d > 0.999999f) return identity;
            if (d < -0.999999f) { var ax = Vector3.Cross(from, Vector3.right); if (ax.sqrMagnitude < 1e-6f) ax = Vector3.Cross(from, Vector3.up); return AngleAxis(180f, ax); }
            var c = Vector3.Cross(from, to); return Norm(new Quaternion(c.x, c.y, c.z, 1f + d));
        }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = forward.normalized; var r = Vector3.Cross(up, f).normalized; var u = Vector3.Cross(f, r);       // (Unity: x = up x forward)
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22; Quaternion q;
            if (tr > 0f) { float s = Mathf.Sqrt(tr + 1f) * 2f; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            else if (m00 > m11 && m00 > m22) { float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            else if (m11 > m22) { float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            else { float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
            return Norm(q);
        }
        public static float Angle(Quaternion a, Quaternion b) { float d = Math.Min(Math.Abs(Dot(a, b)) / (float)Math.Sqrt(Dot(a, a) * Dot(b, b)), 1f); return (float)(2.0 * Math.Acos(d)) * Mathf.Rad2Deg; }
    }

    public class Object
    {
        public string name = "";
        internal bool destroyed;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null) && !o.destroyed;
        public static bool operator ==(Object a, Object b) { bool an = ReferenceEquals(a, null) || a.destroyed, bn = ReferenceEquals(b, null) || b.destroyed; return an || bn ? an == bn : ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
        public static void Destroy(Object o) { if (o is GameObject g) { g.destroyed = true; g.transform.destroyed = true; foreach (var c in g.transform.children.ToArray()) Destroy(c.gameObject); } }
    }

    public class GameObject : Object
    {
        public Transform transform;
        public GameObject(string name) { this.name = name; transform = new Transform { name = name, gameObject = this }; }
    }

    public class Transform : Object
    {
        public GameObject gameObject;
        public Transform transform => this;
        public Transform parent;
        public List<Transform> children = new List<Transform>();
        public Vector3 localPosition = Vector3.zero, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Quaternion rotation
        {
            get => parent ? parent.rotation * localRotation : localRotation;
            set => localRotation = parent ? Quaternion.Inverse(parent.rotation) * value : value;
        }
        public Vector3 lossyScale => parent ? Vector3.Scale(parent.lossyScale, localScale) : localScale;
        public Vector3 position
        {
            get => parent ? parent.position + parent.rotation * Vector3.Scale(localPosition, parent.lossyScale) : localPosition;
            set
            {
                if (!parent) { localPosition = value; return; }
                var d = Quaternion.Inverse(parent.rotation) * (value - parent.position); var s = parent.lossyScale;
                localPosition = new Vector3(d.x / s.x, d.y / s.y, d.z / s.z);
            }
        }
        public void SetParent(Transform p, bool worldPositionStays)
        {
            var wp = position; var wr = rotation;
            if (parent) parent.children.Remove(this);
            parent = p; if (p) p.children.Add(this);
            if (worldPositionStays) { position = wp; rotation = wr; }
        }
        public Vector3 TransformPoint(Vector3 local) => position + rotation * Vector3.Scale(local, lossyScale);
        public Vector3 InverseTransformPoint(Vector3 world) { var d = Quaternion.Inverse(rotation) * (world - position); var s = lossyScale; return new Vector3(d.x / s.x, d.y / s.y, d.z / s.z); }
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;
        public Transform Make(string n, Vector3 localPos) { var g = new GameObject(n); g.transform.SetParent(this, false); g.transform.localPosition = localPos; return g.transform; }
    }
}
namespace UnityEngine
{
    public class ScriptableObject : Object { }
    public class AnimationClip : Object { }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : System.Attribute { public string fileName, menuName; }
}

using System;

namespace BuildingEscape
{
    /// <summary>
    /// The arithmetic of a flat mirror, without Unity types so it can be tested outside Unity: the matrix that reflects the world in
    /// the plane of the mirror (the mirror camera looks at the world through it).
    /// </summary>
    public static class MirrorMath
    {
        /// <summary>
        /// The reflection in the plane n.x + d = 0 (n must have length 1), as a 4x4 matrix in row-major order (16 numbers):
        /// a point is reflected as m * (x, y, z, 1).
        /// </summary>
        public static float[] Reflection(float nx, float ny, float nz, float d)
        {
            return new[]
            {
                1f - 2f * nx * nx, -2f * nx * ny,      -2f * nx * nz,      -2f * d * nx,
                -2f * ny * nx,     1f - 2f * ny * ny,  -2f * ny * nz,      -2f * d * ny,
                -2f * nz * nx,     -2f * nz * ny,      1f - 2f * nz * nz,  -2f * d * nz,
                0f,                0f,                 0f,                 1f
            };
        }

        /// <summary>The plane through <paramref name="px"/>, <paramref name="py"/>, <paramref name="pz"/> with the unit normal n: its d (n.x + d = 0).</summary>
        public static float PlaneD(float nx, float ny, float nz, float px, float py, float pz) => -(nx * px + ny * py + nz * pz);

        /// <summary>Applies a row-major 4x4 matrix to a point.</summary>
        public static void Transform(float[] m, float x, float y, float z, out float ox, out float oy, out float oz)
        {
            ox = m[0] * x + m[1] * y + m[2] * z + m[3];
            oy = m[4] * x + m[5] * y + m[6] * z + m[7];
            oz = m[8] * x + m[9] * y + m[10] * z + m[11];
        }

        /// <summary>The distance of a point from the plane, positive on the side the normal points to.</summary>
        public static float SignedDistance(float nx, float ny, float nz, float d, float x, float y, float z) => nx * x + ny * y + nz * z + d;

        /// <summary>
        /// How big the mirror's picture is: a share of the screen size, kept within sensible limits.
        /// </summary>
        public static void TextureSize(int screenW, int screenH, float quality, out int w, out int h)
        {
            quality = quality < 0.2f ? 0.2f : quality > 1f ? 1f : quality;
            w = Math.Max(64, Math.Min(2048, (int)Math.Round(screenW * quality)));
            h = Math.Max(64, Math.Min(2048, (int)Math.Round(screenH * quality)));
        }
    }
}

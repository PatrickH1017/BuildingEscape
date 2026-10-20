using System;

namespace BuildingEscape
{
    /// <summary>
    /// A wood-veneer texture made from numbers: warm orange-brown with long vertical streaks of grain. It tiles in both directions (the
    /// noise wraps round), so it can be repeated over the walls of the elevator. Pure arithmetic, no Unity types, so it is tested outside Unity.
    /// </summary>
    public static class ProceduralWood
    {
        public const int Width = 128, Height = 256;

        // the dark and light end of the veneer (gamma colours, 0..1)
        static readonly float[] Dark = { 0.58f, 0.25f, 0.08f }, Light = { 0.94f, 0.52f, 0.20f };

        /// <summary>RGBA bytes, row by row from the bottom (like Unity's Texture2D). Alpha is always 255.</summary>
        public static byte[] Pixels(int w, int h, int seed)
        {
            var px = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w;
                    // long streaks: many columns of noise, hardly any change going up
                    float g = 0.50f * Value(u * 22f, v * 2f, 22, 2, seed)
                            + 0.30f * Value(u * 64f, v * 3f, 64, 3, seed + 1)
                            + 0.20f * Value(u * 7f, v * 1f, 7, 1, seed + 2);
                    // a slow light and dark wave going up, like the figure of the veneer
                    float wave = Value(u * 5f, v * 3f, 5, 3, seed + 3);
                    float t = Clamp01((g - 0.5f) * 2.2f + 0.5f + (wave - 0.5f) * 0.35f);
                    // fine pores
                    float pore = Value(u * 128f, v * 96f, 128, 96, seed + 4);
                    t = Clamp01(t - (pore > 0.86f ? 0.18f : 0f));
                    int i = (y * w + x) * 4;
                    for (int c = 0; c < 3; c++)
                        px[i + c] = (byte)Math.Round(255f * (Dark[c] + (Light[c] - Dark[c]) * t));
                    px[i + 3] = 255;
                }
            }
            return px;
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        /// <summary>Smooth value noise at (x, y) whose lattice wraps after <paramref name="px"/> and <paramref name="py"/> cells, so it tiles.</summary>
        static float Value(float x, float y, int px, int py, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            float top = a + (b - a) * fx, bottom = c + (d - c) * fx;
            return top + (bottom - top) * fy;
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)y * 2147483647u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}

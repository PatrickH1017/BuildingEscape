using System;
using System.IO;
public static class Wav
{
    public static float[][] Read(string path, out int rate)
    {
        byte[] b = File.ReadAllBytes(path);
        int pos = 12, ch = 2, bits = 16, tag = 1; rate = 44100; int dataAt = -1, dataLen = 0;
        while (pos < b.Length - 8)
        {
            string id = System.Text.Encoding.ASCII.GetString(b, pos, 4); int len = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ") { tag = BitConverter.ToInt16(b, pos + 8); ch = BitConverter.ToInt16(b, pos + 10); rate = BitConverter.ToInt32(b, pos + 12); bits = BitConverter.ToInt16(b, pos + 22); }
            else if (id == "data") { dataAt = pos + 8; dataLen = len; break; }
            pos += 8 + len + (len & 1);
        }
        int bytes = bits / 8, frames = dataLen / (bytes * ch);
        float[][] o = new float[ch][];
        for (int c = 0; c < ch; c++) o[c] = new float[frames];
        int p = dataAt;
        for (int i = 0; i < frames; i++)
            for (int c = 0; c < ch; c++)
            {
                float v;
                if (bits == 24) { int s = (b[p] | (b[p + 1] << 8) | (b[p + 2] << 16)); if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000); v = s / 8388608f; }
                else if (bits == 16) v = BitConverter.ToInt16(b, p) / 32768f;
                else if (tag == 3) v = BitConverter.ToSingle(b, p);
                else v = BitConverter.ToInt32(b, p) / 2147483648f;
                o[c][i] = v; p += bytes;
            }
        return o;
    }
    public static void Write16(string path, float[][] ch, int rate)
    {
        int frames = ch[0].Length, n = ch.Length;
        using (var f = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(f))
        {
            int dataLen = frames * n * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataLen); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)n); w.Write(rate); w.Write(rate * n * 2); w.Write((short)(n * 2)); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(dataLen);
            var rnd = new Random(7);
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < n; c++)
                {
                    double d = (rnd.NextDouble() - rnd.NextDouble()) / 32768.0;
                    double v = ch[c][i] + d; if (v > 1.0) v = 1.0; if (v < -1.0) v = -1.0;
                    w.Write((short)Math.Round(v * 32767.0));
                }
        }
    }
}
public static class Feat
{
    public static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2.0 * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = i + k + len / 2;
                    double xr = re[b] * cr - im[b] * ci, xi = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - xr; im[b] = im[a] - xi; re[a] += xr; im[a] += xi;
                    double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                }
            }
        }
    }
    // chroma (12) + log band energies (nb) of the mono signal in [start, end)
    public static double[] Vector(float[] l, float[] r, int start, int end, int rate, int nb)
    {
        int N = 8192;
        double[] chroma = new double[12], bands = new double[nb];
        double[] re = new double[N], im = new double[N];
        int frames = 0;
        for (int s = start; s + N <= end; s += N / 2)
        {
            for (int i = 0; i < N; i++)
            {
                double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (N - 1));
                re[i] = 0.5 * (l[s + i] + r[s + i]) * w; im[i] = 0;
            }
            Fft(re, im);
            for (int k = 1; k < N / 2; k++)
            {
                double f = (double)k * rate / N, mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
                if (f >= 55 && f <= 2200)
                {
                    double midi = 69 + 12 * Math.Log(f / 440.0, 2);
                    int pc = ((int)Math.Round(midi) % 12 + 12) % 12;
                    chroma[pc] += mag * mag;
                }
                if (f >= 40 && f <= 12000)
                {
                    int b = (int)(nb * Math.Log(f / 40.0, 2) / Math.Log(12000.0 / 40.0, 2));
                    if (b >= nb) b = nb - 1;
                    bands[b] += mag * mag;
                }
            }
            frames++;
        }
        double cs = 0; for (int i = 0; i < 12; i++) cs += chroma[i]; if (cs <= 0) cs = 1;
        double bsum = 0; for (int i = 0; i < nb; i++) { bands[i] = Math.Log(1e-9 + bands[i] / Math.Max(1, frames)); bsum += bands[i]; }
        double[] v = new double[12 + nb];
        for (int i = 0; i < 12; i++) v[i] = chroma[i] / cs;
        double bm = bsum / nb; for (int i = 0; i < nb; i++) v[12 + i] = bands[i] - bm;
        return v;
    }
    public static double Cos(double[] x, double[] y)
    {
        double d = 0, nx = 0, ny = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double w = i < 12 ? 2.0 : 0.35, xi = x[i] * w, yi = y[i] * w;
            d += xi * yi; nx += xi * xi; ny += yi * yi;
        }
        return d / Math.Sqrt(nx * ny + 1e-12);
    }
}

public static class Arr
{
    // power-complementary crossfade of whole-bar chunks of a looping source (the source wraps round, so the natural lead-in and tail of every chunk are there to fade through)
    public static float[][] Assemble(float[][] src, double barLen, int[] startBars, int barsPerChunk, int fadeSamples, int tailSamples)
    {
        int n = src[0].Length, k = startBars.Length, h = fadeSamples / 2;
        int total = (int)Math.Round(k * barsPerChunk * barLen) + tailSamples;
        float[][] o = new float[2][];
        o[0] = new float[total]; o[1] = new float[total];
        for (int c = 0; c < k; c++)
        {
            int outStart = (int)Math.Round(c * barsPerChunk * barLen);
            int outEnd = (int)Math.Round((c + 1) * barsPerChunk * barLen);
            int srcStart = (int)Math.Round(startBars[c] * barLen);
            int from = c == 0 ? outStart : outStart - h;
            int to = c == k - 1 ? outEnd : outEnd + h;
            for (int p = from; p < to; p++)
            {
                if (p < 0 || p >= total) continue;
                double w = 1.0;
                if (c > 0 && p < outStart + h) w = Math.Sin(Math.PI * 0.5 * (p - (outStart - h)) / (2.0 * h));
                if (c < k - 1 && p >= outEnd - h) w *= Math.Cos(Math.PI * 0.5 * (p - (outEnd - h)) / (2.0 * h));
                int si = (srcStart + (p - outStart)) % n; if (si < 0) si += n;
                o[0][p] += (float)(src[0][si] * w); o[1][p] += (float)(src[1][si] * w);
            }
        }
        return o;
    }

    // a low-pass whose cutoff glides (exponentially) from fc0 to fc1 over [startBar, startBar+bars); the rest is untouched
    public static void Lowpass(float[][] a, double barLen, double startBar, double bars, double fc0, double fc1, double q, int rate)
    {
        int s = (int)Math.Round(startBar * barLen), e = (int)Math.Round((startBar + bars) * barLen);
        if (e > a[0].Length) e = a[0].Length;
        for (int ch = 0; ch < 2; ch++)
        {
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0, b0 = 1, b1 = 0, b2 = 0, a1 = 0, a2 = 0;
            int warm = Math.Max(0, s - 6000);
            for (int i = warm; i < e; i++)
            {
                if (((i - warm) & 31) == 0)
                {
                    double t = i <= s ? 0.0 : (double)(i - s) / Math.Max(1, e - s);
                    double fc = fc0 * Math.Pow(fc1 / fc0, t);
                    if (fc > rate * 0.45) fc = rate * 0.45;
                    double w0 = 2 * Math.PI * fc / rate, cw = Math.Cos(w0), al = Math.Sin(w0) / (2 * q), a0 = 1 + al;
                    b0 = (1 - cw) / 2 / a0; b1 = (1 - cw) / a0; b2 = b0; a1 = -2 * cw / a0; a2 = (1 - al) / a0;
                }
                double x = a[ch][i];
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                if (i >= s) a[ch][i] = (float)y;            // (the run-up before the region only settles the filter)
            }
        }
    }

    // a smooth gain ramp g0 -> g1 over the region (cosine shaped); afterwards the gain stays g1 when holdAfter, else it is 1 again
    public static void Gain(float[][] a, double barLen, double startBar, double bars, double g0, double g1, bool holdAfter)
    {
        int s = (int)Math.Round(startBar * barLen), e = (int)Math.Round((startBar + bars) * barLen);
        for (int i = s; i < a[0].Length; i++)
        {
            double g;
            if (i < e) { double t = (double)(i - s) / Math.Max(1, e - s); t = 0.5 - 0.5 * Math.Cos(Math.PI * t); g = g0 + (g1 - g0) * t; }
            else if (holdAfter) g = g1; else break;
            a[0][i] = (float)(a[0][i] * g); a[1][i] = (float)(a[1][i] * g);
        }
    }

    // white noise through a rising high-pass, swelling to peakGain by the end of the region (a riser)
    public static void Riser(float[][] a, double barLen, double startBar, double bars, double peakGain, int seed, int rate)
    {
        int s = (int)Math.Round(startBar * barLen), e = (int)Math.Round((startBar + bars) * barLen);
        var rnd = new Random(seed);
        double[] lp = new double[2];
        for (int i = s; i < e && i < a[0].Length; i++)
        {
            double t = (double)(i - s) / Math.Max(1, e - s);
            double env = peakGain * t * t;
            double fc = 1500 + 9000 * t;                      // the one-pole cut-off the noise is high-passed at
            double coef = 1 - Math.Exp(-2 * Math.PI * fc / rate);
            for (int ch = 0; ch < 2; ch++)
            {
                double nz = rnd.NextDouble() * 2 - 1;
                lp[ch] += coef * (nz - lp[ch]);
                a[ch][i] += (float)((nz - lp[ch]) * env);
            }
        }
    }

    // a low thump with a short crash on top, at a bar line
    public static void Impact(float[][] a, double barLen, double atBar, double gain, int seed, int rate)
    {
        int s = (int)Math.Round(atBar * barLen);
        int len = (int)(0.9 * rate);
        var rnd = new Random(seed);
        double ph = 0, lp = 0;
        for (int i = 0; i < len && s + i < a[0].Length; i++)
        {
            double t = (double)i / rate;
            double f = 32 + 55 * Math.Exp(-t * 7.0);
            ph += 2 * Math.PI * f / rate;
            double thump = Math.Sin(ph) * Math.Exp(-t * 3.6);
            double nz = rnd.NextDouble() * 2 - 1;
            lp += 0.35 * (nz - lp);
            double crash = (nz - lp) * Math.Exp(-t * 6.0) * 0.55;
            float v = (float)((thump * 0.9 + crash) * gain);
            a[0][s + i] += v; a[1][s + i] += v;
        }
    }

    public static void Normalize(float[][] a, float peak)
    {
        float m = 0; for (int c = 0; c < 2; c++) for (int i = 0; i < a[c].Length; i++) { float x = Math.Abs(a[c][i]); if (x > m) m = x; }
        if (m <= 0) return; float g = peak / m;
        for (int c = 0; c < 2; c++) for (int i = 0; i < a[c].Length; i++) a[c][i] *= g;
    }

    public static double Rms(float[][] a, int s, int e)
    {
        if (s < 0) s = 0; if (e > a[0].Length) e = a[0].Length; if (e <= s) return 0;
        double sum = 0; for (int i = s; i < e; i++) { double x = 0.5 * (a[0][i] + a[1][i]); sum += x * x; }
        return Math.Sqrt(sum / (e - s));
    }
}



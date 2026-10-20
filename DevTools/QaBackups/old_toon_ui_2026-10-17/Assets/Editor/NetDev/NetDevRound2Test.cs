using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>Power of one frequency in a buffer (the Goertzel algorithm), for judging a synthesised sound without listening to it.</summary>
        static float Goertzel(float[] x, float hz)
        {
            float w = 2f * Mathf.PI * hz / Dsp.SampleRate, coeff = 2f * Mathf.Cos(w), s1 = 0f, s2 = 0f;
            for (int i = 0; i < x.Length; i++) { float s0 = x[i] + coeff * s1 - s2; s2 = s1; s1 = s0; }
            return s1 * s1 + s2 * s2 - coeff * s1 * s2;
        }

        /// <summary>Does a ray from <paramref name="o"/> along <paramref name="d"/> hit any triangle of the mesh (Moeller-Trumbore, both sides)?</summary>
        static bool RayHitsMesh(Mesh m, Vector3 o, Vector3 d)
        {
            var v = m.vertices; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], e1 = v[t[i + 1]] - a, e2 = v[t[i + 2]] - a, p = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, p);
                if (Mathf.Abs(det) < 1e-8f) continue;
                float inv = 1f / det; var s = o - a;
                float u = Vector3.Dot(s, p) * inv; if (u < 0f || u > 1f) continue;
                var q = Vector3.Cross(s, e1);
                float w = Vector3.Dot(d, q) * inv; if (w < 0f || u + w > 1f) continue;
                if (Vector3.Dot(e2, q) * inv > 0f) return true;
            }
            return false;
        }

        /// <summary>
        /// The owner's requests of 2026-10-17 afternoon that can be checked without playing: the key ring's jingle, the end screen's button, the elevator's delay and the hallway doors'
        /// real windows. (The default toon character is in <see cref="AppearanceTest"/>.)
        /// </summary>
        public static void Round2Test()
        {
            // ---- "the key jingle sound is a little off": tuned pitches, a soft top, no harsh overtones, the same every time
            bool finite = true, deterministic = true, quiet = true, soft = true, tuned = true;
            float[] pitches = { 2637f, 3136f, 3520f, 3951f, 4699f }, harsh = { 10500f, 12000f, 13500f, 15000f };
            for (int variant = 0; variant < ProceduralSound.KeyVariants; variant++)
                foreach (bool run in new[] { false, true })
                {
                    var k = ProceduralSound.Keys(variant, run); var again = ProceduralSound.Keys(variant, run);
                    float peak = 0f;
                    for (int i = 0; i < k.Length; i++) { if (float.IsNaN(k[i]) || float.IsInfinity(k[i])) finite = false; peak = Mathf.Max(peak, Mathf.Abs(k[i])); if (k[i] != again[i]) deterministic = false; }
                    quiet &= peak > 0.7f && peak <= 0.81f;
                    float mid = 0f, hi = 0f;
                    foreach (var p in pitches) mid += Goertzel(k, p);
                    foreach (var h in harsh) hi += Goertzel(k, h);
                    soft &= hi < mid * 0.05f;
                    tuned &= k.Length >= Dsp.Samples(0.45f) && k.Length <= Dsp.Samples(0.65f);
                }
            Check("keys: the jingle is finite, the same every time and peaks at 0.8", finite && deterministic && quiet);
            Check("keys: nothing harsh above 10 kHz (the old jingle had overtones up to 15 kHz) and the right length", soft && tuned);
            Check("keys: running makes more of it (longer, and louder in the middle)", ProceduralSound.Keys(0, true).Length > ProceduralSound.Keys(0, false).Length);

            // ---- "the exit button after wiping in a run should be higher"
            bool high = true;
            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(1280f, 720f), new Vector2(2560f, 1440f), new Vector2(1920f, 888f) })
            {
                float s = size.y / 1080f;
                var r = HeistHud.EndButtonRect(size.x, size.y, s);
                high &= r.center.y < size.y * 0.32f && r.y >= 140f * s && Mathf.Abs(r.center.x - size.x * 0.5f) < 1f && r.xMin > 0f && r.xMax < size.x;
            }
            Check("end screen: the button sits up under the banner (its middle is above a third of the screen, nowhere near where the cursor appears)", high && HeistHud.EndButtonDelay >= 1f);

            // ---- "a two second delay before the elevator door closes after clicking the Go button"
            Check("elevator: the doors wait two seconds after GO", Mathf.Approximately(ElevatorExtraction.CloseDelay, 2f));

            // ---- "make the windows on the hallway doors see through": two real openings in each leaf
            var leaf = FloorGenerator.MakeHallLeafMesh();
            var along = Vector3.forward;
            bool openLeft = !RayHitsMesh(leaf, new Vector3(-0.25f, 0.18f, -2f), along), openRight = !RayHitsMesh(leaf, new Vector3(0.25f, 0.18f, -2f), along);
            bool openEdges = !RayHitsMesh(leaf, new Vector3(-0.38f, -0.02f, -2f), along) && !RayHitsMesh(leaf, new Vector3(0.38f, 0.38f, -2f), along);
            bool solid = RayHitsMesh(leaf, new Vector3(0f, 0.18f, -2f), along)            // the stile between the openings
                      && RayHitsMesh(leaf, new Vector3(0.25f, -0.3f, -2f), along)         // the lower panel
                      && RayHitsMesh(leaf, new Vector3(-0.25f, 0.45f, -2f), along)        // the top rail
                      && RayHitsMesh(leaf, new Vector3(-0.47f, 0.18f, -2f), along);       // the outer stile
            Check("doors: a hallway door's leaf has two real openings (a ray through each window passes, one through the frame hits)", openLeft && openRight && openEdges && solid);
            Check("doors: ... inside the unit leaf, so the leaf's scaling shrinks them with it", leaf.bounds.min.x >= -0.5001f && leaf.bounds.max.x <= 0.5001f && leaf.bounds.min.y >= -0.5001f && leaf.bounds.max.y <= 0.5001f
                && leaf.bounds.min.z >= -0.5001f && leaf.bounds.max.z <= 0.5001f);
            Object.DestroyImmediate(leaf);

            // ---- "the carpets are a bit too colorful. Each floor should be a set color and floors can have the same colors as others"
            var seenCarpets = new System.Collections.Generic.HashSet<Color>();
            bool nextDiffers = true;
            for (int fl = 1; fl <= 13; fl++)
            {
                seenCarpets.Add(FloorGenerator.CarpetColourOf(fl));
                nextDiffers &= FloorGenerator.CarpetColourOf(fl) != FloorGenerator.CarpetColourOf(fl + 1);
            }
            Check("carpets: a floor has one set colour (the same on every call), no two floors in a row alike, and the palette is reused (floor 1 = floor 7)",
                nextDiffers && seenCarpets.Count == FloorGenerator.CarpetPaletteSize && FloorGenerator.CarpetColourOf(1) == FloorGenerator.CarpetColourOf(7) && FloorGenerator.CarpetColourOf(3) == FloorGenerator.CarpetColourOf(3));
            bool quietColours = true;
            foreach (var quietC in seenCarpets) { Color.RGBToHSV(quietC, out _, out float sat, out float val); quietColours &= sat < 0.36f && val < 0.55f; }
            Check("carpets: the colours are quiet ones (low saturation, not bright)", quietColours);

            // ---- "it should be nighttime outside": a dark sky with stars and a moon, windows lit in some of the towers
            var skyPx = ExteriorCity.NightSky().GetPixels32();
            int starPx = 0, moonPx = 0, counted = 0; double lum = 0;
            for (int sy = 77; sy < 512; sy += 3)
                for (int sx = 0; sx < 2048; sx += 3)
                {
                    var sc = skyPx[sy * 2048 + sx];
                    float sl = (sc.r + sc.g + sc.b) / (3f * 255f);
                    lum += sl; counted++;
                    if (sl > 0.6f && !(Mathf.Abs(sx - 1446) < 60 && Mathf.Abs(sy - 283) < 60)) starPx++;
                }
            for (int my = 270; my < 296; my++)
                for (int mx = 1433; mx < 1460; mx++)
                {
                    var mc = skyPx[my * 2048 + mx];
                    if ((mc.r + mc.g + mc.b) / (3f * 255f) > 0.8f) moonPx++;
                }
            Check("night: the sky is dark (average brightness under 0.12), with stars and a bright moon", lum / counted < 0.12 && starPx >= 40 && moonPx >= 300, $"(average {lum / counted:0.000}, stars {starPx}, moon pixels {moonPx})");
            bool windowsOk = true; string windowNote = "";
            for (int lv = 0; lv < 3; lv++)
            {
                var wp = ExteriorCity.NightWindows(lv).GetPixels32();
                int lit = 0;
                for (int wy = 0; wy < 8; wy++)
                    for (int wx = 0; wx < 8; wx++)
                    {
                        var wc = wp[(wy * 16 + 7) * 128 + wx * 16 + 8];
                        if ((wc.r + wc.g + wc.b) / (3f * 255f) > 0.18f) lit++;
                    }
                windowNote += $" level {lv}: {lit}/64";
                windowsOk &= lit >= 6 && lit <= 44;
            }
            Check("night: the towers have lit windows, some of the panes of every tile at every distance", windowsOk, "(" + windowNote.Trim() + ")");

            // ---- "the offices with glass walls should also have glass doors": a ray through the glass leaf's opening passes, one through a stile or the kick plate hits
            var glassLeaf = FloorGenerator.MakeGlassLeafMesh();
            var straight = Vector3.forward;
            Check("doors: a glass door's leaf has one big opening (a ray through its middle passes; through a stile, the kick plate or the top rail it hits), inside the unit leaf",
                !RayHitsMesh(glassLeaf, new Vector3(0f, 0.1f, -2f), straight) && !RayHitsMesh(glassLeaf, new Vector3(-0.3f, -0.3f, -2f), straight) && !RayHitsMesh(glassLeaf, new Vector3(0.3f, 0.35f, -2f), straight)
                && RayHitsMesh(glassLeaf, new Vector3(-0.45f, 0f, -2f), straight) && RayHitsMesh(glassLeaf, new Vector3(0.45f, 0f, -2f), straight) && RayHitsMesh(glassLeaf, new Vector3(0f, -0.46f, -2f), straight)
                && RayHitsMesh(glassLeaf, new Vector3(0f, 0.47f, -2f), straight) && glassLeaf.bounds.min.x >= -0.5001f && glassLeaf.bounds.max.x <= 0.5001f && glassLeaf.bounds.min.y >= -0.5001f && glassLeaf.bounds.max.y <= 0.5001f);
            Object.DestroyImmediate(glassLeaf);

            // ---- tile and carpet never share a spot of floor: a rectangle with a rectangle cut out of it
            var whole = new System.Collections.Generic.List<Rc> { new Rc(0f, 0f, 10f, 6f) };
            float AreaOf(System.Collections.Generic.List<Rc> rs) { float a = 0f; foreach (var rr in rs) a += rr.W * rr.D; return a; }
            var centreCut = FloorGenerator.CutOut(whole, new Rc(3f, 2f, 7f, 4f));
            var cornerCut = FloorGenerator.CutOut(whole, new Rc(8f, 4f, 14f, 9f));
            var allCut = FloorGenerator.CutOut(whole, new Rc(-1f, -1f, 11f, 7f));
            var apart = FloorGenerator.CutOut(whole, new Rc(20f, 0f, 30f, 6f));
            var twice = FloorGenerator.CutOut(centreCut, new Rc(0f, 0f, 5f, 3f));
            bool noOverlapLeft = true;
            foreach (var pc in centreCut) noOverlapLeft &= !(pc.x1 > 3f && pc.x0 < 7f && pc.z1 > 2f && pc.z0 < 4f);
            Check("floors: cutting a rectangle out of another leaves exactly the rest (a hole in the middle, a corner, all of it, none of it, a second cut)",
                Mathf.Abs(AreaOf(centreCut) - (60f - 8f)) < 1e-3f && centreCut.Count == 4 && noOverlapLeft && Mathf.Abs(AreaOf(cornerCut) - (60f - 4f)) < 1e-3f && allCut.Count == 0 && apart.Count == 1 && Mathf.Abs(AreaOf(twice) - 39f) < 1e-3f);

            // ---- the outpost computer's log-in
            bool repeatable = true, fitsPostIt = true, floorsDiffer = false;
            for (int sd = 1; sd <= 300; sd++)
                for (int fl2 = 1; fl2 <= 12; fl2++)
                {
                    var ca = ComputerRules.CredentialsFor(sd * 31 + 7, fl2);
                    var cb = ComputerRules.CredentialsFor(sd * 31 + 7, fl2);
                    repeatable &= ca.user == cb.user && ca.pass == cb.pass;
                    fitsPostIt &= ComputerRules.IsPostItText(ca.user) && ComputerRules.IsPostItText(ca.pass);
                    if (fl2 > 1) { var prev = ComputerRules.CredentialsFor(sd * 31 + 7, fl2 - 1); floorsDiffer |= ca.user != prev.user || ca.pass != prev.pass; }
                }
            var lg = ComputerRules.CredentialsFor(777, 3);
            Check("computer: the same run and floor give the same log-in, made of capitals and digits that fit the post-it, and floors differ", repeatable && fitsPostIt && floorsDiffer);
            Check("computer: the right words open it in capitals or small letters with spaces round them; a wrong word, an empty one, half the pair or the pair swapped does not",
                ComputerRules.Accepts(lg, lg.user, lg.pass) && ComputerRules.Accepts(lg, lg.user.ToLowerInvariant(), " " + lg.pass.ToLowerInvariant() + " ") && !ComputerRules.Accepts(lg, lg.user, "WRONG1")
                && !ComputerRules.Accepts(lg, "", "") && !ComputerRules.Accepts(lg, lg.user, null) && !ComputerRules.Accepts(lg, lg.pass, lg.user) && !ComputerRules.Accepts(lg, lg.user + "X", lg.pass));
        }
    }
}

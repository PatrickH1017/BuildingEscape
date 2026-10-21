using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The little scoreboard on the wall above the dart board (the owner, 2026-10-19 night: "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone should see
    /// the results"). Three lines in the dot-matrix lettering of the room-number plates (<see cref="DotText"/>, no font needed): who threw, the score of each of the three darts (the middle line, in big dots) and what is left in 301 or the
    /// points in Cricket. It is a plain mesh of lit dots on a dark plate, drawn unlit so that it can be read in the dim docks, and rebuilt when the text changes. What it shows is kept by <see cref="DartsRules.Scoreboard"/>; the
    /// <see cref="DartBoard"/> feeds it (at once for the thrower, through the host for everybody else).
    /// </summary>
    public sealed class DartScoreboard : MonoBehaviour
    {
        /// <summary>Metres per dot, the plate's width in dots, the dots of margin round the lettering and the dots between two lines.</summary>
        public const float Dot = 0.02f;
        public const int PlateColumns = 72, Margin = 3, LineGap = 3;
        /// <summary>How big each line is (a dot is this many dots): the name and the footer small, the three scores big.</summary>
        static readonly int[] LineScale = { 1, 2, 1 };

        static readonly Color FrameColour = new Color(0.30f, 0.30f, 0.33f), BackColour = new Color(0.05f, 0.05f, 0.06f);
        static readonly Color NameColour = new Color(1f, 0.62f, 0.12f), ScoreColour = new Color(1f, 0.86f, 0.32f), FootColour = new Color(1f, 0.62f, 0.12f), WonColour = new Color(0.4f, 1f, 0.5f), BustColour = new Color(1f, 0.25f, 0.2f);

        Mesh mesh;
        Material mat;
        string[] shown = new string[0];
        bool shownBust, shownWon;
        readonly List<DotText.Run> runs = new List<DotText.Run>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colours = new List<Color>();
        readonly List<int> tris = new List<int>();

        /// <summary>The three lines on the plate now.</summary>
        public string[] Lines => shown;
        /// <summary>The plate's width and height in metres, and how many runs of lit dots were drawn the last time (for the tests).</summary>
        public Vector2 PlateSize { get; private set; }
        public int LitRuns { get; private set; }

        /// <summary>The plate's height in dots: the margins, the three lines and the gaps between them.</summary>
        public static int Rows { get { int r = 2 * Margin + 2 * LineGap; foreach (int s in LineScale) r += DotText.Rows * s; return r; } }

        /// <summary>Hangs a scoreboard with its middle at <paramref name="centre"/> on a wall whose face looks along <paramref name="towardsViewers"/> (horizontal).</summary>
        public static DartScoreboard Build(Transform parent, Vector3 centre, Vector3 towardsViewers)
        {
            var go = new GameObject("DartScoreboard");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(-towardsViewers, Vector3.up));          // (its +z goes into the wall: a mesh facing -z is seen from the room, text running to the viewer's right)
            var sb = go.AddComponent<DartScoreboard>();
            sb.Init();
            return sb;
        }

        void Init()
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh) mat = new Material(sh) { name = "DartScoreboard" };
            mesh = new Mesh { name = "DartScoreboard" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            if (mat) mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            PlateSize = new Vector2(PlateColumns * Dot, Rows * Dot);
        }

        bool SameAs(string[] lines)
        {
            if (lines.Length != shown.Length) return false;
            for (int i = 0; i < lines.Length; i++) if (lines[i] != shown[i]) return false;
            return true;
        }

        void Quad(float x0, float y0, float x1, float y1, float z, Color c)
        {
            int b = verts.Count;
            verts.Add(new Vector3(x0, y0, z)); verts.Add(new Vector3(x0, y1, z)); verts.Add(new Vector3(x1, y1, z)); verts.Add(new Vector3(x1, y0, z));
            for (int i = 0; i < 4; i++) colours.Add(c);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2); tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        /// <summary>Shows these lines (letters, digits, dashes and spaces: <see cref="DotText"/>); the footer goes red for a bust and green for a win.</summary>
        public void Show(string[] lines, bool bust = false, bool won = false)
        {
            if (mesh == null || lines == null) return;
            if (SameAs(lines) && bust == shownBust && won == shownWon) return;
            shown = (string[])lines.Clone(); shownBust = bust; shownWon = won;
            verts.Clear(); colours.Clear(); tris.Clear(); LitRuns = 0;
            float w = PlateColumns * Dot, h = Rows * Dot;
            Quad(-w / 2f - 2f * Dot, -h / 2f - 2f * Dot, w / 2f + 2f * Dot, h / 2f + 2f * Dot, 0.004f, FrameColour);        // (the frame, then the dark plate in front of it, then the lit dots in front of that)
            Quad(-w / 2f, -h / 2f, w / 2f, h / 2f, 0.002f, BackColour);
            int top = Margin;                                                                                               // (in dots from the top of the plate)
            for (int i = 0; i < LineScale.Length; i++)
            {
                int s = LineScale[i];
                string text = i < lines.Length ? lines[i] : "";
                runs.Clear();
                DotText.AddRuns(text, 0, 0, runs);
                int left = (PlateColumns - DotText.Columns(text) * s) / 2;
                Color c = i == 0 ? NameColour : i == 1 ? ScoreColour : won ? WonColour : bust ? BustColour : FootColour;
                foreach (var r in runs)
                {
                    float x0 = -w / 2f + (left + r.col * s) * Dot, y1 = h / 2f - (top + r.row * s) * Dot;
                    Quad(x0 + 0.08f * Dot, y1 - s * Dot + 0.12f * Dot, x0 + r.len * s * Dot - 0.08f * Dot, y1 - 0.12f * Dot, 0f, c);
                    LitRuns++;
                }
                top += DotText.Rows * s + LineGap;
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (mat) Destroy(mat);
        }
    }
}

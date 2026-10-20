using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// A tiny dot-matrix lettering for signs, free of Unity types (so it can be tested outside Unity and needs no font asset): every
    /// letter, digit and hyphen is 3 dots wide and 5 dots high, and there are two arrows, 7 x 7 dots. It is the lettering of the room
    /// number plates (<see cref="RoomNumbers"/>) with the rest of the alphabet added. A text becomes <see cref="Run"/>s - a row of lit dots
    /// side by side - which the builder turns into one rectangle each, so a whole sign is a handful of quads.
    /// </summary>
    public static class DotText
    {
        /// <summary>The height of the lettering in dots.</summary>
        public const int Rows = 5;
        /// <summary>The width and height of an arrow in dots.</summary>
        public const int ArrowSize = 7;
        /// <summary>The width of a space between words, in dots.</summary>
        public const int SpaceColumns = 3;

        /// <summary>A row of lit dots: <see cref="len"/> of them from column <see cref="col"/> on row <see cref="row"/> (row 0 is the top).</summary>
        public struct Run
        {
            public int col, row, len;
            public Run(int col, int row, int len) { this.col = col; this.row = row; this.len = len; }
        }

        const string Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";

        // rows of three dots, top to bottom, left to right ('1' = lit)
        static readonly string[] Glyphs =
        {
            "010101111101101", // A
            "110101110101110", // B
            "011100100100011", // C
            "110101101101110", // D
            "111100110100111", // E
            "111100110100100", // F
            "011100101101011", // G
            "101101111101101", // H
            "111010010010111", // I
            "001001001101010", // J
            "101101110101101", // K
            "100100100100111", // L
            "101111111101101", // M
            "110101101101101", // N
            "111101101101111", // O
            "111101111100100", // P
            "111101101111001", // Q
            "110101110101101", // R
            "011100010001110", // S
            "111010010010010", // T
            "101101101101111", // U
            "101101101101010", // V
            "101101111111101", // W
            "101101010101101", // X
            "101101010010010", // Y
            "111001010100111", // Z
            "111101101101111", // 0
            "010110010010111", // 1
            "111001111100111", // 2
            "111001111001111", // 3
            "101101111001001", // 4
            "111100111001111", // 5
            "111100111101111", // 6
            "111001010010010", // 7
            "111101111101111", // 8
            "111101111001111", // 9
            "000000111000000", // -
        };

        // the arrows, 7 x 7 ('#' = lit); the left one is the right one turned round
        static readonly string[] ArrowRight =
        {
            "...#...",
            "...##..",
            "######.",
            "#######",
            "######.",
            "...##..",
            "...#...",
        };

        public const int GlyphColumns = 3;

        /// <summary>Is this a character the lettering has (a space counts)?</summary>
        public static bool Has(char ch) => ch == ' ' || Chars.IndexOf(char.ToUpperInvariant(ch)) >= 0;

        /// <summary>How many columns a character takes (a space is <see cref="SpaceColumns"/>, everything else 3; unknown characters are blank spaces).</summary>
        public static int Width(char ch) => Chars.IndexOf(char.ToUpperInvariant(ch)) >= 0 ? GlyphColumns : SpaceColumns;

        /// <summary>Is this dot of the character lit? Row 0 is the top row, column 0 the left one.</summary>
        public static bool Lit(char ch, int column, int row)
        {
            int at = Chars.IndexOf(char.ToUpperInvariant(ch));
            if (at < 0 || column < 0 || column >= GlyphColumns || row < 0 || row >= Rows) return false;
            return Glyphs[at][row * GlyphColumns + column] == '1';
        }

        /// <summary>The width of a text in columns: the letters, with one empty column between neighbours and none after the last.</summary>
        public static int Columns(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int w = 0;
            for (int i = 0; i < text.Length; i++) w += Width(text[i]) + (i > 0 ? 1 : 0);
            return w;
        }

        /// <summary>The lit dots of a text, as runs; its top left dot is at (<paramref name="col0"/>, <paramref name="row0"/>).</summary>
        public static void AddRuns(string text, int col0, int row0, List<Run> into)
        {
            if (string.IsNullOrEmpty(text)) return;
            int x = col0;
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0) x++;
                char ch = text[i];
                int w = Width(ch);
                for (int row = 0; row < Rows; row++)
                {
                    int col = 0;
                    while (col < w)
                    {
                        if (!Lit(ch, col, row)) { col++; continue; }
                        int start = col;
                        while (col < w && Lit(ch, col, row)) col++;
                        into.Add(new Run(x + start, row0 + row, col - start));
                    }
                }
                x += w;
            }
        }

        /// <summary>Is this dot of the arrow lit?</summary>
        public static bool ArrowLit(bool pointsRight, int column, int row)
        {
            if (column < 0 || column >= ArrowSize || row < 0 || row >= ArrowSize) return false;
            return ArrowRight[row][pointsRight ? column : ArrowSize - 1 - column] == '#';
        }

        /// <summary>The lit dots of an arrow, as runs; its top left dot is at (<paramref name="col0"/>, <paramref name="row0"/>).</summary>
        public static void AddArrowRuns(bool pointsRight, int col0, int row0, List<Run> into)
        {
            for (int row = 0; row < ArrowSize; row++)
            {
                int col = 0;
                while (col < ArrowSize)
                {
                    if (!ArrowLit(pointsRight, col, row)) { col++; continue; }
                    int start = col;
                    while (col < ArrowSize && ArrowLit(pointsRight, col, row)) col++;
                    into.Add(new Run(col0 + start, row0 + row, col - start));
                }
            }
        }
    }
}

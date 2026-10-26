namespace BuildingEscape
{
    /// <summary>
    /// Where the floor buttons of the service elevator's wall panel stand (the owner, 2026-10-23, with a photograph of a real panel: "The elevator buttons should go in order from left to right ascending right now they are descending"):
    /// three buttons to a row, the numbers rising from left to right and the rows rising from the bottom row up - the Loading Docks "Ld" first, then 1 to 13 - as on the photograph (its rows from the top: 12 14 / 9 10 11 / 6 7 8 / 3 4 5 / G L 2).
    /// The short top row (12 and 13) stands at the left. Pure numbers, no Unity types, so the order is tested outside the editor; <c>ElevatorPanel.Construct</c> puts the buttons where this says.
    /// </summary>
    public static class ElevatorPanelRules
    {
        /// <summary>Buttons across, and how many there are: the docks and floors 1 to 13.</summary>
        public const int Columns = 3, Buttons = 14;
        /// <summary>The distance between two buttons, across and up (metres, on the panel).</summary>
        public const float Pitch = 0.4f;

        /// <summary>The floor of button number <paramref name="k"/> (0 is the first, at the bottom left): 0 is the Loading Docks, 1 to 13 the office floors.</summary>
        public static int FloorOf(int k) => k;

        /// <summary>The column (0 left to 2 right) and the row counted from the bottom (0 is the bottom row) of button <paramref name="k"/>.</summary>
        public static void Cell(int k, out int column, out int rowFromBottom)
        {
            column = k % Columns;
            rowFromBottom = k / Columns;
        }

        /// <summary>How many rows the panel has (the short top row counts).</summary>
        public static int Rows => (Buttons + Columns - 1) / Columns;

        /// <summary>Where button <paramref name="k"/> stands on the plate: <paramref name="x"/> from the middle (the middle column at 0), <paramref name="y"/> with the top row at <paramref name="topY"/> and the rows below it <see cref="Pitch"/> apart.</summary>
        public static void Position(int k, float topY, out float x, out float y)
        {
            Cell(k, out int column, out int rowFromBottom);
            x = (column - 1) * Pitch;
            y = topY - (Rows - 1 - rowFromBottom) * Pitch;
        }
    }
}

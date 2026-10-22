# LootQa: "the loot on show can be walked to (NavMesh path from a patrolling guard)" failed with 4 unreachable servers (the limit is 3): the walk grid kept a PERSON's 0.3 m clear, the floor's NavMesh keeps an agent's 0.5 m.
# The grid keeps 0.5 m now (what the guards and the NavMesh call walkable), and a standing spot counts as reached when open ground that was reached lies within 0.8 m of it (the spot itself is 0.45 m from the box, closer than an agent can go).
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if ($s.Contains('ReachSlack')) { "already"; return }
Rep '            const float Step = 0.25f, Person = 0.3f;' '            const float Step = 0.25f, Person = 0.5f, ReachSlack = 0.8f;       // (0.5 m is the radius of an agent: what the floor''s NavMesh and the guards call walkable)'
Rep '        /// The floor of one room as a grid of cells a person can stand in (clear of furniture, walls and the room''s edge by a person''s half width), made once for every spot a treasure is tried at.' '        /// The floor of one room as a grid of cells an agent can walk in (clear of furniture, walls and the room''s edge by 0.5 m, as the floor''s NavMesh is), made once for every spot a treasure is tried at.'
Rep '            /// <summary>The cells that can be walked to from the way in with a box standing at <paramref name="box"/> (a cell is open when a person fits there: clear of everything, the box too, by a person''s half width).</summary>' '            /// <summary>The cells that can be walked to from the way in with a box standing at <paramref name="box"/> (a cell is open when an agent fits there: clear of everything, the box too, by 0.5 m).</summary>'
Rep '            /// <summary>Can a person get to this spot (it is an open cell that the flood reached)?</summary>
            public bool Reached(bool[] seen, float x, float z) { int c = CellOf(x, z); return c >= 0 && seen[c]; }' '            /// <summary>Can a person get to this spot: has the flood reached open ground within <see cref="ReachSlack"/> of it? (A carrier''s spot is 0.45 m from the box: closer than an agent can go, so the ground they come from is what counts.)</summary>
            public bool Reached(bool[] seen, float x, float z)
            {
                int r = (int)Math.Ceiling(ReachSlack / Step);
                int cx = (int)Math.Floor((x - x0) / Step), cz = (int)Math.Floor((z - z0) / Step);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int ix = cx + dx, iz = cz + dz;
                        if (ix < 0 || iz < 0 || ix >= nx || iz >= nz || !seen[iz * nx + ix]) continue;
                        float ex = x0 + (ix + 0.5f) * Step - x, ez = z0 + (iz + 0.5f) * Step - z;
                        if (ex * ex + ez * ez <= ReachSlack * ReachSlack) return true;
                    }
                return false;
            }'
Save-Src
"p22 done"

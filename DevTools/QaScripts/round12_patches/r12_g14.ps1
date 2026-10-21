# Round 12, batch G14: the docks' pallet blocks are placed BEFORE the box stacks. G10's new keep-clear rectangles (the doorways, the dart board) left so little free floor after the 22 stacks that only one pallet block was placed
# (DockLook: "5 blocks", it wants 6 with the PVP room's four). The pallets (radius 1 m) get their places first, the small stacks fill what is left.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
            int stacks = 22;
            for (int tries = 0; tries < 400 && stacks > 0; tries++)
            {
                float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                if (!Free(x, z, 0.5f)) continue;
                int height = 1 + rng.Next(3);
                DockDressing.BoxStack(dockHolder, x, z, 90f * rng.Next(4), height, rng.Next(1000));
                placed.Add(new Vector3(x, z, 0.55f));
                stacks--; count += height;
            }

            // a few pallets with their boxes squared on them
            for (int tries = 0, pallets = 4; tries < 200 && pallets > 0; tries++)
            {
                float x = F(1.2f, width - 1.2f), z = F(1.2f, depth - 1.2f);
                if (!Free(x, z, 1.0f)) continue;
                DockDressing.BoxBlock(dockHolder, x, z, 90f * rng.Next(4), 2, 2, 1 + rng.Next(2), true, rng.Next(1000));
                placed.Add(new Vector3(x, z, 1.0f));
                pallets--; count += 4;
            }
'@ @'
            // a few pallets with their boxes squared on them (first: they are the big ones, the stacks fill what is left)
            for (int tries = 0, pallets = 4; tries < 200 && pallets > 0; tries++)
            {
                float x = F(1.2f, width - 1.2f), z = F(1.2f, depth - 1.2f);
                if (!Free(x, z, 1.0f)) continue;
                DockDressing.BoxBlock(dockHolder, x, z, 90f * rng.Next(4), 2, 2, 1 + rng.Next(2), true, rng.Next(1000));
                placed.Add(new Vector3(x, z, 1.0f));
                pallets--; count += 4;
            }

            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
            int stacks = 22;
            for (int tries = 0; tries < 400 && stacks > 0; tries++)
            {
                float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                if (!Free(x, z, 0.5f)) continue;
                int height = 1 + rng.Next(3);
                DockDressing.BoxStack(dockHolder, x, z, 90f * rng.Next(4), height, rng.Next(1000));
                placed.Add(new Vector3(x, z, 0.55f));
                stacks--; count += height;
            }
'@
Save-Src
Write-Output 'round 12 batch G14 patched'

# Round 12, batch G31 (after DockSeeds `t14`: 19 of 24 seeds short - drums 0 in most, three forks in a keep-clear rectangle, stacks as low as 8): the docks' scatter is capacity-limited (the floor is mostly rooms, the hallway is cleared), so it is
# placed in ROUNDS: one forklift, one pallet block and one group of drums first (every kind is there), then ten box stacks, then the rest of the big things if there is room (a second forklift, two more pallet blocks, two more groups of drums),
# then the stacks up to 12 - 16. A forklift is checked where it really stands (its middle and the four corners of its box: the forks reach 1.9 m), the pallets and the drums with the radii of their real footprints.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\FloorGenerator.Dock.cs"
RepRegion '// two forklifts (first of all' 'return count;' @'
// the big things in rounds (the owner, 2026-10-20: the hallway is cleared, the rest of the floor is mostly rooms, so what is left holds only a few of them, and every kind must be there): one forklift, one pallet block and one group of
            // drums first, then ten box stacks, then the rest of the big things if there is room, then the stacks up to 12 - 16. Each is checked where it really stands.
            int PlaceForklifts(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f), yaw = F(0f, 360f);
                    var rot = Quaternion.Euler(0f, yaw, 0f);
                    if (!Free(x, z, 1.35f)) continue;
                    bool fits = true;                                                                                       // (the forks point along +z and reach 1.9 m from its middle: the four corners of its box are checked too)
                    foreach (var (lx, lz) in new[] { (0.7f, 1.9f), (-0.7f, 1.9f), (0.7f, -1.4f), (-0.7f, -1.4f) })
                    {
                        var corner = rot * new Vector3(lx, 0f, lz);
                        if (!Free(x + corner.x, z + corner.z, 0.1f)) { fits = false; break; }
                    }
                    if (!fits) continue;
                    DockDressing.Forklift(dockHolder, x, z, yaw);
                    var tip = rot * new Vector3(0f, 0f, 1.5f);
                    placed.Add(new Vector3(x, z, 1.35f));
                    placed.Add(new Vector3(x + tip.x, z + tip.z, 0.45f));
                    done++; count++;
                }
                return done;
            }

            // pallets with their boxes squared on them
            int PlacePallets(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1.2f, width - 1.2f), z = F(1.2f, depth - 1.2f);
                    if (!Free(x, z, 0.9f)) continue;
                    DockDressing.BoxBlock(dockHolder, x, z, 90f * rng.Next(4), 2, 2, 1 + rng.Next(2), true, rng.Next(1000));
                    placed.Add(new Vector3(x, z, 0.9f));
                    done++; count += 4;
                }
                return done;
            }

            // drums in twos and threes
            int PlaceDrums(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                    if (!Free(x, z, 0.85f)) continue;
                    int drums = 2 + rng.Next(2);
                    for (int k = 0; k < drums; k++) DockDressing.Drum(dockHolder, x + (k % 2) * 0.62f, z + (k / 2) * 0.62f, rng.Next(2));
                    placed.Add(new Vector3(x + 0.3f, z + 0.3f, 0.85f));
                    done++; count += drums;
                }
                return done;
            }

            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
            int PlaceStacks(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 2000 && done < n; tries++)
                {
                    float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                    if (!Free(x, z, 0.5f)) continue;
                    int height = 1 + rng.Next(3);
                    DockDressing.BoxStack(dockHolder, x, z, 90f * rng.Next(4), height, rng.Next(1000));
                    placed.Add(new Vector3(x, z, 0.55f));
                    done++; count += height;
                }
                return done;
            }

            int wantStacks = 12 + rng.Next(5);                                      // (12 to 16 whatever the seed)
            PlaceForklifts(1); PlacePallets(1); PlaceDrums(1);
            int haveStacks = PlaceStacks(10);
            PlaceForklifts(1); PlacePallets(2); PlaceDrums(2);
            PlaceStacks(wantStacks - haveStacks);

            return count;
'@
Save-Src
Write-Output 'round 12 batch G31 patched'

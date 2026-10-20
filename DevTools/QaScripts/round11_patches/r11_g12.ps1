# Round 11, batch G12 (the owner, 2026-10-19: "the Vendor's shop should look much nicer with the new assets now" / "the Vendor's truck looking nicer with the new assets"): the vendor's counter gets a dark steel body, a wooden top and a yellow stripe,
# the sign a dark frame, a lamp hangs over the counter, a rubber mat lies in front of it; the tests learn that a second forklift may not fit and that the two bollards are meant to stand in front of the truck.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Src\DockDressing.cs"
Rep @'
        /// <summary>The glowing strip under a trailer's roof, as in the lobby's trailer.</summary>
'@ @'
        /// <summary>The look of the vendor's counter: a dark steel body with a wooden top (its top is at 1.1 m, where the counter's own hidden box has it) and a yellow safety stripe on the customer's side, two boxes on one end and a rubber mat in front.</summary>
        public static void CounterLook(Transform holder, float x, float z, float width, float depth)
        {
            LobbyDock.Box("CounterBody", new Vector3(x, 0.5f, z), new Vector3(width, 1.0f, depth), SteelDark, holder);
            LobbyDock.Box("CounterTop", new Vector3(x, 1.05f, z), new Vector3(width + 0.08f, 0.1f, depth + 0.08f), Wood, holder);
            LobbyDock.Box("CounterStripe", new Vector3(x, 0.72f, z - depth * 0.5f - 0.006f), new Vector3(width, 0.1f, 0.012f), Hazard, holder);
            LobbyDock.Box("CounterBoxA", new Vector3(x - width * 0.5f + 0.4f, 1.1f + 0.19f, z + 0.1f), new Vector3(0.46f, 0.38f, 0.4f), Card, holder, new Vector3(0f, 6f, 0f));
            LobbyDock.Box("CounterBoxB", new Vector3(x - width * 0.5f + 0.4f, 1.1f + 0.38f + 0.14f, z + 0.1f), new Vector3(0.34f, 0.28f, 0.3f), Card2, holder, new Vector3(0f, -9f, 0f));
            LobbyDock.Box("CounterMat", new Vector3(x, 0.012f, z - depth * 0.5f - 0.75f), new Vector3(width - 0.4f, 0.024f, 1.0f), LobbyDock.Mat(new Color(0.09f, 0.09f, 0.10f), 0.05f), holder);
        }

        /// <summary>A dark steel frame round a sign board of <paramref name="w"/> x <paramref name="h"/> centred at (x, y, z).</summary>
        public static void SignFrame(Transform holder, float x, float y, float z, float w, float h)
        {
            const float t = 0.07f;
            LobbyDock.Box("SignFrameTop", new Vector3(x, y + h * 0.5f + t * 0.5f, z), new Vector3(w + t * 2f, t, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameBottom", new Vector3(x, y - h * 0.5f - t * 0.5f, z), new Vector3(w + t * 2f, t, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameLeft", new Vector3(x - w * 0.5f - t * 0.5f, y, z), new Vector3(t, h, 0.12f), SteelDark, holder);
            LobbyDock.Box("SignFrameRight", new Vector3(x + w * 0.5f + t * 0.5f, y, z), new Vector3(t, h, 0.12f), SteelDark, holder);
        }

        /// <summary>A lamp that hangs still (a cord, a shade, a glowing tube; no light of its own) from the ceiling at <paramref name="ceilingY"/>.</summary>
        public static void LampFixture(Transform holder, Vector3 at, float ceilingY, float drop)
        {
            LobbyDock.Cyl("FixtureCord", new Vector3(at.x, ceilingY - drop * 0.5f, at.z), 0.015f, drop, Dark, holder);
            LobbyDock.Cyl("FixtureShade", new Vector3(at.x, ceilingY - drop - 0.05f, at.z), 0.34f, 0.2f, SteelDark, holder);
            LobbyDock.Box("FixtureTube", new Vector3(at.x, ceilingY - drop - 0.17f, at.z), new Vector3(0.42f, 0.04f, 0.42f), LobbyDock.Glow(new Color(1f, 0.9f, 0.7f), 2.0f), holder);
        }

        /// <summary>The glowing strip under a trailer's roof, as in the lobby's trailer.</summary>
'@
Save-Src

Open-Src "$Src\FloorGenerator.Dock.cs"
Rep @'
            Box("VendorCounter", new Vector3(tx, 0.55f, counterZ), new Vector3(TrailerInner - 0.2f, 1.1f, 0.9f), deskMat, root.transform);
'@ @'
            Box("VendorCounter", new Vector3(tx, 0.55f, counterZ), new Vector3(TrailerInner - 0.2f, 1.1f, 0.9f), deskMat, root.transform).GetComponent<Renderer>().enabled = false;          // (its collider stays; DockDressing.CounterLook gives it its look)
'@
Rep @'
            DockDressing.TrailerStrip(h, new Vector3(tx, TrailerHeight - 0.06f, zm - 1.5f), 3.4f);
'@ @'
            DockDressing.TrailerStrip(h, new Vector3(tx, TrailerHeight - 0.06f, zm - 1.5f), 3.4f);
            // the vendor's shop (the owner, 2026-10-19: "the Vendor's truck looking nicer with the new assets"): his counter, his sign and a lamp over the counter
            float zf = zWall + 0.35f + TrailerLength, counterZ = zf - 2.9f;
            DockDressing.CounterLook(h, tx, counterZ, TrailerInner - 0.2f, 0.9f);
            DockDressing.SignFrame(h, tx, 2.45f, zf - 0.28f, 2.0f, 0.55f);
            DockDressing.LampFixture(h, new Vector3(tx, 0f, counterZ - 0.3f), TrailerHeight - 0.02f, 0.35f);
'@
Rep 'tries < 200 && lifts > 0' 'tries < 600 && lifts > 0'
Rep 'if (!Free(x, z, 1.5f)) continue;' 'if (!Free(x, z, 1.35f)) continue;'
Rep 'placed.Add(new Vector3(x, z, 1.5f));' 'placed.Add(new Vector3(x, z, 1.35f));'
Save-Src

Open-Src "$Ed\NetDevDockLook.cs"
Rep 'lifts == 2 && drums >= 4 && bollards == 2' 'lifts >= 1 && drums >= 4 && bollards == 2'
Rep 'two forklifts, drums and two bollards' 'a forklift or two, drums and two bollards'
Rep 'Count(col => col.transform.IsChildOf(holder));' 'Count(col => col.transform.IsChildOf(holder) && col.transform.name != "Bollard");          // (the two bollards stand in front of the truck on purpose)'
Rep @'
            // ---- the lobby's life: the clock tells the real time, two lamps sway, the beacon turns
'@ @'
            // ---- the vendor's shop: the counter keeps its box collider but not its plain look, the counter, the sign and the lamp have their new dress
            var counterOld = vendor.Find("VendorCounter");
            var counterBox = counterOld != null ? counterOld.GetComponent<Collider>() : null;
            var counterRend = counterOld != null ? counterOld.GetComponent<Renderer>() : null;
            DockLookCheck("the vendor's counter: its own box is kept for the collision (hidden), and the steel body, the wooden top, the stripe, the sign frame and the lamp are there",
                counterBox != null && counterRend != null && !counterRend.enabled && holder.Find("CounterBody") != null && holder.Find("CounterTop") != null && holder.Find("CounterStripe") != null && holder.Find("SignFrameTop") != null && holder.Find("FixtureShade") != null);

            // ---- the lobby's life: the clock tells the real time, two lamps sway, the beacon turns
'@
Rep @'
                    ("trailer_inside", gen.DockTrailerEntrance + new Vector3(0f, 1.7f, 2.5f), new Vector3(tx, 1.4f, zWall + 7f)),
'@ @'
                    ("trailer_inside", gen.DockTrailerEntrance + new Vector3(0f, 1.7f, 2.5f), new Vector3(tx, 1.4f, zWall + 7f)),
                    ("vendor_counter", new Vector3(tx + 0.9f, 1.8f, zWall + 0.35f + 8.5f - 5.6f), new Vector3(tx, 1.2f, zWall + 0.35f + 8.5f - 2.2f)),
'@
Save-Src

Open-Src "$Ed\NetDevSneak.cs"
Rep 'forklifts == 2 && drums >= 4' 'forklifts >= 1 && drums >= 4'
Rep 'two forklifts and some drums' 'a forklift or two and some drums'
Save-Src

using System.Collections;
using System.Linq;
using UnityEngine;
using BuildingEscape.Net;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check and pictures of the basement's loading dock in the lobby's clean look (the owner, 2026-10-19: "the assets from the lobby scene look way cleaner ... make the current loading dock look better with all the new assets, the layout is
    /// great. Use the lobby as a reference, for example the clock is great"): the clean props stand where the old ones stood (the way through, the front of the truck and the dummy stay clear), none of the old prop-pack cabinets, shelves, boxes, pallets
    /// or chairs is left in the dock, every prop has ONE box collider on its root, the clock tells the real time, the lobby's life (lamps, beacon) is there. Pictures: %TEMP%\be_shot_docklook_*.png, the lobby's own dock beside them (docklook_lobby_*).
    /// Needs graphics for the pictures: <c>qa_regress.ps1 -Methods DockLook:gfx</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int dockLookFails;

        /// <summary>A cardboard box of the dock's clean props (a box of a stack or of a pallet block).</summary>
        static bool IsDockBox(Transform t) => t && t.name == "Box" && t.parent != null && (t.parent.name == "BoxBlock" || t.parent.name == "BoxStack");

        void DockLookCheck(string name, bool ok, string detail = "")
        {
            if (!ok) dockLookFails++;
            Log($"[DockLook] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator DockLookScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitForSeconds(1.0f);
            var vendor = gen != null && gen.Current ? gen.Current.Find("Vendor") : null;
            DockLookCheck("the basement has its dock root", vendor != null);
            if (vendor == null) { Log($"[DockLook] DONE fails={dockLookFails}"); Quit(1); yield break; }

            // ---- nothing of the old prop pack is left in the dock
            string[] old = { "cabinet_med", "cabinet_small", "kshelve", "bookshelf", "shelf", "box", "pallet", "chair_office", "chair_std", "chair_simple" };          // (the briefcases are back in the vendor's truck: the owner liked them)
            int leftover = vendor.GetComponentsInChildren<Transform>(true).Count(t => old.Contains(t.name));
            DockLookCheck("none of the old prop-pack cabinets, shelves, boxes, pallets or chairs is left in the dock", leftover == 0, $"({leftover} left)");

            // ---- the clean props
            var holder = vendor.Find("DockDressing");
            DockLookCheck("the dock's dressing holder is there, at the world origin", holder != null && holder.position == Vector3.zero);
            if (holder == null) { Log($"[DockLook] DONE fails={dockLookFails}"); Quit(1); yield break; }
            var roots = holder.Cast<Transform>().ToList();
            int Count(string n) => roots.Count(t => t.name == n);
            int stockShelves = Count("StockShelf"), shelves = Count("Shelf"), stacks = Count("BoxStack"), blocks = Count("BoxBlock"), lifts = Count("Forklift"), drums = Count("Drum"), bollards = Count("Bollard");
            DockLookCheck("the clean props are there: steel shelving, box stacks, pallet blocks, a forklift or two, drums and two bollards", shelves >= 4 && stacks >= 10 && blocks >= 6 && lifts >= 1 && drums >= 2 && bollards == 2 && stockShelves == 4,
                $"({shelves} shelves, {stockShelves} stock shelves, {stacks} stacks, {blocks} blocks, {lifts} forklifts, {drums} drums, {bollards} bollards)");
            // ---- the vendor's shop: black-market stock and no cardboard on his shelves, and the briefcases on his floor
            bool noCardboard = true, stocked = true;
            foreach (var ss in roots.Where(t => t.name == "StockShelf"))
            {
                var parts = ss.GetComponentsInChildren<Transform>(true);
                noCardboard &= !parts.Any(t => t.name == "Box" || t.name == "ShelfBox" || t.name == "BoxTape");
                stocked &= parts.Count(t => t.name != "Upright" && t.name != "ShelfBoard" && t != ss) >= 20;
            }
            var cases = vendor.GetComponentsInChildren<Transform>(true).Where(t => t.name == "briefcase").ToList();
            int briefcases = cases.Count;
            bool briefcasesFlat = cases.Count >= 4 && cases.All(t =>
            {
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) return false;
                var bb = rs[0].bounds;
                foreach (var rr in rs) bb.Encapsulate(rr.bounds);
                return bb.size.y < 0.25f && bb.size.y <= Mathf.Min(bb.size.x, bb.size.z) * 1.1f && bb.min.y > 0.1f;          // (lying flat, on a board: not standing, not on the floor)
            });
            DockLookCheck("the vendor's shelves hold black-market stock (gadgets, weapons, clips), not a single cardboard box, and the briefcases lie on his floor", noCardboard && stocked && stockShelves == 4 && briefcasesFlat, $"({stockShelves} stock shelves, {briefcases} briefcases, flat on the shelves {briefcasesFlat})");
            bool oneCollider = true;
            foreach (var r in roots.Where(t => t.name == "Shelf" || t.name == "StockShelf" || t.name == "BoxStack" || t.name == "BoxBlock" || t.name == "Forklift" || t.name == "Drum" || t.name == "Bollard"))
                oneCollider &= r.GetComponentsInChildren<Collider>().Length == 1 && r.GetComponent<BoxCollider>() != null;
            DockLookCheck("every prop has exactly ONE collider, a box on its root (the primitives' own were taken off)", oneCollider);

            // ---- the layout is the same: the way through, the front of the truck and the dummy stay clear of the new props
            var elev = gen.CellCentre(Vector2Int.zero);
            var c = vendor.position;
            float tx = c.x + 5.8f, zWall = c.z + gen.cellSize * 0.5f;
            int Blocking(Vector3 centre, Vector3 size) => Physics.OverlapBox(centre, size * 0.5f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Count(col => col.transform.IsChildOf(holder) && col.transform.name != "Bollard");          // (the two bollards stand in front of the truck on purpose)
            int wayBlocked = Blocking(new Vector3((elev.x + 3f + tx + 3.4f) * 0.5f, 1.2f, c.z), new Vector3(tx + 3.4f - (elev.x + 3f), 2.2f, 6f));
            int truckBlocked = Blocking(new Vector3(tx, 1.2f, zWall - 4.25f), new Vector3(7.2f, 2.2f, 8.5f));
            int dummyBlocked = Blocking(gen.DummyAt + Vector3.up * 1.2f, new Vector3(4.4f, 2.2f, 4.4f));
            DockLookCheck("the way from the elevator to the bay, the front of the truck and the room to swing at the dummy are clear of the new props", wayBlocked == 0 && truckBlocked == 0 && dummyBlocked == 0, $"(way {wayBlocked}, truck {truckBlocked}, dummy {dummyBlocked})");

            // ---- the vendor's shop: the counter keeps its box collider but not its plain look, the counter, the sign and the lamp have their new dress
            var counterOld = vendor.Find("VendorCounter");
            var counterBox = counterOld != null ? counterOld.GetComponent<Collider>() : null;
            var counterRend = counterOld != null ? counterOld.GetComponent<Renderer>() : null;
            DockLookCheck("the vendor's counter: its own box is kept for the collision (hidden), and the steel body, the wooden top, the stripe, the sign frame and the lamp are there",
                counterBox != null && counterRend != null && !counterRend.enabled && holder.Find("CounterBody") != null && holder.Find("CounterTop") != null && holder.Find("CounterStripe") != null && holder.Find("SignFrameTop") != null && holder.Find("FixtureShade") != null);

            // ---- the wall of guns behind the vendor: a bracket for every gun, a gun hangs there only once it is unlocked (the owner, 2026-10-19)
            var gunWall = vendor.GetComponentInChildren<VendorGunWall>(true);
            RunUnlocks.Reset();
            yield return new WaitForSeconds(0.6f);
            DockLookCheck("behind the vendor is a wall of guns with a bracket for every gun, and at the start no gun hangs on it (nothing is unlocked)", gunWall != null && gunWall.kinds != null && gunWall.kinds.Length == 5 && gunWall.kinds.All(k => !gunWall.IsShown(k)));
            RunUnlocks.Unlock(RangedWeapon.Kind.Pew); RunUnlocks.Unlock(RangedWeapon.Kind.Rifle); RunUnlocks.Unlock(RangedWeapon.Kind.Sniper);
            yield return new WaitForSeconds(0.8f);
            DockLookCheck("a gun hangs on the wall once it is unlocked (the pew, the rifle and the sniper rifle here), and only those", gunWall != null && gunWall.IsShown(RangedWeapon.Kind.Pew) && gunWall.IsShown(RangedWeapon.Kind.Rifle) && gunWall.IsShown(RangedWeapon.Kind.Sniper) && !gunWall.IsShown(RangedWeapon.Kind.Shotgun) && !gunWall.IsShown(RangedWeapon.Kind.Smg));

            // ---- the lobby's life: the clock tells the real time, two lamps sway, the beacon turns
            var life = vendor.GetComponent<LobbyDockLife>();
            DockLookCheck("the lobby's life is in the dock: a clock with three hands, two lamps and a turning beacon", life != null && life.clockHour && life.clockMinute && life.clockSecond && life.lamps != null && life.lamps.Length == 2 && life.beaconSpin);
            yield return null;
            if (life != null && life.clockMinute)
            {
                var now = System.DateTime.Now;
                float expected = -(now.Minute + now.Second / 60f) * 6f;
                float shown = life.clockMinute.localEulerAngles.z;
                DockLookCheck("the clock tells the real time (the minute hand is where it should be)", Mathf.Abs(Mathf.DeltaAngle(shown, expected)) < 4f, $"(hand {shown:0.0}, should be {Mathf.Repeat(expected, 360f):0.0})");
            }

            // ---- pictures
            if (gfx && life != null)
            {
                var cam = new GameObject("DockLookCam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f); cam.fieldOfView = 68f; cam.nearClipPlane = 0.1f; cam.enabled = false;
                var clock = life.clockHour ? life.clockHour.parent : null;
                var lift = roots.FirstOrDefault(t => t.name == "Forklift");
                var stock = roots.FirstOrDefault(t => t.name == "StockShelf");
                var shelf = roots.FirstOrDefault(t => t.name == "Shelf");
                var spots = new System.Collections.Generic.List<(string, Vector3, Vector3)>
                {
                    ("hall_from_elevator", new Vector3(elev.x + 5.2f, 1.7f, elev.z), gen.DockTrailerEntrance + Vector3.up * 1.4f),
                    ("bay_wall", new Vector3(c.x - 6f, 1.8f, c.z - 3f), new Vector3(c.x + 2f, 1.6f, zWall)),
                    ("bay_wall_close", new Vector3(tx - 3f, 1.7f, zWall - 5f), new Vector3(tx - 1f, 1.8f, zWall)),
                    ("trailer_inside", gen.DockTrailerEntrance + new Vector3(0f, 1.7f, 2.5f), new Vector3(tx, 1.4f, zWall + 7f)),
                    ("vendor_counter", new Vector3(tx + 0.9f, 1.8f, zWall + 0.35f + 8.5f - 5.6f), new Vector3(tx, 1.2f, zWall + 0.35f + 8.5f - 2.2f)),
                    ("gun_wall", new Vector3(tx - 0.5f, 1.7f, zWall + 0.35f + 8.5f - 4.4f), new Vector3(tx, 1.5f, zWall + 0.35f + 8.5f - 0.2f)),
                };
                if (clock) spots.Add(("clock_wall", clock.position + clock.rotation * new Vector3(0.5f, -0.5f, -7f), clock.position));
                if (stock) spots.Add(("vendor_shelf", stock.TransformPoint(new Vector3(0f, 1.5f, 2.3f)), stock.TransformPoint(new Vector3(0f, 1.2f, 0f))));
                if (lift) spots.Add(("forklift", lift.position + new Vector3(3.2f, 1.9f, -3.2f), lift.position + Vector3.up * 0.8f));
                if (shelf) spots.Add(("shelf", shelf.position + new Vector3(2.4f, 1.7f, 2.4f), shelf.position + Vector3.up * 1.2f));
                foreach (var (name, at, look) in spots)
                {
                    cam.transform.position = at;
                    cam.transform.LookAt(look);
                    yield return null;
                    ShotCam(cam, "docklook_" + name);
                }

                // the lobby's own loading dock, built far away, for the comparison
                var lobbyRoot = new GameObject("LobbyRef").transform;
                lobbyRoot.position = new Vector3(0f, 0f, 600f);
                LobbyDock.Build(lobbyRoot);
                yield return new WaitForSeconds(0.5f);
                foreach (var (name, at, look) in new[] { ("lobby_a", new Vector3(-2f, 1.8f, 599f), new Vector3(2f, 1.8f, 608f)), ("lobby_b", new Vector3(3f, 1.8f, 598f), new Vector3(7f, 1.0f, 605f)) })
                {
                    cam.transform.position = at;
                    cam.transform.LookAt(look);
                    yield return null;
                    ShotCam(cam, "docklook_" + name);
                }
                Destroy(lobbyRoot.gameObject);
                Destroy(cam.gameObject);
            }

            Log($"[DockLook] DONE fails={dockLookFails}");
            Quit(dockLookFails == 0 ? 0 : 1);
        }
    }
}

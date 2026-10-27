using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>Play Mode check of crouching, voice noise and the loading dock.</summary>
    public partial class NetDevRunner
    {
        int sneakFails;

        void SneakCheck(string name, bool ok, string detail = "")
        {
            if (!ok) sneakFails++;
            Log($"[Sneak] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator SneakScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var cap = player.GetComponent<CapsuleCollider>();
            var vis = player.GetComponent<PlayerVisual>();

            // ================= the loading dock (the basement)
            var root = gen.Current;
            SneakCheck("dock: two shut garage doors", root.Find("Vendor/GarageDoor_0") && root.Find("Vendor/GarageDoor_1"));
            SneakCheck("dock: the third door is open", root.Find("Vendor/GarageDoor_2") == null);
            SneakCheck("dock: the trailer is there", root.Find("Vendor/TrailerFloor") && root.Find("Vendor/TrailerRoof") && root.Find("Vendor/TrailerWallL") && root.Find("Vendor/TrailerWallR") && root.Find("Vendor/TrailerFront"));
            var vendor = Vendor.Current;
            SneakCheck("dock: vendor exists", vendor != null && vendor.npc != null);
            var ent = gen.DockTrailerEntrance;
            var counter = gen.DockCounter;
            SneakCheck("dock: the vendor is inside the trailer", vendor && vendor.npc.position.z > ent.z + 3f && Mathf.Abs(vendor.npc.position.x - ent.x) < 1.3f,
                $"(npc {vendor.npc.position}, entrance {ent})");
            // the way in from the bay to the counter is open
            bool clear = !Physics.CapsuleCast(new Vector3(ent.x, 0.6f, ent.z), new Vector3(ent.x, 1.5f, ent.z), 0.45f, Vector3.forward,
                out var hit, counter.z - 1.0f - ent.z, ~0, QueryTriggerInteraction.Ignore);
            SneakCheck("dock: a clear way from the bay to the counter", clear, clear ? "" : $"(blocked by {hit.collider.name} at {hit.distance:0.0} m)");
            var bayCentre = new Vector3(ent.x, 0.6f, gen.CellCentre(new Vector2Int(1, 0)).z);
            var toEntrance = ent - bayCentre; toEntrance.y = 0f;
            bool bayClear = !Physics.CapsuleCast(bayCentre, bayCentre + Vector3.up * 0.9f, 0.45f, toEntrance.normalized, out var bhit, toEntrance.magnitude, ~0, QueryTriggerInteraction.Ignore);
            SneakCheck("dock: nothing stacked in front of the open bay", bayClear, bayClear ? "" : $"(blocked by {bhit.collider.name})");

            // stand at the counter and buy something
            player.TeleportTo(new Vector3(ent.x, 1.1f, counter.z - 2.0f), player.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            hm.AddCash(2000);
            yield return new WaitForSeconds(1.0f);
            SneakCheck("dock: standing in the truck opens the shop", Vendor.Nearby == vendor && vendor.CanTalk, $"(nearby {(Vendor.Nearby == vendor)}, canTalk {vendor.CanTalk})");
            // the crosshair on the dealer lets E talk to him; looking at the trailer wall does not
            {
                var aimEye = player.transform.position + Vector3.back * 2.0f + Vector3.up * 0.4f;     // behind the player (he is looking towards +z)
                var aimHead = vendor.npc.position + Vector3.up * 0.6f;
                var onHim = Crosshair.Resolve(aimEye, aimHead - aimEye, player);
                SneakCheck("dock: the crosshair on the dealer targets him", onHim.Kind == AimKind.Dealer, $"({onHim.Kind})");
                var side = Crosshair.Resolve(aimEye, Vector3.right + Vector3.up * 0.1f, player);
                SneakCheck("dock: the crosshair on the trailer wall does not", side.Kind != AimKind.Dealer, $"({side.Kind})");
            }
            var pack = player.GetComponent<Backpack>();
            int packBefore = pack.Items.Count, cashBefore = hm.Cash;
            var buy = typeof(Vendor).GetMethod("Buy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            buy.Invoke(vendor, new object[] { hm, 4 });          // (the crowbar: the coffee mug, entry 0, is a vending machine's since T6)
            yield return new WaitForSeconds(0.6f);
            SneakCheck("dock: buying from inside the truck works (item in the pack, cash spent)",
                pack.Items.Count == packBefore + 1 && hm.Cash < cashBefore, $"(pack {packBefore} -> {pack.Items.Count}, cash {cashBefore} -> {hm.Cash})");
            // and the shelf point where bought things appear is on the counter in the truck
            SneakCheck("dock: the shelf is on the counter", vendor.shelf && Mathf.Abs(vendor.shelf.position.z - counter.z) < 0.5f && Mathf.Abs(vendor.shelf.position.x - ent.x) < 0.5f);

            // ---- size: wider doors, wider truck
            var door0 = root.Find("Vendor/GarageDoor_0");
            SneakCheck("dock: the garage doors are wide", door0 && door0.localScale.x >= 4.9f, door0 ? $"(width {door0.localScale.x:0.0} m)" : "");
            var tfloor = root.Find("Vendor/TrailerFloor");
            SneakCheck("dock: the trailer is wide", tfloor && tfloor.localScale.x >= 3.7f, tfloor ? $"(outside width {tfloor.localScale.x:0.0} m)" : "");

            // ---- storage: the lobby's clean props in the dock's dressing holder (the owner, 2026-10-19: "the assets from the lobby scene look way cleaner")
            int stacks = 0, blocks = 0, shelves = 0, forklifts = 0, drums = 0;
            var dressing = root.Find("Vendor/DockDressing");
            if (dressing != null)
                foreach (Transform tr in dressing)
                {
                    if (tr.name == "BoxStack") stacks++;
                    else if (tr.name == "BoxBlock") blocks++;
                    else if (tr.name == "Shelf" || tr.name == "StockShelf") shelves++;
                    else if (tr.name == "Forklift") forklifts++;
                    else if (tr.name == "Drum") drums++;
                }
            var kinds = root.Find("Vendor").Cast<Transform>().GroupBy(x => x.name).OrderByDescending(g => g.Count()).Select(g => g.Key + "x" + g.Count());
            Log("[Sneak] info: dock objects: " + string.Join(", ", kinds));
            SneakCheck("dock: a lot of cardboard (stacks and pallet blocks)", stacks + blocks >= 14, $"({stacks} stacks, {blocks} pallet blocks)");
            SneakCheck("dock: steel shelving (the hall's and the trailer's)", shelves >= 6, $"({shelves} shelves)");
            SneakCheck("dock: a forklift or two and some drums", forklifts >= 1 && drums >= 2, $"({forklifts} forklifts, {drums} drums)");
            var elevPt = gen.CellCentre(Vector2Int.zero);
            var bayPt = gen.CellCentre(new Vector2Int(1, 0));
            var from = new Vector3(elevPt.x + 4.8f, 0.6f, bayPt.z);
            var to = new Vector3(ent.x, 0.6f, bayPt.z);
            bool corridor = !Physics.CapsuleCast(from, from + Vector3.up * 0.9f, 0.45f, Vector3.right, out var chit, to.x - from.x, ~0, QueryTriggerInteraction.Ignore);
            SneakCheck("dock: the way from the elevator to the truck is clear of junk", corridor, corridor ? "" : $"(blocked by {chit.collider.name} at {chit.distance:0.0} m)");

            // ---- the vendor's greeting
            var chat = vendor.GetComponent<VendorChat>();
            SneakCheck("dock: the vendor has a chat bubble, and his first line is the greeting", chat != null && chat.lines != null && chat.lines.Length > 0 && chat.lines[0] == CustodyRules.GreetingLine);
            player.TeleportTo(new Vector3(ent.x, 1.1f, counter.z - 3.0f), player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.9f);
            SneakCheck("dock: walking up to him opens the bubble", chat && chat.Visible && chat.Text == CustodyRules.GreetingLine, chat ? $"(visible {chat.Visible}, \"{chat.Text}\")" : "");
            // a real screen capture (the bubble is drawn by the GUI, not by a camera)
            yield return new WaitForSeconds(chat.showSeconds + 1.0f);
            SneakCheck("dock: it fades after a few seconds", chat && !chat.Visible);
            player.TeleportTo(new Vector3(gen.CellCentre(Vector2Int.zero).x + 1f, 1.1f, gen.CellCentre(Vector2Int.zero).z + 6f), player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.6f);
            player.TeleportTo(new Vector3(ent.x, 1.1f, counter.z - 3.0f), player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.9f);
            SneakCheck("dock: it pops up again on the next visit", chat && chat.Visible);

            // ---- the training dummy
            var dummy = TrainingDummy.Current;
            SneakCheck("dock: a training dummy stands in the dock", dummy != null, dummy ? $"(at {dummy.transform.position})" : "");
            if (dummy)
            {
                SneakCheck("dock: it has a health bar", dummy.transform.Find("HealthBar") != null && Mathf.Approximately(dummy.Health, dummy.maxHealth));
                var ctrl = player.GetComponent<MeleeController>();
                var meleeHit = typeof(MeleeController).GetMethod("Hit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var spec = MeleeWeapon.Specs[(int)MeleeWeapon.Kind.Crowbar];
                // A swing goes where the crosshair points (Crosshair.SwingForward: the camera's aim, not the way the body faces), so the
                // camera looks at the dummy as a player's would - or, for the miss, the other way.
                void Swing(bool lookAway = false)
                {
                    var to2 = dummy.transform.position - player.transform.position; to2.y = 0f;
                    player.TeleportTo(dummy.transform.position - to2.normalized * 1.2f + Vector3.up * 1.0f, player.transform.rotation);
                    player.transform.rotation = Quaternion.LookRotation(to2.normalized);
                    Physics.SyncTransforms();
                    var focus = lookAway ? player.transform.position - to2.normalized * 5f + Vector3.up * 1.0f : dummy.transform.position + Vector3.up * 1.0f;
                    AimCamera(Camera.main, player, focus);
                    meleeHit.Invoke(ctrl, new object[] { spec, MeleeWeapon.Kind.Crowbar });
                }
                float before = dummy.Health;
                Swing(true);
                SneakCheck("dock: a swing goes where the crosshair points: with the camera looking away it misses the dummy", Mathf.Approximately(dummy.Health, before), $"({before} -> {dummy.Health})");
                Swing();
                SneakCheck("dock: a hit takes health off the dummy", dummy.Health < before && Mathf.Approximately(before - dummy.Health, spec.damage), $"({before} -> {dummy.Health})");
                // a picture of it half beaten up
                Swing(); Swing(); Swing();
                var dcam = new GameObject("DummyEye").AddComponent<Camera>();
                dcam.clearFlags = CameraClearFlags.SolidColor; dcam.backgroundColor = new Color(0.2f, 0.22f, 0.26f); dcam.fieldOfView = 50f; dcam.enabled = false;
                dcam.transform.position = dummy.transform.position + new Vector3(2.4f, 1.9f, -3.2f);
                dcam.transform.LookAt(dummy.transform.position + Vector3.up * 1.5f);
                yield return null;
                ShotCam(dcam, "dock_dummy");
                int swings = 4;
                while (!dummy.Down && swings < 30) { Swing(); swings++; yield return new WaitForSeconds(0.02f); }
                SneakCheck("dock: it goes down after enough hits", dummy.Down && dummy.Health <= 0f, $"({swings} hits)");
                float h0 = dummy.Health;
                Swing();
                SneakCheck("dock: a downed dummy takes no more", Mathf.Approximately(dummy.Health, h0));
                yield return new WaitForSeconds(dummy.resetSeconds + 0.6f);
                SneakCheck("dock: it stands up again, full", !dummy.Down && Mathf.Approximately(dummy.Health, dummy.maxHealth), $"(health {dummy.Health})");
                // regeneration after a single hit
                Swing();
                float damaged = dummy.Health;
                yield return new WaitForSeconds(dummy.regenDelay + 1.5f);
                SneakCheck("dock: it heals when left alone", dummy.Health > damaged, $"({damaged} -> {dummy.Health})");
            }
            // pictures of the dock
            var eye = new GameObject("DockEye").AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(0.2f, 0.22f, 0.26f);
            eye.fieldOfView = 68f; eye.enabled = false;
            var bay = gen.CellCentre(new Vector2Int(1, 0));
            eye.transform.position = new Vector3(bay.x - 7.5f, 1.9f, bay.z - 7.5f);
            eye.transform.LookAt(new Vector3(ent.x - 2.0f, 1.4f, bay.z + 8.5f));
            yield return null;
            ShotCam(eye, "dock_bay");
            eye.transform.position = new Vector3(ent.x, 1.7f, ent.z - 1.0f);
            eye.transform.LookAt(new Vector3(ent.x, 1.3f, counter.z + 1.5f));
            yield return null;
            ShotCam(eye, "dock_truck_inside");
            eye.transform.position = new Vector3(bay.x + 2f, 14f, bay.z - 1f);
            eye.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            yield return null;
            ShotCam(eye, "dock_overview");

            // ================= crouching
            player.TeleportTo(new Vector3(bay.x - 4f, 1.1f, bay.z - 4f), player.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.6f);
            float standH = cap.height, bottomStand = cap.center.y - cap.height / 2f;
            var noises = new List<float>();
            void OnNoise(NoiseEvent n) { if (n.Source == player.gameObject) noises.Add(n.Loudness); }
            NoiseBus.OnNoise += OnNoise;

            // is the player considered grounded standing on the dock floor?
            var groundedField = typeof(WobblyPlayer).GetField("grounded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool hitFloor = Physics.Raycast(player.transform.position + Vector3.up * 0.1f, Vector3.down, out var gh, 0.35f);
            Log($"[Sneak] info: grounded={groundedField.GetValue(player)} ray hit={hitFloor} {(hitFloor ? gh.collider.name : "")} player y={player.transform.position.y:0.00} capsule centre y={cap.center.y:0.00} height={cap.height:0.00} radius={cap.radius:0.00}");

            // walking
            // (the dock is full of boxes now, so take the fastest the body got to rather than its speed at the end)
            var corridorStart = new Vector3(gen.CellCentre(Vector2Int.zero).x + 5.5f, 1.1f, bay.z - 2.6f);
            player.TeleportTo(corridorStart, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            player.testMove = new Vector3(0f, 0f, 1f);
            noises.Clear();
            float walkSpeed = 0f;
            for (float el = 0f; el < 2.2f; el += Time.deltaTime)
            {
                walkSpeed = Mathf.Max(walkSpeed, new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude);
                yield return null;
            }
            float walkNoise = noises.Count > 0 ? noises.Max() : -1f;
            SneakCheck("walking footsteps carry the normal radius", Mathf.Approximately(walkNoise, player.footstepNoise), $"({walkNoise} m, {noises.Count} steps)");

            // crouched
            player.TeleportTo(corridorStart, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            player.testCrouch = true;
            noises.Clear();
            float crouchSpeed = 0f;
            for (float el = 0f; el < 2.4f; el += Time.deltaTime)
            {
                crouchSpeed = Mathf.Max(crouchSpeed, new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude);
                yield return null;
            }
            float crouchNoise = noises.Count > 0 ? noises.Max() : -1f;
            SneakCheck("crouched footsteps are much quieter", crouchNoise > 0f && Mathf.Approximately(crouchNoise, player.crouchNoise) && crouchNoise < walkNoise * 0.5f,
                $"({crouchNoise} m vs {walkNoise} m, {noises.Count} steps)");
            SneakCheck("crouching is slower", crouchSpeed < walkSpeed * 0.85f, $"({crouchSpeed:0.0} vs {walkSpeed:0.0} m/s)");
            SneakCheck("crouching is flagged", player.IsCrouching);
            SneakCheck("the capsule is lower with the feet in the same place", cap.height < standH * 0.8f && Mathf.Abs((cap.center.y - cap.height / 2f) - bottomStand) < 0.01f,
                $"(height {cap.height:0.00} was {standH:0.00})");
            var model = vis ? vis.Anim : null;
            SneakCheck("the model is squashed down", model && model.transform.localScale.y < model.transform.localScale.x * 0.85f,
                model ? $"(scale {model.transform.localScale})" : "(no model)");

            // sprint is not possible while crouched
            player.testSprint = true;
            noises.Clear();
            yield return new WaitForSeconds(1.5f);
            SneakCheck("no sprinting while crouched", !player.IsSprinting && (noises.Count == 0 || noises.Max() <= player.crouchNoise + 0.01f));
            player.testCrouch = false;
            noises.Clear();
            yield return new WaitForSeconds(1.5f);
            SneakCheck("sprinting is loud", noises.Count > 0 && noises.Max() >= player.sprintNoise - 0.01f, $"({(noises.Count > 0 ? noises.Max() : -1f)} m)");
            player.testSprint = false; player.testMove = null;
            yield return new WaitForSeconds(0.8f);
            SneakCheck("standing up restores the capsule", Mathf.Abs(cap.height - standH) < 0.01f);
            NoiseBus.OnNoise -= OnNoise;

            // ================= voice attracts guards
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 60, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(1f);
            var vn = player.GetComponent<VoiceNoise>();
            SneakCheck("the player carries a VoiceNoise", vn != null);
            SneakCheck("a normal voice carries a long way", vn.RadiusFor(0.5f) >= 12f && vn.RadiusFor(1f) >= 25f && vn.RadiusFor(0.1f) >= 5f,
                $"(0.1 -> {vn.RadiusFor(0.1f):0} m, 0.5 -> {vn.RadiusFor(0.5f):0} m, 1 -> {vn.RadiusFor(1f):0} m)");

            var guards = GuardAI.All.Where(g => g && !g.IsPuppet && !g.Dead).ToList();
            SneakCheck("there are guards", guards.Count >= 2);
            foreach (var g in guards) g.sightRange = 0.1f; // blind, so only hearing can attract it
            var target = guards[0];
            var other = guards[1];
            // put the player 9 m from the first guard, on the NavMesh, and make sure the guard is idle
            Vector3 at = default; bool placed = false;
            foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, new Vector3(1, 0, 1), new Vector3(-1, 0, -1) })
            {
                var p = target.transform.position + dir.normalized * 9f;
                if (NavMesh.SamplePosition(p, out var nh, 2f, NavMesh.AllAreas) && Vector3.Distance(nh.position, target.transform.position) > 7f)
                { at = nh.position; placed = true; break; }
            }
            SneakCheck("found a spot 9 m from a guard", placed);
            if (placed)
            {
                player.TeleportTo(at + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                // far from the second guard so it cannot be the one that hears
                yield return new WaitForSeconds(0.5f);
                var before0 = target.State;
                var dist = Vector3.Distance(player.transform.position, target.transform.position);
                // whisper: below the threshold (minLevel), nothing happens
                vn.SetLevel(vn.minLevel * 0.5f);
                yield return new WaitForSeconds(1.0f);
                SneakCheck("a murmur below the threshold is not heard", target.State == before0 || target.State == GuardState.Patrol,
                    $"(state {target.State}, level {vn.Level:0.00})");
                // talking: the guard comes to look
                vn.SetLevel(0.5f);
                yield return new WaitForSeconds(1.2f);
                SneakCheck("talking at 9 m attracts the guard", target.State == GuardState.Investigate || target.State == GuardState.Calling,
                    $"(state {target.State}, distance {dist:0.0} m, radius {vn.RadiusFor(0.5f):0} m)");
                vn.SetLevel(0f);
                yield return new WaitForSeconds(0.5f);

                // loudness decides the distance: what VoiceNoise puts on the noise channel for a murmur and a loud voice
                var heard = new List<float>();
                void OnVoice(NoiseEvent n) { if (n.Source == player.gameObject) heard.Add(n.Loudness); }
                NoiseBus.OnNoise += OnVoice;
                float murmur = VoiceNoise.LevelFromRms(0.02f, 0.012f);   // a low murmur
                float loud = VoiceNoise.LevelFromRms(0.08f, 0.012f);     // talking loudly (-22 dBFS; from -18 dBFS on it is a shout)
                float shout = VoiceNoise.LevelFromRms(0.30f, 0.012f);    // a shout
                var radii = new List<float>();
                foreach (var lvl in new[] { murmur, loud, shout })
                {
                    heard.Clear();
                    vn.SetLevel(lvl);
                    yield return new WaitForSeconds(0.7f);
                    radii.Add(heard.Count > 0 ? heard.Max() : -1f);
                }
                vn.SetLevel(0f);
                NoiseBus.OnNoise -= OnVoice;
                SneakCheck("the louder the voice, the further the noise carries", radii[0] > 0f && radii[0] < radii[1] && radii[1] < radii[2]
                    && Mathf.Approximately(radii[0], vn.RadiusFor(murmur)) && Mathf.Approximately(radii[2], vn.RadiusFor(shout)),
                    $"(murmur {radii[0]:0.0} m, loud {radii[1]:0.0} m, shout {radii[2]:0.0} m)");            }

            Log($"[Sneak] DONE fails={sneakFails}");
            Quit(sneakFails == 0 ? 0 : 1);
        }
    }
}

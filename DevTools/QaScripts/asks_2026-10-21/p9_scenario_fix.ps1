# Round 2 of the Asks1021 scenario: a test hook for the cursor (a batch run cannot lock it), a person-sized capsule for the carry check, a better layout for the ping items, and pictures of the servers.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- the gun: a test hook for the cursor (batch runs cannot lock it)
Open-Src "$Tree\Assets\Scripts\Guns\GunController.cs"
if (-not $s.Contains('TestCursorLocked')) {
Rep '        bool CanUse() => !Crosshair.MenuOpen && Cursor.lockState == CursorLockMode.Locked && BodyFree();' '        bool CanUse() => !Crosshair.MenuOpen && (TestCursorLocked ?? Cursor.lockState == CursorLockMode.Locked) && BodyFree();'
Rep '        static void ResetTestTrigger() { TestTrigger = null; }' '        static void ResetTestTrigger() { TestTrigger = null; TestCursorLocked = null; }'
Rep '        public static bool? TestTrigger;' @'
        public static bool? TestTrigger;

        /// <summary>A test's own answer to "is the cursor locked in the game" (null: the real one): a batch run cannot lock the cursor.</summary>
        public static bool? TestCursorLocked;
'@
Save-Src
}

$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if (-not $s.Contains('TestCursorLocked')) {
# the gun section
Rep '            AsksCheck("the cursor is in the game (the gun can be used)", Cursor.lockState == CursorLockMode.Locked, $"(cursor {Cursor.lockState})");' '            GunController.TestCursorLocked = true;                                      // (a batch run cannot lock the cursor: the gun is told it is in the game)'
Rep '            GunController.TestTrigger = null;
            yield return new WaitForSeconds(0.3f);' '            GunController.TestTrigger = null; GunController.TestCursorLocked = null;
            yield return new WaitForSeconds(0.3f);'
# the carry check: a person-sized capsule above the floor and the knee-high things
Rep '            var bottom = new Vector3(at.x, floorY + 0.4f, at.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 1.2f, 0.4f, ~0, QueryTriggerInteraction.Ignore))' '            // a person: 0.3 m wide each way, from the knee (above the floor and the low things the planner ignores) up to the head
            var bottom = new Vector3(at.x, floorY + 0.7f, at.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 0.7f, 0.3f, ~0, QueryTriggerInteraction.Ignore))'
# the ping items: not in a line
Rep '            var spare = AsksItem("Spare Clip", eye + forward * 4f + Vector3.down * 0.9f, 0, true);
            var gunPick = RangedWeapon.Create(RangedWeapon.Kind.Smg, eye + forward * 3.5f + Vector3.down * 1.1f + Vector3.right * 0.8f, null, 0f, 1).GetComponent<Pickup>();' '            var side = Vector3.Cross(Vector3.up, forward).normalized;                  // (to the right of the line of sight)
            var spare = AsksItem("Spare Clip", eye + forward * 3.6f + side * 0.9f + Vector3.down * 0.9f, 0, true);
            var gunPick = RangedWeapon.Create(RangedWeapon.Kind.Smg, eye + forward * 3.4f - side * 0.9f + Vector3.down * 1.25f, null, 0f, 1).GetComponent<Pickup>();'
Rep '            bool aimedGun = gunPick && PingSystem.TryAim(eye, (gunPick.transform.position + Vector3.up * 0.05f - eye).normalized, player, out var tGun) && tGun.category == PingCategory.Gear;
            AsksCheck("a ping on a gun lying about: a weapon (gear)", aimedGun);' '            Bounds gunBox = default;
            bool gunHasBox = gunPick && gunPick.TryGetBounds(out gunBox);
            PingTarget tGun = default;
            bool aimedGun = gunHasBox && PingSystem.TryAim(eye, (gunBox.center - eye).normalized, player, out tGun) && tGun.category == PingCategory.Gear;
            AsksCheck("a ping on a gun lying about: a weapon (gear)", aimedGun, gunHasBox ? $"({tGun.category} {tGun.label}, box {gunBox.size:F2})" : "(the gun has no box)");'
# the marker tests: replace by the clip's marker, then the taken item is the watch
Rep '            PingSystem.OnPinged(2, (byte)PingTargetKind.Item, loot.NetId, loot.transform.position);
            AsksCheck("a teammate''s ping (the host''s message) adds a marker of theirs: two markers now", PingSystem.Count == 2 && PingSystem.TryGet(2, out var mk3, out _) && mk3.label.StartsWith("Gold Watch"));
            pack.TryStore(spare);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("an item that is taken into a pack loses its marker", !PingSystem.TryGet(PingSystem.LocalSlot, out _, out _) && PingSystem.Count == 1, $"(markers {PingSystem.Count})");
            Destroy(loot.gameObject);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("... and so does one that is destroyed (banked, sold)", PingSystem.Count == 0, $"(markers {PingSystem.Count})");' '            PingSystem.OnPinged(2, (byte)PingTargetKind.Item, loot.NetId, loot.transform.position);
            AsksCheck("a teammate''s ping (the host''s message) adds a marker of theirs: two markers now", PingSystem.Count == 2 && PingSystem.TryGet(2, out var mk3, out _) && mk3.label.StartsWith("Gold Watch"));
            bool stored = pack.TryStore(loot);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("an item that is taken into a pack loses its marker (the teammate''s marker on the watch is gone, the player''s own on the clip stays)", stored && !PingSystem.TryGet(2, out _, out _) && PingSystem.TryGet(PingSystem.LocalSlot, out _, out _) && PingSystem.Count == 1, $"(stored {stored}, markers {PingSystem.Count})");
            Destroy(spare.gameObject);
            yield return new WaitForSeconds(0.3f);
            AsksCheck("... and so does one that is destroyed (banked, sold)", PingSystem.Count == 0, $"(markers {PingSystem.Count})");'
# pictures of the prototype servers (the owner's picture: the server between two racks), from a side that is open
Rep '                if (pass == 0) { tm.GoToLevel(2);' @'
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    foreach (var srv in FindObjectsByType<HeavyLoot>(FindObjectsSortMode.None).Where(x => x && x.displayName == "Prototype Server").Take(3))
                    {
                        var sb = srv.GetComponentInChildren<Collider>().bounds;
                        foreach (var axis in new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back })
                        {
                            var camPos = sb.center + axis * 3.0f + Vector3.up * 1.2f;
                            if (Physics.Raycast(sb.center + Vector3.up * 0.3f, axis, out var vis, 3.0f, ~0, QueryTriggerInteraction.Ignore) && vis.collider.GetComponentInParent<HeavyLoot>() != srv) continue;
                            var sc = new GameObject("AsksServerCam").AddComponent<Camera>(); sc.enabled = false; sc.fieldOfView = 62f;
                            sc.transform.position = camPos; sc.transform.LookAt(sb.center);
                            ShotCam(sc, $"asks_server_f{hm.Floor}_{(int)srv.transform.position.x}"); Destroy(sc.gameObject);
                            break;
                        }
                    }
                if (pass == 0) { tm.GoToLevel(2);
'@
Save-Src
}
"p9 prepared"

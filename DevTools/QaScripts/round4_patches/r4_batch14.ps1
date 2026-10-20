# Round 4, batch 14 (A3): the Shaft scenario's gate checks follow the owner's new rule: the guard on duty is blind and deaf outside his room while the door is LOCKED; once it is unlocked he sees and
# can leave the outpost (the rest of the block, the calling / hits / knock-out inside the room, runs with the room-only gate as before). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevShaft.cs')
Rep '                var cone = ConeOf(duty);
                if (cone != null)
                {
                    var wide = room.Grow(0.45f);
                    bool all = cone.All(v => wide.Contains(v.x, v.z));
                    ShaftCheck("gate: the cone he shows on the floor stops at the walls of his room, even with the door open", all);
                    var saved0 = duty.noticeGate;
                    duty.noticeGate = null;
                    yield return new WaitForSeconds(0.25f);
                    var cone2 = ConeOf(duty);
                    bool reaches = cone2 != null && cone2.Any(v => !wide.Contains(v.x, v.z));
                    duty.noticeGate = saved0;
                    ShaftCheck("gate: without the gate the same cone would reach out through the open door", reaches);
                    yield return new WaitForSeconds(0.25f);
                }
                else Log("[Shaft] SKIP gate: the cone on the floor is not drawn in this run (no transparent shader)");

                var through = post + (door.Centre - post) * 1.6f;       // a spot out in the corridor, in line with the open door
                GoTo(through);
                yield return new WaitForSeconds(0.7f);
                var saved = duty.noticeGate;
                duty.noticeGate = null;
                bool seenUngated = GuardSees(duty, player);
                duty.noticeGate = saved;
                bool seenGated = GuardSees(duty, player);
                ShaftCheck("gate: out in the corridor, in line with the open door, an ordinary guard would see the player", seenUngated);
                ShaftCheck("gate: the guard on duty does not", !seenGated);' '                // (the owner, 2026-10-17 night: "The outpost guard should be able to leave the outpost": once the lock is open the gate is open, he sees and hears like any guard and may chase out of the room)
                var realGate = duty.noticeGate;
                System.Func<Vector3, bool> roomOnly = p => room.Contains(p.x, p.z);
                var cone = ConeOf(duty);
                if (cone != null)
                {
                    var wide = room.Grow(0.45f);
                    ShaftCheck("gate: once the lock is open the cone he shows on the floor reaches out through the open door (he can leave the outpost)", cone.Any(v => !wide.Contains(v.x, v.z)));
                }
                else Log("[Shaft] SKIP gate: the cone on the floor is not drawn in this run (no transparent shader)");
                duty.noticeGate = roomOnly;       // (the rest of this block tests the calling, the hits and the knock-out inside the room, with the room-only gate as before)

                var through = post + (door.Centre - post) * 1.6f;       // a spot out in the corridor, in line with the open door
                GoTo(through);
                yield return new WaitForSeconds(0.7f);
                duty.noticeGate = null;
                bool seenUngated = GuardSees(duty, player);
                duty.noticeGate = realGate;
                bool seenOpenGate = GuardSees(duty, player);
                duty.noticeGate = roomOnly;
                ShaftCheck("gate: out in the corridor, in line with the open door, an ordinary guard would see the player", seenUngated);
                ShaftCheck("gate: with the door unlocked the guard on duty sees him too (he may leave the outpost)", seenOpenGate);
                bool seenGated = GuardSees(duty, player);
                ShaftCheck("gate: (the room-only gate used for the rest of this block does not see him out there)", !seenGated);'
Save-Src
Write-Output 'round 4 batch 14 patched'

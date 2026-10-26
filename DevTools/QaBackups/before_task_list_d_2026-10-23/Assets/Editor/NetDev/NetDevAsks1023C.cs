using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch B of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, on the real floors): the new guard that comes out of a stairwell 15 seconds after one is eliminated (T13) and the guards who see the body of an eliminated guard
    /// and are alerted (T11). Run: <c>qa_regress_bd.ps1 -Methods Asks1023C</c>; the log ends with <c>[Asks1023C] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23cFails;

        void A23C(string name, bool ok, string detail = "")
        {
            if (!ok) a23cFails++;
            Log($"[Asks1023C] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static List<GuardAI> AliveGuards() => GuardAI.All.Where(g => g && g.enabled && !g.IsPuppet && !g.IsBoss && g.name != "Guard_Outpost" && !g.Dead).ToList();

        static void Eliminate(GuardAI g) { int n = 0; while (!g.Dead && n < 20) { g.BulletHitHost(100f, false, null); n++; } }

        IEnumerator Asks1023CScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fState = typeof(GuardAI).GetField("state", any);
            A23C("(test) the guard's state field is where the test expects it", fState != null);
            if (fState == null) { Quit(1); yield break; }

            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var guards = AliveGuards();
            A23C("(test) floor 2 has guards to try things on", guards.Count >= 5, $"({guards.Count})");
            if (guards.Count < 5) { Quit(1); yield break; }
            GuardDrops.TestRoll = 0;                                           // (no bullets lie about: nothing to do with this test)

            // ---- T22: solo mode: the microphone is heard by the guards (a tone stands in for the microphone; nothing is sent, there is nobody to send it to)
            {
                var vc = BuildingEscape.Net.VoiceChat.Instance;
                A23C("T22: the voice chat exists in a solo run", vc != null);
                var me = WobblyPlayerRegistry.All[0];
                var meBody = me.GetComponent<Rigidbody>();
                var hearer = guards.FirstOrDefault(g => !g.IsBackup && g.State == GuardState.Patrol);
                A23C("(test) a guard on his round to listen", hearer != null);
                if (vc != null && hearer != null && OpenSpot(hearer.transform.position + Vector3.up * 0.9f, 12f, out var talkSpot, true))
                {
                    var away = hearer.transform.position - talkSpot; away.y = 0f;
                    hearer.transform.rotation = Quaternion.LookRotation(away.normalized);                // (he faces away: he hears the voice, he does not see the player)
                    me.TeleportTo(talkSpot + Vector3.up * 1.1f, me.transform.rotation);
                    meBody.linearVelocity = Vector3.zero;
                    vc.Muted = false;
                    A23C("T22: the player is not speaking before the tone starts, and the guards hear nothing", !vc.Speaking && vc.HearingRadius <= 0.01f);
                    vc.UseTestInput(new BuildingEscape.Net.ToneVoiceInput(440f));
                    yield return new WaitForSeconds(1.2f);
                    A23C("T22: with a tone as the microphone the solo player is speaking and his voice carries 30 m", vc.Speaking && vc.HearingRadius > 20f, $"(speaking {vc.Speaking}, radius {vc.HearingRadius:0.0} m)");
                    var voice = me.GetComponent<VoiceNoise>();
                    A23C("T22: the solo player has a voice noise (added for the run) with a level", voice != null && voice.Level > 0.5f, $"(level {(voice ? voice.Level : -1f):0.00})");
                    A23C("T22: a guard 12 m away who did not see him heard it and goes to look where it came from", hearer.State == GuardState.Investigate && Vector3.Distance(hearer.GetComponent<NavMeshAgent>().destination, me.transform.position) < 4f,
                        $"(state {hearer.State}, going to {hearer.GetComponent<NavMeshAgent>().destination}, the player is at {me.transform.position})");
                    vc.Muted = true;                                                                     // (M: muted, the guards hear nothing)
                    yield return new WaitForSeconds(1.2f);
                    A23C("T22: muted, the microphone is shut and the guards hear nothing (the radius falls to 0)", !vc.Speaking && vc.HearingRadius <= 0.01f, $"(speaking {vc.Speaking}, radius {vc.HearingRadius:0.0} m)");
                    foreach (var g in guards) if (g && !g.Dead) fState.SetValue(g, GuardState.Patrol);
                }
            }

            // ---- T13: 15 seconds after a guard is eliminated a new guard comes out of a stairwell
            {
                yield return LoadLevel(hm, 2);                                  // (a fresh floor: the voice test above made guards go and look and call backup, which would change the counts)
                yield return new WaitForSeconds(1.0f);
                gen = FindFirstObjectByType<FloorGenerator>();
                guards = AliveGuards();
                int alive0 = guards.Count;
                var victim = guards[0];
                Eliminate(victim);
                yield return null;
                A23C("T13: the eliminated guard is down and his replacement is waiting", victim.Dead && hm.ReplacementsWaiting == 1 && hm.ReplacementsMade == 0, $"(waiting {hm.ReplacementsWaiting}, made {hm.ReplacementsMade})");
                float t0 = Time.time;
                while (Time.time - t0 < 14f) yield return null;
                A23C("T13: after 14 seconds no new guard has come", hm.ReplacementsMade == 0 && AliveGuards().Count == alive0 - 1, $"(made {hm.ReplacementsMade}, alive {AliveGuards().Count} of {alive0})");
                float madeAt = -1f;
                while (Time.time - t0 < 19f && hm.ReplacementsMade == 0) yield return null;
                if (hm.ReplacementsMade > 0) madeAt = Time.time - t0;
                var fresh = GuardAI.All.FirstOrDefault(g => g && g.name.StartsWith("Replacement_"));
                A23C("T13: a new guard came after about 15 seconds", madeAt >= 14f && madeAt <= 16.5f && fresh != null, $"(made after {madeAt:0.0} s)");
                if (fresh != null)
                {
                    A23C("T13: he came out of a stairwell door (within 3 m of it), is no backup officer and is as many guards alive as before",
                        gen.NearStairDoor(fresh.transform.position, 3f) && !fresh.IsBackup && !fresh.Dead && AliveGuards().Count == alive0, $"(at {fresh.transform.position}, alive {AliveGuards().Count} of {alive0})");
                    var spawnedAt = fresh.transform.position;
                    yield return new WaitForSeconds(2.5f);
                    A23C("T13: and he goes about the floor (after 2.5 s he is more than a metre from the door)", Vector3.Distance(fresh.transform.position, spawnedAt) > 1.0f && !fresh.Dead, $"(moved {Vector3.Distance(fresh.transform.position, spawnedAt):0.0} m, state {fresh.State})");
                }
            }

            // ---- T11: guards see the body of an eliminated guard and are alerted, once per body
            {
                yield return LoadLevel(hm, 2);                                  // (a fresh floor again: no backup has been called yet, no replacement is waiting)
                yield return new WaitForSeconds(1.0f);
                gen = FindFirstObjectByType<FloorGenerator>();
                var alive = AliveGuards();
                A23C("(test) enough guards for the body checks", alive.Count >= 5, $"({alive.Count})");
                if (alive.Count >= 5)
                {
                    var body = alive[0];
                    var watcher = alive[1]; var late = alive[2];
                    // a guard as far from the body as can be, to show that a body is not seen from across the floor
                    var distant = alive.Skip(3).OrderByDescending(g => Vector3.Distance(g.transform.position, body.transform.position)).First();
                    Eliminate(body);
                    yield return null;
                    var bodyAt = body.transform.position;
                    bool open = OpenSpot(bodyAt + Vector3.up * 0.9f, 6f, out var spot, true);
                    A23C("(test) a clear spot 6 m from the body", open);
                    if (open)
                    {
                        // the distant guard looks at the body from where he is: not close enough to see it
                        float far = Vector3.Distance(distant.transform.position, bodyAt);
                        var df = bodyAt - distant.transform.position; df.y = 0f;
                        if (df.sqrMagnitude > 0.01f) distant.transform.rotation = Quaternion.LookRotation(df);
                        // the watcher stands 6 m from the body, facing it, on his round
                        watcher.GetComponent<NavMeshAgent>().Warp(spot);
                        var to = bodyAt - spot; to.y = 0f;
                        watcher.transform.rotation = Quaternion.LookRotation(to);
                        fState.SetValue(watcher, GuardState.Patrol);
                        bool backup0 = hm.BackupCalled;
                        yield return new WaitForSeconds(0.7f);
                        A23C("T11: the guard who sees the body stops and radios for backup", watcher.IsCalling && watcher.State == GuardState.Calling && body.BodyAlarmRaised, $"(state {watcher.State}, calling {watcher.IsCalling}, alarm raised {body.BodyAlarmRaised})");
                        A23C("T11: a guard too far away to see it is not alerted", far < 36f || (!distant.IsCalling && distant.State != GuardState.Calling), $"(he is {far:0} m away, state {distant.State})");
                        yield return new WaitForSeconds(2.6f);
                        A23C("T11: when his call is through backup has been called and he goes to look at the body", !backup0 && hm.BackupCalled && watcher.State == GuardState.Investigate && Vector3.Distance(watcher.GetComponent<NavMeshAgent>().destination, bodyAt) < 2.5f,
                            $"(backup {hm.BackupCalled}, state {watcher.State}, going to {watcher.GetComponent<NavMeshAgent>().destination})");

                        // the same body raises the alarm only once: another guard who comes to look does not call again
                        late.GetComponent<NavMeshAgent>().Warp(spot + new Vector3(0.5f, 0f, 0f));
                        var to2 = bodyAt - late.transform.position; to2.y = 0f;
                        late.transform.rotation = Quaternion.LookRotation(to2);
                        fState.SetValue(late, GuardState.Patrol);
                        yield return new WaitForSeconds(0.9f);
                        A23C("T11: a second guard who sees the same body does not raise the alarm again", !late.IsCalling && late.State != GuardState.Calling, $"(state {late.State})");
                    }
                }
            }

            // ---- T9: the prototype server (two lifters, 12 kg: it fits a pack) dropped from the pack in the elevator stays in the elevator, wherever the player stands
            {
                var car = ElevatorExtraction.Current;
                A23C("T9: the elevator car is there, with its door", car != null && car.doorDir != Vector2.zero);
                var trigger = car ? car.GetComponent<Collider>() : null;
                if (car != null && trigger != null && gen != null)
                {
                    var me2 = WobblyPlayerRegistry.All[0];
                    var body2 = me2.GetComponent<Rigidbody>();
                    var pack2 = me2.GetComponent<Backpack>();
                    SetProp(hm, "Quota", 10000000);                                                      // (banking four servers must not end the floor)
                    var b = trigger.bounds;
                    var doorWay = new Vector3(car.doorDir.x, 0f, car.doorDir.y);
                    var side = new Vector3(-doorWay.z, 0f, doorWay.x);
                    float edge = CarLootRules.InnerHalf - 0.5f;
                    var cases = new[]
                    {
                        ("in the middle, facing the door", b.center, doorWay),
                        ("half a metre from the back wall, facing it", b.center - doorWay * edge, -doorWay),
                        ("half a metre from a side wall, facing it", b.center + side * edge, side),
                        ("in a corner, facing it", b.center - doorWay * edge + side * edge, (side - doorWay).normalized),
                    };
                    foreach (var c in cases)
                    {
                        var go = gen.SpawnLoot(2, new Vector3(c.Item2.x, b.min.y + 1.0f, c.Item2.z) + doorWay * 0.5f, 2);          // (the prototype server)
                        var server = go.GetComponent<Pickup>();
                        yield return new WaitForSeconds(0.4f);
                        me2.TeleportTo(new Vector3(c.Item2.x, b.min.y + 1.1f, c.Item2.z), Quaternion.LookRotation(c.Item3));
                        body2.linearVelocity = Vector3.zero;
                        yield return new WaitForSeconds(0.3f);
                        bool stored = pack2.TryStore(server);
                        yield return null;
                        A23C($"(test) the server goes into the pack ({c.Item1})", stored && pack2.Contains(server), $"(weight {(server ? server.weight : -1f)} kg)");
                        if (!stored) { if (go) Destroy(go); continue; }
                        pack2.Drop(server);
                        float t0 = Time.time, furthest = 0f; bool gone = false;
                        while (Time.time - t0 < 3.5f)
                        {
                            yield return null;
                            if (!server) { gone = true; break; }
                            var off = server.transform.position - b.center;
                            furthest = Mathf.Max(furthest, Mathf.Max(Mathf.Abs(off.x), Mathf.Abs(off.z)));
                        }
                        A23C($"T9: dropped from the pack {c.Item1}, the server stays inside the car and is banked", furthest <= CarLootRules.InnerHalf + 0.05f && (gone || server != null), $"(furthest {furthest:0.00} m from the middle, the wall's face is at {CarLootRules.InnerHalf}; banked {gone})");
                        if (server) Destroy(server.gameObject);
                        yield return null;
                    }
                }
            }

            GuardDrops.TestRoll = -1;
            Log($"[Asks1023C] DONE fails={a23cFails}");
            Quit(a23cFails == 0 ? 0 : 1);
        }
    }
}

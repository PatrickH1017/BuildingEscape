using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 2 (2026-10-17, "make sure no items are floating on tables"): every small thing that stands on a surface - the furniture's own desk items (monitors, keyboards, papers ...),
    /// the valuables, the mugs and the other loot - is dropped a ray straight down (onto the real meshes of the desks and counters, which are given temporary mesh colliders for the test) and
    /// the gap between its underside and what is under it is measured. A gap of more than 3 cm, or nothing at all under its middle, is a floating item. Run: <c>NetDevHarness.FloatQa</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int floatFails;

        void FloatCheck(string name, bool ok, string detail = "")
        {
            if (!ok) floatFails++;
            Log($"[Float] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static bool TryBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        IEnumerator FloatQaScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            foreach (int floor in new[] { 1, 2, 3, 4 })
            {
                TowerManager.Instance.GoToLevel(floor);
                yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
                yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(1.5f);
                var layout = gen.CurrentLayout;
                if (layout == null) { FloatCheck($"floor {floor}: there is a plan", false); continue; }
                var interior = gen.Current.Find("Interior");
                if (interior)
                {
                    var culler = interior.GetComponent<PropCuller>();
                    if (culler) culler.enabled = false;                                          // (the far rooms' furniture is hidden by distance: show all of it for the test)
                    foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;
                }

                // ---- real surfaces: a temporary mesh collider on every piece of furniture and prop (so a ray lands on the model, not on the box the game puts round it)
                var added = new List<MeshCollider>();
                var surfaceOwners = new HashSet<Transform>();
                foreach (var mf in gen.Current.GetComponentsInChildren<MeshFilter>(false))
                {
                    var go = mf.gameObject;
                    if (!mf.sharedMesh || go.GetComponent<MeshCollider>()) continue;
                    var mr = go.GetComponent<MeshRenderer>();
                    if (!mr || !mr.enabled) continue;
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    added.Add(mc);
                }
                Physics.SyncTransforms();

                // ---- candidates: the pieces that stand on something, and the pickups that lie about
                var candidates = new List<(string what, GameObject go)>();
                var byName = new Dictionary<string, List<Transform>>();
                if (interior)
                    foreach (var t in interior.GetComponentsInChildren<Transform>(false))
                    {
                        if (!byName.TryGetValue(t.name, out var l)) byName[t.name] = l = new List<Transform>();
                        l.Add(t);
                    }
                int pieces = 0, unmatched = 0;
                foreach (var p in layout.pieces)
                {
                    if (!p.onTop) continue;
                    pieces++;
                    GameObject found = null;
                    if (byName.TryGetValue(p.key, out var list))
                        foreach (var t in list)
                        {
                            if (!TryBounds(t.gameObject, out var b)) continue;
                            if (Mathf.Abs(b.center.x - p.x) < 0.06f && Mathf.Abs(b.center.z - p.z) < 0.06f && Mathf.Abs(b.min.y - p.y) < 0.03f) { found = t.gameObject; break; }
                        }
                    if (found != null) candidates.Add(($"{p.key} on {(p.host != null ? p.host.key : "?")}", found)); else unmatched++;
                }
                foreach (var pk in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                {
                    if (!pk || !pk.isActiveAndEnabled) continue;
                    if (!TryBounds(pk.gameObject, out var b) || b.min.y < 0.35f) continue;        // (on the floor, or in a hand: not on a table)
                    candidates.Add(($"pickup {pk.Label}", pk.gameObject));
                }

                // ---- the gaps
                int checkedCount = 0, floating = 0, nothing = 0, sunk = 0, hanging = 0;
                var worst = new List<(float gap, string what, Vector3 at)>();
                foreach (var (what, go) in candidates)
                {
                    if (!TryBounds(go, out var b)) continue;
                    checkedCount++;
                    var origin = new Vector3(b.center.x, b.min.y + 0.06f, b.center.z);
                    var hits = Physics.RaycastAll(origin, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore).Where(h => !h.collider.transform.IsChildOf(go.transform)).OrderBy(h => h.distance).ToArray();
                    if (hits.Length == 0) { nothing++; worst.Add((99f, what + " (nothing under it)", b.center)); continue; }
                    float gap = b.min.y - hits[0].point.y;
                    if (gap > 0.03f) { floating++; worst.Add((gap, what, b.center)); }
                    else if (gap < -0.06f) sunk++;
                    else if (b.size.x > 0.06f && b.size.z > 0.06f)
                    {
                        // a 3 x 3 grid over the footprint: an item with half of it out in the air hangs over the edge
                        int unsupported = 0;
                        for (int gx = -1; gx <= 1; gx++)
                            for (int gz = -1; gz <= 1; gz++)
                            {
                                var o2 = new Vector3(b.center.x + gx * 0.4f * b.size.x, b.min.y + 0.06f, b.center.z + gz * 0.4f * b.size.z);
                                var h2 = Physics.RaycastAll(o2, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore).Where(h => !h.collider.transform.IsChildOf(go.transform)).OrderBy(h => h.distance).ToArray();
                                if (h2.Length == 0 || b.min.y - h2[0].point.y > 0.05f) unsupported++;
                            }
                        if (unsupported >= 5) { hanging++; worst.Add((0.5f + unsupported * 0.01f, what + " (hangs over an edge)", b.center)); }
                    }
                }
                foreach (var mc in added) if (mc) Destroy(mc);
                Log($"[Float] floor {floor}: {checkedCount} things on surfaces ({pieces} furniture pieces, {unmatched} not matched to an object), floating {floating}, over nothing {nothing}, sunk in {sunk}, hanging over an edge {hanging}");
                foreach (var g in worst.OrderByDescending(w => w.gap).Take(12)) Log($"[Float]   {g.gap:0.000} m  {g.what}  at {g.at.x:0.0},{g.at.y:0.00},{g.at.z:0.0}");
                var kinds = worst.GroupBy(w => w.what.Split(' ')[0] + " " + (w.what.Contains(" on ") ? w.what.Substring(w.what.IndexOf(" on ") + 4) : "")).OrderByDescending(g => g.Count()).Take(8);
                foreach (var k in kinds) Log($"[Float]   kind {k.Key}: {k.Count()}");
                FloatCheck($"floor {floor}: no item floats above a table or counter, and none stands over nothing", floating == 0 && nothing == 0 && hanging == 0, $"(floating {floating}, over nothing {nothing}, hanging {hanging} of {checkedCount})");
            }
            Log($"[Float] DONE fails={floatFails}");
            Quit(floatFails == 0 ? 0 : 1);
        }

    }
}

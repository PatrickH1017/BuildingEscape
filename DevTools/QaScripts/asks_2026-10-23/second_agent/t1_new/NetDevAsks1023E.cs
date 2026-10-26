using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch D, T1, of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63; offline, on the real floors): floors 1 to 7 are built with the Floor 7 art - the pack's walls and ceiling, its doors and the dressing - and floors 8 and up as before.
    /// It also reports how long each floor took to build (the new art puts thousands of pieces on every floor). Run: <c>qa_regress_bd.ps1 -Methods Asks1023E</c>; the log ends with <c>[Asks1023E] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23eFails;

        void A23E(string name, bool ok, string detail = "")
        {
            if (!ok) a23eFails++;
            Log($"[Asks1023E] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Asks1023EScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;

            foreach (int f in new[] { 1, 2, 4, 6, 7, 8, 9 })
            {
                float t0 = Time.realtimeSinceStartup;
                yield return LoadLevel(hm, f);
                float built = Time.realtimeSinceStartup - t0;
                yield return new WaitForSeconds(0.6f);
                var gen = FindFirstObjectByType<FloorGenerator>();
                var lay = gen != null ? gen.CurrentLayout : null;
                if (gen == null || lay == null) { A23E($"floor {f} has a generator and a layout", false); continue; }
                bool art = FloorArtRules.AppliesTo(f);
                int dressed = lay.pieces.Count(p => p.dressing);
                A23E($"T1: floor {f} {(art ? "has" : "does not have")} the Floor 7 walls and ceiling", gen.WallSkinActive == art && (art ? gen.SkinPieces > 0 && gen.SkinTilesBuilt > 0 : gen.SkinPieces == 0 && gen.SkinTilesBuilt == 0), $"({gen.SkinPieces} wall pieces, {gen.SkinTilesBuilt} ceiling tiles)");
                A23E($"T1: floor {f} {(art ? "has" : "does not have")} the dressing", art ? dressed > 150 : dressed == 0, $"({dressed} dressing pieces of {lay.pieces.Count})");
                A23E($"T1: floor {f} {(art ? "has" : "does not have")} the pack's doors", art ? gen.PackDoorCount > 0 : gen.PackDoorCount == 0, $"({gen.PackDoorCount} doors: {gen.PackDoorsWhite} white, {gen.PackDoorsWood} wood, {gen.PackDoorsGlass} glass)");
                var player = WobblyPlayerRegistry.All[0];
                bool onMesh = NavMesh.SamplePosition(player.transform.position, out _, 2.5f, NavMesh.AllAreas);
                int guards = GuardAI.All.Count(g => g && !g.IsPuppet && !g.IsBoss);
                A23E($"T1: floor {f} still has its guards and the player stands on its floor plan", guards >= GuardRules.PatrolCount(f, gen.baseGuards) && onMesh, $"({guards} guards, the rule says {GuardRules.PatrolCount(f, gen.baseGuards)} patrolling)");
                Log($"[Asks1023E] INFO floor {f} took {built:0.0} s to build ({lay.pieces.Count} pieces, {lay.walls.Count} walls)");
            }

            Log($"[Asks1023E] DONE fails={a23eFails}");
            Quit(a23eFails == 0 ? 0 : 1);
        }
    }
}

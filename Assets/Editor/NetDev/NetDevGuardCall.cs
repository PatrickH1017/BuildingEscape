using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Guards that are hit call for backup when they get up, and backup officers spot a player faster (the owner, 2026-10-20: "Guards that are hit should call for backup upon getting up" / "Backup guards should have a much shorter scan time when a
    /// player enters their vision cone"). On floor 1 nobody can see the player (sight range 0.1 m), so only the blows count: a melee blow knocks a guard over, on his feet he radios (the call ring) and backup is called when it is through; a second
    /// blow cuts the call; a punch does the same; a bullet does not knock over, but (since 2026-10-21) it starts a call too: the shot officer stands and radios at once (the call is cancelled when he is put down in time: scenario <c>Asks1021</c>); the officers that come are backup officers with a quarter of the usual spotting time. Run: <c>qa_regress.ps1 -Methods GuardCall</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int guardCallFails;

        void GuardCallCheck(string name, bool ok, string detail = "")
        {
            if (!ok) guardCallFails++;
            Log($"[GuardCall] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator GuardCallScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 90, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => GuardAI.All.Count(g => g && !g.Dead && !g.IsBoss) >= 3, 30, "three guards on the floor");
            yield return new WaitForSeconds(1.0f);
            foreach (var g in GuardAI.All) if (g) g.sightRange = 0.1f;                 // (nobody sees the player: only the blows count)
            var guards = GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup).ToList();
            var melee = guards[0]; var bullet = guards[1]; var punch = guards[2];
            GuardCallCheck("no backup has been called yet and the guards are ordinary ones (not backup officers)", !hm.BackupCalled && guards.All(g => !g.IsBackup));

            // ---- a melee blow: knocked over, then on his feet he radios; a second blow cuts the call and he gets up and calls again
            melee.MeleeHitHost(1.0f, player.gameObject);
            GuardCallCheck("a melee blow knocks the guard over: he is stunned and not calling yet", melee.State == GuardState.Stunned && !melee.IsCalling);
            yield return new WaitForSeconds(0.5f);
            var meleeVisual = melee.GetComponent<GuardVisual>();
            var meleeWeights = meleeVisual && meleeVisual.Anim ? meleeVisual.Anim.DebugWeights() : null;
            GuardCallCheck("a knocked-over officer stands dazed (the Blink stunned loop), he does not lie down (the owner, 2026-10-20)", meleeWeights != null && meleeVisual.Anim.HasStun && meleeWeights[6] > 0.5f && meleeWeights[3] < 0.2f,
                meleeWeights != null ? $"(weights {string.Join(",", meleeWeights.Select(x => x.ToString("0.00")))}, state {melee.State})" : "(no animator)");
            yield return new WaitForSeconds(1.0f);
            GuardCallCheck("on his feet again he radios for backup where he stands (the call has begun, no officer is sent yet)", melee.State == GuardState.Calling && melee.IsCalling && !hm.BackupCalled, $"(state {melee.State}, calling {melee.IsCalling}, backup {hm.BackupCalled})");
            melee.MeleeHitHost(1.0f, player.gameObject);
            GuardCallCheck("another blow cuts the call and knocks him over again", melee.State == GuardState.Stunned && !melee.IsCalling && !hm.BackupCalled);
            yield return new WaitForSeconds(1.5f);
            GuardCallCheck("and when he gets up the second time he radios again", melee.State == GuardState.Calling && melee.IsCalling && !hm.BackupCalled);
            int before = GuardAI.All.Count(g => g && !g.Dead);
            yield return new WaitForSeconds(melee.callSeconds + 0.8f);
            int after = GuardAI.All.Count(g => g && !g.Dead);
            GuardCallCheck("when the call is through backup is called: officers are sent", hm.BackupCalled && after > before, $"(backup {hm.BackupCalled}, guards {before} -> {after})");
            GuardCallCheck("and he goes after the player who hit him (or to where the blow came from), no longer calling", !melee.IsCalling && (melee.State == GuardState.Chase || melee.State == GuardState.Investigate), $"(state {melee.State})");

            // ---- the officers that came are backup officers: a quarter of the usual spotting time
            var officers = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
            float usual = melee.SpotSecondsAt(10f);
            GuardCallCheck("the officers that came are backup officers and notice a player at 10 m in a quarter of the usual time", officers.Count >= 1 && officers.All(g => g.IsBackup && Mathf.Abs(g.SpotSecondsAt(10f) - GuardRules.SpotSeconds(10f, g.spotBase, g.spotPerMetre) * GuardRules.BackupSpotFactor) < 0.001f) && usual > 1f && !melee.IsBackup,
                $"({officers.Count} officers, usual {usual:0.00} s, backup {(officers.Count > 0 ? officers[0].SpotSecondsAt(10f) : -1f):0.00} s)");

            // ---- a punch knocks over too: he calls on getting up. A bullet does not knock over, but it starts the call at once (the owner, 2026-10-21)
            punch.PunchHost(1.0f, player.gameObject);
            bullet.BulletHitHost(1f, false, player.gameObject);
            GuardCallCheck("a punch knocks a guard over, a bullet does not", punch.State == GuardState.Stunned && bullet.State != GuardState.Stunned);
            GuardCallCheck("a bullet that does not put him down starts his radio call at once: he stands where he is and radios (the owner, 2026-10-21)", bullet.State == GuardState.Calling && bullet.IsCalling, $"(state {bullet.State}, calling {bullet.IsCalling})");
            yield return new WaitForSeconds(1.5f);
            GuardCallCheck("the guard who was punched radios for backup on getting up", punch.State == GuardState.Calling && punch.IsCalling, $"(state {punch.State}, calling {punch.IsCalling})");
            yield return new WaitForSeconds(2.0f);
            GuardCallCheck("the shot guard's call is through by now (backup was called) and he is after the player who shot him", !bullet.IsCalling && (bullet.State == GuardState.Chase || bullet.State == GuardState.Investigate), $"(state {bullet.State})");

            Log($"[GuardCall] DONE fails={guardCallFails}");
            Quit(guardCallFails == 0 ? 0 : 1);
        }
    }
}
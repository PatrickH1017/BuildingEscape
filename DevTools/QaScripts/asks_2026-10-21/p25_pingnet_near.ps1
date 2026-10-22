# PingNet picks the two valuables NEAREST to the elevator (where the players stand), not the two with the lowest network ids: the host takes a ping only from a player within PingRules.MaxRange + HostSlack (90 m) of his body, a floor is 144 m x 90 m and
# the run's seed is random, so the lowest ids could lie further away than that and the host (rightly) dropped the client's ping: a flaky test, not a fault of the game.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev', [string]$File = '')
. "$PSScriptRoot\patchlib.ps1"
$target = if ($File) { $File } else { "$Tree\Assets\Editor\NetDev\NetDevPingNet.cs" }
Open-Src $target
if ($s.Contains('OrderBy(p => (p.transform.position - from).sqrMagnitude)')) { "already: $target"; return }
Rep '        /// <summary>Two pickups worth money on this floor (the same two on every machine: the ids are creation order).</summary>
        static Pickup[] TwoValuables() =>
            NetIds.All.Select(kv => kv.Value as Pickup).Where(p => p && p.isActiveAndEnabled && p.Value >= PingRules.LootMinValue && !p.IsHeld && p.Loot == null).OrderBy(p => p.NetId).Take(2).ToArray();' '        /// <summary>
        /// Two pickups worth money on this floor, the two nearest to the elevator where the players stand (the same two on every machine). Not the two with the lowest ids: the host takes a ping from a player only within
        /// <see cref="PingRules.MaxRange"/> + <see cref="PingRules.HostSlack"/> of his body, and a floor is longer than that, so an item at the far end is (rightly) not pinged from the elevator.
        /// </summary>
        static Pickup[] TwoValuables()
        {
            var tm = TowerManager.Instance;
            var from = tm != null ? tm.CurrentInfo.spawn : Vector3.zero;
            return NetIds.All.Select(kv => kv.Value as Pickup).Where(p => p && p.isActiveAndEnabled && p.Value >= PingRules.LootMinValue && !p.IsHeld && p.Loot == null)
                .OrderBy(p => (p.transform.position - from).sqrMagnitude).ThenBy(p => p.NetId).Take(2).ToArray();
        }'
Save-Src

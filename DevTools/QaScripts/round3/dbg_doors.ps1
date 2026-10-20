# temporary: a debug method that prints why doors narrow instead of sliding
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
$code = @'
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        public static void DoorSlideDebug()
        {
            var stats = new System.Collections.Generic.Dictionary<string, int>();
            for (int seed = 1; seed <= 8; seed++)
            {
                var shape = FloorShape.Maze(new System.Random(seed * 31 + 4), 8, 5, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(4, seed * 31 + 4, 18f, 3.5f, 777));
                var beams = new System.Collections.Generic.List<WallBox>();
                var plans = DoorSlide.ForLayout(layout, 3.5f, null, beams);
                for (int i = 0; i < beams.Count; i++)
                {
                    var r = beams[i].rc; bool ax = r.W >= r.D; float gap = ax ? r.W : r.D, leaf = gap * 0.5f;
                    string key = (plans[i].slide ? "slide" : "SHRINK") + $" gap={gap:0.0} lo={plans[i].lowTravel / leaf:0.0} hi={plans[i].highTravel / leaf:0.0}";
                    stats[key] = stats.TryGetValue(key, out var n) ? n + 1 : 1;
                    if (!plans[i].slide && seed == 1) Debug.Log($"[Dbg] SHRINK beam {r} ax={ax} lo={plans[i].lowTravel:0.00} hi={plans[i].highTravel:0.00}");
                }
            }
            foreach (var kv in stats) Debug.Log($"[Dbg] {kv.Value,5} x {kv.Key}");
        }
    }
}
'@
[IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevDoorDebug.cs", $code.Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))

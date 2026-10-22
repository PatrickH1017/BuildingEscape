# The ping's name shows what a loot item is worth and how many carry it (like the scanner's chip): "Gold Watch  $700", "Prototype Server  $2,500  (x2 carry)".
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Ping\PingRules.cs"
if (-not $s.Contains('int lifters = 1')) {
Rep @'
        /// <summary>The name on a marker: what it is, and for a thing worth money what it is worth.</summary>
        public static string NameOf(string label, PingCategory c, bool locked = false)
        {
            if (c == PingCategory.Door) return locked ? "Locked door" : "Door";
            return string.IsNullOrWhiteSpace(label) ? "Item" : label;
        }
'@ @'
        /// <summary>The name on a marker: what it is, and for loot what it is worth and, when it takes more than one to carry, how many ("Gold Watch  $700", "Prototype Server  $2,500  (x2 carry)").</summary>
        public static string NameOf(string label, PingCategory c, bool locked = false, int value = 0, int lifters = 1)
        {
            if (c == PingCategory.Door) return locked ? "Locked door" : "Door";
            string name = string.IsNullOrWhiteSpace(label) ? "Item" : label;
            if (c != PingCategory.Loot || value <= 0) return name;
            string money = "$" + value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return lifters > 1 ? $"{name}  {money}  (x{lifters} carry)" : $"{name}  {money}";
        }
'@
Save-Src
}

Open-Src "$Tree\Assets\Scripts\Ping\PingSystem.cs"
if (-not $s.Contains('item.Value, item.Loot')) {
Rep 'if (p) { t.category = CategoryOf(p); t.label = PingRules.NameOf(p.Label, t.category); }' 'if (p) { t.category = CategoryOf(p); t.label = PingRules.NameOf(p.Label, t.category, false, p.Value, p.Loot != null ? p.Loot.lifters : 1); }'
Rep 'label = PingRules.NameOf(item.Label, cat), category = cat, distance = itemS };' 'label = PingRules.NameOf(item.Label, cat, false, item.Value, item.Loot != null ? item.Loot.lifters : 1), category = cat, distance = itemS };'
Save-Src
}

# the checks that named the watch
$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if (-not $s.Contains('Contains("$700")')) {
Rep 'tLoot.label == "Gold Watch" &&' 'tLoot.label.StartsWith("Gold Watch") && tLoot.label.Contains("$700") &&'
Rep 'mk.label == "Gold Watch"' 'mk.label.StartsWith("Gold Watch")'
Rep 'mk3.label == "Gold Watch"' 'mk3.label.StartsWith("Gold Watch")'
Save-Src
}
$p2 = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p2
if (-not $s.Contains('Prototype Server  $2,500')) {
Rep '            Check("ping: the signs in the marker are' @'
            Check("ping: loot shows what it is worth and how many carry it, gear and used-up things only their name",
                PingRules.NameOf("Gold Watch", PingCategory.Loot, false, 700) == "Gold Watch  $700" && PingRules.NameOf("Prototype Server", PingCategory.Loot, false, 2500, 2) == "Prototype Server  $2,500  (x2 carry)"
                && PingRules.NameOf("Pew", PingCategory.Gear, false, 900) == "Pew" && PingRules.NameOf("Mug", PingCategory.Consumable, false, 25) == "Mug" && PingRules.NameOf("Gold Watch", PingCategory.Loot) == "Gold Watch");
            Check("ping: the signs in the marker are
'@
Save-Src
}
"p8 prepared"

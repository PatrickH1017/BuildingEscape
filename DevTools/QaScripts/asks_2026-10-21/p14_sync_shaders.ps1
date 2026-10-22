# The server pictures came out pink: the editor compiles a shader variant in the background the first time something draws it and shows a pink stand-in meanwhile (the owner's Editor has the variants cached for days).
# The scenario switches the background compile off while it runs, so a picture is taken with the real materials.
. "$PSScriptRoot\patchlib.ps1"
$p = "$PSScriptRoot\NetDevAsks1021.cs.txt"
Open-Src $p
if ($s.Contains('asyncShaderCompilation')) { "already"; return }
Rep '            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            yield return new WaitForSeconds(1.5f);' '            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            UnityEditor.EditorSettings.asyncShaderCompilation = false;                   // (no pink stand-ins in the pictures while a shader variant compiles in the background)
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            yield return new WaitForSeconds(1.5f);'
Rep '            Log($"[Asks] DONE fails={asksFails}");' '            UnityEditor.EditorSettings.asyncShaderCompilation = true;
            Log($"[Asks] DONE fails={asksFails}");'
Save-Src
"p14 done"

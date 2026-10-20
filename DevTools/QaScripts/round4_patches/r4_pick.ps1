# Round 4 (A7): SyntySheet.Pick = a contact sheet of the prefabs named in a text file (one path per line, relative to Assets/Synty/PolygonOffice/Prefabs), to choose and check props by eye.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\SyntySheet.cs')
Rep '        /// <summary>Imports the pack''s shader graphs again, sub graphs first, and says how each one came out.</summary>' '        /// <summary>
        /// A contact sheet of the prefabs listed in the file SYNTY_LIST (one path per line, relative to the pack''s Prefabs folder, with or without ".prefab"), written to SYNTY_OUT (png).
        /// SYNTY_TILE (pixels, default 192), SYNTY_COLS (default 8) and SYNTY_YAW (degrees the prop is turned, default 160; 180 = seen from +Z, i.e. its front when the front is +Z) are optional.
        /// </summary>
        public static void Pick()
        {
            string Env(string n, string d) { var v = System.Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(v) ? d : v; }
            var lines = File.ReadAllLines(Env("SYNTY_LIST", "")).Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => l.EndsWith(".prefab") ? l : l + ".prefab").Select(l => Root + "/" + l).ToList();
            Sheet(lines, Env("SYNTY_OUT", Path.Combine(Path.GetTempPath(), "synty_pick.png")), null, int.Parse(Env("SYNTY_COLS", "8")), int.Parse(Env("SYNTY_TILE", "192")), float.Parse(Env("SYNTY_YAW", "160"), System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.Exit(0);
        }

        /// <summary>Imports the pack''s shader graphs again, sub graphs first, and says how each one came out.</summary>'
Rep '        static void Sheet(List<string> paths, string file, Texture2D forceAtlas, int cols, int tile)
        {' '        static void Sheet(List<string> paths, string file, Texture2D forceAtlas, int cols, int tile, float yaw = 160f)
        {'
Rep '                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 160f, 0f));
                var rs = inst.GetComponentsInChildren<Renderer>(false).Where(x => x && x.enabled).ToArray();
                if (rs.Length > 0)
                {
                    foreach (var r in rs)' '                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
                var rs = inst.GetComponentsInChildren<Renderer>(false).Where(x => x && x.enabled).ToArray();
                if (rs.Length > 0)
                {
                    foreach (var r in rs)'
Save-Src
Write-Output 'pick tool patched'

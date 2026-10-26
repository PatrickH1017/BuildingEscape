using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Builds the Windows player of the game (the friends' demo): the scenes ticked in File > Build Settings (Boot, Menu, Greybox), 64 bit, the
    /// project's own scripting backend, no development build; then puts <c>steam_appid.txt</c> (480) next to the exe, because Steam needs it
    /// when the game is not started from Steam.
    /// Menu: Employees Take Revenge > Build demo (Windows) builds into &lt;project&gt;/Builds/Demo_&lt;date&gt;_&lt;time&gt; (Builds is ignored by git).
    /// Command line (a project that is NOT open in an Editor, e.g. a scratch copy):
    /// <c>Unity.exe -batchmode -nographics -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.DemoBuild.Build -buildOutput &lt;folder&gt; -logFile &lt;log&gt;</c>.
    /// The built game can be tested without a person: <c>EmployeesTakeRevenge.exe -be-autopilot -be-out &lt;folder&gt;</c> (see QaAutopilot).
    /// </summary>
    public static class DemoBuild
    {
        public const string ProductName = "Employees Take Revenge";
        public const string ExeName = "EmployeesTakeRevenge.exe";

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("Employees Take Revenge/Build demo (Windows)", priority = 50)]
        static void BuildFromMenu()
        {
            string dir = Path.Combine(ProjectRoot, "Builds", "Demo_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm"));
            if (EditorUtility.DisplayDialog("Build the demo", "Build the Windows player into\n" + dir + "\n\nThis takes a few minutes and the editor is busy meanwhile.", "Build", "Cancel"))
                Run(dir, false);
        }

        /// <summary>Command line entry: reads <c>-buildOutput &lt;folder&gt;</c>.</summary>
        public static void Build()
        {
            string dir = Arg("-buildOutput");
            if (string.IsNullOrEmpty(dir)) { Debug.LogError("[DemoBuild] pass -buildOutput <folder>"); EditorApplication.Exit(2); return; }
            Run(dir, true);
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        static void Run(string dir, bool exitAfter)
        {
            PlayerSettings.productName = ProductName;
            var backend = Arg("-buildBackend");
            if (!string.IsNullOrEmpty(backend))
            {
                // (the friends' build can be made with IL2CPP: the game's code is then native code with a metadata file, not a plain .NET assembly that any decompiler reads)
                var impl = backend.ToLowerInvariant() == "il2cpp" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
                PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, impl);
                Debug.Log($"[DemoBuild] scripting backend {impl}");
            }        // (the window title, the exe's properties and the folder of the game's log are named after it)
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            Debug.Log($"[DemoBuild] building {scenes.Length} scenes ({string.Join(", ", scenes)}) to {options.locationPathName}");
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[DemoBuild] result {s.result}: {s.totalErrors} errors, {s.totalWarnings} warnings, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:0} s, output {dir}");
            if (s.result == BuildResult.Succeeded)
            {
                File.Copy(Path.Combine(ProjectRoot, "steam_appid.txt"), Path.Combine(dir, "steam_appid.txt"), true);
                Debug.Log("[DemoBuild] steam_appid.txt copied next to the exe");
            }
            if (exitAfter) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}

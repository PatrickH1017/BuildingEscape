using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dock radio's catalog (<see cref="RadioCatalog"/>, <c>Assets/Resources/RadioCatalog.asset</c>; the owner, 2026-10-27): the POLYGON Construction radio and the tracks of the three stations - Midnight Mode vol.1 (the ten full
    /// versions, not the loops), the FREE Energy Hard Rock Music Pack (the ten demos) and Urban Jazz (the two standard versions, with and without the solo). The tracks are set to stream from disk, compressed (Vorbis): the packs are
    /// 680 MB of WAV, and only what the catalog names goes into a build. Menu: Employees Take Revenge > Setup Dock Radio. Batch: -executeMethod BuildingEscape.EditorTools.RadioSetup.BuildBatch
    /// </summary>
    public static class RadioSetup
    {
        const string CatalogPath = "Assets/Resources/" + RadioCatalog.ResourceName + ".asset";
        const string RadioPrefab = "Assets/Synty/PolygonConstruction/Prefabs/Props/SM_Prop_Radio_01.prefab";
        /// <summary>Each station's folder and which of its files are the station's (a part of the file name; empty: all).</summary>
        static readonly (string folder, string[] only)[] Stations =
        {
            ("Assets/Midnight Mode vol.1/FULL", new string[0]),
            ("Assets/Energy Hard Rock Pack DEMO", new string[0]),
            ("Assets/Urban Jazz/WAV", new[] { "(STANDARD_WITH_SOLO)", "(STANDARD_WITHOUT_SOLO)" }),
        };
        /// <summary>The compression of the tracks (0..1: Vorbis quality; 0.6 keeps the music clean at about a tenth of the WAV).</summary>
        const float Quality = 0.6f;

        [MenuItem("Employees Take Revenge/Setup Dock Radio")]
        static void Menu()
        {
            bool ok = Setup(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Dock radio", ok ? "The dock radio is set up." : "Something went wrong - see the Console.\n\n" + report, "OK");
        }

        public static void BuildBatch()
        {
            bool ok = Setup(out string report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Setup(out string report)
        {
            var log = new System.Text.StringBuilder("[Radio] setup\n");
            bool ok = true;
            var radio = AssetDatabase.LoadAssetAtPath<GameObject>(RadioPrefab);
            if (!radio) { log.AppendLine("[Radio] FAIL: no radio at " + RadioPrefab); ok = false; }
            var stations = new RadioCatalog.Station[Stations.Length];
            for (int s = 0; s < Stations.Length; s++)
            {
                var (folder, only) = Stations[s];
                var paths = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.wav").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList() : new System.Collections.Generic.List<string>();
                if (only.Length > 0) paths = paths.Where(p => only.Any(o => Path.GetFileName(p).Contains(o))).ToList();
                foreach (var p in paths) Compress(p);
                var clips = paths.Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p)).Where(c => c).ToArray();
                stations[s] = new RadioCatalog.Station { name = RadioRules.Names[s], tracks = clips };
                log.AppendLine($"[Radio] station {s + 1} {RadioRules.Names[s]}: {clips.Length} tracks from {folder} ({clips.Sum(c => c.length) / 60f:0.0} min)");
                if (clips.Length == 0) ok = false;
            }
            var cat = AssetDatabase.LoadAssetAtPath<RadioCatalog>(CatalogPath);
            if (!cat)
            {
                cat = ScriptableObject.CreateInstance<RadioCatalog>();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            cat.radio = radio;
            cat.stations = stations;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            log.Append(ok ? "[Radio] DONE ok" : "[Radio] DONE with FAILURES");
            report = log.ToString();
            return ok;
        }

        /// <summary>A track streams from disk, compressed, and loads in the background (it is not held in memory whole).</summary>
        static void Compress(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) return;
            var want = imp.defaultSampleSettings;
            bool change = want.loadType != AudioClipLoadType.Streaming || want.compressionFormat != AudioCompressionFormat.Vorbis || Mathf.Abs(want.quality - Quality) > 0.001f || !imp.loadInBackground;
            if (!change) return;
            want.loadType = AudioClipLoadType.Streaming;
            want.compressionFormat = AudioCompressionFormat.Vorbis;
            want.quality = Quality;
            want.preloadAudioData = false;
            imp.defaultSampleSettings = want;
            imp.loadInBackground = true;
            imp.SaveAndReimport();
        }
    }
}

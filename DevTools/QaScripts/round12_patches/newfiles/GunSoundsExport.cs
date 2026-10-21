using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Writes the synthesised silenced shots of the guns (the owner, 2026-10-20: "the weapons should sound silenced") as WAV files, so that they can be listened to - and compared with the pack's loud recordings - without starting the game:
    /// Employees Take Revenge > Export silenced shot samples (into DevTools/AudioSamples), or <c>Unity -batchmode -executeMethod BuildingEscape.EditorTools.GunSoundsExport.ExportBatch -outDir &lt;folder&gt;</c>.
    /// </summary>
    public static class GunSoundsExport
    {
        static string DefaultDir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DevTools", "AudioSamples");

        [MenuItem("Employees Take Revenge/Export silenced shot samples", priority = 90)]
        static void Menu() => Export(DefaultDir);

        public static void ExportBatch()
        {
            try
            {
                var args = System.Environment.GetCommandLineArgs();
                string dir = null;
                for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-outDir") dir = args[i + 1];
                Export(dir ?? DefaultDir);
            }
            finally { EditorApplication.Exit(0); }
        }

        static void Export(string dir)
        {
            Directory.CreateDirectory(dir);
            int n = 0;
            foreach (var (set, tag) in new[] { (RangedWeapon.SoundSet.Handgun, "handgun"), (RangedWeapon.SoundSet.Rifle, "rifle") })
            {
                var clips = GunSounds.SilencedShots(set);
                for (int i = 0; i < clips.Length; i++) { Write(Path.Combine(dir, $"silenced_{tag}_{i + 1}.wav"), clips[i]); n++; }
            }
            Write(Path.Combine(dir, "silenced_tail.wav"), GunSounds.SilencedTail()); n++;
            Debug.Log($"[GunSoundsExport] {n} files written to {dir}");
        }

        static void Write(string path, AudioClip clip)
        {
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)clip.channels); w.Write(clip.frequency); w.Write(clip.frequency * clip.channels * 2); w.Write((short)(clip.channels * 2)); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var f in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(f * 32767f), -32768, 32767));
            }
        }
    }
}

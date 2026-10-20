using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The settings controls, drawn by the lobby's Settings page and by the Esc menu's Settings page (the owner, 2026-10-18: "There should be a Settings button on escape menu"): master and music volume, mouse sensitivity,
    /// voice chat (the mode and the volume of the voices), fullscreen and a Keybinds button. One place, so the two pages cannot drift apart; what they change is saved the way it always was (PlayerPrefs, the
    /// <see cref="MusicDirector"/>, the voice chat's own settings).
    /// </summary>
    public static class SettingsPanel
    {
        /// <summary>The master volume's key (shared by the lobby menu and the in-run Esc menu).</summary>
        public const string VolumeKey = "OvertimeEscape.MasterVolume";
        /// <summary>The mouse sensitivity's key (the camera reads it).</summary>
        public const string SensitivityKey = "OvertimeEscape.MouseSensitivity";

        /// <summary>The Settings button's colour, in the lobby and in the Esc menu.</summary>
        public static readonly Color Violet = new Color32(0x8A, 0x5C, 0xFF, 255);

        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };

        /// <summary>Draws the controls into <paramref name="f"/> (a flow that is already placed); returns true when the KEYBINDS button was pressed.</summary>
        public static bool DrawControls(MenuKit.Flow f)
        {
            float vol = MenuKit.SliderRow(f, "MASTER VOLUME", Mathf.RoundToInt(AudioListener.volume * 100f) + "%", AudioListener.volume, 0f, 1f, HudTheme.Cash);
            if (!Mathf.Approximately(vol, AudioListener.volume))
            {
                AudioListener.volume = vol;
                PlayerPrefs.SetFloat(VolumeKey, vol);
            }
            float music = MenuKit.SliderRow(f, "MUSIC VOLUME", Mathf.RoundToInt(MusicDirector.Volume * 100f) + "%", MusicDirector.Volume, 0f, 1f, HudTheme.Info);
            if (!Mathf.Approximately(music, MusicDirector.Volume)) MusicDirector.Volume = music;

            float sens = PlayerPrefs.GetFloat(SensitivityKey, 2.4f);
            float ns = MenuKit.SliderRow(f, "MOUSE SENSITIVITY", sens.ToString("0.0"), sens, 0.5f, 6f, HudTheme.Info);
            if (!Mathf.Approximately(ns, sens)) PlayerPrefs.SetFloat(SensitivityKey, ns);

            f.Gap(6f);
            MenuKit.Header(f.Row(28f, 8f), "VOICE CHAT", HudTheme.Warning);
            int vm = MenuKit.Choice(f, VoiceModes, (int)Net.VoiceChat.CurrentMode, 1, HudTheme.Quota, 44f, 18f);
            if (vm != (int)Net.VoiceChat.CurrentMode) Net.VoiceChat.CurrentMode = (Net.VoiceChat.Mode)vm;
            f.Gap(4f);
            float vv = MenuKit.SliderRow(f, "VOICES", Mathf.RoundToInt(Net.VoiceChat.Volume * 100f) + "%", Net.VoiceChat.Volume, 0f, 1f, HudTheme.Warning);
            if (!Mathf.Approximately(vv, Net.VoiceChat.Volume)) Net.VoiceChat.Volume = vv;

            f.Gap(6f);
            bool fs = MenuKit.ToggleRow(f.Row(52f, 14f), "FULLSCREEN", Screen.fullScreen);
            if (fs != Screen.fullScreen) Screen.fullScreen = fs;

            return MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f);
        }
    }
}

using System.Collections;
using System.Reflection;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The audio device choice (the owner, 2026-10-24: "Please add Audio Options so players can choose the audio input/output devices they want to use in-game"). The pure rules and the voice chat's choice of input are checked in
    /// <c>NetDevTest.PolicyTest</c> (<see cref="NetDevTest.AudioDevicesTest"/>); <c>AudioDevicesUi</c> (<c>ui.ps1 -Project C:\bd -Methods AudioDevicesUi</c>, a real window) takes pictures of the Esc menu's Settings page with
    /// three microphones, scrolled to the bottom, and with none: %TEMP%\be_ui_audio_*.png. It uses a made-up list of microphones and puts the player's own pick back afterwards.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void AudioDevicesTest()
        {
            var mics = new[] { "Microphone (Realtek High Definition Audio)", "Headset Microphone (USB Audio Device)", "Line In" };
            Check("audio: a saved pick is found by its NAME in the list of plugged-in microphones; nothing saved, or a pick that is not plugged in, is the default (-1)",
                AudioDeviceRules.Resolve("Line In", mics) == 2 && AudioDeviceRules.Resolve("Headset Microphone (USB Audio Device)", mics) == 1 && AudioDeviceRules.Resolve("", mics) == -1 && AudioDeviceRules.Resolve(null, mics) == -1
                && AudioDeviceRules.Resolve("Gone Mic", mics) == -1 && AudioDeviceRules.Resolve("Line In", null) == -1 && AudioDeviceRules.Resolve("Line In", new string[0]) == -1);
            Check("audio: the microphone to open is the pick while it is plugged in, otherwise the default (an empty name)",
                AudioDeviceRules.Chosen("Line In", mics) == "Line In" && AudioDeviceRules.Chosen("Gone Mic", mics) == "" && AudioDeviceRules.Chosen("", mics) == "" && AudioDeviceRules.Chosen("Line In", new string[0]) == "");
            var labels = AudioDeviceRules.Labels(mics);
            Check("audio: the list has the default first and then one button for each microphone; a long name is cut to 34 letters with '...'",
                labels.Length == 4 && labels[0] == AudioDeviceRules.DefaultLabel && labels[3] == "Line In" && labels[1].Length == AudioDeviceRules.LabelChars && labels[1].EndsWith("...") && labels[2].Length == AudioDeviceRules.LabelChars && AudioDeviceRules.Labels(null).Length == 1);
            var twins = AudioDeviceRules.Labels(new[] { "USB Mic", "USB Mic", "USB Mic" });
            Check("audio: microphones with the same name are told apart by numbers", twins[1] == "USB Mic" && twins[2] == "USB Mic (2)" && twins[3] == "USB Mic (3)");
            Check("audio: the lit button is the default for no pick (or a pick that is gone), else the pick's place plus one; a button saves its device's name, the default saves an empty name",
                AudioDeviceRules.SelectedOption("", mics) == 0 && AudioDeviceRules.SelectedOption("Gone Mic", mics) == 0 && AudioDeviceRules.SelectedOption("Line In", mics) == 3
                && AudioDeviceRules.NameForOption(0, mics) == "" && AudioDeviceRules.NameForOption(1, mics) == mics[0] && AudioDeviceRules.NameForOption(3, mics) == "Line In" && AudioDeviceRules.NameForOption(4, mics) == "" && AudioDeviceRules.NameForOption(-1, mics) == "");
            Check("audio: Steam's own voice is used only while no microphone was picked and Steam is up (a picked one is opened by Unity: Steam records only the device Windows uses by default)",
                AudioDeviceRules.SteamVoice("", true) && !AudioDeviceRules.SteamVoice("", false) && !AudioDeviceRules.SteamVoice("Line In", true) && !AudioDeviceRules.SteamVoice("Line In", false));

            // the voice chat's choice of input (nothing is opened: only SetActive opens a microphone)
            var viaSteam = VoiceChat.MakeInput("", true);
            var picked = VoiceChat.MakeInput("Line In", true);
            var plain = VoiceChat.MakeInput("", false);
            Check("audio: the voice chat makes Steam's input for no pick, and the pick's own microphone input (named) for a pick, Steam up or not",
                viaSteam is SteamVoiceInput && picked is MicrophoneVoiceInput pm && pm.DeviceName == "Line In" && plain is MicrophoneVoiceInput pl && pl.DeviceName == "");
            viaSteam.Dispose(); picked.Dispose(); plain.Dispose();

            // the saved pick and the list of the machine (a made-up list; the player's own pick is put back)
            bool had = UnityEngine.PlayerPrefs.HasKey(AudioDevices.MicKey);
            string before = AudioDevices.SavedMic;
            try
            {
                AudioDevices.TestInputs = new[] { "Fake Mic A", "Fake Mic B" };
                AudioDevices.SavedMic = "Fake Mic B";
                bool plugged = AudioDevices.ChosenMic == "Fake Mic B" && AudioDevices.InputLabels.Length == 3;
                AudioDevices.TestInputs = new[] { "Fake Mic A" };
                bool unplugged = AudioDevices.ChosenMic == "" && AudioDevices.SavedMic == "Fake Mic B";
                AudioDevices.TestInputs = new[] { "Fake Mic B", "Fake Mic A" };
                bool back = AudioDevices.ChosenMic == "Fake Mic B";
                Check("audio: the pick is used while that microphone is plugged in, falls back to the default when it is unplugged (the pick is kept), and is used again when it is back, wherever it stands in the list", plugged && unplugged && back);
            }
            finally
            {
                AudioDevices.TestInputs = null;
                AudioDevices.SavedMic = before;
                if (!had) UnityEngine.PlayerPrefs.DeleteKey(AudioDevices.MicKey);
                AudioDevices.ForceRefresh();
            }
            Check("audio: the speakers are picked in Windows' own page (Unity cannot pick them): the settings page opens 'App volume and device preferences'", AudioDevices.SoundSettingsUri == "ms-settings:apps-volume");
        }
    }

    public partial class NetDevRunner
    {
        IEnumerator AudioDevicesUiScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            yield return new WaitForSeconds(1.0f);
            bool had = PlayerPrefs.HasKey(AudioDevices.MicKey);
            string before = AudioDevices.SavedMic;
            AudioDevices.TestInputs = new[] { "Microphone (Realtek High Definition Audio)", "Headset Microphone (USB Audio Device)", "Line In" };
            AudioDevices.SavedMic = "Headset Microphone (USB Audio Device)";
            EscapeMenu.OpenForTests(true);
            yield return new WaitForSeconds(0.8f);
            yield return Snap("audio_top");
            var inst = typeof(EscapeMenu).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var scroll = (MenuKit.Scroll)typeof(EscapeMenu).GetField("settingsScroll", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inst);
            scroll.pos = 10000f;
            yield return new WaitForSeconds(0.5f);
            yield return Snap("audio_bottom");
            AudioDevices.TestInputs = new string[0];
            yield return new WaitForSeconds(0.5f);
            yield return Snap("audio_nomic");
            EscapeMenu.CloseAll();
            AudioDevices.TestInputs = null;
            AudioDevices.SavedMic = before;
            if (!had) PlayerPrefs.DeleteKey(AudioDevices.MicKey);
            AudioDevices.ForceRefresh();
            Log("[AudioDevices] DONE pictures");
            Quit(0);
        }
    }
}

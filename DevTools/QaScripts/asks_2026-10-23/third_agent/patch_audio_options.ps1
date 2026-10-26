# The owner, 2026-10-24: "Please add Audio Options so players can choose the audio input/output devices they want to use in-game".
#  - AudioDeviceRules (pure) + AudioDevices (the machine's microphones, the saved pick, the way to Windows' page for the speakers);
#  - the voice chat opens the picked microphone itself (a pick cannot go through Steam's voice recording, which takes Windows' default device only) and switches when the pick changes;
#  - SettingsPanel (the lobby's and the Esc menu's Settings page): an AUDIO DEVICES section: MICROPHONE (a list of buttons) and SPEAKERS (a button that opens Windows' App volume and device preferences: Unity has no call to pick the output device).
# Patches the scratch copy C:\bd; exact matches only (patchlib); new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'audio_new\AudioDeviceRules.cs') ($A + 'Scripts\Audio\AudioDeviceRules.cs')
Put-Crlf (Join-Path $here 'audio_new\AudioDevices.cs') ($A + 'Scripts\Audio\AudioDevices.cs')
Put-Crlf (Join-Path $here 'audio_new\NetDevAudioDevices.cs') ($A + 'Editor\NetDev\NetDevAudioDevices.cs')

# ---- the microphone input opens the device it is asked for
Open-Src ($A + 'Scripts\Net\Voice\VoiceCodec.cs')
Rep @'
        string device;
        AudioClip clip;
        int readPos;

        public void SetActive(bool on)
        {
            if (on && clip == null)
            {
                if (Microphone.devices.Length == 0) return;
                device = Microphone.devices[0];
                clip = Microphone.Start(device, true, 1, Rate);
                readPos = 0;
            }
'@ @'
        string device;
        AudioClip clip;
        int readPos;
        readonly string wanted;

        /// <param name="deviceName">The microphone to open (the player's pick in the settings, the owner, 2026-10-24); "" opens the first one Windows lists, its default.</param>
        public MicrophoneVoiceInput(string deviceName = "") { wanted = deviceName ?? ""; }

        /// <summary>The microphone this input was asked to open ("": the default).</summary>
        public string DeviceName => wanted;

        public void SetActive(bool on)
        {
            if (on && clip == null)
            {
                if (Microphone.devices.Length == 0) return;
                device = wanted.Length > 0 ? wanted : Microphone.devices[0];
                clip = Microphone.Start(device, true, 1, Rate);
                if (clip == null && wanted.Length > 0)            // (the pick will not open - in use, or gone: the default instead)
                {
                    device = Microphone.devices[0];
                    clip = Microphone.Start(device, true, 1, Rate);
                }
                readPos = 0;
            }
'@
Save-Src

# ---- the voice chat: the picked microphone, and a switch when the pick changes
Open-Src ($A + 'Scripts\Net\Voice\VoiceChat.cs')
Rep '        IVoiceInput input;' @'
        IVoiceInput input;
        string inputFor = "";          // (the microphone the open input was made for: "" is the default; see PickInput)
'@
Rep @'
        IVoiceInput PickInput()
        {
            var steam = SteamService.Instance;
            if (steam != null && steam.Ready) return new SteamVoiceInput();
            return new MicrophoneVoiceInput();
        }
'@ @'
        IVoiceInput PickInput()
        {
            inputFor = AudioDevices.ChosenMic;
            var steam = SteamService.Instance;
            return MakeInput(inputFor, steam != null && steam.Ready);
        }

        /// <summary>
        /// The input for the microphone the player picked in the settings (the owner, 2026-10-24: "Please add Audio Options so players can choose the audio input/output devices"); <paramref name="chosen"/> is "" when none was picked.
        /// A pick is opened by Unity itself (Steam's voice recording takes only the device Windows uses by default and cannot be pointed at another); with no pick it is Steam's voice while Steam is up, else the first microphone.
        /// </summary>
        public static IVoiceInput MakeInput(string chosen, bool steamReady)
        {
            if (AudioDeviceRules.SteamVoice(chosen, steamReady)) return new SteamVoiceInput();
            return new MicrophoneVoiceInput(chosen);
        }
'@
Rep '            if (wantMic && input == null) input = PickInput();' @'
            if (input != null && !testInput && inputFor != AudioDevices.ChosenMic) { input.Dispose(); input = null; inputActive = false; }          // (another microphone was picked in the settings: the next one opens that)
            if (wantMic && input == null) input = PickInput();
'@
Save-Src

# ---- the settings pages (the lobby's and the Esc menu's)
Open-Src ($A + 'Scripts\Hud\SettingsPanel.cs')
Rep '    /// voice chat (the mode and the volume of the voices), fullscreen and a Keybinds button.' '    /// voice chat (the mode and the volume of the voices), the audio devices (the microphone from a list; the speakers through Windows'' own page), fullscreen and a Keybinds button.'
Rep @'
            if (!Mathf.Approximately(vv, Net.VoiceChat.Volume)) Net.VoiceChat.Volume = vv;

'@ @'
            if (!Mathf.Approximately(vv, Net.VoiceChat.Volume)) Net.VoiceChat.Volume = vv;

            // the audio devices (the owner, 2026-10-24: "Audio Options so players can choose the audio input/output devices they want to use in-game")
            f.Gap(6f);
            MenuKit.Header(f.Row(28f, 8f), "AUDIO DEVICES", HudTheme.Info);
            MenuKit.Label(f.Row(24f, 4f), "MICROPHONE", 17f, HudTheme.Bright);
            var mics = AudioDevices.Inputs;
            if (mics.Length == 0) MenuKit.Notice(f.Row(36f, 8f), "NO MICROPHONE FOUND", HudTheme.Shade(HudTheme.Card, 0.3f), 14f);
            else
            {
                int micNow = AudioDeviceRules.SelectedOption(AudioDevices.SavedMic, mics);
                int micPick = MenuKit.Choice(f, AudioDevices.InputLabels, micNow, 1, HudTheme.Quota, 40f, 16f, 6f);
                if (micPick != micNow) AudioDevices.SavedMic = AudioDeviceRules.NameForOption(micPick, mics);
            }
            if (AudioDevices.CanOpenSoundSettings)
            {
                f.Gap(4f);
                MenuKit.Label(f.Row(24f, 4f), "SPEAKERS", 17f, HudTheme.Bright);
                if (MenuKit.Button(f.Row(48f, 8f), "WINDOWS SOUND SETTINGS", HudTheme.Info, 20f)) AudioDevices.OpenSoundSettings();
            }

'@
Save-Src

# ---- the scenario (pictures of the Settings page)
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void AudioDevicesUi()
        {
            SessionState.SetString("netdev.mode", "audiodevicesui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep 'mode != "pvpbattlefieldui" &&' 'mode != "pvpbattlefieldui" && mode != "audiodevicesui" &&'
Rep '                case "pvpbattlefieldui": yield return PvpBattlefieldUiScenario(); break;' @'
                case "pvpbattlefieldui": yield return PvpBattlefieldUiScenario(); break;
                case "audiodevicesui": yield return AudioDevicesUiScenario(); break;
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            ViewClearTest();' @'
            ViewClearTest();
            AudioDevicesTest();
'@
Save-Src
"audio options patched"

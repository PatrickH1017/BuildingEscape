using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Proximity voice chat. The local microphone (Steam voice, or the Unity microphone without Steam) is sent to
    /// the host, which relays it over the existing connection - in a run only to the players within earshot (<see cref="VoiceRules"/>: loud up to
    /// 4 m, silent from 20 m; walkie-talkies will carry a voice further, see <see cref="RadioOf"/>); in the lobby, where there are no bodies, to everybody.
    /// A voice is played louder or quieter by how far the two characters are apart and comes from the side the speaker is on. Your own voice
    /// level also drives VoiceNoise, so guards hear you talk.
    ///
    /// Controls: open mic by default; M toggles mute; push-to-talk (T) and the voice volume are in Settings.
    /// Created automatically; works whenever a Netcode session is running.
    /// </summary>
    public class VoiceChat : MonoBehaviour
    {
        public enum Mode { OpenMic, PushToTalk, Off }

        public const string ModeKey = "OvertimeEscape.VoiceMode";
        public const string VolumeKey = "OvertimeEscape.VoiceVolume";
        const string MessageName = "be.voice";
        const int MaxPacket = 4000;       // compressed bytes
        const int ReliableAbove = 1000;   // bigger than one unreliable packet: send fragmented
        const float SendHangover = 0.25f; // keep sending a moment after the level drops

        public static VoiceChat Instance { get; private set; }

        // The settings are read from PlayerPrefs once and kept: they are asked for every frame (and every GUI event).
        static int modeCache = -1;
        static float volumeCache = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { modeCache = -1; volumeCache = -1f; Instance = null; RadioOf = null; }

        /// <summary>Walkie-talkies (bought at the vendor; not built yet): set by whatever knows which client carries one. Two players who both do hear each other at
        /// any distance (<see cref="VoiceRules.RouteFor"/>, which the host also uses to decide who is sent a voice); until then nobody does.</summary>
        public static System.Func<ulong, bool> RadioOf;
        static bool HasRadio(ulong clientId) => RadioOf != null && RadioOf(clientId);

        public static Mode CurrentMode
        {
            get
            {
                if (modeCache < 0) modeCache = Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
                return (Mode)modeCache;
            }
            set { modeCache = (int)value; PlayerPrefs.SetInt(ModeKey, modeCache); }
        }

        public static float Volume
        {
            get
            {
                if (volumeCache < 0f) volumeCache = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
                return volumeCache;
            }
            set { volumeCache = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, volumeCache); }
        }

        public KeyCode talkKey = KeyCode.T;
        public KeyCode muteKey = KeyCode.M;
        [Tooltip("Level (0..1 RMS) below which the microphone counts as silent.")]
        public float gate = 0.012f;

        public bool Muted { get; set; }
        public bool Speaking { get; private set; }
        public float MicLevel { get; private set; }
        /// <summary>How far, in metres, a guard hears you right now (0 when you are quiet). Louder voice = further.</summary>
        public float HearingRadius { get; private set; }

        // test / diagnostics
        public int PacketsSent, PacketsReceived, PacketsRelayed, PacketsHeldBack;     // (held back: a voice the host did not send on because the listener is out of earshot)

        IVoiceInput input;
        bool inputActive;
        readonly byte[] packet = new byte[MaxPacket];     // outgoing: what the microphone produced
        readonly byte[] received = new byte[MaxPacket];   // incoming: one packet at a time (reused, so a talker makes no garbage)
        readonly float[] pcm = new float[48000];
        ushort seq;
        float lastLoud;
        bool registered;

        readonly Dictionary<ulong, VoiceVoice> voices = new();
        // Host: a talker sends about 50 packets a second; a burst of 100 then 70 a second is far more than speech needs, so a client
        // that sends faster (to flood the others through the host's relay) is simply not relayed.
        readonly RateLimiter voiceBudget = new RateLimiter(100f, 70f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<VoiceChat>()) return;
            var go = new GameObject("VoiceChat");
            DontDestroyOnLoad(go);
            go.AddComponent<VoiceChat>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            input?.Dispose();
            if (Instance == this) Instance = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Dev only: use a tone instead of the microphone.</summary>
        public void UseTestInput(IVoiceInput testInput)
        {
            input?.Dispose();
            input = testInput;
            inputActive = false;
        }
#endif

        // ------------------------------------------------------------------ capture + send

        IVoiceInput PickInput()
        {
            var steam = SteamService.Instance;
            if (steam != null && steam.Ready) return new SteamVoiceInput();
            return new MicrophoneVoiceInput();
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            bool online = nm != null && nm.IsListening;
            if (online && !registered) Register(nm);
            if (!online && registered) { registered = false; DropAllVoices(); }

            if (Input.GetKeyDown(muteKey) && !EscapeMenu.Open && !Backpack.UiOpen && !Vendor.ShopOpen && !OutpostUi.Open) Muted = !Muted;

            var mode = CurrentMode;
            bool wantMic = online && !Muted && mode != Mode.Off && (mode == Mode.OpenMic || (Input.GetKey(talkKey) && !OutpostUi.Open));
            if (wantMic && input == null) input = PickInput();
            if (input != null && wantMic != inputActive) { input.SetActive(wantMic); inputActive = wantMic; }
            if (!wantMic) { Speaking = false; MicLevel = Mathf.MoveTowards(MicLevel, 0f, Time.deltaTime * 4f); }

            bool gotPacket = online && wantMic && PumpInput(nm);
            if (!gotPacket)
            {
                // No audio this frame (silence, or Steam sends nothing between words): the level must still fall,
                // or the last loud word would keep guards hearing you.
                Speaking = wantMic && Time.unscaledTime - lastLoud < SendHangover;
                MicLevel = Mathf.MoveTowards(MicLevel, 0f, Time.deltaTime * 2f);
                FeedGuards();
            }
            if (online) UpdateVoices(nm);
        }

        /// <summary>Reads and sends what the microphone has. Returns true if it handled any packet this frame.</summary>
        bool PumpInput(NetworkManager nm)
        {
            bool any = false;
            int guard = 8; // a few packets per frame at most
            while (guard-- > 0 && input.TryRead(packet, out int length) && length > 0)
            {
                any = true;
                var decoder = VoiceCodecs.DecoderFor(input.Codec);
                float rms = 0f;
                if (decoder != null)
                {
                    int n = decoder.Decode(packet, length, pcm);
                    if (n > 0)
                    {
                        double sum = 0;
                        for (int i = 0; i < n; i++) sum += pcm[i] * pcm[i];
                        rms = Mathf.Sqrt((float)(sum / n));
                    }
                }
                MicLevel = Mathf.Max(rms, MicLevel * 0.85f);
                if (rms >= gate) lastLoud = Time.unscaledTime;
                Speaking = Time.unscaledTime - lastLoud < SendHangover;
                FeedGuards();
                if (!Speaking) continue;   // silence is not worth the bandwidth
                SendPacket(nm, input.Codec, length);
            }
            return any;
        }

        /// <summary>Your voice makes noise: guards hear a talker (VoiceNoise sends it on to the host).</summary>
        void FeedGuards()
        {
            var me = NetPlayer.Local;
            var vn = me ? me.GetComponent<VoiceNoise>() : null;
            float level = Speaking ? VoiceNoise.LevelFromRms(MicLevel, gate) : 0f;
            if (vn) vn.SetLevel(level);
            HearingRadius = vn && level > 0f ? vn.RadiusFor(level) : 0f;
        }

        void SendPacket(NetworkManager nm, byte codec, int length)
        {
            using var w = new FastBufferWriter(length + 16, Allocator.Temp);
            w.WriteValueSafe(nm.LocalClientId);   // the host replaces this with the real sender
            w.WriteValueSafe(codec);
            w.WriteValueSafe(seq++);
            w.WriteValueSafe((ushort)length);
            w.WriteBytesSafe(packet, length);
            var delivery = length > ReliableAbove ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
            if (nm.IsServer)
                Relay(nm, nm.LocalClientId, w, delivery, nm.LocalClientId);
            else
                nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, w, delivery);
            PacketsSent++;
        }

        // ------------------------------------------------------------------ receive + relay

        void Register(NetworkManager nm)
        {
            if (nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnVoice);
            registered = true;
        }

        void OnVoice(ulong sender, FastBufferReader r)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            r.ReadValueSafe(out ulong claimed);
            r.ReadValueSafe(out byte codec);
            r.ReadValueSafe(out ushort seqNo);
            r.ReadValueSafe(out ushort length);
            if (length == 0 || length > MaxPacket || r.Length - r.Position < length) return;
            var data = received;
            r.ReadBytesSafe(ref data, length);

            // On the host the real sender is known; on a client the host vouches for who it relayed.
            ulong speaker = nm.IsServer ? sender : claimed;
            if (nm.IsServer && sender != nm.LocalClientId && !voiceBudget.Allow(sender, Time.unscaledTime)) return;
            if (speaker == nm.LocalClientId) return; // never play my own voice back

            PacketsReceived++;
            Play(speaker, codec, data, length);

            if (nm.IsServer)
            {
                using var w = new FastBufferWriter(length + 16, Allocator.Temp);
                w.WriteValueSafe(speaker);
                w.WriteValueSafe(codec);
                w.WriteValueSafe(seqNo);
                w.WriteValueSafe(length);
                w.WriteBytesSafe(data, length);
                var delivery = length > ReliableAbove ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.UnreliableSequenced;
                Relay(nm, speaker, w, delivery, nm.LocalClientId);
            }
        }

        /// <summary>Host: send to everyone who can hear the speaker (not the speaker, not itself): in a run the players within earshot (<see cref="VoiceRules.ShouldRelay"/>:
        /// the range plus a margin, or two radios), in the lobby everybody.</summary>
        void Relay(NetworkManager nm, ulong speaker, FastBufferWriter w, NetworkDelivery delivery, ulong self)
        {
            bool inRun = RunSync.Instance != null;
            var from = inRun ? PlayerOf(speaker) : null;
            bool fromRadio = HasRadio(speaker);
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == speaker || id == self) continue;
                if (inRun)
                {
                    var to = PlayerOf(id);
                    if (from == null || to == null) { PacketsHeldBack++; continue; }          // a run, and one of the two has no body: nothing to hear
                    if (!VoiceRules.ShouldRelay(Vector3.Distance(from.transform.position, to.transform.position), fromRadio, HasRadio(id))) { PacketsHeldBack++; continue; }
                }
                nm.CustomMessagingManager.SendNamedMessage(MessageName, id, w, delivery);
                PacketsRelayed++;
            }
        }

        // ------------------------------------------------------------------ playback

        void Play(ulong speaker, byte codec, byte[] data, int length)
        {
            var decoder = VoiceCodecs.DecoderFor(codec);
            if (decoder == null) return;
            if (codec == VoiceCodecs.Steam && (SteamService.Instance == null || !SteamService.Instance.Ready)) return;
            float gain = Listen(speaker, out float pan);
            if (gain < VoiceRules.AudibleGain) return;        // too far to hear: not even decoded
            int n = decoder.Decode(data, length, pcm);
            if (n <= 0) return;

            if (!voices.TryGetValue(speaker, out var voice) || !voice)
            {
                var go = new GameObject("Voice_" + speaker);
                go.transform.SetParent(transform, false);
                go.AddComponent<AudioSource>();
                voice = go.AddComponent<VoiceVoice>();
                voice.speakerId = speaker;
                voices[speaker] = voice;
            }
            voice.Push(pcm, n, decoder.SampleRate);
            voice.SetMix(Volume * gain, pan);
        }

        void UpdateVoices(NetworkManager nm)
        {
            float vol = Volume;
            foreach (var kv in voices)
            {
                var v = kv.Value;
                if (!v) continue;
                float gain = Listen(kv.Key, out float pan);
                var owner = RunSync.Instance != null ? PlayerOf(kv.Key) : null;
                if (owner) v.transform.position = owner.transform.position + Vector3.up * 1.6f;      // (where the voice is, for a look in the editor)
                v.SetMix(vol * gain, pan);
            }
        }

        static NetPlayer PlayerOf(ulong clientId)
        {
            foreach (var p in NetPlayer.All) if (p && p.OwnerClientId == clientId) return p;
            return null;
        }

        AudioListener ears;

        /// <summary>How loud a speaker is for the local player right now (1 = full, 0 = not heard) and which side he is on (-1 left .. 1 right).
        /// In the lobby there are no bodies, so everybody hears everybody in the middle at <see cref="VoiceRules.LobbyGain"/>; in a run the distance between the two characters decides
        /// (<see cref="VoiceRules"/>) and a voice with no body is not heard.</summary>
        float Listen(ulong speaker, out float pan)
        {
            pan = 0f;
            var me = NetPlayer.Local;
            if (RunSync.Instance == null || me == null) return VoiceRules.LobbyGain;
            var from = PlayerOf(speaker);
            if (from == null) return 0f;
            var nm = NetworkManager.Singleton;
            float d = Vector3.Distance(me.transform.position, from.transform.position);
            var route = VoiceRules.RouteFor(d, HasRadio(speaker), nm != null && HasRadio(nm.LocalClientId));
            if (route == VoiceRules.Route.Near)
            {
                if (!ears) ears = FindFirstObjectByType<AudioListener>();
                if (ears)
                {
                    var side = from.transform.position - ears.transform.position; side.y = 0f;
                    var right = ears.transform.right; right.y = 0f;
                    if (side.sqrMagnitude > 0.01f && right.sqrMagnitude > 0.01f) pan = VoiceRules.PanFor(Vector3.Dot(side.normalized, right.normalized), side.magnitude);
                }
            }
            return VoiceRules.GainFor(route, d);
        }

        void DropAllVoices()
        {
            foreach (var kv in voices) if (kv.Value) Destroy(kv.Value.gameObject);
            voices.Clear();
            input?.SetActive(false);
            inputActive = false;
        }

        /// <summary>Speakers heard in the last half second (for the HUD).</summary>
        public IEnumerable<ulong> ActiveSpeakers()
        {
            foreach (var kv in voices)
                if (kv.Value && Time.unscaledTime - kv.Value.lastPacketTime < 0.5f) yield return kv.Key;
        }

        public VoiceVoice VoiceOf(ulong speaker) => voices.TryGetValue(speaker, out var v) ? v : null;

        // ------------------------------------------------------------------ HUD



        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;
            var mode = CurrentMode;
            string state = mode == Mode.Off ? "voice off"
                : Muted ? $"MIC MUTED ({muteKey})"
                : mode == Mode.PushToTalk ? $"hold {talkKey} to talk"
                : Speaking ? $"MIC: talking - guards hear you up to {HearingRadius:0} m" : $"mic open ({muteKey} mutes)";
            bool off = Muted || mode == Mode.Off;
            float y = HudTheme.Px(42f);
            HudWidgets.VoicePill(ref y, state, off ? HudTheme.Warning : (Speaking ? HudTheme.Good : HudTheme.Muted), true, off);
            foreach (var kv in voices)
            {
                if (!kv.Value || Time.unscaledTime - kv.Value.lastPacketTime >= 0.5f) continue;   // (the speakers heard in the last half second)
                string who = "teammate";
                foreach (var p in NetPlayer.All) if (p.OwnerClientId == kv.Key) { who = $"Player {p.Slot + 1}"; break; }
                HudWidgets.VoicePill(ref y, $"{who} is talking", HudTheme.Info);
            }
        }
    }
}

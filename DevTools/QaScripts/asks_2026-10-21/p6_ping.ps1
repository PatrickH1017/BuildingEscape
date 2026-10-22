# 2026-10-21, the owner's list: "Add a ping system. Players can ping by pressing down on the scroll wheel ..." - the hooks into the existing files (the new files are PingRules.cs, PingSystem.cs, RunSync.Ping.cs)
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- PingSystem: a destroyed item (Unity's fake null) must be told from an item that was never found
Open-Src "$Tree\Assets\Scripts\Ping\PingSystem.cs"
if (-not $s.Contains('hadItem')) {
Rep '            public Pickup item;            // an item ping follows its item (it is pushed, carried...) and ends when it is taken' @'
            public Pickup item;            // an item ping follows its item (it is pushed, carried...) and ends when it is taken
            public bool hadItem;           // the item was found when the marker went up (so a destroyed one can be told from one that was never there)
'@
Rep '                if (m.item) m.offset = t.pos - m.item.transform.position;' '                if (m.item) { m.offset = t.pos - m.item.transform.position; m.hadItem = true; }'
Rep '                if (!over && m.target.kind == PingTargetKind.Item && m.item != null && !m.item) over = true;                          // (destroyed)' '                if (!over && m.hadItem && !m.item) over = true;                                                                       // (destroyed: banked, sold, shattered)'
Save-Src
}

# ---- the sound: a new SoundId at the END of the enum and of the variants list, built in OutpostSound
Open-Src "$Tree\Assets\Scripts\Audio\SoundBank.cs"
if (-not $s.Contains('SoundId.Ping')) {
Rep 'KeypadBeep, HackWin, HackFail, Alarm, SafeOpen, SafeWrong, Pry, CrowbarSnap, Paper, AlarmOff, MetalLock, Punch, Munch }' 'KeypadBeep, HackWin, HackFail, Alarm, SafeOpen, SafeWrong, Pry, CrowbarSnap, Paper, AlarmOff, MetalLock, Punch, Munch, Ping }'
Rep 'OutpostSound.PunchVariants, OutpostSound.MunchVariants,' 'OutpostSound.PunchVariants, OutpostSound.MunchVariants, OutpostSound.PingVariants,'
Rep '                case SoundId.Munch: return OutpostSound.Munch(variant);' @'
                case SoundId.Munch: return OutpostSound.Munch(variant);
                case SoundId.Ping: return OutpostSound.Ping(variant);
'@
Save-Src
}
Open-Src "$Tree\Assets\Scripts\Audio\OutpostSound.cs"
if (-not $s.Contains('PingVariants')) {
Rep '        public const int MunchVariants = 2;' @'
        public const int PingVariants = 1;

        /// <summary>A ping (the owner, 2026-10-21): two short bright notes going up (A5, then E6), soft at both ends - a marker has gone up. The game shifts its pitch a little for each player's slot.</summary>
        public static float[] Ping(int variant)
        {
            var o = new float[Dsp.Samples(0.34f)];
            float[] hz = { 880f, 1318.5f };
            for (int n = 0; n < hz.Length; n++)
            {
                int at = Dsp.Samples(0.11f * n), len = Dsp.Samples(0.2f), atk = Dsp.Samples(0.004f);
                float ph = 0f, dec = Dsp.DecayPerSample(0.09f), env = 1f;
                for (int i = 0; at + i < o.Length && i < len; i++)
                {
                    ph += hz[n] / SR; ph -= (int)ph;
                    float h2 = ph * 2f; h2 -= (int)h2;
                    float tone = Dsp.Sine(ph) + 0.2f * Dsp.Sine(h2);
                    o[at + i] += tone * env * Mathf.Min(1f, i / (float)atk) * 0.6f;
                    env *= dec;
                }
            }
            Dsp.FadeEnds(o, 0.006f);
            Dsp.Normalize(o, 0.55f);
            return o;
        }

        public const int MunchVariants = 2;
'@
Save-Src
}

# ---- the key lists
Open-Src "$Tree\Assets\Scripts\Hud\KeybindScreen.cs"
if (-not $s.Contains('"MMB"')) {
Rep '                    new Entry("M", "mute your microphone"),' @'
                    new Entry("MMB", "ping loot, gear, doors (wheel)"),
                    new Entry("M", "mute your microphone"),
'@
Save-Src
}
Open-Src "$Tree\Assets\Scripts\HowToPlayContent.cs"
if (-not $s.Contains('Middle mouse')) {
Rep '            ("Left mouse", "Throw what you hold / swing your weapon"),' @'
            ("Left mouse", "Throw what you hold / swing your weapon"),
            ("Middle mouse (click the wheel)", "Ping: point the crosshair at an item, a weapon or a door and click the mouse wheel - a marker with its name and how far it is shows for your whole team for a few seconds"),
'@
Save-Src
}

# ---- the protocol: a new message, and floors where a treasure for two or three stands somewhere else
Open-Src "$Tree\Assets\Scripts\Net\NetConst.cs"
if (-not $s.Contains('"27"')) {
Rep 'public const string ProtocolVersion = "26";   // 26: ' 'public const string ProtocolVersion = "27";   // 27: pings (a player marks an item or a door with the middle mouse button: a request to the host and an announcement to the team), the elevator countdown is cancelled when somebody steps out of the car, and a treasure for two or three on the floor stands where its carriers can reach it from both sides (a floor is built differently, so old and new cannot share a run); 26: '
Save-Src
}
"p6 done"

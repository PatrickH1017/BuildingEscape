using System;

namespace BuildingEscape
{
    /// <summary>What a hack is doing.</summary>
    public enum HackPhase
    {
        /// <summary>The panel has just come up; the first key has not been shown yet.</summary>
        Intro,
        /// <summary>A key is on screen and the player has one second to press it.</summary>
        Prompt,
        /// <summary>All the keys were pressed in time: the lock is open.</summary>
        Won,
        /// <summary>A wrong key, or too slow: the lock shorts out and sounds the alarm.</summary>
        Lost,
        /// <summary>The player backed out before it counted (no alarm).</summary>
        Cancelled
    }

    /// <summary>
    /// The quick hack on the outpost's door lock, free of Unity types so it can be tested outside Unity. A key from W, A, S, D is
    /// shown, the player has one second to press it, then the next one is shown straight away; after four right keys the lock opens.
    /// The wrong key, or letting the second run out, loses (and sets the alarm off). The four keys of a hack are all different (each of
    /// W, A, S and D comes once, in a random order).
    ///
    /// The caller feeds it the keys pressed this frame (<see cref="Press"/>) and then the time (<see cref="Tick"/>).
    /// </summary>
    public sealed class HackSession
    {
        /// <summary>How many keys have to be pressed.</summary>
        public const int Steps = 4;
        /// <summary>How long the player has for each key, in seconds.</summary>
        public const float Window = 1f;
        /// <summary>A short wait with "hacking" on screen before the first key is shown.</summary>
        public const float IntroSeconds = 0.6f;
        /// <summary>A press this much after the second is up still counts (a slow frame must not lose the hack).</summary>
        public const float Grace = 0.05f;
        /// <summary>The keys that can be asked for.</summary>
        public const string KeyPool = "WASD";

        /// <summary>
        /// A random sequence of <see cref="Steps"/> different keys: with four steps and four keys, each of W, A, S and D once, in a random
        /// order. (Were there ever more steps than keys, the keys would come round again, never the same one twice in a row.)
        /// </summary>
        public static char[] MakeKeys(Random rng)
        {
            var keys = new char[Steps];
            var pool = KeyPool.ToCharArray();
            int filled = 0;
            while (filled < Steps)
            {
                for (int i = pool.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    char t = pool[i]; pool[i] = pool[j]; pool[j] = t;
                }
                if (filled > 0 && pool.Length > 1 && pool[0] == keys[filled - 1]) { char t = pool[0]; pool[0] = pool[1]; pool[1] = t; }
                for (int i = 0; i < pool.Length && filled < Steps; i++) keys[filled++] = pool[i];
            }
            return keys;
        }

        public readonly char[] Keys;
        public HackPhase Phase { get; private set; } = HackPhase.Intro;
        /// <summary>Which key is asked for now (0 to <see cref="Steps"/> - 1); <see cref="Steps"/> once the hack is won.</summary>
        public int Step { get; private set; }
        /// <summary>Why it was lost, for the message on screen.</summary>
        public string Reason { get; private set; } = "";

        float phaseStart;

        public HackSession(char[] keys, float now)
        {
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            phaseStart = now;
        }

        public bool Finished => Phase == HackPhase.Won || Phase == HackPhase.Lost || Phase == HackPhase.Cancelled;

        /// <summary>The key asked for right now (a blank while there is none).</summary>
        public char Expected => Phase == HackPhase.Prompt && Step < Keys.Length ? Keys[Step] : ' ';

        /// <summary>Seconds left for the key on screen (the full window outside <see cref="HackPhase.Prompt"/>).</summary>
        public float TimeLeft(float now) => Phase == HackPhase.Prompt ? Math.Max(0f, Window - (now - phaseStart)) : Window;

        /// <summary>
        /// The player pressed one of the keys at time <paramref name="now"/>. A key pressed before the first prompt is shown, or in the
        /// very frame a prompt appears (nobody could have seen it), is ignored.
        /// </summary>
        public HackPhase Press(char key, float now)
        {
            if (Finished) return Phase;
            key = char.ToUpperInvariant(key);
            Tick(now);                                            // (a press that comes after the second ran out loses)
            if (Phase != HackPhase.Prompt || now <= phaseStart) return Phase;
            if (key != Keys[Step]) { Phase = HackPhase.Lost; Reason = "Wrong key"; return Phase; }
            Step++;
            if (Step >= Keys.Length) { Phase = HackPhase.Won; return Phase; }
            phaseStart = now;                                       // the next key is shown at once
            return Phase;
        }

        /// <summary>Time has moved on to <paramref name="now"/>: shows the first key after the intro, and loses if the second ran out.</summary>
        public HackPhase Tick(float now)
        {
            if (Finished) return Phase;
            if (Phase == HackPhase.Intro)
            {
                if (now - phaseStart >= IntroSeconds) { Phase = HackPhase.Prompt; phaseStart = now; }
            }
            else if (Phase == HackPhase.Prompt && now - phaseStart > Window + Grace)
            {
                Phase = HackPhase.Lost; Reason = "Too slow";
            }
            return Phase;
        }

        /// <summary>The player backed out (Esc, walked off, got hit). No alarm. Does nothing once the hack has ended.</summary>
        public void Cancel()
        {
            if (!Finished) Phase = HackPhase.Cancelled;
        }
    }
}

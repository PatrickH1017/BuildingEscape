using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The shooter's hit feedback (the owner, 2026-10-27: "There should be a Hit-marker and sound indicating a hit, as well as a different sound for headshots and a different sound for eliminating"): a small X over the crosshair that flashes white for a hit, gold for a headshot and red (bigger) for an
    /// elimination, with a sound of its own for each (<see cref="SoundId.HitMarker"/>, <see cref="SoundId.HitHead"/>, <see cref="SoundId.HitKill"/>: generated placeholders until the owner supplies sounds; the bullet's own impact thud, heard by everybody, is unchanged). Only the player whose bullet it was gets it: offline
    /// <see cref="GunController"/> calls <see cref="Play"/> when its bullet lands; in a networked run the shooter's machine plays a plain hit at once when its crosshair is on a foe (a guess) and the host's answer (<c>RunSync.ShotFeedbackRpc</c>) adds the headshot or the elimination.
    /// </summary>
    public sealed class HitFeedback : MonoBehaviour
    {
        /// <summary>How long the marker shows (s): a hit and a headshot, an elimination.</summary>
        public const float Seconds = 0.22f, KillSeconds = 0.45f;

        static HitFeedback instance;

        /// <summary>What was shown last and when (for the tests), and how many were shown.</summary>
        public static HitKind Last { get; private set; }
        public static float LastAt { get; private set; } = -100f;
        public static int Count { get; private set; }
        /// <summary>The sound last played (for the tests).</summary>
        public static SoundId LastSound { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; Last = HitKind.None; LastAt = -100f; Count = 0; guesses.Clear(); }

        // a networked run: the shooter's machine plays a plain hit at once when its crosshair is on a foe (a guess, see Guess) and the host's answer for that bullet (see Answer) must not play it a second time
        static readonly System.Collections.Generic.Queue<float> guesses = new System.Collections.Generic.Queue<float>();
        /// <summary>How long (s) a guess waits for the host's answer to its bullet before it is forgotten (the host said no hit).</summary>
        public const float GuessLife = 1f;

        /// <summary>The shooter's machine guesses a hit at once: the plain marker and its sound play, and the guess is kept until the host's answer for the bullet comes.</summary>
        public static void Guess()
        {
            guesses.Enqueue(Time.unscaledTime);
            Play(HitKind.Hit);
        }

        /// <summary>
        /// The host's answer for a bullet (the host's own player on the host, a client's through <c>RunSync.ShotFeedbackRpc</c>): what the guess before it could not know - a headshot or an elimination - plays (the stronger marker over the plain one); a
        /// plain hit is not shown again. An answer with no guess before it plays as it is.
        /// </summary>
        public static void Answer(HitKind kind)
        {
            while (guesses.Count > 0 && Time.unscaledTime - guesses.Peek() > GuessLife) guesses.Dequeue();
            if (guesses.Count > 0) { guesses.Dequeue(); if (kind == HitKind.Hit) return; }
            Play(kind);
        }

        public static SoundId SoundOf(HitKind kind) => kind == HitKind.Kill ? SoundId.HitKill : kind == HitKind.Head ? SoundId.HitHead : SoundId.HitMarker;

        /// <summary>The marker flashes and its sound plays (flat: it is the shooter's own).</summary>
        public static void Play(HitKind kind)
        {
            if (kind == HitKind.None) return;
            Ensure();
            Last = kind; LastAt = Time.unscaledTime; Count++;
            LastSound = SoundOf(kind);
            GameAudio.PlaySound(LastSound, Vector3.zero, kind == HitKind.Kill ? 0.9f : 0.7f, 1f, false);
        }

        static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("HitFeedback");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitFeedback>();
        }

        /// <summary>How visible the marker is now (1 just after the hit, 0 when it is gone): for the picture and the tests.</summary>
        public static float Visible
        {
            get
            {
                float life = Last == HitKind.Kill ? KillSeconds : Seconds;
                float age = Time.unscaledTime - LastAt;
                return age < 0f || age >= life ? 0f : 1f - age / life;
            }
        }

        static readonly Color Hit = new Color(1f, 1f, 1f), Head = new Color(1f, 0.78f, 0.12f), Kill = new Color(1f, 0.22f, 0.18f);

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            float vis = Visible;
            if (vis <= 0f) return;
            float s = HudTheme.S;
            Color c = Last == HitKind.Kill ? Kill : Last == HitKind.Head ? Head : Hit;
            float gap = (Last == HitKind.Kill ? 9f : 7f) * s + (1f - vis) * 6f * s;          // (the arms move outwards as it fades)
            float len = (Last == HitKind.Kill ? 20f : 14f) * s, th = (Last == HitKind.Kill ? 4f : 3f) * s;
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var old = GUI.color; var matrix = GUI.matrix;
            c.a = Mathf.Clamp01(vis * 1.4f);
            for (int i = 0; i < 4; i++)
            {
                GUI.matrix = matrix;
                GUIUtility.RotateAroundPivot(45f + 90f * i, centre);
                GUI.color = new Color(0f, 0f, 0f, c.a * 0.6f);                                       // (a dark edge, so that it shows on anything)
                GUI.DrawTexture(new Rect(centre.x + gap - 1.5f * s, centre.y - th * 0.5f - 1.5f * s, len + 3f * s, th + 3f * s), Texture2D.whiteTexture);
                GUI.color = c;
                GUI.DrawTexture(new Rect(centre.x + gap, centre.y - th * 0.5f, len, th), Texture2D.whiteTexture);
            }
            GUI.matrix = matrix; GUI.color = old;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A guard's occasional speech bubble: a few random symbols, like a comic-book curse (never real words, see
    /// <see cref="SymbolSpeech"/>), popping up now and then at random, and when he is knocked down. This only keeps the timing and the
    /// text; <see cref="GuardBubbleHud"/> draws it. Local to each player's screen (nothing is sent over the network): every machine
    /// shows its own random bubbles over the guards it sees. Added to every guard by GuardVisual.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    public sealed class GuardBubble : MonoBehaviour
    {
        /// <summary>Every guard that can talk (the HUD walks this list).</summary>
        public static readonly List<GuardBubble> All = new List<GuardBubble>();

        [Tooltip("Seconds until the first bubble, and between bubbles after that (a random time in the range).")]
        public Vector2 firstAfter = new Vector2(2f, 12f), every = new Vector2(6f, 20f);
        [Tooltip("How long a bubble stays up.")]
        public Vector2 showFor = new Vector2(1.6f, 2.6f);
        [Tooltip("The bubble hangs this far above the guard's feet.")]
        public float height = 3.1f;

        GuardAI ai;
        SynthRandom rng;
        float nextAt, shownAt = -99f, shownFor;
        GuardState lastState;

        /// <summary>The symbols currently (or last) shown.</summary>
        public string Text { get; private set; } = "";
        public Vector3 Anchor => transform.position + Vector3.up * height;
        public GuardAI Guard => ai;

        /// <summary>0 = nothing showing, 1 = fully shown; fades in quickly and out a little slower.</summary>
        public float Alpha
        {
            get
            {
                float t = Time.time - shownAt;
                if (t < 0f || t > shownFor || Text.Length == 0) return 0f;
                return Mathf.Clamp01(Mathf.Min(t / 0.12f, (shownFor - t) / 0.25f));
            }
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            ai = GetComponent<GuardAI>();
            rng = new SynthRandom(unchecked(ai.NetId * 104729 + 7));      // his own run of symbols
            nextAt = Time.time + rng.Range(firstAfter.x, firstAfter.y);
            lastState = ai.State;
        }

        void Update()
        {
            if (ai == null) return;
            if (ai.Dead) { Text = ""; return; }
            var state = ai.State;
            if (state == GuardState.Stunned && lastState != GuardState.Stunned) Say();      // knocked down: he swears
            lastState = state;

            if (Time.time < nextAt) return;
            nextAt = Time.time + rng.Range(every.x, every.y);
            if (state != GuardState.Stunned && !ai.IsCalling) Say();                       // (not while he is radioing for backup)
        }

        void Say()
        {
            Text = SymbolSpeech.Make(rng);
            shownAt = Time.time;
            shownFor = rng.Range(showFor.x, showFor.y);
        }
    }
}

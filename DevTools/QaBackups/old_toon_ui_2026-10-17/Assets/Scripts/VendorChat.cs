using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The speech bubble over the vendor's head. When your character walks up to him (back from a floor, or just
    /// into the dock) a bubble pops up with a line and fades after a few seconds; walk away and come back and it
    /// shows the next line. Local to each player's screen: nothing is sent over the network.
    ///
    /// The lines are placeholders for the game's own writing: edit <see cref="lines"/> on the Vendor object.
    /// </summary>
    public class VendorChat : MonoBehaviour
    {
        [Tooltip("What the vendor is looking at / hangs the bubble above (his head).")]
        public Transform head;
        [Tooltip("One line per visit, in order, then round again. (While a teammate is under arrest he says the buy-back line instead.)")]
        public string[] lines = { CustodyRules.GreetingLine };
        [Tooltip("How close a player has to be, in metres, for him to speak.")]
        public float range = 10f;
        public float showSeconds = 5f;

        int next;
        string text = "";
        float shownAt = -99f, alpha;
        bool near;

        /// <summary>True while the bubble is up (for tests).</summary>
        public bool Visible => alpha > 0.5f;
        public string Text => text;

        void Update()
        {
            float d = NearestLocalPlayer();
            if (!near && d <= range)
            {
                near = true;
                shownAt = Time.time;
                if (AnyoneBusted()) text = CustodyRules.BuyBackLine;   // "One of your friends got got..."
                else if (lines != null && lines.Length > 0) text = lines[next++ % lines.Length];
            }
            else if (near && d > range + 2f) near = false;

            bool want = near && Time.time - shownAt < showSeconds && !Vendor.ShopOpen && !EscapeMenu.Open && !string.IsNullOrEmpty(text);
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.deltaTime * 4f);
        }

        static bool AnyoneBusted()
        {
            foreach (var p in WobblyPlayerRegistry.All) if (p && p.Custody == CustodyState.Busted) return true;
            return false;
        }

        float NearestLocalPlayer()
        {
            if (!head) return float.MaxValue;
            float best = float.MaxValue;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p || !Net.NetPlayer.IsLocalPlayer(p)) continue;
                float d = Vector3.Distance(p.transform.position, head.position);
                if (d < best) best = d;
            }
            return best;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || alpha < 0.01f || !head) return;
            if (SpeechBubble.ToGui(Camera.main, head.position + Vector3.up * 0.75f, out var gui))
                SpeechBubble.Draw(gui, text, alpha);
        }
    }
}

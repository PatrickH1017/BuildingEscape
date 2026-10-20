using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Draws the speech bubbles of the guards (see <see cref="GuardBubble"/>). A bubble is only shown when the guard is within range,
    /// on screen and in clear view of the camera: a guard behind a wall or a closed door never shows where he is. Created automatically.
    /// </summary>
    public sealed class GuardBubbleHud : MonoBehaviour
    {
        const float FullRange = 16f, MaxRange = 22f;      // a bubble fades out between these distances

        struct Shown { public Vector2 anchor; public string text; public float alpha; }
        readonly List<Shown> shown = new List<Shown>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<GuardBubbleHud>()) return;
            var go = new GameObject("GuardBubbleHud");
            DontDestroyOnLoad(go);
            go.AddComponent<GuardBubbleHud>();
        }

        /// <summary>Works out which bubbles to draw this frame (the ray casts belong here, not in OnGUI).</summary>
        void Update()
        {
            shown.Clear();
            var cam = Camera.main;
            var hm = HeistManager.Instance;
            if (!cam || !hm || (hm.Phase != HeistPhase.Heist && hm.Phase != HeistPhase.FloorComplete)) return;
            var eye = cam.transform.position;

            foreach (var b in GuardBubble.All)
            {
                if (!b) continue;
                float alpha = b.Alpha;
                if (alpha <= 0.01f) continue;
                float dist = Vector3.Distance(eye, b.transform.position);
                if (dist > MaxRange) continue;
                var anchor = b.Anchor;
                if (!SpeechBubble.ToGui(cam, anchor, out var gui)) continue;
                if (gui.x < 0f || gui.x > Screen.width || gui.y < 0f || gui.y > Screen.height) continue;
                if (!InClearView(eye, anchor)) continue;
                shown.Add(new Shown { anchor = gui, text = b.Text, alpha = alpha * Mathf.Clamp01((MaxRange - dist) / (MaxRange - FullRange)) });
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < shown.Count; i++) SpeechBubble.Draw(shown[i].anchor, shown[i].text, shown[i].alpha, 24f, 170f);
        }

        /// <summary>
        /// True when nothing solid is between the camera and the point. People and loose objects do not count (the camera lets them
        /// pass too); walls, furniture that is part of the building and shut doors do.
        /// </summary>
        static bool InClearView(Vector3 from, Vector3 to)
        {
            var dir = to - from;
            float remaining = dir.magnitude;
            if (remaining < 0.2f) return true;
            dir /= remaining;
            var origin = from;
            for (int i = 0; i < 5 && remaining > 0.2f; i++)        // step past people and loose objects until the point is reached
            {
                if (!Physics.Raycast(origin, dir, out var hit, remaining, ~0, QueryTriggerInteraction.Ignore)) return true;
                if (hit.rigidbody == null && !hit.collider.GetComponentInParent<GuardAI>() && !hit.collider.GetComponentInParent<WobblyPlayer>()) return false;
                float used = hit.distance + 0.05f;
                origin += dir * used;
                remaining -= used;
            }
            return true;
        }
    }
}

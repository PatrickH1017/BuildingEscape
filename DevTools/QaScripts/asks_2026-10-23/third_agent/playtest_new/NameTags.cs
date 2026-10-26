using System.Collections.Generic;
using UnityEngine;
using BuildingEscape.Net;

namespace BuildingEscape
{
    /// <summary>
    /// The nametags over the other players' heads (the owner, 2026-10-23, after the playtest with friends: "Players need nametags above their heads"). Every frame the teammates the camera can see - in front of it, near enough (<see cref="NameRules.MaxDistance"/>),
    /// nothing solid in the way, not hiding - are collected into <see cref="Tags"/> (this part runs without a screen, so the tests read it); <c>OnGUI</c> only draws them, fading with the distance. The name is the one the player's own machine wrote (<see cref="NetPlayer.DisplayName"/>:
    /// his Steam name, or "Player n"). A developer who watches has no tag of his own and sees everybody's.
    /// </summary>
    public sealed class NameTags : MonoBehaviour
    {
        public struct Tag { public string text; public Vector3 world; public float alpha; public int slot; }

        static readonly List<Tag> tags = new List<Tag>();
        static readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>The tags of this frame.</summary>
        public static IReadOnlyList<Tag> Tags => tags;

        /// <summary>How far above the body's middle the name floats (m): over the head.</summary>
        public const float HeadLift = 1.3f;

        /// <summary>Tests: no view or line-of-sight check, every other player within range gets a tag.</summary>
        public static bool TestAllVisible;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { tags.Clear(); TestAllVisible = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<NameTags>()) return;
            var go = new GameObject("NameTags");
            DontDestroyOnLoad(go);
            go.AddComponent<NameTags>();
        }

        void Update() => Collect(Camera.main);

        static void Collect(Camera cam)
        {
            tags.Clear();
            if (!cam || NetPlayer.All.Count == 0) return;
            var me = NetPlayer.Local;
            foreach (var p in NetPlayer.All)
            {
                if (!p || p == me || !p.IsSpawned || !p.gameObject.activeInHierarchy) continue;
                if (p.TryGetComponent(out WobblyPlayer w) && w.IsHidden) continue;                          // (hiding at a desk: nobody knows where)
                var head = p.transform.position + Vector3.up * HeadLift;
                float d = Vector3.Distance(cam.transform.position, head);
                float a = NameRules.Alpha(d);
                if (a <= 0f) continue;
                if (!TestAllVisible)
                {
                    var vp = cam.WorldToViewportPoint(head);
                    if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) continue;
                    if (!Clear(cam.transform.position, head, p.transform)) continue;
                }
                tags.Add(new Tag { text = NameRules.Tag(p.DisplayName, p.Slot), world = head, alpha = a, slot = p.Slot });
            }
        }

        /// <summary>Is the line from the camera to the head free of anything solid (people and loose things do not count)?</summary>
        static bool Clear(Vector3 from, Vector3 to, Transform self)
        {
            var d = to - from;
            float dist = d.magnitude;
            if (dist < 0.1f) return true;
            int n = Physics.RaycastNonAlloc(from, d / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c.attachedRigidbody != null || c.transform.IsChildOf(self)) continue;
                return false;
            }
            return true;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || tags.Count == 0) return;
            var cam = Camera.main;
            if (!cam) return;
            GUI.depth = 20;                                                // (behind the HUD and every screen)
            float s = HudTheme.S;
            foreach (var t in tags)
            {
                var sp = cam.WorldToScreenPoint(t.world);
                if (sp.z <= 0f) continue;
                float y = Screen.height - sp.y;
                var r = new Rect(sp.x - 130f * s, y - 26f * s, 260f * s, 26f * s);
                HudTheme.Text(r, t.text, 17f, new Color(1f, 1f, 1f, t.alpha), TextAnchor.LowerCenter);
            }
        }
    }
}

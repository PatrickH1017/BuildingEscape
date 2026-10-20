using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The roller door of the vendor's bay in the Loading Docks (the one the trailer is backed into). It is rolled up all game - the vendor's trailer is open, as it always was -
    /// except during the run-start cutscene: the vendor is the guy the workers are going to call afterwards, so he must not be seen while they watch the boss (the owner,
    /// 2026-10-17: "the loading dock door should still be shut"). <see cref="IntroCinematic"/> shuts it as the cutscene begins and rolls it up again when it ends. The panel hangs from
    /// the top edge of the opening and rolls up by shrinking towards it; only while it is fully down does it have a collider. Purely local and cosmetic: nothing is sent.
    /// </summary>
    public sealed class DockBayDoor : MonoBehaviour
    {
        public static DockBayDoor Current { get; private set; }
        /// <summary>Seconds the door takes to roll all the way up.</summary>
        public const float RollSeconds = 1.3f;

        float openness = 1f, target = 1f;       // 1 = rolled up (open), 0 = shut
        Collider[] colliders;
        Renderer[] renderers;

        /// <summary>Is the door fully down (the vendor's bay is hidden)?</summary>
        public bool IsShut => openness <= 0.001f;
        /// <summary>Is it all the way up (the way it is for the whole game but the cutscene)?</summary>
        public bool IsOpen => openness >= 0.999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }

        void Awake()
        {
            Current = this;
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            Apply();
        }

        void OnDestroy() { if (Current == this) Current = null; }

        /// <summary>Shuts it at once (no roll): the cutscene's first picture must not show the vendor.</summary>
        public void Close() { openness = target = 0f; Apply(); }

        /// <summary>Rolls it up.</summary>
        public void Open() { target = 1f; }

        void Update()
        {
            if (Mathf.Approximately(openness, target)) return;
            openness = Mathf.MoveTowards(openness, target, Time.unscaledDeltaTime / RollSeconds);
            Apply();
        }

        void Apply()
        {
            bool show = openness < 0.995f;
            transform.localScale = new Vector3(1f, Mathf.Max(0.0001f, 1f - openness), 1f);
            foreach (var r in renderers) if (r && r.enabled != show) r.enabled = show;
            bool solid = openness <= 0.001f;
            foreach (var c in colliders) if (c && c.enabled != solid) c.enabled = solid;
        }
    }
}

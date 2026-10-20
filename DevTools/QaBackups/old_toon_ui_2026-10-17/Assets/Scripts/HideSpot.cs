using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Desk / locker / closet. Player inside the trigger can press H to hide.</summary>
    [RequireComponent(typeof(Collider))]
    public class HideSpot : MonoBehaviour
    {
        public KeyCode hideKey = KeyCode.H;
        WobblyPlayer inside;
        bool hiding;

        void Reset() { GetComponent<Collider>().isTrigger = true; }
        void OnTriggerEnter(Collider o)
        {
            var p = o.GetComponentInParent<WobblyPlayer>();
            if (p && Net.NetPlayer.IsLocalPlayer(p)) inside = p; // only your own player hides when you press H
        }
        void OnTriggerExit(Collider o)
        {
            var p = o.GetComponentInParent<WobblyPlayer>();
            if (p && p == inside) { p.IsHidden = false; hiding = false; inside = null; }
        }

        void Update()
        {
            if (!inside || inside.InCustody || OutpostUi.Open || !Input.GetKeyDown(hideKey)) return;
            hiding = !hiding;
            inside.IsHidden = hiding;
        }
    }
}

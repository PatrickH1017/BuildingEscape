using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The bullets an eliminated guard drops (<see cref="GuardDrops"/>) are taken by running over them (the owner, 2026-10-23: "A player should have to run over the eliminated guard to get the dropped bullets"), never with E:
    /// a player who comes within <see cref="GuardDropRules.RunOverRadius"/> of them and carries a gun of their kind has the rounds put in that gun's reserve at once and the bullets are gone. Somebody without such a gun runs over them
    /// and nothing happens (nothing is said either), so they stay for a friend who has one.
    /// Only the host (or an offline game) looks: a client sees the bullets as a puppet and is told when they are taken (<see cref="Net.RunSync.HostRunOver"/> announces the rounds to every machine, as for the dealer's ammunition).
    /// </summary>
    public sealed class AmmoRunOver : MonoBehaviour
    {
        Pickup pickup;
        AmmoClip clip;

        void Awake()
        {
            pickup = GetComponent<Pickup>();
            clip = GetComponent<AmmoClip>();
        }

        void Update()
        {
            if (!pickup || !clip || Net.NetRole.IsRemoteClient || !pickup.isActiveAndEnabled) return;
            if (Net.NetRole.Synced)
            {
                var sync = Net.RunSync.Instance;
                if (sync == null) return;
                foreach (var np in Net.NetPlayer.All)
                {
                    if (!np || !Near(np.transform)) continue;
                    np.TryGetComponent(out WobblyPlayer who);
                    if (!CanLoad(np.GetComponent<Backpack>(), who)) continue;
                    if (sync.HostRunOver(np, pickup)) return;
                }
            }
            else
            {
                foreach (var who in WobblyPlayerRegistry.All)
                {
                    if (!who || !Near(who.transform)) continue;
                    who.TryGetComponent(out Backpack pack);
                    if (CanLoad(pack, who) && pack.TakeClip(pickup, clip)) return;
                }
            }
        }

        bool Near(Transform t)
        {
            var d = t.position - transform.position;
            return GuardDropRules.RunsOver(new Vector2(d.x, d.z).magnitude, d.y);
        }

        /// <summary>Does this player carry a gun these bullets fit, and is he free to bend down for them?</summary>
        bool CanLoad(Backpack pack, WobblyPlayer who) => pack && (!who || !who.InCustody) && Vendor.GunOf(pack, clip.kind) != null;
    }
}

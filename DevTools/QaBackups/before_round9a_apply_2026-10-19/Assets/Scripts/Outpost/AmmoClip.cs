using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A spare clip for the pew pistol, lying in a safe (the owner, 2026-10-18: a weapon safe may hold "a back up clip of ammo" that is picked up on its own, like the weapon and the cash). It is not an item for the
    /// backpack: taking it (the crosshair on it and E, like anything else) puts <see cref="rounds"/> more rounds in the reserve of the pew the taker carries and the clip is gone (<see cref="Backpack.TryStore"/> offline,
    /// <c>RunSync.HostStore</c> in a networked run, which has every machine add the rounds). Somebody who carries no pew leaves it where it lies.
    /// </summary>
    public sealed class AmmoClip : MonoBehaviour
    {
        /// <summary>The rounds it adds to the pew's reserve (a clip of the pew: the same as the dealer's).</summary>
        public int rounds = 8;
    }
}

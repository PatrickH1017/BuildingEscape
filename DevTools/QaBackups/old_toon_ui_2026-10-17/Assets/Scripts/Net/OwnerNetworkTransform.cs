using Unity.Netcode.Components;

namespace BuildingEscape.Net
{
    /// <summary>NetworkTransform the owning player drives (responsive movement; the host trusts the owner).</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
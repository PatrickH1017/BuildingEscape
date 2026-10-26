namespace BuildingEscape
{
    /// <summary>
    /// The walkie talkie (the owner, 2026-10-23: "There should be a walkie talkie that the Vendor sells and the player should have to hold out the walkie talkie to use it, please use the SPAWNCAMPGAMES asset"): the numbers.
    /// The price and the weight are placeholders for the owner to set. The radio itself already existed in the voice system (<c>VoiceChat.RadioOf</c>, <c>VoiceRules.RouteFor</c>): two players who both hold one out hear each other at any distance;
    /// what was missing was the item. Pure constants, no Unity types.
    /// </summary>
    public static class WalkieRules
    {
        /// <summary>What the Vendor asks for it (a placeholder).</summary>
        public const int Price = 250;

        /// <summary>What it weighs in the pack, in kilograms (a placeholder: the pack holds 20).</summary>
        public const float Weight = 0.4f;

        /// <summary>Its height in metres, lying about and in the hand (the model is scaled to it).</summary>
        public const float ModelHeight = 0.18f;

        /// <summary>What the item is called (the owner's own words for it).</summary>
        public const string Name = "Walkie Talkie";
    }
}

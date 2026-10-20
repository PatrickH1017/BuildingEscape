namespace BuildingEscape
{
    /// <summary>
    /// One call that closes every menu and overlay that is open (the owner, 2026-10-18: "All open menus should close when accepting an invite"): the Esc menu and its pages (Settings, Keybinds), the inventory, the shop,
    /// the elevator panel, the guard outpost's screens (the lock hack, the safe, the journal, the computer) and the page the lobby menu is on (it goes back to the main page, which shows the connection). Everything
    /// is local; the intro cinematic is not touched (an invite is never shown while a run is on, and the cinematic is the start of one).
    /// </summary>
    public static class MenuRegistry
    {
        /// <summary>How many times <see cref="CloseAll"/> has run (for the tests).</summary>
        public static int Closed { get; private set; }

        /// <summary>Closes every open menu and overlay.</summary>
        public static void CloseAll()
        {
            Closed++;
            EscapeMenu.CloseAll();
            Backpack.CloseUi();
            Vendor.CloseShop();
            ElevatorPanel.CloseAll();
            OutpostUi.HideAll();
            Net.LobbyMenu.CloseAll();
            UnityEngine.GUI.FocusControl(null);
        }

        /// <summary>Is any menu or overlay open that <see cref="CloseAll"/> would close?</summary>
        public static bool AnyOpen => EscapeMenu.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || (OutpostUi.Open && OutpostUi.Current != OutpostUi.Kind.Intro) || Net.LobbyMenu.PageOpen;
    }
}

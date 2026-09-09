using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Where the adventure lobby's menus come from. Everything the lobby draws hangs off one of three
    /// objects, so this file resolves those three and every screen takes a leaf source off them
    /// (AGENTS.md, "Screen Resolution").
    ///
    /// <see cref="Navigation"/> is the scene's own <c>LobbyNavigation</c>, which nothing binds: one
    /// gated root walk of the lobby scene finds it, and its five serialized sub-menu fields answer
    /// the map type, map select, challenge map, random layout and lobby pages. The game settings
    /// popup is on the lobby page's own <c>_mapSettingsMenu</c>.
    ///
    /// <see cref="PlayerMenu"/> is the <c>LobbyPlayerMenu</c>, which the lobby installs into a
    /// <c>GameObjectContext</c> of its own; its settings hold the ONE icon dropdown the whole lobby
    /// shares, the per-player settings popup, and the lobby's own platform user menu.
    ///
    /// <c>LobbyMapSettings</c> and <c>LobbyMultiplayerPanel</c> are wired in the Unity scene and
    /// nothing in the game refers to either by name, so each takes a root walk of its own.
    ///
    /// The sources are shared: every screen that hangs off one of them makes its own leaf source with
    /// <c>FromOwner</c>, and they all share the one memo, so the scene is walked once per scene load
    /// however many screens ask.
    /// </summary>
    public static class LobbySources
    {
        /// <summary>The additive scene the main menu loads for the lobby, and the only scene any of
        /// these objects can be in.</summary>
        public const string LobbyScene = "AdventureLobbyScene";

        private static readonly AccessTools.FieldRef<LobbyNavigation, MapTypeMenu> MapTypeRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MapTypeMenu>("_mapTypeMenu");
        private static readonly AccessTools.FieldRef<LobbyNavigation, ChallengeMapsMenu> ChallengeMapsRef =
            AccessTools.FieldRefAccess<LobbyNavigation, ChallengeMapsMenu>("_challengeMapsMenu");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MapSelectMenu> MapSelectRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MapSelectMenu>("_mapSelectMenu");
        private static readonly AccessTools.FieldRef<LobbyNavigation, LobbyRandomMapSelectionMenu> RandomMapRef =
            AccessTools.FieldRefAccess<LobbyNavigation, LobbyRandomMapSelectionMenu>("_randomMapMenu");
        private static readonly AccessTools.FieldRef<LobbyNavigation, LobbyMenu> LobbyMenuRef =
            AccessTools.FieldRefAccess<LobbyNavigation, LobbyMenu>("_lobbyMenu");
        private static readonly AccessTools.FieldRef<LobbyMenu, LobbyMapSettingsMenu> MapSettingsMenuRef =
            AccessTools.FieldRefAccess<LobbyMenu, LobbyMapSettingsMenu>("_mapSettingsMenu");
        private static readonly AccessTools.FieldRef<LobbyPlayerMenu, LobbyPlayerMenu.Settings> PlayerMenuSettingsRef =
            AccessTools.FieldRefAccess<LobbyPlayerMenu, LobbyPlayerMenu.Settings>("_settings");
        private static readonly AccessTools.FieldRef<LobbyMenuButtons, LobbyMenuButtons.Settings> LobbyButtonsSettingsRef =
            AccessTools.FieldRefAccess<LobbyMenuButtons, LobbyMenuButtons.Settings>("_settings");

        /// <summary>The lobby scene's navigator, and through its fields every page the lobby draws.
        /// </summary>
        public static readonly ScreenSource<LobbyNavigation> Navigation =
            ScreenSource<LobbyNavigation>.FromRootWalk(LobbyScene);

        public static readonly ScreenSource<MapTypeMenu> MapType =
            ScreenSource<MapTypeMenu>.FromOwner(Navigation, navigation => MapTypeRef(navigation));

        public static readonly ScreenSource<ChallengeMapsMenu> ChallengeMaps =
            ScreenSource<ChallengeMapsMenu>.FromOwner(Navigation, navigation => ChallengeMapsRef(navigation));

        public static readonly ScreenSource<MapSelectMenu> MapSelect =
            ScreenSource<MapSelectMenu>.FromOwner(Navigation, navigation => MapSelectRef(navigation));

        public static readonly ScreenSource<LobbyRandomMapSelectionMenu> RandomLayout =
            ScreenSource<LobbyRandomMapSelectionMenu>.FromOwner(Navigation, navigation => RandomMapRef(navigation));

        /// <summary>The players page, which the game settings popup hangs off in turn.</summary>
        public static readonly ScreenSource<LobbyMenu> Lobby =
            ScreenSource<LobbyMenu>.FromOwner(Navigation, navigation => LobbyMenuRef(navigation));

        public static readonly ScreenSource<LobbyMapSettingsMenu> GameSettings =
            ScreenSource<LobbyMapSettingsMenu>.FromOwner(Lobby, menu => MapSettingsMenuRef(menu));

        /// <summary>The player rows' owner: bound into a <c>GameObjectContext</c>, so it is resolved
        /// from the scene's sub-containers rather than from the scene container.</summary>
        public static readonly ScreenSource<LobbyPlayerMenu> PlayerMenu =
            ScreenSource<LobbyPlayerMenu>.FromSubContainer(LobbyScene);

        /// <summary>The ONE icon dropdown the lobby has: <c>LobbyPlayerMenu</c> hands the same object
        /// to every row it spawns, so there is nothing per-row to look for.</summary>
        public static readonly ScreenSource<IconDropdown> Dropdown =
            ScreenSource<IconDropdown>.FromOwner(PlayerMenu, menu => Setting(menu, s => s.IconDropdown));

        public static readonly ScreenSource<LobbyPlayerSettingsMenu> PlayerSettings =
            ScreenSource<LobbyPlayerSettingsMenu>.FromOwner(PlayerMenu, menu => Setting(menu, s => s.PlayerSettingsMenu));

        /// <summary>The lobby's OWN platform user menu. The project container binds a second one,
        /// which the options window's blocked-players page uses; a lobby row shows this one.</summary>
        public static readonly ScreenSource<PlatformUserMenu> PlatformUser =
            ScreenSource<PlatformUserMenu>.FromOwner(PlayerMenu, menu => Setting(menu, s => s.PlatformUserMenu));

        /// <summary>The map settings band drawn beside the player rows. Wired in the scene and
        /// referred to by nothing, so it takes a walk of its own.</summary>
        public static readonly ScreenSource<LobbyMapSettings> MapSettings =
            ScreenSource<LobbyMapSettings>.FromRootWalk(LobbyScene);

        /// <summary>The online band, absent from an offline lobby: the object is there but inactive,
        /// and the adapter reads that.</summary>
        public static readonly ScreenSource<LobbyMultiplayerPanel> MultiplayerPanel =
            ScreenSource<LobbyMultiplayerPanel>.FromRootWalk(LobbyScene);

        /// <summary>The Set Ready, Set Not Ready and Start Game buttons, on the settings of a service
        /// bound in the same sub-container as the player menu.</summary>
        public static readonly ScreenSource<LobbyMenuButtons.Settings> LobbyButtons =
            ScreenSource<LobbyMenuButtons.Settings>.FromOwner(
                ScreenSource<LobbyMenuButtons>.FromSubContainer(LobbyScene),
                buttons => LobbyButtonsSettingsRef(buttons));

        private static T Setting<T>(LobbyPlayerMenu menu, Func<LobbyPlayerMenu.Settings, T> read)
            where T : class
        {
            LobbyPlayerMenu.Settings settings = menu == null ? null : PlayerMenuSettingsRef(menu);
            return settings == null ? null : read(settings);
        }
    }
}

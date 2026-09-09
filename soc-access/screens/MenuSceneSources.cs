using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Menu;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The three menus that live in a main-menu overlay scene of their own: the online game list, the
    /// player statistics page and the loading screen. Each is found once per set of loaded scenes and
    /// gated to the scene that can hold it, so a page that is not loaded costs one comparison a frame.
    ///
    /// The scene names are the ones <c>MainMenuSceneLoader.GetSceneName</c> maps its
    /// <c>MainMenuSceneType</c>s to; the loading screen's is the scene the async operation loads.
    ///
    /// The three resolution shapes here are all different, and each was checked in the running game:
    /// <c>GameListMenu</c> is bound <c>BindInterfacesTo</c> and so answers to no contract of its own -
    /// only the sub-container's <c>IInitializable</c> list names it; <c>PlayerStatsMenuNavigation</c>
    /// is a <c>MonoBehaviour</c> nothing binds at all; <c>LoadingScreenMenu</c> is bound plainly, but
    /// into a <c>GameObjectContext</c> the scene container cannot see into.
    /// </summary>
    public static class MenuSceneSources
    {
        /// <summary>The scene the online game list is loaded into.</summary>
        public const string OnlineGameListScene = "OnlineGameListScene";

        /// <summary>The scene the player statistics page is loaded into.</summary>
        public const string PlayerStatsScene = "PlayerStatsScene";

        /// <summary>The scene the loading screen is loaded into. It sits over whatever is being
        /// loaded, so it is the one scene here that shares the stage with another.</summary>
        public const string LoadingScreenScene = "AOLoadingScreenScene";

        /// <summary>The game list menu, which draws BOTH the list and the host-game popup over it, so
        /// the two screens share this one source and each decides from the game whether it is the page
        /// drawing. <c>GameListMenuInstaller</c> binds it with <c>BindInterfacesTo</c> into a
        /// <c>GameObjectContext</c>, which leaves the sub-container's <c>IInitializable</c> list as the
        /// only way to name it.</summary>
        public static readonly ScreenSource<GameListMenu> GameList =
            ScreenSource<GameListMenu>.From(
                SceneSubContainers.ResolveInitializable<GameListMenu>,
                OnlineGameListScene);

        /// <summary>The player statistics page: an unbound scene object, so one gated walk of its
        /// scene's roots.</summary>
        public static readonly ScreenSource<PlayerStatsMenuNavigation> PlayerStats =
            ScreenSource<PlayerStatsMenuNavigation>.FromRootWalk(PlayerStatsScene);

        /// <summary>The loading screen, bound plainly but into a <c>GameObjectContext</c> of its own.
        /// </summary>
        public static readonly ScreenSource<LoadingScreenMenu> LoadingScreen =
            ScreenSource<LoadingScreenMenu>.FromSubContainer(LoadingScreenScene);
    }
}

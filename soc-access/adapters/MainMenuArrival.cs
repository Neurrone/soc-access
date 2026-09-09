using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// Whether the game has just arrived at the main menu, read from the game's own main-menu scene
    /// loader once a frame. <c>MainMenu.HandleSceneLoaded</c> used to tell the mod; the loader's
    /// <c>CurrentlyLoadedScene</c> is the same fact and needs no hook (AGENTS.md, "Screen
    /// Resolution").
    ///
    /// What the mod does with it: the adventure map's notification review buffer and the scanner's
    /// state describe a game that is over by the time the main menu is up, and nothing in the game
    /// clears them.
    /// </summary>
    public sealed class MainMenuArrival
    {
        private bool _wasAtMainMenu;

        /// <summary>True on the first read after the main menu became the loaded main-menu scene.
        /// A hot reload starts with no memory of the previous state, so the first read after one
        /// while the main menu is already up answers true; clearing a buffer twice costs nothing.
        /// </summary>
        public bool Arrived()
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            bool atMainMenu = loader != null && loader.CurrentlyLoadedScene == MainMenuSceneType.MainMenu;
            bool arrived = atMainMenu && !_wasAtMainMenu;
            _wasAtMainMenu = atMainMenu;
            return arrived;
        }
    }
}

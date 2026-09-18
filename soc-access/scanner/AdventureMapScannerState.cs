using System;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// What the scanner remembers about ONE adventure game. Which game that is, is read from the
    /// game itself - <see cref="Rebind"/> is handed the object the adventure IS - rather than from
    /// the menu scene the player came through, because a finished campaign mission returns through
    /// the campaign map and a multiplayer session through the game list, and neither is the main
    /// menu.
    /// </summary>
    public sealed class AdventureMapScannerState
    {
        private readonly AdventureMapRevealedRegistry _revealedRegistry = new AdventureMapRevealedRegistry();

        // The adventure game the registry describes, held WEAKLY: the mod must not keep a finished
        // game's whole state alive, and a collected one is not the game being played either way.
        private readonly WeakReference _adventureGame = new WeakReference(null);

        public AdventureMapRevealedRegistry RevealedRegistry
        {
            get { return _revealedRegistry; }
        }

        /// <summary>Point the state at the adventure game now running, and answer whether that is a
        /// DIFFERENT game from the one it described - which is when everything it holds is thrown
        /// away. An unknown game (null) leaves it alone, so a lookup that failed for a frame does
        /// not empty a running game's discoveries.</summary>
        public bool Rebind(object adventureGame)
        {
            if (adventureGame == null || ReferenceEquals(adventureGame, _adventureGame.Target))
            {
                return false;
            }

            Clear();
            _adventureGame.Target = adventureGame;
            return true;
        }

        public void Clear()
        {
            _revealedRegistry.Clear();
            _adventureGame.Target = null;
        }
    }
}

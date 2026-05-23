namespace SongsOfConquestAccess.Scanner
{
    internal sealed class AdventureMapScannerState
    {
        private readonly AdventureMapRevealedRegistry _revealedRegistry = new AdventureMapRevealedRegistry();

        public AdventureMapRevealedRegistry RevealedRegistry
        {
            get { return _revealedRegistry; }
        }

        public void Clear()
        {
            _revealedRegistry.Clear();
        }
    }
}

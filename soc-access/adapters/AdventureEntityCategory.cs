namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// What kind of thing occupies an adventure tile, using the same facts the scanner
    /// categorises by. One classification serves both the tile cues and the sonar sweep.
    /// </summary>
    public enum AdventureEntityCategory
    {
        None,
        Wielder,
        Settlement,
        ResourceDeposit,
        Pickup
    }
}

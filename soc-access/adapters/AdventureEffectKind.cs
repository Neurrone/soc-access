namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The map's effect layer: weather and aftermath painted over the ground rather than a kind of
    /// ground. A tile has at most one, and <see cref="Unknown"/> is a tile with none.
    /// </summary>
    public enum AdventureEffectKind
    {
        Unknown,
        Fireflies,
        BurnMarks,
        Fog,
        Smoke,
        Wildfire,
        RaysOfLight,
        Snow
    }
}

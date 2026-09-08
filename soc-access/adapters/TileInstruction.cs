namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// What the game says a click on a map or battle tile would DO, as the native tooltip's
    /// instruction row names it ("Adventure/TooltipInstruction/&lt;Kind&gt;",
    /// "Battle/InspectTile/ClickToMove"). The adapter classifies the row and drops it from the
    /// tooltip text; the screen turns the kind into a usage hint on the key that performs it.
    /// <see cref="None"/> is a tile with no such row, and also a row whose wording the mod does not
    /// recognize.
    /// </summary>
    public enum TileInstruction
    {
        None,
        Select,
        Visit,
        Trade,
        Repair,
        Interact,
        Pillage,
        Attack,
        Pickup,
        Claim,
        Teleport,
        Move
    }
}

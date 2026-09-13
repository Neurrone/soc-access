namespace SongsOfConquestAccess.Adapters
{
    /// <summary>What lies around a battlefield, as the game itself decided it: when a fight starts
    /// the server samples the adventure tiles west, north and east of the tile it is fought on and
    /// keeps them with the battle, and the scenery around the board - its forests, its mountains,
    /// its water - is drawn from those three. This is those three tiles read as ground the mod
    /// already has a meaning for; the sentence made of them belongs to
    /// <see cref="UI.BattlefieldText"/>.</summary>
    public sealed class BattlefieldSurroundings
    {
        public BattlefieldSurroundings(AdventureTerrainKind west, AdventureTerrainKind north, AdventureTerrainKind east)
        {
            West = west;
            North = north;
            East = east;
        }

        public AdventureTerrainKind West { get; private set; }

        public AdventureTerrainKind North { get; private set; }

        public AdventureTerrainKind East { get; private set; }
    }
}

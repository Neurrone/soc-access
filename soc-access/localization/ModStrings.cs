namespace SongsOfConquestAccess.Localization
{
    public static class ModStrings
    {
        public static class Common
        {
            public static readonly ModString CountOf = new ModString("Common.CountOf", "{0} of {1}");
            public static readonly ModString ListFinal = new ModString("Common.ListFinal", "{0}, and {1}");
            public static readonly ModString ListPair = new ModString("Common.ListPair", "{0} and {1}");
            public static readonly ModString ListSeparator = new ModString("Common.ListSeparator", "{0}, {1}");
            public static readonly ModString EssenceVariant = new ModString("Common.EssenceVariant", "{0} ({1})");
            public static readonly ModString NamePossessive = new ModString("Common.NamePossessive", "{0}'s");
            public static readonly ModString Quantity = new ModString("Common.Quantity", "quantity");
            public static readonly ModString ResourceAmount = new ModString("Common.ResourceAmount", "{0} {1}");
            public static readonly ModPluralString TileCount = new ModPluralString("Common.TileCount", "{0} tile", "{0} tiles");
            public static readonly ModString Sentence = new ModString("Common.Sentence", "{0}.");
            public static readonly ModString SentenceSeparator = new ModString("Common.SentenceSeparator", "{0}. {1}");
            public static readonly ModString ClauseSeparator = new ModString("Common.ClauseSeparator", "{0}; {1}");
            public static readonly ModString PhraseSeparator = new ModString("Common.PhraseSeparator", "{0} {1}");
            public static readonly ModString Parenthetical = new ModString("Common.Parenthetical", "{0} ({1})");
            public static readonly ModString PositiveAmount = new ModString("Common.PositiveAmount", "+{0}");
        }

        public static class TeamColors
        {
            public static readonly ModString Black = new ModString("TeamColors.Black", "black");
            public static readonly ModString Blue = new ModString("TeamColors.Blue", "blue");
            public static readonly ModString DarkRed = new ModString("TeamColors.DarkRed", "dark red");
            public static readonly ModString Green = new ModString("TeamColors.Green", "green");
            public static readonly ModString Neutral = new ModString("TeamColors.Neutral", "neutral");
            public static readonly ModString Orange = new ModString("TeamColors.Orange", "orange");
            public static readonly ModString Pink = new ModString("TeamColors.Pink", "pink");
            public static readonly ModString Purple = new ModString("TeamColors.Purple", "purple");
            public static readonly ModString Red = new ModString("TeamColors.Red", "red");
            public static readonly ModString Teal = new ModString("TeamColors.Teal", "teal");
            public static readonly ModString Yellow = new ModString("TeamColors.Yellow", "yellow");
        }

        public static class Artifacts
        {
            public static readonly ModString ColorBlue = new ModString("Artifacts.ColorBlue", "blue");
            public static readonly ModString ColorGreen = new ModString("Artifacts.ColorGreen", "green");
            public static readonly ModString ColorGrey = new ModString("Artifacts.ColorGrey", "grey");
            public static readonly ModString ColorOrange = new ModString("Artifacts.ColorOrange", "orange");
            public static readonly ModString ColorViolet = new ModString("Artifacts.ColorViolet", "violet");
            public static readonly ModString NameWithColor = new ModString("Artifacts.NameWithColor", "{0} ({1})");
        }

        public static class Audio
        {
            public static readonly ModString CategoryCombat = new ModString("Audio.CategoryCombat", "Battlefield");
            public static readonly ModString CategoryOverworld = new ModString("Audio.CategoryOverworld", "Map contents");
            public static readonly ModString CategoryTerrain = new ModString("Audio.CategoryTerrain", "Terrain");
            public static readonly ModString EntityEnemy = new ModString("Audio.EntityEnemy", "Enemy");
            public static readonly ModString EntityFriendly = new ModString("Audio.EntityFriendly", "Ally");
            public static readonly ModString HexActive = new ModString("Audio.HexActive", "Battlefield: acting troop");
            public static readonly ModString HexDanger = new ModString("Audio.HexDanger", "Battlefield: threatened hex");
            public static readonly ModString HexElevation1 = new ModString("Audio.HexElevation1", "Battlefield: hex, elevation 1");
            public static readonly ModString HexElevation2 = new ModString("Audio.HexElevation2", "Battlefield: hex, elevation 2");
            public static readonly ModString HexElevation3 = new ModString("Audio.HexElevation3", "Battlefield: hex, elevation 3");
            public static readonly ModString HexEmpty = new ModString("Audio.HexEmpty", "Battlefield: empty hex");
            public static readonly ModString MoveDenied = new ModString("Audio.MoveDenied", "Map edge");
            public static readonly ModString SweepPickup = new ModString("Audio.SweepPickup", "Pickup");
            public static readonly ModString SweepResource = new ModString("Audio.SweepResource", "Resource deposit");
            public static readonly ModString SweepSettlement = new ModString("Audio.SweepSettlement", "Settlement");
            public static readonly ModString SweepWielder = new ModString("Audio.SweepWielder", "Wielder");
            public static readonly ModString TerrainGround = new ModString("Audio.TerrainGround", "Open ground");
            public static readonly ModString TerrainImpassable = new ModString("Audio.TerrainImpassable", "Impassable tile");
            public static readonly ModString TerrainRoad = new ModString("Audio.TerrainRoad", "Road");
            public static readonly ModString TerrainSand = new ModString("Audio.TerrainSand", "Sand");
            public static readonly ModString TerrainTrees = new ModString("Audio.TerrainTrees", "Trees");
            public static readonly ModString TerrainUnexplored = new ModString("Audio.TerrainUnexplored", "Unexplored tile");
            public static readonly ModString TerrainWater = new ModString("Audio.TerrainWater", "Water");
        }

        /// <summary>
        /// WHAT BLOCKS A BATTLEFIELD CELL, and how a whole feature is written into a description.
        /// The game has no player-facing name for an obstacle, so these words are the owner's, one
        /// per theme for each of the four decoration bytes a theme draws props for: 0 Arleon,
        /// 1 Loth, 2 Barya, 3 Rana, 4 Vanir, 5 Roots, 6 Yulan, with 7 a second Arleon that reads as
        /// Arleon. Each is singular and plural because a run of them is counted, and a cell full of
        /// them takes whichever form names the cell: the plural where a cell holds many (boulders,
        /// bushes), the singular where it holds one (a statue, a torch).
        ///
        /// Each word comes in two forms, because a description is prose and a cursor line is a
        /// label. The plain name is the DESCRIPTION form and carries an article, so a placeholder
        /// expands to a noun phrase - "a wall of bushes", "a gatepost"; the Bare name beside it is
        /// the bare noun the CURSOR AND THE SCANNER speak - "gatepost, impassable". They are two
        /// strings rather than one with the article cut off at runtime, because the article's form
        /// follows the noun's gender and its opening sound in most of the thirteen languages. A
        /// word English writes without an article (water, fire, purple heather, glowing blue
        /// mushrooms) still gets both strings: English fills them alike, and French does not -
        /// "bloqué par de l'eau" in prose against "eau, infranchissable" on the cursor.
        ///
        /// These are spoken IN COMBAT ONLY. The placement page's preview draws a blocked cell as a
        /// puck with no style, so there it stays plain impassable.
        ///
        /// The Description forms are the same features as an authored description names them: the
        /// height in parentheses, and impassability never spoken - a wall of bushes is a wall of
        /// bushes, not a wall of impassable bushes.
        /// </summary>
        public static class Battlefield
        {
            public static readonly ModPluralString ObstacleRock = new ModPluralString("Battlefield.ObstacleRock", "a boulder", "boulders");
            public static readonly ModPluralString ObstacleRockBare = new ModPluralString("Battlefield.ObstacleRockBare", "boulder", "boulders");
            public static readonly ModPluralString ObstacleGrowthArleon = new ModPluralString("Battlefield.ObstacleGrowthArleon", "a bush", "bushes");
            public static readonly ModPluralString ObstacleGrowthArleonBare = new ModPluralString("Battlefield.ObstacleGrowthArleonBare", "bush", "bushes");
            public static readonly ModPluralString ObstacleGrowthLoth = new ModPluralString("Battlefield.ObstacleGrowthLoth", "a dark leafy plant", "dark leafy plants");
            public static readonly ModPluralString ObstacleGrowthLothBare = new ModPluralString("Battlefield.ObstacleGrowthLothBare", "dark leafy plant", "dark leafy plants");
            public static readonly ModPluralString ObstacleGrowthBarya = new ModPluralString("Battlefield.ObstacleGrowthBarya", "a desert plant", "desert plants");
            public static readonly ModPluralString ObstacleGrowthBaryaBare = new ModPluralString("Battlefield.ObstacleGrowthBaryaBare", "desert plant", "desert plants");
            public static readonly ModPluralString ObstacleGrowthRana = new ModPluralString("Battlefield.ObstacleGrowthRana", "a pink mushroom", "pink mushrooms");
            public static readonly ModPluralString ObstacleGrowthRanaBare = new ModPluralString("Battlefield.ObstacleGrowthRanaBare", "pink mushroom", "pink mushrooms");
            public static readonly ModPluralString ObstacleGrowthVanir = new ModPluralString("Battlefield.ObstacleGrowthVanir", "purple heather", "purple heather");
            public static readonly ModPluralString ObstacleGrowthVanirBare = new ModPluralString("Battlefield.ObstacleGrowthVanirBare", "purple heather", "purple heather");
            public static readonly ModPluralString ObstacleGrowthRoots = new ModPluralString("Battlefield.ObstacleGrowthRoots", "a teal fungus", "teal fungi");
            public static readonly ModPluralString ObstacleGrowthRootsBare = new ModPluralString("Battlefield.ObstacleGrowthRootsBare", "teal fungus", "teal fungi");
            public static readonly ModPluralString ObstacleGrowthYulan = new ModPluralString("Battlefield.ObstacleGrowthYulan", "a purple flowering shrub", "purple flowering shrubs");
            public static readonly ModPluralString ObstacleGrowthYulanBare = new ModPluralString("Battlefield.ObstacleGrowthYulanBare", "purple flowering shrub", "purple flowering shrubs");
            public static readonly ModPluralString ObstacleManufacturedArleon = new ModPluralString("Battlefield.ObstacleManufacturedArleon", "a knight statue", "knight statues");
            public static readonly ModPluralString ObstacleManufacturedArleonBare = new ModPluralString("Battlefield.ObstacleManufacturedArleonBare", "knight statue", "knight statues");
            public static readonly ModPluralString ObstacleManufacturedLoth = new ModPluralString("Battlefield.ObstacleManufacturedLoth", "a gargoyle statue", "gargoyle statues");
            public static readonly ModPluralString ObstacleManufacturedLothBare = new ModPluralString("Battlefield.ObstacleManufacturedLothBare", "gargoyle statue", "gargoyle statues");
            public static readonly ModPluralString ObstacleManufacturedBarya = new ModPluralString("Battlefield.ObstacleManufacturedBarya", "an animal statue", "animal statues");
            public static readonly ModPluralString ObstacleManufacturedBaryaBare = new ModPluralString("Battlefield.ObstacleManufacturedBaryaBare", "animal statue", "animal statues");
            public static readonly ModPluralString ObstacleManufacturedRana = new ModPluralString("Battlefield.ObstacleManufacturedRana", "a dragon statue", "dragon statues");
            public static readonly ModPluralString ObstacleManufacturedRanaBare = new ModPluralString("Battlefield.ObstacleManufacturedRanaBare", "dragon statue", "dragon statues");
            public static readonly ModPluralString ObstacleManufacturedVanir = new ModPluralString("Battlefield.ObstacleManufacturedVanir", "a standing stone", "standing stones");
            public static readonly ModPluralString ObstacleManufacturedVanirBare = new ModPluralString("Battlefield.ObstacleManufacturedVanirBare", "standing stone", "standing stones");
            public static readonly ModPluralString ObstacleManufacturedRoots = new ModPluralString("Battlefield.ObstacleManufacturedRoots", "a giant mushroom", "giant mushrooms");
            public static readonly ModPluralString ObstacleManufacturedRootsBare = new ModPluralString("Battlefield.ObstacleManufacturedRootsBare", "giant mushroom", "giant mushrooms");
            public static readonly ModPluralString ObstacleManufacturedYulan = new ModPluralString("Battlefield.ObstacleManufacturedYulan", "a sage statue", "sage statues");
            public static readonly ModPluralString ObstacleManufacturedYulanBare = new ModPluralString("Battlefield.ObstacleManufacturedYulanBare", "sage statue", "sage statues");
            public static readonly ModPluralString ObstacleLightArleon = new ModPluralString("Battlefield.ObstacleLightArleon", "a torch", "torches");
            public static readonly ModPluralString ObstacleLightArleonBare = new ModPluralString("Battlefield.ObstacleLightArleonBare", "torch", "torches");
            public static readonly ModPluralString ObstacleLightLoth = new ModPluralString("Battlefield.ObstacleLightLoth", "a glowing crystal", "glowing crystals");
            public static readonly ModPluralString ObstacleLightLothBare = new ModPluralString("Battlefield.ObstacleLightLothBare", "glowing crystal", "glowing crystals");
            public static readonly ModPluralString ObstacleLightBarya = new ModPluralString("Battlefield.ObstacleLightBarya", "a golden torch", "golden torches");
            public static readonly ModPluralString ObstacleLightBaryaBare = new ModPluralString("Battlefield.ObstacleLightBaryaBare", "golden torch", "golden torches");
            public static readonly ModPluralString ObstacleLightRana = new ModPluralString("Battlefield.ObstacleLightRana", "a glowing pool", "glowing pools");
            public static readonly ModPluralString ObstacleLightRanaBare = new ModPluralString("Battlefield.ObstacleLightRanaBare", "glowing pool", "glowing pools");
            public static readonly ModPluralString ObstacleLightVanir = new ModPluralString("Battlefield.ObstacleLightVanir", "a torch", "torches");
            public static readonly ModPluralString ObstacleLightVanirBare = new ModPluralString("Battlefield.ObstacleLightVanirBare", "torch", "torches");
            public static readonly ModPluralString ObstacleLightRoots = new ModPluralString("Battlefield.ObstacleLightRoots", "glowing blue mushrooms", "glowing blue mushrooms");
            public static readonly ModPluralString ObstacleLightRootsBare = new ModPluralString("Battlefield.ObstacleLightRootsBare", "glowing blue mushrooms", "glowing blue mushrooms");
            public static readonly ModPluralString ObstacleLightYulan = new ModPluralString("Battlefield.ObstacleLightYulan", "a paper lantern", "paper lanterns");
            public static readonly ModPluralString ObstacleLightYulanBare = new ModPluralString("Battlefield.ObstacleLightYulanBare", "paper lantern", "paper lanterns");
            public static readonly ModPluralString ObstacleGatepost = new ModPluralString("Battlefield.ObstacleGatepost", "a gatepost", "gateposts");
            public static readonly ModPluralString ObstacleGatepostBare = new ModPluralString("Battlefield.ObstacleGatepostBare", "gatepost", "gateposts");
            public static readonly ModPluralString ObstacleFire = new ModPluralString("Battlefield.ObstacleFire", "fire", "fire");
            public static readonly ModPluralString ObstacleFireBare = new ModPluralString("Battlefield.ObstacleFireBare", "fire", "fire");
            public static readonly ModPluralString ObstacleWater = new ModPluralString("Battlefield.ObstacleWater", "water", "water");
            public static readonly ModPluralString ObstacleWaterBare = new ModPluralString("Battlefield.ObstacleWaterBare", "water", "water");
            public static readonly ModString DescriptionChokePoint = new ModString("Battlefield.DescriptionChokePoint", "a choke point");
            public static readonly ModString DescriptionCliffs = new ModString("Battlefield.DescriptionCliffs", "cliffs");
            public static readonly ModString DescriptionImpassableCell = new ModString("Battlefield.DescriptionImpassableCell", "an impassable cell");
            public static readonly ModString DescriptionImpassableCells = new ModString("Battlefield.DescriptionImpassableCells", "impassable cells");
            public static readonly ModString DescriptionImpassableWall = new ModString("Battlefield.DescriptionImpassableWall", "a wall of impassable cells");
            public static readonly ModString DescriptionObstacleWall = new ModString("Battlefield.DescriptionObstacleWall", "a wall of {0}");
            public static readonly ModString DescriptionPatch = new ModString("Battlefield.DescriptionPatch", "a patch (height {0})");
            public static readonly ModString DescriptionRidge = new ModString("Battlefield.DescriptionRidge", "a ridge (height {0})");
            public static readonly ModString DescriptionRidgeDiagonal = new ModString("Battlefield.DescriptionRidgeDiagonal", "a diagonal ridge (height {0})");
            public static readonly ModString DescriptionRidgeVertical = new ModString("Battlefield.DescriptionRidgeVertical", "a vertical ridge (height {0})");
            public static readonly ModString DescriptionSingleCell = new ModString("Battlefield.DescriptionSingleCell", "a single cell (height {0})");
            public static readonly ModString DescriptionStairs = new ModString("Battlefield.DescriptionStairs", "stairs (height {0})");
            public static readonly ModString DescriptionTower = new ModString("Battlefield.DescriptionTower", "a tower (height {0})");
            public static readonly ModString DescriptionUnreachableGround = new ModString("Battlefield.DescriptionUnreachableGround", "unreachable ground");
            public static readonly ModString DescriptionWall = new ModString("Battlefield.DescriptionWall", "a wall (height {0})");
            // WHAT LIES AROUND THE BATTLEFIELD, from the three adventure tiles the game built the
            // battle's scenery from. One clause per kind of ground, naming the directions it lies
            // in, and no directions at all where it lies in all three.
            public static readonly ModString Surroundings = new ModString("Battlefield.Surroundings", "The battlefield is surrounded by {0}.");
            public static readonly ModString SurroundingDirection = new ModString("Battlefield.SurroundingDirection", "{0} to the {1}");
            public static readonly ModString SurroundingFarmland = new ModString("Battlefield.SurroundingFarmland", "farmland");
            public static readonly ModString SurroundingForest = new ModString("Battlefield.SurroundingForest", "forest");
            public static readonly ModString SurroundingMountains = new ModString("Battlefield.SurroundingMountains", "mountains");
            public static readonly ModString SurroundingOpenLand = new ModString("Battlefield.SurroundingOpenLand", "open land");
            public static readonly ModString SurroundingWalls = new ModString("Battlefield.SurroundingWalls", "walls");
            public static readonly ModString SurroundingWater = new ModString("Battlefield.SurroundingWater", "water");
        }

        public static class Draft
        {
            public static readonly ModPluralString AvailableTroops = new ModPluralString("Draft.AvailableTroops", "{0} available", "{0} available");
            public static readonly ModString Purchase = new ModString("Draft.Purchase", "Purchase");
            public static readonly ModString Upgrade = new ModString("Draft.Upgrade", "Upgrade");
            public static readonly ModString UpgradeAvailableTroops = new ModString("Draft.UpgradeAvailableTroops", "Upgrade available troops");
            public static readonly ModString UpgradeChoice = new ModString("Draft.UpgradeChoice", "{0} to {1}");
        }

        public static class Events
        {
            public static readonly ModString BuildSiteLarge = new ModString("Events.BuildSiteLarge", "large");
            public static readonly ModString BuildSiteLargeSettlement = new ModString("Events.BuildSiteLargeSettlement", "large settlement");
            public static readonly ModString BuildSiteMedium = new ModString("Events.BuildSiteMedium", "medium");
            public static readonly ModString BuildSiteSmall = new ModString("Events.BuildSiteSmall", "small");
            public static readonly ModString BuildSiteSmallSettlement = new ModString("Events.BuildSiteSmallSettlement", "small settlement");
            public static readonly ModString BuildSiteTown = new ModString("Events.BuildSiteTown", "town");
            public static readonly ModString DestinationCleared = new ModString("Events.DestinationCleared", "{0}'s destination cleared.");
            public static readonly ModString DestinationSet = new ModString("Events.DestinationSet", "{0}'s destination set to {1}");
            public static readonly ModString DestinationSetInteraction = new ModString("Events.DestinationSetInteraction", "Cost: {0}. {1} will {2} {3}.");
            public static readonly ModString DestinationSetInteractionFree = new ModString("Events.DestinationSetInteractionFree", "{0} will {1} {2}.");
            public static readonly ModString DestinationSetRoute = new ModString("Events.DestinationSetRoute", "Cost: {0}. {1} will move {2}.");
            public static readonly ModString DestinationSetRouteInteraction = new ModString("Events.DestinationSetRouteInteraction", "Cost: {0}. {1} will move {2} and {3} {4}.");
            public static readonly ModString HudClosed = new ModString("Events.HudClosed", "HUD closed");
            public static readonly ModString HudOpen = new ModString("Events.HudOpen", "HUD open");
            public static readonly ModString LeveledUpTo = new ModString("Events.LeveledUpTo", "{0} leveled up to {1}");
            public static readonly ModString MapCameraFocus = new ModString("Events.MapCameraFocus", "Map camera focuses on {0}");
            public static readonly ModString ModError = new ModString("Events.ModError", "Mod error: {0}");
            public static readonly ModString RecruitedWielder = new ModString("Events.RecruitedWielder", "Recruited wielder {0}");
            public static readonly ModString Revealed = new ModString("Events.Revealed", "Revealed {0}");
            public static readonly ModString RouteCostInTurns = new ModString("Events.RouteCostInTurns", "{0} in {1} turns");
            public static readonly ModString RouteCostNextTurn = new ModString("Events.RouteCostNextTurn", "{0} next turn");
            public static readonly ModString RouteCostThisTurn = new ModString("Events.RouteCostThisTurn", "{0} this turn");
            public static readonly ModString SelectedBuildSite = new ModString("Events.SelectedBuildSite", "Selected {0} build site at {1}");
            public static readonly ModString SelectedWielder = new ModString("Events.SelectedWielder", "Selected wielder {0}");
            public static readonly ModString StoryCameraFocusAreaAround = new ModString("Events.StoryCameraFocusAreaAround", "Camera focuses on the area around {0}.");
            public static readonly ModString StoryCameraFocusConversationArea = new ModString("Events.StoryCameraFocusConversationArea", "Camera focuses on the conversation area: {0}.");
            public static readonly ModString StoryCameraFocusMapEntity = new ModString("Events.StoryCameraFocusMapEntity", "map entity");
            public static readonly ModString StoryCameraFocusTargetAt = new ModString("Events.StoryCameraFocusTargetAt", "{0} at {1}");
            public static readonly ModString StoryCameraFocusTile = new ModString("Events.StoryCameraFocusTile", "tile");
            public static readonly ModString Wielder = new ModString("Events.Wielder", "wielder");
            public static readonly ModString WieldersNoLongerVisible = new ModString("Events.WieldersNoLongerVisible", "{0} no longer visible");
            public static readonly ModString WielderMoved = new ModString("Events.WielderMoved", "{0} moved to {1}");
            public static readonly ModString WielderTeleported = new ModString("Events.WielderTeleported", "{0} teleported to {1}");

            /// <summary>The mod could not reach the game's own map input at all. Spoken inside
            /// <see cref="ModError"/>; these are mod failures, not the game refusing an action.
            /// </summary>
            public static readonly ModString MapInputNotReady = new ModString("Events.MapInputNotReady", "Map input is not ready.");
            public static readonly ModString MapInteractionNotReady = new ModString("Events.MapInteractionNotReady", "Map interaction is not ready.");
            public static readonly ModString MapTileNotTargetable = new ModString("Events.MapTileNotTargetable", "Could not target tile.");
            public static readonly ModString MapPrimaryActionFailed = new ModString("Events.MapPrimaryActionFailed", "Could not perform primary action.");
            public static readonly ModString MapSecondaryActionFailed = new ModString("Events.MapSecondaryActionFailed", "Could not perform secondary action.");
        }

        public static class Graph
        {
            public static readonly ModString DragCancelled = new ModString("Graph.DragCancelled", "Cancelled drag");
            public static readonly ModString DragDropHint = new ModString("Graph.DragDropHint", "{0} to drop {1}.");
            public static readonly ModString DragDropRefused = new ModString("Graph.DragDropRefused", "{0} cannot go there");
            public static readonly ModString DragDropTarget = new ModString("Graph.DragDropTarget", "drop target");
            public static readonly ModString DragDropped = new ModString("Graph.DragDropped", "Dropped {0}");
            public static readonly ModString DragHint = new ModString("Graph.DragHint", "{0} to drag {1}.");
            public static readonly ModString DragStarted = new ModString("Graph.DragStarted", "Dragging {0}. {1} to drop, {2} to cancel.");
            public static readonly ModString DragStartedPlain = new ModString("Graph.DragStartedPlain", "Dragging {0}");
            public static readonly ModString FractionUnit = new ModString("Graph.FractionUnit", "{0} of {1} {2}");
            public static readonly ModString FragmentSeparator = new ModString("Graph.FragmentSeparator", " ");
            public static readonly ModString ListSeparator = new ModString("Graph.ListSeparator", ", ");
            public static readonly ModString Quantity = new ModString("Graph.Quantity", "x {0}");
        }

        /// <summary>
        /// The words a KEY CHORD is spoken with (<c>input/ChordNames.cs</c>): the modifier names, the
        /// joiner that puts a chord together, and the mod's own names for the keys it binds gestures
        /// to. A language that spells a chord differently - a different joiner, another word order -
        /// changes <see cref="ChordJoiner"/> and nothing else. Keys the mod binds nothing to are named
        /// by Unity's own display name for them, so this list only grows when a new gesture lands on
        /// a key nobody has named.
        /// </summary>
        public static class Keys
        {
            public static readonly ModString Alt = new ModString("Keys.Alt", "Alt");
            public static readonly ModString Backquote = new ModString("Keys.Backquote", "Backquote");
            public static readonly ModString Backslash = new ModString("Keys.Backslash", "Backslash");
            public static readonly ModString Backspace = new ModString("Keys.Backspace", "Backspace");
            public static readonly ModString ChordJoiner = new ModString("Keys.ChordJoiner", "{0}+{1}");
            public static readonly ModString Ctrl = new ModString("Keys.Ctrl", "Ctrl");
            public static readonly ModString DownArrow = new ModString("Keys.DownArrow", "Down arrow");
            public static readonly ModString End = new ModString("Keys.End", "End");
            public static readonly ModString Enter = new ModString("Keys.Enter", "Enter");
            public static readonly ModString Escape = new ModString("Keys.Escape", "Escape");
            public static readonly ModString Home = new ModString("Keys.Home", "Home");
            public static readonly ModString LeftArrow = new ModString("Keys.LeftArrow", "Left arrow");
            public static readonly ModString NumpadEnter = new ModString("Keys.NumpadEnter", "Numpad Enter");
            public static readonly ModString RightArrow = new ModString("Keys.RightArrow", "Right arrow");
            public static readonly ModString Shift = new ModString("Keys.Shift", "Shift");
            public static readonly ModString Space = new ModString("Keys.Space", "Space");
            public static readonly ModString Tab = new ModString("Keys.Tab", "Tab");
            public static readonly ModString UpArrow = new ModString("Keys.UpArrow", "Up arrow");
        }

        public static class Spatial
        {
            public static readonly ModString Acting = new ModString("Spatial.Acting", "acting");
            public static readonly ModString Attack = new ModString("Spatial.Attack", "attack");
            public static readonly ModString AttackRange = new ModString("Spatial.AttackRange", "Attack range");
            public static readonly ModString AridTrees = new ModString("Spatial.AridTrees", "Arid trees");
            public static readonly ModString Bamboo = new ModString("Spatial.Bamboo", "Bamboo");
            public static readonly ModString Barricade = new ModString("Spatial.Barricade", "Barricade");
            public static readonly ModString BirchForest = new ModString("Spatial.BirchForest", "Birch forest");
            public static readonly ModString Bones = new ModString("Spatial.Bones", "Bones");
            public static readonly ModString Bridge = new ModString("Spatial.Bridge", "Bridge");
            public static readonly ModString Campfire = new ModString("Spatial.Campfire", "Campfire");
            public static readonly ModString CliffHeight = new ModString("Spatial.CliffHeight", "cliff, height {0}");
            public static readonly ModString Commander = new ModString("Spatial.Commander", "Commander");
            public static readonly ModString CommanderPossessive = new ModString("Spatial.CommanderPossessive", "commander's");
            public static readonly ModString CobblestoneRoad = new ModString("Spatial.CobblestoneRoad", "Cobblestone road");
            public static readonly ModString Cost = new ModString("Spatial.Cost", "cost {0}");
            public static readonly ModString DamagePreview = new ModString("Spatial.DamagePreview", "{0}damage {1}");
            public static readonly ModString DeadlyRange = new ModString("Spatial.DeadlyRange", "Deadly range");
            public static readonly ModString Deadly = new ModString("Spatial.Deadly", "deadly");
            public static readonly ModString DeadBodies = new ModString("Spatial.DeadBodies", "Dead bodies");
            public static readonly ModString DeadSoldiers = new ModString("Spatial.DeadSoldiers", "Dead soldiers");
            public static readonly ModString DeepWater = new ModString("Spatial.DeepWater", "Deep water");
            public static readonly ModString Deforestation = new ModString("Spatial.Deforestation", "Deforestation");
            public static readonly ModString Debris = new ModString("Spatial.Debris", "debris");
            public static readonly ModString Destination = new ModString("Spatial.Destination", "Destination");
            public static readonly ModString DestinationAt = new ModString("Spatial.DestinationAt", "Destination: {0}");
            public static readonly ModString Destroys = new ModString("Spatial.Destroys", "destroys");
            public static readonly ModString DragonBones = new ModString("Spatial.DragonBones", "Dragon bones");
            public static readonly ModString Dirt = new ModString("Spatial.Dirt", "Dirt");
            public static readonly ModString DirtRoad = new ModString("Spatial.DirtRoad", "Dirt road");
            // The effect layer's words. They follow the terrain in the same sentence, so they are
            // lower case like "impassable" rather than named the way a terrain is.
            public static readonly ModString EffectBurnMarks = new ModString("Spatial.EffectBurnMarks", "burn marks");
            public static readonly ModString EffectFireflies = new ModString("Spatial.EffectFireflies", "fireflies");
            public static readonly ModString EffectFog = new ModString("Spatial.EffectFog", "fog");
            public static readonly ModString EffectRaysOfLight = new ModString("Spatial.EffectRaysOfLight", "rays of light");
            public static readonly ModString EffectSmoke = new ModString("Spatial.EffectSmoke", "smoke");
            public static readonly ModString EffectSnow = new ModString("Spatial.EffectSnow", "snow");
            public static readonly ModString EffectWildfire = new ModString("Spatial.EffectWildfire", "wildfire");
            public static readonly ModString ElevatedGroundHeight = new ModString("Spatial.ElevatedGroundHeight", "elevated ground, height {0}");
            public static readonly ModString EnemySpawnPoint = new ModString("Spatial.EnemySpawnPoint", "enemy spawn point");
            public static readonly ModString Enemy = new ModString("Spatial.Enemy", "enemy");
            public static readonly ModString FaeyForest = new ModString("Spatial.FaeyForest", "Faey forest");
            public static readonly ModString Farmland = new ModString("Spatial.Farmland", "Farmland");
            public static readonly ModString Excavation = new ModString("Spatial.Excavation", "Excavation");
            public static readonly ModString ExtraTargetPrefix = new ModString("Spatial.ExtraTargetPrefix", "extra target ");
            public static readonly ModString FishingSpot = new ModString("Spatial.FishingSpot", "Fishing spot");
            public static readonly ModString FortifiedGate = new ModString("Spatial.FortifiedGate", "Fortified gate");
            public static readonly ModString Friendly = new ModString("Spatial.Friendly", "friendly");
            public static readonly ModString FurthestReachableInTurns = new ModString("Spatial.FurthestReachableInTurns", "furthest reachable in {0} turns");
            public static readonly ModString FurthestReachableNextTurn = new ModString("Spatial.FurthestReachableNextTurn", "furthest reachable next turn");
            public static readonly ModString FurthestReachableThisTurn = new ModString("Spatial.FurthestReachableThisTurn", "furthest reachable this turn");
            public static readonly ModString Grass = new ModString("Spatial.Grass", "Grass");
            public static readonly ModString Health = new ModString("Spatial.Health", "{0} / {1} health");
            public static readonly ModString Here = new ModString("Spatial.Here", "here");
            public static readonly ModString HuntingCamp = new ModString("Spatial.HuntingCamp", "Hunting camp");
            public static readonly ModString Impassable = new ModString("Spatial.Impassable", "impassable");
            // The same, with what is standing there: a fight paints the board with the ground the
            // battle was joined on, so a blocked cell has something to be called.
            public static readonly ModString ImpassableObstacle = new ModString("Spatial.ImpassableObstacle", "{0}, impassable");
            public static readonly ModString InteractionPoint = new ModString("Spatial.InteractionPoint", "Interaction point");
            public static readonly ModString Interactable = new ModString("Spatial.Interactable", "interactable");
            public static readonly ModString InteractableNextTurn = new ModString("Spatial.InteractableNextTurn", "interactable next turn");
            public static readonly ModString Kills = new ModString("Spatial.Kills", "kills {0}");
            public static readonly ModString MayDestroy = new ModString("Spatial.MayDestroy", "may destroy");
            public static readonly ModString Magnolia = new ModString("Spatial.Magnolia", "Magnolia trees");
            public static readonly ModString MidsummerDecorations = new ModString("Spatial.MidsummerDecorations", "Midsummer decorations");
            public static readonly ModString Movement = new ModString("Spatial.Movement", "Movement");
            public static readonly ModString MovementCost = new ModString("Spatial.MovementCost", "Movement cost: {0}");
            public static readonly ModString MovementRange = new ModString("Spatial.MovementRange", "Movement range");
            public static readonly ModString Mountain = new ModString("Spatial.Mountain", "Mountain");
            public static readonly ModString Obstruction = new ModString("Spatial.Obstruction", "Obstruction");
            public static readonly ModString Neutral = new ModString("Spatial.Neutral", "neutral");
            public static readonly ModString NextTurn = new ModString("Spatial.NextTurn", "next turn");
            public static readonly ModString NoRoutePreview = new ModString("Spatial.NoRoutePreview", "no route preview");
            public static readonly ModString OnRoute = new ModString("Spatial.OnRoute", "On route");
            public static readonly ModString Palisade = new ModString("Spatial.Palisade", "Palisade");
            public static readonly ModString PositionAndMapSize = new ModString("Spatial.PositionAndMapSize", "Position {0}, {1}. Map is {2} by {3}.");
            public static readonly ModString PrimaryPrefix = new ModString("Spatial.PrimaryPrefix", "primary ");
            public static readonly ModString RangeAndMovement = new ModString("Spatial.RangeAndMovement", "{0} and movement range");
            public static readonly ModString Reachable = new ModString("Spatial.Reachable", "reachable");
            public static readonly ModString Reloading = new ModString("Spatial.Reloading", "reloading");
            public static readonly ModString Road = new ModString("Spatial.Road", "Road");
            public static readonly ModString RoadDirectionSeparator = new ModString("Spatial.RoadDirectionSeparator", "{0} {1}");
            public static readonly ModString Ruins = new ModString("Spatial.Ruins", "Ruins");
            public static readonly ModString Sand = new ModString("Spatial.Sand", "Sand");
            public static readonly ModString ShallowWater = new ModString("Spatial.ShallowWater", "Shallow water");
            public static readonly ModPluralString SkippedTileCount = new ModPluralString("Spatial.SkippedTileCount", "Skipped {0} tile", "Skipped {0} tiles");
            public static readonly ModString SpawnPoint = new ModString("Spatial.SpawnPoint", "spawn point");
            // The siege structures a cell can be. The scanner names a whole tower or flight of
            // stairs with these same two lines, since one tower is one tower however many cells
            // it covers.
            public static readonly ModString StairsHeight = new ModString("Spatial.StairsHeight", "stairs, height {0}");
            public static readonly ModString Structures = new ModString("Spatial.Structures", "Structures");
            public static readonly ModString TemperateTrees = new ModString("Spatial.TemperateTrees", "Temperate trees");
            public static readonly ModString Tombstones = new ModString("Spatial.Tombstones", "Tombstones");
            public static readonly ModString Torch = new ModString("Spatial.Torch", "Torch");
            public static readonly ModString Threatened = new ModString("Spatial.Threatened", "Threatened");
            public static readonly ModString ThisTurnAt = new ModString("Spatial.ThisTurnAt", "This turn: {0}");
            public static readonly ModString TowerHeight = new ModString("Spatial.TowerHeight", "tower, height {0}");
            public static readonly ModString TurnsIn = new ModString("Spatial.TurnsIn", "in {0} turns");
            public static readonly ModString Unexplored = new ModString("Spatial.Unexplored", "Unexplored");
            // Walkable ground no troop can ever be standing on, so there is no height worth
            // saying: nothing will be there to make use of it.
            public static readonly ModString Unreachable = new ModString("Spatial.Unreachable", "unreachable");
            public static readonly ModString Unseen = new ModString("Spatial.Unseen", "Unseen");
            public static readonly ModString Visited = new ModString("Spatial.Visited", "visited");
            public static readonly ModString WinterDecorations = new ModString("Spatial.WinterDecorations", "Winter decorations");
            public static readonly ModString Wall = new ModString("Spatial.Wall", "Wall");
            public static readonly ModString WallHeight = new ModString("Spatial.WallHeight", "wall, height {0}");
            public static readonly ModString Water = new ModString("Spatial.Water", "Water");
            public static readonly ModString WaterEdge = new ModString("Spatial.WaterEdge", "Water edge");
            public static readonly ModString WithinZoneOfControl = new ModString("Spatial.WithinZoneOfControl", "Within {0} zone of control");
            public static readonly ModString ZoneOfControl = new ModString("Spatial.ZoneOfControl", "Zone of control");
            public static readonly ModString ZoneOfControlTiles = new ModString("Spatial.ZoneOfControlTiles", "{0} within {1} zone of control");
            public static readonly ModString ZoneOfControlInstance = new ModString("Spatial.ZoneOfControlInstance", "{0} within {1}");
            public static readonly ModString Coordinates = new ModString("Spatial.Coordinates", "{0}, {1}");
            public static readonly ModString MovementOfMax = new ModString("Spatial.MovementOfMax", "{0} / {1}");
        }

        public static class Actions
        {
            public static readonly ModString Cancel = new ModString("Actions.Cancel", "Cancel");
            public static readonly ModString CombatFocusActingTroop = new ModString("Actions.CombatFocusActingTroop", "Focus Acting Troop");
            public static readonly ModString CombatFocusTimeline = new ModString("Actions.CombatFocusTimeline", "Focus Timeline");
            public static readonly ModString CombatInspect = new ModString("Actions.CombatInspect", "Inspect Combat Hex");
            public static readonly ModString CombatNextActingTroop = new ModString("Actions.CombatNextActingTroop", "Next Acting Troop");
            public static readonly ModString CombatNextEnemyTroop = new ModString("Actions.CombatNextEnemyTroop", "Next Enemy Troop");
            public static readonly ModString CombatPreviousActingTroop = new ModString("Actions.CombatPreviousActingTroop", "Previous Acting Troop");
            public static readonly ModString CombatPreviousEnemyTroop = new ModString("Actions.CombatPreviousEnemyTroop", "Previous Enemy Troop");
            public static readonly ModString DescribeBattlefield = new ModString("Actions.DescribeBattlefield", "Describe battlefield");
            public static readonly ModString DescribePosition = new ModString("Actions.DescribePosition", "Describe map");
            public static readonly ModString Dismiss = new ModString("Actions.Dismiss", "Dismiss");
            public static readonly ModString FirstBufferLine = new ModString("Actions.FirstBufferLine", "First Buffer Line");
            public static readonly ModString FocusHudNotifications = new ModString("Actions.FocusHudNotifications", "Focus HUD Notifications");
            public static readonly ModString FocusHudObjectives = new ModString("Actions.FocusHudObjectives", "Focus HUD Objectives");
            public static readonly ModString FocusHudResources = new ModString("Actions.FocusHudResources", "Focus HUD Resources");
            public static readonly ModString FocusHudTroops = new ModString("Actions.FocusHudTroops", "Focus HUD Troops");
            public static readonly ModString HexGridEast = new ModString("Actions.HexGridEast", "Hex Grid East");
            public static readonly ModString HexGridNorthEast = new ModString("Actions.HexGridNorthEast", "Hex Grid Northeast");
            public static readonly ModString HexGridNorthWest = new ModString("Actions.HexGridNorthWest", "Hex Grid Northwest");
            public static readonly ModString HexGridSouthEast = new ModString("Actions.HexGridSouthEast", "Hex Grid Southeast");
            public static readonly ModString HexGridSouthWest = new ModString("Actions.HexGridSouthWest", "Hex Grid Southwest");
            public static readonly ModString HexGridFocusCenterTile = new ModString("Actions.HexGridFocusCenterTile", "Focus Center Tile");
            public static readonly ModString HexGridWest = new ModString("Actions.HexGridWest", "Hex Grid West");
            public static readonly ModString HexGridSkipEast = new ModString("Actions.HexGridSkipEast", "Move to next interesting tile east");
            public static readonly ModString HexGridSkipNorthEast = new ModString("Actions.HexGridSkipNorthEast", "Move to next interesting tile north east");
            public static readonly ModString HexGridSkipNorthWest = new ModString("Actions.HexGridSkipNorthWest", "Move to next interesting tile north west");
            public static readonly ModString HexGridSkipSouthEast = new ModString("Actions.HexGridSkipSouthEast", "Move to next interesting tile south east");
            public static readonly ModString HexGridSkipSouthWest = new ModString("Actions.HexGridSkipSouthWest", "Move to next interesting tile south west");
            public static readonly ModString HexGridSkipWest = new ModString("Actions.HexGridSkipWest", "Move to next interesting tile west");
            public static readonly ModString JumpToBookmark = new ModString("Actions.JumpToBookmark", "Jump To Bookmark {0}");
            public static readonly ModString LastBufferLine = new ModString("Actions.LastBufferLine", "Last Buffer Line");
            public static readonly ModString MapMoveEast = new ModString("Actions.MapMoveEast", "Map Move East");
            public static readonly ModString MapMoveNorth = new ModString("Actions.MapMoveNorth", "Map Move North");
            public static readonly ModString MapMoveSouth = new ModString("Actions.MapMoveSouth", "Map Move South");
            public static readonly ModString MapMoveWest = new ModString("Actions.MapMoveWest", "Map Move West");
            public static readonly ModString MapSkipEast = new ModString("Actions.MapSkipEast", "Move to next interesting tile east");
            public static readonly ModString MapSkipNorth = new ModString("Actions.MapSkipNorth", "Move to next interesting tile north");
            public static readonly ModString MapSkipSouth = new ModString("Actions.MapSkipSouth", "Move to next interesting tile south");
            public static readonly ModString MapSkipWest = new ModString("Actions.MapSkipWest", "Move to next interesting tile west");
            public static readonly ModString NextBuffer = new ModString("Actions.NextBuffer", "Next Buffer");
            public static readonly ModString NextBufferLine = new ModString("Actions.NextBufferLine", "Next Buffer Line");
            public static readonly ModString NextSettlement = new ModString("Actions.NextSettlement", "Next Settlement");
            public static readonly ModString NextWielder = new ModString("Actions.NextWielder", "Next Wielder");
            public static readonly ModString PreviousBuffer = new ModString("Actions.PreviousBuffer", "Previous Buffer");
            public static readonly ModString PreviousBufferLine = new ModString("Actions.PreviousBufferLine", "Previous Buffer Line");
            public static readonly ModString ReadThreat = new ModString("Actions.ReadThreat", "Read Threat");
            public static readonly ModString SaveBookmark = new ModString("Actions.SaveBookmark", "Save Bookmark {0}");
            public static readonly ModString ScannerDecreaseLookAroundRadius = new ModString("Actions.ScannerDecreaseLookAroundRadius", "Decrease Look Around Radius");
            public static readonly ModString ScannerIncreaseLookAroundRadius = new ModString("Actions.ScannerIncreaseLookAroundRadius", "Increase Look Around Radius");
            public static readonly ModString ScannerJumpToResult = new ModString("Actions.ScannerJumpToResult", "Jump To Scanner Result");
            public static readonly ModString ScannerLookAround = new ModString("Actions.ScannerLookAround", "Look Around");
            public static readonly ModString ScannerNextCategory = new ModString("Actions.ScannerNextCategory", "Next Scanner Category");
            public static readonly ModString ScannerNextCustomEntry = new ModString("Actions.ScannerNextCustomEntry", "Move to next result in custom category {0}");
            public static readonly ModString ScannerNextInstance = new ModString("Actions.ScannerNextInstance", "Next Scanner Instance");
            public static readonly ModString ScannerNextItem = new ModString("Actions.ScannerNextItem", "Next Scanner Item");
            public static readonly ModString ScannerNextSubcategory = new ModString("Actions.ScannerNextSubcategory", "Next Scanner Subcategory");
            public static readonly ModString ScannerPreviousCategory = new ModString("Actions.ScannerPreviousCategory", "Previous Scanner Category");
            public static readonly ModString ScannerPreviousCustomEntry = new ModString("Actions.ScannerPreviousCustomEntry", "Move to previous result in custom category {0}");
            public static readonly ModString ScannerPreviousInstance = new ModString("Actions.ScannerPreviousInstance", "Previous Scanner Instance");
            public static readonly ModString ScannerPreviousItem = new ModString("Actions.ScannerPreviousItem", "Previous Scanner Item");
            public static readonly ModString ScannerPreviousSubcategory = new ModString("Actions.ScannerPreviousSubcategory", "Previous Scanner Subcategory");
            public static readonly ModString ScannerReturnFromJump = new ModString("Actions.ScannerReturnFromJump", "Return To Tile Before Jump");
            public static readonly ModString ScannerSpeakDistanceAndDirection = new ModString("Actions.ScannerSpeakDistanceAndDirection", "Scanner Result Distance and Direction");
            public static readonly ModString SonarSweep = new ModString("Actions.SonarSweep", "Play Sonar Sweep");
            public static readonly ModString SpeakBookmarkDirection = new ModString("Actions.SpeakBookmarkDirection", "Speak Direction To Bookmark {0}");
            public static readonly ModString SummarizeReachableEntities = new ModString("Actions.SummarizeReachableEntities", "Summarize Reachable Entities");
            public static readonly ModString SummarizeEnemyResources = new ModString("Actions.SummarizeEnemyResources", "Summarize enemy essence");
            public static readonly ModString SummarizeResources = new ModString("Actions.SummarizeResources", "Summarize Resources");
            /// <summary>The Keybinds row's tooltip: what the gesture does where there are no resources.</summary>
            public static readonly ModString SummarizeResourcesTooltip = new ModString("Actions.SummarizeResourcesTooltip", "Summarizes your essence in combat");
            public static readonly ModString ToggleBookmarkBeacon = new ModString("Actions.ToggleBookmarkBeacon", "Toggle Beacon For Bookmark {0}");
            public static readonly ModPluralString TroopSplit = new ModPluralString("Actions.TroopSplit", "Split Off {0} Troop", "Split Off {0} Troops");
            public static readonly ModString UiBack = new ModString("Actions.UiBack", "Back");
            /// <summary>The Keybinds row's tooltip for Back: what it does on the two screens with a grid.</summary>
            public static readonly ModString UiBackTooltip = new ModString("Actions.UiBackTooltip", "Moves focus back to the adventure map or combat grid when elsewhere on those screens");
            public static readonly ModString UiCarry = new ModString("Actions.UiCarry", "Start drag");
            public static readonly ModString UiClearSearch = new ModString("Actions.UiClearSearch", "Clear typeahead");
            public static readonly ModString UiCoarseDecrease = new ModString("Actions.UiCoarseDecrease", "Decrease slider by 10%");
            public static readonly ModString UiCoarseIncrease = new ModString("Actions.UiCoarseIncrease", "Increase slider by 10%");
            public static readonly ModString UiDown = new ModString("Actions.UiDown", "Move Down");
            public static readonly ModString UiEnd = new ModString("Actions.UiEnd", "Last Item");
            public static readonly ModString UiHome = new ModString("Actions.UiHome", "First Item");
            public static readonly ModString UiLeft = new ModString("Actions.UiLeft", "Move Left");
            public static readonly ModString UiLeftClick = new ModString("Actions.UiLeftClick", "Left click or complete drag");
            public static readonly ModString UiNext = new ModString("Actions.UiNext", "Next Stop");
            public static readonly ModString UiPrev = new ModString("Actions.UiPrev", "Previous Stop");
            public static readonly ModString UiRegionNext = new ModString("Actions.UiRegionNext", "Next Region");
            public static readonly ModString UiRegionPrev = new ModString("Actions.UiRegionPrev", "Previous Region");
            public static readonly ModString UiRight = new ModString("Actions.UiRight", "Move Right");
            public static readonly ModString UiRightClick = new ModString("Actions.UiRightClick", "Right click");
            public static readonly ModString UiUp = new ModString("Actions.UiUp", "Move Up");
        }

        public static class Bookmarks
        {
            public static readonly ModString BeaconActivated = new ModString("Bookmarks.BeaconActivated", "Beacon {0} activated");
            public static readonly ModString BeaconDeactivated = new ModString("Bookmarks.BeaconDeactivated", "Beacon {0} deactivated");
            public static readonly ModString BookmarkSaved = new ModString("Bookmarks.BookmarkSaved", "Bookmark saved");
            public static readonly ModString NoBookmark = new ModString("Bookmarks.NoBookmark", "No bookmark");
        }

        public static class Combat
        {
            public static readonly ModString AbilityCancelled = new ModString("Combat.AbilityCancelled", "Ability cancelled");
            public static readonly ModString AbilityUsed = new ModString("Combat.AbilityUsed", "{0} uses {1}");
            public static readonly ModString Affects = new ModString("Combat.Affects", "{0} affects {1}");
            public static readonly ModString Appears = new ModString("Combat.Appears", "{0} appears");
            public static readonly ModString ArcanaEssence = new ModString("Combat.ArcanaEssence", "arcana");
            public static readonly ModString Attack = new ModString("Combat.Attack", "{0} {1} {2}");
            public static readonly ModString AttackLeft = new ModString("Combat.AttackLeft", "{0} attacks left");
            public static readonly ModString AttackRight = new ModString("Combat.AttackRight", "{0} attacks right");
            public static readonly ModString AttackableEntity = new ModString("Combat.AttackableEntity", "attackable entity");
            public static readonly ModString AttackVerbDefault = new ModString("Combat.AttackVerbDefault", "attacks");
            public static readonly ModString AttackVerbOpportunity = new ModString("Combat.AttackVerbOpportunity", "makes an opportunity attack against");
            public static readonly ModString AttackVerbOverwatch = new ModString("Combat.AttackVerbOverwatch", "makes an overwatch attack against");
            public static readonly ModString AttackVerbRetaliation = new ModString("Combat.AttackVerbRetaliation", "retaliates against");
            public static readonly ModString AttackVerbSpearwall = new ModString("Combat.AttackVerbSpearwall", "makes a spearwall attack against");
            public static readonly ModString AllMeleeTroops = new ModString("Combat.AllMeleeTroops", "all melee troops");
            public static readonly ModString AllRangedTroops = new ModString("Combat.AllRangedTroops", "all ranged troops");
            public static readonly ModString AllTroops = new ModString("Combat.AllTroops", "all troops");
            public static readonly ModString BattleOver = new ModString("Combat.BattleOver", "Battle over");
            public static readonly ModString BlindFury = new ModString("Combat.BlindFury", "blind fury");
            public static readonly ModString BurrowsUp = new ModString("Combat.BurrowsUp", "{0} burrows up");
            public static readonly ModString CastsSpell = new ModString("Combat.CastsSpell", "{0} casts {1}");
            public static readonly ModString CastsSpellAt = new ModString("Combat.CastsSpellAt", "{0} casts {1} at {2}");
            public static readonly ModString Challenge = new ModString("Combat.Challenge", "challenge");
            public static readonly ModString ChaosEssence = new ModString("Combat.ChaosEssence", "chaos");
            public static readonly ModString Created = new ModString("Combat.Created", "{0} created");
            public static readonly ModString CreationEssence = new ModString("Combat.CreationEssence", "creation");
            public static readonly ModString DamageDestroyingItSuffix = new ModString("Combat.DamageDestroyingItSuffix", ", destroying it");
            public static readonly ModString DamageKillsSuffix = new ModString("Combat.DamageKillsSuffix", ", killing {0}");
            public static readonly ModString DealsDamage = new ModString("Combat.DealsDamage", "{0} deals {1} {2} damage to {3}{4}");
            public static readonly ModString Defeat = new ModString("Combat.Defeat", "Defeat");
            public static readonly ModString Destroyed = new ModString("Combat.Destroyed", "{0} destroyed");
            public static readonly ModString DestructionEssence = new ModString("Combat.DestructionEssence", "destruction");
            public static readonly ModString Draw = new ModString("Combat.Draw", "Draw");
            public static readonly ModString Effect = new ModString("Combat.Effect", "effect");
            public static readonly ModString EnemyMeleeTroops = new ModString("Combat.EnemyMeleeTroops", "enemy melee troops");
            public static readonly ModString EnemyCommander = new ModString("Combat.EnemyCommander", "enemy {0}");
            public static readonly ModString EnemyRangedTroops = new ModString("Combat.EnemyRangedTroops", "enemy ranged troops");
            public static readonly ModString EnemyTroop = new ModString("Combat.EnemyTroop", "{0} enemy {1}");
            public static readonly ModString EnemyTroops = new ModString("Combat.EnemyTroops", "enemy troops");
            public static readonly ModString EssenceAmountCompact = new ModString("Combat.EssenceAmountCompact", "+{0} {1}");
            public static readonly ModString EssenceAmounts = new ModString("Combat.EssenceAmounts", "{0} essence");
            public static readonly ModString ExplosiveBarrel = new ModString("Combat.ExplosiveBarrel", "explosive barrel");
            public static readonly ModString FailedBurrow = new ModString("Combat.FailedBurrow", "{0}, failed burrow");
            public static readonly ModString FaeyFire = new ModString("Combat.FaeyFire", "Faey Fire");
            public static readonly ModString FaeyFireWithBolts = new ModString("Combat.FaeyFireWithBolts", "{0} casts {1}, {2}");
            public static readonly ModString FacingLeft = new ModString("Combat.FacingLeft", "facing left");
            public static readonly ModString FacingRight = new ModString("Combat.FacingRight", "facing right");
            public static readonly ModString Lunge = new ModString("Combat.Lunge", "lunge");
            public static readonly ModString MapEntityDamage = new ModString("Combat.MapEntityDamage", "map entity");
            public static readonly ModString MeleeDamage = new ModString("Combat.MeleeDamage", "melee");
            public static readonly ModString MothersLove = new ModString("Combat.MothersLove", "mother's love");
            public static readonly ModString NewTurn = new ModString("Combat.NewTurn", "It is {0}'s turn");
            public static readonly ModString OpportunityAttack = new ModString("Combat.OpportunityAttack", "opportunity attack");
            public static readonly ModString OrderEssence = new ModString("Combat.OrderEssence", "order");
            public static readonly ModString Overwatch = new ModString("Combat.Overwatch", "overwatch");
            public static readonly ModString PushedTo = new ModString("Combat.PushedTo", "{0} pushed to {1}");
            public static readonly ModString RangedDamage = new ModString("Combat.RangedDamage", "ranged");
            public static readonly ModString RavenformsTo = new ModString("Combat.RavenformsTo", "{0} ravenforms to {1}");
            public static readonly ModString RemovedFrom = new ModString("Combat.RemovedFrom", "{0} removed from {1}");
            public static readonly ModString Retaliation = new ModString("Combat.Retaliation", "retaliation");
            public static readonly ModString Spearwall = new ModString("Combat.Spearwall", "spearwall");
            public static readonly ModString SpellCancelled = new ModString("Combat.SpellCancelled", "Spell cancelled");
            public static readonly ModString SpellDamage = new ModString("Combat.SpellDamage", "spell");
            public static readonly ModString Splash = new ModString("Combat.Splash", "splash");
            public static readonly ModString Summoned = new ModString("Combat.Summoned", "{0} summoned");
            public static readonly ModString TakesDamage = new ModString("Combat.TakesDamage", "{0} takes {1} {2} damage{3}");
            public static readonly ModString TeleportsTo = new ModString("Combat.TeleportsTo", "{0} teleports to {1}");
            public static readonly ModString Tile = new ModString("Combat.Tile", "tile {0}");
            public static readonly ModString Troop = new ModString("Combat.Troop", "troop");
            public static readonly ModString TroopAt = new ModString("Combat.TroopAt", "{0} at {1}");
            public static readonly ModString TroopMoved = new ModString("Combat.TroopMoved", "{0} moves to {1}");
            public static readonly ModString TroopQuantity = new ModString("Combat.TroopQuantity", "{0} {1}");
            public static readonly ModString Unknown = new ModString("Combat.Unknown", "unknown");
            public static readonly ModString UnknownEntity = new ModString("Combat.UnknownEntity", "unknown entity");
            public static readonly ModString UnknownTarget = new ModString("Combat.UnknownTarget", "unknown target");
            public static readonly ModString UnknownTroop = new ModString("Combat.UnknownTroop", "unknown troop");
            public static readonly ModString Victory = new ModString("Combat.Victory", "Victory");
            public static readonly ModString Walkover = new ModString("Combat.Walkover", "Walkover");
            public static readonly ModString Wielder = new ModString("Combat.Wielder", "wielder");
            public static readonly ModString WielderEssenceGenerated = new ModString("Combat.WielderEssenceGenerated", "{0}: {1}");
            public static readonly ModString YourMeleeTroops = new ModString("Combat.YourMeleeTroops", "your melee troops");
            public static readonly ModString YourRangedTroops = new ModString("Combat.YourRangedTroops", "your ranged troops");
            public static readonly ModString YourTroops = new ModString("Combat.YourTroops", "your troops");
            public static readonly ModPluralString BoltAt = new ModPluralString("Combat.BoltAt", "{0} bolt at {1}", "{0} bolts at {1}");
            public static readonly ModString Spell = new ModString("Combat.Spell", "Spell");
        }

        public static class UI
        {
            public static readonly ModString Blank = new ModString("UI.Blank", "blank");
            public static readonly ModString BufferEmpty = new ModString("UI.BufferEmpty", "Buffer empty");
            public static readonly ModString CannotDropThere = new ModString("UI.CannotDropThere", "Cannot drop there.");
            public static readonly ModString CharacterColon = new ModString("UI.CharacterColon", "colon");
            public static readonly ModString CharacterComma = new ModString("UI.CharacterComma", "comma");
            public static readonly ModString CharacterDash = new ModString("UI.CharacterDash", "dash");
            public static readonly ModString CharacterDot = new ModString("UI.CharacterDot", "dot");
            public static readonly ModString CharacterSemicolon = new ModString("UI.CharacterSemicolon", "semicolon");
            public static readonly ModString CharacterSpace = new ModString("UI.CharacterSpace", "space");
            public static readonly ModString CharacterTab = new ModString("UI.CharacterTab", "tab");
            public static readonly ModString CharacterUnderscore = new ModString("UI.CharacterUnderscore", "underscore");
            public static readonly ModString ColumnDetails = new ModString("UI.ColumnDetails", "Details");
            public static readonly ModString ColumnElement = new ModString("UI.ColumnElement", "Element");
            public static readonly ModString ColumnFaction = new ModString("UI.ColumnFaction", "Faction");
            public static readonly ModString ColumnGames = new ModString("UI.ColumnGames", "Games");
            public static readonly ModString ColumnKills = new ModString("UI.ColumnKills", "Kills");
            public static readonly ModString ColumnMap = new ModString("UI.ColumnMap", "Map");
            public static readonly ModString ColumnPlayDistribution = new ModString("UI.ColumnPlayDistribution", "Play distribution");
            public static readonly ModString ColumnRank = new ModString("UI.ColumnRank", "Rank");
            public static readonly ModString ColumnRound = new ModString("UI.ColumnRound", "Round");
            public static readonly ModString ColumnSpell = new ModString("UI.ColumnSpell", "Spell");
            public static readonly ModString ColumnTimesCast = new ModString("UI.ColumnTimesCast", "Times cast");
            public static readonly ModString ColumnTimesRecruited = new ModString("UI.ColumnTimesRecruited", "Times recruited or started with");
            public static readonly ModString ColumnTimesTrained = new ModString("UI.ColumnTimesTrained", "Times trained");
            public static readonly ModString ColumnTroop = new ModString("UI.ColumnTroop", "Troop");
            public static readonly ModString ColumnWielder = new ModString("UI.ColumnWielder", "Wielder");
            public static readonly ModString CombatDisconnectedTiles = new ModString("UI.CombatDisconnectedTiles", "Some shown tiles are separated. Use W and Shift W to reach all range tiles.");
            public static readonly ModString Battlefield = new ModString("UI.Battlefield", "Battlefield");
            public static readonly ModString Cancelled = new ModString("UI.Cancelled", "Cancelled.");
            public static readonly ModString DragCancelled = new ModString("UI.DragCancelled", "Drag cancelled.");
            public static readonly ModString DragStarted = new ModString("UI.DragStarted", "Started drag. Move to destination and press enter to drop.");
            public static readonly ModString EditCancelled = new ModString("UI.EditCancelled", "Cancelled");
            public static readonly ModString EditCommitted = new ModString("UI.EditCommitted", "edited");
            public static readonly ModString EditStarted = new ModString("UI.EditStarted", "editing");
            public static readonly ModString Empty = new ModString("UI.Empty", "Empty");
            public static readonly ModString EmptyTroopSlot = new ModString("UI.EmptyTroopSlot", "Empty troop {0}");
            public static readonly ModString ExitedInspectMode = new ModString("UI.ExitedInspectMode", "Exited inspect mode");
            public static readonly ModString Filters = new ModString("UI.Filters", "Filters");
            public static readonly ModString Heading = new ModString("UI.Heading", "{0} heading");
            public static readonly ModString Inspecting = new ModString("UI.Inspecting", "Inspecting {0}");
            public static readonly ModString InventoryGrid = new ModString("UI.InventoryGrid", "Inventory grid");
            public static readonly ModString InvalidDestination = new ModString("UI.InvalidDestination", "Invalid destination.");
            public static readonly ModString LabelValue = new ModString("UI.LabelValue", "{0}: {1}");
            public static readonly ModString NoDetails = new ModString("UI.NoDetails", "Nothing in here");
            public static readonly ModString NoScannerResults = new ModString("UI.NoScannerResults", "No scanner results");
            public static readonly ModString NotInMovementRange = new ModString("UI.NotInMovementRange", "Not in movement range");
            public static readonly ModString Percent = new ModString("UI.Percent", "{0}%");
            public static readonly ModString PressSpaceSelectItemToDrag = new ModString("UI.PressSpaceSelectItemToDrag", "Press space to select an item to drag.");
            public static readonly ModString PressSpaceToDrag = new ModString("UI.PressSpaceToDrag", "Press space to drag.");
            public static readonly ModString ReviewBufferEvents = new ModString("UI.ReviewBufferEvents", "Events");
            public static readonly ModString ReviewBufferLine = new ModString("UI.ReviewBufferLine", "{0}. {1}");
            public static readonly ModString ReviewBufferNotifications = new ModString("UI.ReviewBufferNotifications", "Notifications");
            public static readonly ModString ReviewBufferUi = new ModString("UI.ReviewBufferUi", "UI");
            public static readonly ModString RoleButton = new ModString("UI.RoleButton", "button");
            public static readonly ModString RoleCheckBox = new ModString("UI.RoleCheckBox", "check box");
            public static readonly ModString RoleCheckbox = new ModString("UI.RoleCheckbox", "checkbox");
            public static readonly ModString RoleColumnHeader = new ModString("UI.RoleColumnHeader", "column header");
            public static readonly ModString RoleComboBox = new ModString("UI.RoleComboBox", "combo box");
            public static readonly ModString RoleDocument = new ModString("UI.RoleDocument", "document");
            public static readonly ModString RoleEdit = new ModString("UI.RoleEdit", "edit");
            public static readonly ModString RoleEditable = new ModString("UI.RoleEditable", "editable");
            public static readonly ModString RoleGroup = new ModString("UI.RoleGroup", "group");
            public static readonly ModString RoleMenu = new ModString("UI.RoleMenu", "menu");
            public static readonly ModString RoleRadioButton = new ModString("UI.RoleRadioButton", "radio button");
            public static readonly ModString RoleSlider = new ModString("UI.RoleSlider", "slider");
            public static readonly ModString RoleTab = new ModString("UI.RoleTab", "tab");
            public static readonly ModString RoleTable = new ModString("UI.RoleTable", "table");
            public static readonly ModString ScannerPath = new ModString("UI.ScannerPath", "{0}, {1}. {2}");
            public static readonly ModString SearchCleared = new ModString("UI.SearchCleared", "Search cleared");
            public static readonly ModString SearchNoMatch = new ModString("UI.SearchNoMatch", "No match for {0}");
            public static readonly ModString Selected = new ModString("UI.Selected", "selected");
            public static readonly ModString SelectedCount = new ModString("UI.SelectedCount", "selected {0}x");
            public static readonly ModString Slot = new ModString("UI.Slot", "slot {0}");
            public static readonly ModString Status = new ModString("UI.Status", "status");
            public static readonly ModString StatusChecked = new ModString("UI.StatusChecked", "checked");
            public static readonly ModString StatusCollapsed = new ModString("UI.StatusCollapsed", "collapsed");
            public static readonly ModString StatusCorrupt = new ModString("UI.StatusCorrupt", "corrupt");
            public static readonly ModString StatusDisabled = new ModString("UI.StatusDisabled", "disabled");
            public static readonly ModString StatusDraggable = new ModString("UI.StatusDraggable", "draggable");
            public static readonly ModString StatusDragging = new ModString("UI.StatusDragging", "dragging");
            public static readonly ModString StatusExpanded = new ModString("UI.StatusExpanded", "expanded");
            public static readonly ModString StatusBattleLost = new ModString("UI.StatusBattleLost", "battle lost");
            public static readonly ModString StatusMissing = new ModString("UI.StatusMissing", "missing");
            public static readonly ModString StatusNotChecked = new ModString("UI.StatusNotChecked", "not checked");
            public static readonly ModString StatusUnchecked = new ModString("UI.StatusUnchecked", "unchecked");
            public static readonly ModString StatusUnavailable = new ModString("UI.StatusUnavailable", "unavailable");
            public static readonly ModString SortAscending = new ModString("UI.SortAscending", "ascending");
            public static readonly ModString SortDescending = new ModString("UI.SortDescending", "descending");
            public static readonly ModString Target = new ModString("UI.Target", "target");
            public static readonly ModString TroopSlot = new ModString("UI.TroopSlot", "{0}, {1}");
            public static readonly ModString TroopSlotWithSize = new ModString("UI.TroopSlotWithSize", "{0}, {1} / {2}, {3}");
            public static readonly ModString TroopWithSize = new ModString("UI.TroopWithSize", "{0}, {1} / {2}");
            public static readonly ModString Unselected = new ModString("UI.Unselected", "unselected");
            public static readonly ModString ModReady = new ModString("UI.ModReady", "Songs of Conquest Access v{0} ready");
            public static readonly ModString RankNumber = new ModString("UI.RankNumber", "#{0}");
            public static readonly ModString ColumnGameName = new ModString("UI.ColumnGameName", "Game name");
            public static readonly ModString ColumnPlayers = new ModString("UI.ColumnPlayers", "Players");
        }

        public static class Screens
        {
            public static readonly ModString AdventureMap = new ModString("Screens.AdventureMap", "Adventure map");
            public static readonly ModString Add = new ModString("Screens.Add", "Add");
            public static readonly ModString AddKeyword = new ModString("Screens.AddKeyword", "Add keyword");
            /// <summary>The caption over the wielder's own army on a screen that draws two armies to
            /// move troops between, spoken on entering it: {0} is the wielder's name in the possessive
            /// (<c>ModText.FormatPossessiveName</c>). Right from one of its rows crosses to the other
            /// army, which says the matching "right" caption.</summary>
            public static readonly ModString ArmyLeft = new ModString("Screens.ArmyLeft", "{0} army, left");
            /// <summary>The caption over the other wielder's army on the trade screen: {0} is that
            /// wielder's name in the possessive (<c>ModText.FormatPossessiveName</c>).</summary>
            public static readonly ModString ArmyRight = new ModString("Screens.ArmyRight", "{0} army, right");
            /// <summary>The caption over the settlement's own army on the settlement and defence pages:
            /// {0} is the settlement's name, as the page is named.</summary>
            public static readonly ModString SettlementArmyRight = new ModString("Screens.SettlementArmyRight", "{0} army, right");
            /// <summary>The caption over the army offering to join, on the hostile join offer.</summary>
            public static readonly ModString JoiningArmyRight = new ModString("Screens.JoiningArmyRight", "Joining army, right");
            public static readonly ModString ArtifactDestroyHint = new ModString("Screens.ArtifactDestroyHint", "{0} destroys.");
            public static readonly ModString ArtifactDropHint = new ModString("Screens.ArtifactDropHint", "{0} to drop.");
            public static readonly ModString ArtifactEquipHint = new ModString("Screens.ArtifactEquipHint", "{0} equips.");
            public static readonly ModString ArtifactSelectForSaleHint = new ModString("Screens.ArtifactSelectForSaleHint", "{0} selects for sale.");
            public static readonly ModString ArtifactSellHint = new ModString("Screens.ArtifactSellHint", "{0} sells.");
            public static readonly ModString ArtifactTradeHint = new ModString("Screens.ArtifactTradeHint", "{0} moves to the other backpack.");
            public static readonly ModString ArtifactUnequipHint = new ModString("Screens.ArtifactUnequipHint", "{0} unequips.");
            public static readonly ModString ArtifactUseHint = new ModString("Screens.ArtifactUseHint", "{0} uses.");
            public static readonly ModString AiControl = new ModString("Screens.AiControl", "AI control");
            public static readonly ModString AiDifficulty = new ModString("Screens.AiDifficulty", "{0} AI");
            public static readonly ModString Attacker = new ModString("Screens.Attacker", "Attacker");
            public static readonly ModString AttackerPortrait = new ModString("Screens.AttackerPortrait", "Attacker portrait");
            public static readonly ModString Audio = new ModString("Screens.Audio", "Audio");
            public static readonly ModString AudioGlossary = new ModString("Screens.AudioGlossary", "Audio glossary");
            public static readonly ModString Available = new ModString("Screens.Available", "Available");
            public static readonly ModString Back = new ModString("Screens.Back", "Back");
            public static readonly ModString Ballista = new ModString("Screens.Ballista", "Ballista");
            public static readonly ModString BattleLog = new ModString("Screens.BattleLog", "Battle log");
            public static readonly ModString Battle = new ModString("Screens.Battle", "Battle");
            /// <summary>The bug reporter's loading window, which has no text of its own.</summary>
            public static readonly ModString BugReportSending = new ModString("Screens.BugReportSending", "Sending report...");
            /// <summary>A search-result row in the bug reporter: the issue's title, its state, and its
            /// upvote count, all read from the game.</summary>
            public static readonly ModString BugReportSearchResult = new ModString("Screens.BugReportSearchResult", "{0}, {1}, {2}");
            public static readonly ModString BuyArtifact = new ModString("Screens.BuyArtifact", "Buy");
            public static readonly ModString Categories = new ModString("Screens.Categories", "Categories");
            public static readonly ModString Chat = new ModString("Screens.Chat", "Chat");
            public static readonly ModString ChatInput = new ModString("Screens.ChatInput", "Message");
            public static readonly ModString ChatSendTo = new ModString("Screens.ChatSendTo", "Send to");
            public static readonly ModString ChatUnreadMessages = new ModString("Screens.ChatUnreadMessages", "Chat, unread messages");
            public static readonly ModString Close = new ModString("Screens.Close", "Close");
            public static readonly ModString CampaignMissionProgress = new ModString("Screens.CampaignMissionProgress", "Completed: {0} / {1} missions");
            public static readonly ModString CopiedGameCodeToClipboard = new ModString("Screens.CopiedGameCodeToClipboard", "Copied game code to clipboard.");
            public static readonly ModString CopyGameCodeToClipboard = new ModString("Screens.CopyGameCodeToClipboard", "Copy game code to clipboard: {0}");
            public static readonly ModString Combat = new ModString("Screens.Combat", "Combat");
            public static readonly ModString Configure = new ModString("Screens.Configure", "Configure");
            public static readonly ModString ConfigureAnnouncementElement = new ModString("Screens.ConfigureAnnouncementElement", "Configure {0}");
            public static readonly ModString CustomCategories = new ModString("Screens.CustomCategories", "{0} custom categories");
            public static readonly ModString ClearCustomCategory = new ModString("Screens.ClearCustomCategory", "Clear this custom category");
            public static readonly ModString CustomCategoryCleared = new ModString("Screens.CustomCategoryCleared", "Custom category {0} cleared");
            public static readonly ModString CustomCategoryDefaultName = new ModString("Screens.CustomCategoryDefaultName", "Custom {0}");
            /// <summary>One of the three fixed slots as the settings list names it: its number and what it holds.</summary>
            public static readonly ModString CustomCategorySlot = new ModString("Screens.CustomCategorySlot", "Custom category {0}: {1}");
            public static readonly ModString CustomCategorySlotEmpty = new ModString("Screens.CustomCategorySlotEmpty", "empty");
            public static readonly ModString CustomCategoryName = new ModString("Screens.CustomCategoryName", "Name");
            public static readonly ModString CustomCategoryNameEmpty = new ModString("Screens.CustomCategoryNameEmpty", "A category needs a name");
            public static readonly ModString CustomCategoryNameMissingTitle = new ModString("Screens.CustomCategoryNameMissingTitle", "Name missing");
            public static readonly ModString CustomCategoryNameTaken = new ModString("Screens.CustomCategoryNameTaken", "{0} is already the name of a category");
            public static readonly ModString CustomCategoryNameTakenTitle = new ModString("Screens.CustomCategoryNameTakenTitle", "Name already in use");
            /// <summary>The Keybinds tab region for the graph cursor: arrows, stops, regions, clicks.</summary>
            public static readonly ModString Cursor = new ModString("Screens.Cursor", "Cursor");
            public static readonly ModString CancelAbility = new ModString("Screens.CancelAbility", "Cancel ability");
            public static readonly ModString CancelSpell = new ModString("Screens.CancelSpell", "Cancel spell");
            public static readonly ModString CurrentTroop = new ModString("Screens.CurrentTroop", "Current troop, {0}");
            public static readonly ModString CurrentTroopSection = new ModString("Screens.CurrentTroopSection", "Current troop");
            public static readonly ModString CombatPortraitDetail = new ModString("Screens.CombatPortraitDetail", "{0}, {1}, level {2}");
            public static readonly ModString Defender = new ModString("Screens.Defender", "Defender");
            public static readonly ModString DefenderPortrait = new ModString("Screens.DefenderPortrait", "Defender portrait");
            /// <summary>The stop on the troop placement page that holds the authored description of
            /// the battlefield layout the battle is fought on.</summary>
            public static readonly ModString Description = new ModString("Screens.Description", "Description");
            /// <summary>The three lines of an authored battlefield description, one per line so the
            /// review buffer holds three. {0} is the authored text.</summary>
            public static readonly ModString DescriptionTerrain = new ModString("Screens.DescriptionTerrain", "Terrain: {0}");
            public static readonly ModString DescriptionAttacker = new ModString("Screens.DescriptionAttacker", "Attacker: {0}");
            public static readonly ModString DescriptionDefender = new ModString("Screens.DescriptionDefender", "Defender: {0}");
            public static readonly ModString Duration = new ModString("Screens.Duration", "Duration");
            public static readonly ModString Empty = new ModString("Screens.Empty", "empty");
            public static readonly ModString Enabled = new ModString("Screens.Enabled", "Enabled");
            public static readonly ModString EntityAnnouncements = new ModString("Screens.EntityAnnouncements", "Entity announcements");
            public static readonly ModString GraphType = new ModString("Screens.GraphType", "Graph type");
            public static readonly ModString General = new ModString("Screens.General", "General");
            public static readonly ModString Group = new ModString("Screens.Group", "Group {0}");
            public static readonly ModString Income = new ModString("Screens.Income", "Income");
            public static readonly ModString InviteFriend = new ModString("Screens.InviteFriend", "Invite friend");
            public static readonly ModString KeywordAlreadyAdded = new ModString("Screens.KeywordAlreadyAdded", "That keyword is already in this custom category");
            public static readonly ModString Layout = new ModString("Screens.Layout", "Layout");
            public static readonly ModString LevelValue = new ModString("Screens.LevelValue", "level {0}");
            public static readonly ModString LoadingProgress = new ModString("Screens.LoadingProgress", "Loading progress, {0} percent");
            public static readonly ModString MainMenu = new ModString("Screens.MainMenu", "Main menu");
            /// <summary>The adventure map itself, as the stop the tile cursor lives in.</summary>
            public static readonly ModString Map = new ModString("Screens.Map", "Map");
            public static readonly ModString Menu = new ModString("Screens.Menu", "Menu");
            /// <summary>The adventure map's band of kingdom-wide buttons: the game menu and the
            /// overview pages.</summary>
            public static readonly ModString Kingdom = new ModString("Screens.Kingdom", "Kingdom");
            public static readonly ModString MissingBuilding = new ModString("Screens.MissingBuilding", "missing building");
            public static readonly ModString ModOptions = new ModString("Screens.ModOptions", "Mod options");
            public static readonly ModString Modifiers = new ModString("Screens.Modifiers", "Modifiers");
            public static readonly ModString MoveDown = new ModString("Screens.MoveDown", "Move down");
            public static readonly ModString MoveUp = new ModString("Screens.MoveUp", "Move up");
            public static readonly ModString MovedAfter = new ModString("Screens.MovedAfter", "Moved after {0}");
            public static readonly ModString MovedBefore = new ModString("Screens.MovedBefore", "Moved before {0}");
            public static readonly ModString MovedBetween = new ModString("Screens.MovedBetween", "Moved between {0} and {1}");
            public static readonly ModString NewChatMessage = new ModString("Screens.NewChatMessage", "New chat message");
            public static readonly ModString MarketplaceTradeColumn = new ModString("Screens.MarketplaceTradeColumn", "{0} {1}");
            public static readonly ModString MoveAllLeft = new ModString("Screens.MoveAllLeft", "Move all left");
            public static readonly ModString MoveAllRight = new ModString("Screens.MoveAllRight", "Move all right");
            public static readonly ModString MoveAllToDefence = new ModString("Screens.MoveAllToDefence", "Move all to defence");
            public static readonly ModString MoveAllToWielder = new ModString("Screens.MoveAllToWielder", "Move all to wielder");
            public static readonly ModString StoreWielder = new ModString("Screens.StoreWielder", "Store wielder");
            public static readonly ModString LeftRightDistribution = new ModString("Screens.LeftRightDistribution", "Left: {0}, right: {1}");
            public static readonly ModString MultiEssenceSpells = new ModString("Screens.MultiEssenceSpells", "Multi-essence spells");
            public static readonly ModString NamedLevel = new ModString("Screens.NamedLevel", "{0}, level {1}");
            public static readonly ModString NoBattlefieldDescription = new ModString("Screens.NoBattlefieldDescription", "No description for this battlefield");
            public static readonly ModString NoBuildSiteSelected = new ModString("Screens.NoBuildSiteSelected", "No build site selected");
            public static readonly ModString None = new ModString("Screens.None", "None");
            public static readonly ModString NotReady = new ModString("Screens.NotReady", "Not ready");
            public static readonly ModString Notifications = new ModString("Screens.Notifications", "Notifications");
            public static readonly ModString NotificationDismissHint = new ModString("Screens.NotificationDismissHint", "{0} dismisses");
            public static readonly ModString Objectives = new ModString("Screens.Objectives", "Objectives");
            public static readonly ModString ObjectiveCannotBeCompleted = new ModString("Screens.ObjectiveCannotBeCompleted", "Cannot be completed");
            public static readonly ModString ObjectiveCompleted = new ModString("Screens.ObjectiveCompleted", "Completed");
            public static readonly ModString ObjectiveIncomplete = new ModString("Screens.ObjectiveIncomplete", "Incomplete");
            public static readonly ModString ObjectiveMarkerFarAway = new ModString("Screens.ObjectiveMarkerFarAway", "far away");
            public static readonly ModString ObjectiveMarkerNearby = new ModString("Screens.ObjectiveMarkerNearby", "nearby");
            public static readonly ModString ObjectiveMarkerSomeDistance = new ModString("Screens.ObjectiveMarkerSomeDistance", "some distance");
            public static readonly ModString LoseCondition = new ModString("Screens.LoseCondition", "Lose condition");
            public static readonly ModString Ok = new ModString("Screens.Ok", "OK");
            public static readonly ModString Options = new ModString("Screens.Options", "Options");
            /// <summary>The binding chip of a key-binding row that the game draws empty.</summary>
            public static readonly ModString NotBound = new ModString("Screens.NotBound", "not bound");
            /// <summary>Spoken when a key-binding capture starts and the game's own instruction popup
            /// is empty or unreadable: the next key pressed becomes the binding, with no way to cancel.
            /// </summary>
            public static readonly ModString CaptureNoCancel = new ModString("Screens.CaptureNoCancel", "The next key you press becomes the binding.");
            /// <summary>The Keybinds tab: the mod's own gestures as a rebindable table.</summary>
            public static readonly ModString Keybinds = new ModString("Screens.Keybinds", "Keybinds");
            /// <summary>The Keybinds tab region for the bookmark gestures, and the Bookmarks tab.</summary>
            public static readonly ModString Bookmarks = new ModString("Screens.Bookmarks", "Bookmarks");
            /// <summary>The Bookmarks tab's caption while a game with a bookmarks file is being
            /// played: the full path of that file.</summary>
            public static readonly ModString BookmarksSavedTo = new ModString("Screens.BookmarksSavedTo", "Bookmarks are saved to {0}");
            /// <summary>The Bookmarks tab's caption in a game that has none yet.</summary>
            public static readonly ModString NoBookmarksForThisGame = new ModString("Screens.NoBookmarksForThisGame", "No bookmarks set for this game");
            public static readonly ModString CopyBookmarksToClipboard = new ModString("Screens.CopyBookmarksToClipboard", "Copy bookmarks to clipboard");
            public static readonly ModString ImportBookmarksFromClipboard = new ModString("Screens.ImportBookmarksFromClipboard", "Import bookmarks from clipboard");
            public static readonly ModString OpenBookmarksFolder = new ModString("Screens.OpenBookmarksFolder", "Open bookmarks folder");
            /// <summary>Spoken, queued, once the bookmarks file is on the clipboard.</summary>
            public static readonly ModString BookmarksCopied = new ModString("Screens.BookmarksCopied", "Bookmarks copied to the clipboard");
            public static readonly ModString BookmarksNotRead = new ModString("Screens.BookmarksNotRead", "The bookmarks file could not be read");
            /// <summary>What the import says when there was nothing to paste.</summary>
            public static readonly ModString ClipboardEmpty = new ModString("Screens.ClipboardEmpty", "The clipboard is empty");
            public static readonly ModString ClipboardNotBookmarks = new ModString("Screens.ClipboardNotBookmarks", "The clipboard does not hold a bookmarks file");
            public static readonly ModString BookmarksNotWritten = new ModString("Screens.BookmarksNotWritten", "The bookmarks file could not be written");
            /// <summary>What the import says when the pasted file belongs to the game being played.</summary>
            public static readonly ModPluralString BookmarksImported = new ModPluralString("Screens.BookmarksImported", "Imported {0} bookmark for this game", "Imported {0} bookmarks for this game");
            /// <summary>And when it belongs to another game, which is allowed and worth saying.</summary>
            public static readonly ModPluralString BookmarksImportedForOtherGame = new ModPluralString("Screens.BookmarksImportedForOtherGame", "Imported {0} bookmark for a different game than the one being played", "Imported {0} bookmarks for a different game than the one being played");
            /// <summary>And when no game is being played at all, so there is nothing for the file to
            /// differ from: it waits for the game it was written for.</summary>
            public static readonly ModPluralString BookmarksImportedForLaterGame = new ModPluralString("Screens.BookmarksImportedForLaterGame", "Imported {0} bookmark. It will be used when that game is played.", "Imported {0} bookmarks. They will be used when that game is played.");
            /// <summary>The Keybinds tab region for the battle hex-grid gestures.</summary>
            public static readonly ModString HexGrid = new ModString("Screens.HexGrid", "Hex grid");
            /// <summary>The Help tab: the three places to read about the mod, ask about it and support it.</summary>
            public static readonly ModString Help = new ModString("Screens.Help", "Help");
            public static readonly ModString ModHomepage = new ModString("Screens.ModHomepage", "Mod homepage");
            public static readonly ModString JoinDiscordServer = new ModString("Screens.JoinDiscordServer", "Join Discord server");
            public static readonly ModString SupportOnPatreon = new ModString("Screens.SupportOnPatreon", "Support my work on Patreon");
            /// <summary>Spoken, queued, when a mod-gesture capture starts: the next key becomes the
            /// binding.</summary>
            public static readonly ModString KeybindPressKey = new ModString("Screens.KeybindPressKey", "Press a key for {0}");
            /// <summary>Spoken, queued, once a mod gesture is rebound: the gesture and its new chord.</summary>
            public static readonly ModString KeybindSet = new ModString("Screens.KeybindSet", "{0} is now {1}");
            /// <summary>Warned, queued, when a mod gesture and a game hotkey share a chord: on a mod
            /// screen the mod's gesture wins and the game's does not fire.</summary>
            public static readonly ModString KeybindShadowed = new ModString("Screens.KeybindShadowed", "While the mod's {0} is active, the game's {1} will not fire.");
            public static readonly ModString Pitch = new ModString("Screens.Pitch", "Pitch");
            public static readonly ModString Play = new ModString("Screens.Play", "Play");
            public static readonly ModString PlayTileSoundCues = new ModString("Screens.PlayTileSoundCues", "Play tile sound cues");
            public static readonly ModString PlayerStats = new ModString("Screens.PlayerStats", "Player stats");
            public static readonly ModString PlayerStatsBattle = new ModString("Screens.PlayerStatsBattle", "Conquest - Battle");
            public static readonly ModString PlayerStatsOverall = new ModString("Screens.PlayerStatsOverall", "Conquest - Overall");
            public static readonly ModString PostAdventureResult = new ModString("Screens.PostAdventureResult", "Post adventure result");
            public static readonly ModString Purchase = new ModString("Screens.Purchase", "Purchase");
            public static readonly ModString Quickbar = new ModString("Screens.Quickbar", "Quickbar");
            public static readonly ModString Ready = new ModString("Screens.Ready", "Ready");
            public static readonly ModString ReadEnemyInfluence = new ModString("Screens.ReadEnemyInfluence", "Read attack, deadly and movement range for enemies on tiles in combat");
            public static readonly ModString ReadLongTooltips = new ModString("Screens.ReadLongTooltips", "Read long tooltips");
            public static readonly ModString ReadLongTooltipsTooltip = new ModString("Screens.ReadLongTooltipsTooltip", "Whether long tooltips like wielder and troop information are automatically read");
            public static readonly ModString ReadStoryCameraFocusChanges = new ModString("Screens.ReadStoryCameraFocusChanges", "Read story camera focus change events");
            public static readonly ModString ReadUsageHints = new ModString("Screens.ReadUsageHints", "Read usage hints");
            public static readonly ModString ReadUsageHintsTooltip = new ModString("Screens.ReadUsageHintsTooltip", "Whether usage hints in buffers are automatically read");
            public static readonly ModString RecruitFrom = new ModString("Screens.RecruitFrom", "Recruit from");
            public static readonly ModString RecruitingFrom = new ModString("Screens.RecruitingFrom", "Recruiting from {0}");
            public static readonly ModString Recruits = new ModString("Screens.Recruits", "Recruits");
            public static readonly ModString Remove = new ModString("Screens.Remove", "Remove");
            public static readonly ModString RemoveKeyword = new ModString("Screens.RemoveKeyword", "Remove keyword, {0}");
            public static readonly ModString ResearchTier = new ModString("Screens.ResearchTier", "{0} ({1} {2})");
            public static readonly ModString ResetAllToDefaults = new ModString("Screens.ResetAllToDefaults", "Reset all to defaults");
            /// <summary>The Keybinds tab region for the review-buffer gestures.</summary>
            public static readonly ModString ReviewBuffer = new ModString("Screens.ReviewBuffer", "Review buffer");
            public static readonly ModString ResetToDefaults = new ModString("Screens.ResetToDefaults", "Reset to defaults");
            public static readonly ModString Resources = new ModString("Screens.Resources", "Resources");
            public static readonly ModString Round = new ModString("Screens.Round", "Round {0}");
            public static readonly ModString Scanner = new ModString("Screens.Scanner", "Scanner");
            public static readonly ModString ScannerContentAnnouncements = new ModString("Screens.ScannerContentAnnouncements", "Scanner content announcements");
            public static readonly ModString ScannerUsesLongDirections = new ModString("Screens.ScannerUsesLongDirections", "Long directions");
            public static readonly ModString AdventureMapUsesLongRoadDirections = new ModString("Screens.AdventureMapUsesLongRoadDirections", "Long road directions");
            public static readonly ModString ScannerResultAnnouncements = new ModString("Screens.ScannerResultAnnouncements", "Scanner result announcements");
            public static readonly ModPluralString SelectedSubcategoryCount = new ModPluralString("Screens.SelectedSubcategoryCount", "{0} selected", "{0} selected");
            public static readonly ModString Skill = new ModString("Screens.Skill", "Skill {0}");
            public static readonly ModString SpellbookAddToQuickbarHint = new ModString("Screens.SpellbookAddToQuickbarHint", "{0} adds to quickbar");
            public static readonly ModString SpellbookDropToRemove = new ModString("Screens.SpellbookDropToRemove", "Drop here to remove");
            public static readonly ModString Sell = new ModString("Screens.Sell", "Sell");
            public static readonly ModString Suffix = new ModString("Screens.Suffix", "Include suffix punctuation");
            public static readonly ModString TeamValue = new ModString("Screens.TeamValue", "Team {0}");
            public static readonly ModString TileAnnouncements = new ModString("Screens.TileAnnouncements", "Tile announcements");
            public static readonly ModString TileAttackHint = new ModString("Screens.TileAttackHint", "{0} attacks");
            public static readonly ModString TileClaimHint = new ModString("Screens.TileClaimHint", "{0} claims");
            public static readonly ModString TileInteractHint = new ModString("Screens.TileInteractHint", "{0} interacts");
            public static readonly ModString TileMoveHint = new ModString("Screens.TileMoveHint", "{0} moves");
            public static readonly ModString TilePickupHint = new ModString("Screens.TilePickupHint", "{0} picks up");
            public static readonly ModString TilePillageHint = new ModString("Screens.TilePillageHint", "{0} pillages");
            public static readonly ModString TileRepairHint = new ModString("Screens.TileRepairHint", "{0} repairs");
            public static readonly ModString TileSelectHint = new ModString("Screens.TileSelectHint", "{0} selects");
            public static readonly ModString TileTeleportHint = new ModString("Screens.TileTeleportHint", "{0} teleports");
            public static readonly ModString TileTradeHint = new ModString("Screens.TileTradeHint", "{0} trades");
            public static readonly ModString TileVisitHint = new ModString("Screens.TileVisitHint", "{0} visits");
            public static readonly ModString Tier = new ModString("Screens.Tier", "Tier");
            public static readonly ModString TownStatus = new ModString("Screens.TownStatus", "Town status");
            public static readonly ModString TownStatusRounds = new ModString("Screens.TownStatusRounds", "{0}: {1} rounds complete, {2} rounds remaining");
            public static readonly ModString Tower = new ModString("Screens.Tower", "Tower {0}");
            public static readonly ModString Towers = new ModString("Screens.Towers", "Towers");
            /// <summary>The adventure map's list of the towns the player owns.</summary>
            public static readonly ModString Towns = new ModString("Screens.Towns", "Towns");
            public static readonly ModString TroopDistribution = new ModString("Screens.TroopDistribution", "troop distribution");
            public static readonly ModString TroopDisbandHint = new ModString("Screens.TroopDisbandHint", "{0} disbands");
            public static readonly ModString TroopMoveWholeHint = new ModString("Screens.TroopMoveWholeHint", "{0} moves the whole troop");
            public static readonly ModString TroopDeployment = new ModString("Screens.TroopDeployment", "Troop deployment");
            public static readonly ModString TroopPlacement = new ModString("Screens.TroopPlacement", "Troop placement");
            public static readonly ModString TroopAnnouncements = new ModString("Screens.TroopAnnouncements", "Troop announcements");
            public static readonly ModString TurnOrder = new ModString("Screens.TurnOrder", "Turn order");
            public static readonly ModString UnknownSpell = new ModString("Screens.UnknownSpell", "unknown spell");
            public static readonly ModString Volume = new ModString("Screens.Volume", "Volume");
            public static readonly ModString WielderAnnouncements = new ModString("Screens.WielderAnnouncements", "Wielder announcements");
            public static readonly ModString WielderDead = new ModString("Screens.WielderDead", "dead");
            public static readonly ModString WielderOwned = new ModString("Screens.WielderOwned", "owned");
            /// <summary>A section of the page that belongs to one wielder, named after them: {0} is the
            /// wielder's name in the possessive (<c>ModText.FormatPossessiveName</c>), {1} the game's own
            /// caption for the section ("Equipment", "Inventory").</summary>
            public static readonly ModString WielderSection = new ModString("Screens.WielderSection", "{0} {1}");
            public static readonly ModString Wielders = new ModString("Screens.Wielders", "Wielders");
            public static readonly ModString MapEntityAnnouncements = new ModString("Screens.MapEntityAnnouncements", "Map entity announcements");

            public static readonly ModString AnnouncementActing = new ModString("Screens.AnnouncementActing", "Acting");
            public static readonly ModString AnnouncementAffiliation = new ModString("Screens.AnnouncementAffiliation", "Affiliation");
            public static readonly ModString AnnouncementAttackable = new ModString("Screens.AnnouncementAttackable", "Attackable");
            public static readonly ModString AnnouncementChokePoint = new ModString("Screens.AnnouncementChokePoint", "Choke point");
            public static readonly ModString AnnouncementContent = new ModString("Screens.AnnouncementContent", "Content");
            public static readonly ModString AnnouncementCoordinates = new ModString("Screens.AnnouncementCoordinates", "Coordinates");
            public static readonly ModString AnnouncementDecorativeFeatures = new ModString("Screens.AnnouncementDecorativeFeatures", "Decorative features");
            public static readonly ModString AnnouncementDestination = new ModString("Screens.AnnouncementDestination", "Destination");
            public static readonly ModString AnnouncementDirection = new ModString("Screens.AnnouncementDirection", "Direction");
            public static readonly ModString AnnouncementElevation = new ModString("Screens.AnnouncementElevation", "Elevation");
            public static readonly ModString AnnouncementEffects = new ModString("Screens.AnnouncementEffects", "Effects");
            public static readonly ModString AnnouncementEntityName = new ModString("Screens.AnnouncementEntityName", "Entity name");
            public static readonly ModString AnnouncementExplorationState = new ModString("Screens.AnnouncementExplorationState", "Exploration state");
            public static readonly ModString AnnouncementFacingDirectionForBeamAttacks = new ModString("Screens.AnnouncementFacingDirectionForBeamAttacks", "Facing direction for troops with beam attacks");
            public static readonly ModString AnnouncementHealth = new ModString("Screens.AnnouncementHealth", "Health");
            public static readonly ModString AnnouncementImpassable = new ModString("Screens.AnnouncementImpassable", "Impassable");
            public static readonly ModString AnnouncementItem = new ModString("Screens.AnnouncementItem", "Item name");
            public static readonly ModString AnnouncementInfluence = new ModString("Screens.AnnouncementInfluence", "Influence");
            public static readonly ModString AnnouncementInteractionPoint = new ModString("Screens.AnnouncementInteractionPoint", "Interaction point");
            public static readonly ModString AnnouncementMapEntity = new ModString("Screens.AnnouncementMapEntity", "Map entity");
            public static readonly ModString AnnouncementMovement = new ModString("Screens.AnnouncementMovement", "Movement");
            public static readonly ModString AnnouncementMovementCost = new ModString("Screens.AnnouncementMovementCost", "Movement cost");
            public static readonly ModString AnnouncementName = new ModString("Screens.AnnouncementName", "Name");
            public static readonly ModString AnnouncementOccupant = new ModString("Screens.AnnouncementOccupant", "Occupant");
            public static readonly ModString AnnouncementOwner = new ModString("Screens.AnnouncementOwner", "Owner");
            public static readonly ModString AnnouncementOrderRow = new ModString("Screens.AnnouncementOrderRow", "{0} horizontal row");
            public static readonly ModString AnnouncementReachable = new ModString("Screens.AnnouncementReachable", "Reachable");
            public static readonly ModString AnnouncementReachabilityOrRoutePreview = new ModString("Screens.AnnouncementReachabilityOrRoutePreview", "Reachability or route preview");
            public static readonly ModString AnnouncementResultPosition = new ModString("Screens.AnnouncementResultPosition", "Result position");
            public static readonly ModString AnnouncementRestrictions = new ModString("Screens.AnnouncementRestrictions", "Restrictions");
            public static readonly ModString AnnouncementRoadDirections = new ModString("Screens.AnnouncementRoadDirections", "Road directions");
            public static readonly ModString AnnouncementSelected = new ModString("Screens.AnnouncementSelected", "Selected");
            public static readonly ModString AnnouncementSelectedForSpellcast = new ModString("Screens.AnnouncementSelectedForSpellcast", "Selected for spellcast");
            public static readonly ModString AnnouncementSpawnPoint = new ModString("Screens.AnnouncementSpawnPoint", "Spawn point");
            public static readonly ModString AnnouncementStatus = new ModString("Screens.AnnouncementStatus", "Status");
            public static readonly ModString AnnouncementStackSize = new ModString("Screens.AnnouncementStackSize", "Stack size");
            public static readonly ModString AnnouncementTerrain = new ModString("Screens.AnnouncementTerrain", "Terrain");
            public static readonly ModString AnnouncementThisTurnDestination = new ModString("Screens.AnnouncementThisTurnDestination", "This turn destination");
            public static readonly ModString AnnouncementTileEffects = new ModString("Screens.AnnouncementTileEffects", "Tile effects");
            public static readonly ModString AnnouncementTroop = new ModString("Screens.AnnouncementTroop", "Troop");
            public static readonly ModString AnnouncementTroopName = new ModString("Screens.AnnouncementTroopName", "Troop name");
            public static readonly ModString AnnouncementVisited = new ModString("Screens.AnnouncementVisited", "Visited");
            public static readonly ModString AnnouncementWielder = new ModString("Screens.AnnouncementWielder", "Wielder");
            public static readonly ModString AnnouncementZoneOfControl = new ModString("Screens.AnnouncementZoneOfControl", "Zone of control");
            public static readonly ModString BuildingNumber = new ModString("Screens.BuildingNumber", "Building {0}");
            public static readonly ModString TierNumber = new ModString("Screens.TierNumber", "Tier {0}");
            public static readonly ModString ResearchNumber = new ModString("Screens.ResearchNumber", "Research {0}");
            public static readonly ModString ResearchCategoryNumber = new ModString("Screens.ResearchCategoryNumber", "Research category {0}");

            /// <summary>The selected wielder's experience bar as one line: {0} is the game's caption
            /// for experience, {1} its caption for level, {2} the level reached, {3} the experience
            /// earned and {4} the experience the next level asks for.</summary>
            public static readonly ModString WielderExperience = new ModString("Screens.WielderExperience", "{0}, {1} {2}, {3} / {4}");

            /// <summary>How many of a map entity's upgrade tiers are built: {0} is the game's own
            /// caption for the row, {1} the tiers built and {2} the tiers it has.</summary>
            public static readonly ModString UpgradeTiers = new ModString("Screens.UpgradeTiers", "{0} {1} / {2}");

            /// <summary>The chat window's send button, when neither the button nor the game's key
            /// carries a word for it.</summary>
            public static readonly ModString ChatSend = new ModString("Screens.ChatSend", "Send");

            public static readonly ModString CodexTabNumber = new ModString("Screens.CodexTabNumber", "Tab {0}");
            public static readonly ModString CodexCategoryNumber = new ModString("Screens.CodexCategoryNumber", "Category {0}");
            public static readonly ModString CampaignNumber = new ModString("Screens.CampaignNumber", "Campaign {0}");
            public static readonly ModString CustomCampaigns = new ModString("Screens.CustomCampaigns", "Custom campaigns");
            public static readonly ModString Tales = new ModString("Screens.Tales", "Tales");
            public static readonly ModString Confirm = new ModString("Screens.Confirm", "Confirm");
            public static readonly ModString ShowTutorials = new ModString("Screens.ShowTutorials", "Show tutorials");
        }

        public static class Scanner
        {
            public static readonly ModString All = new ModString("Scanner.All", "All");
            public static readonly ModString ArtifactMarkets = new ModString("Scanner.ArtifactMarkets", "Artifact markets");
            public static readonly ModString Attackable = new ModString("Scanner.Attackable", "Attackable");
            public static readonly ModString Barriers = new ModString("Scanner.Barriers", "Barriers");
            public static readonly ModString Beacons = new ModString("Scanner.Beacons", "Beacons");
            public static readonly ModString Buildings = new ModString("Scanner.Buildings", "Buildings");
            public static readonly ModString CustomCategoryEmpty = new ModString("Scanner.CustomCategoryEmpty", "Custom category {0} is empty");
            public static readonly ModString Dangerous = new ModString("Scanner.Dangerous", "Dangerous");
            public static readonly ModString DirectionStep = new ModString("Scanner.DirectionStep", "{0}{1}");
            public static readonly ModString DirectionStepLong = new ModString("Scanner.DirectionStepLong", "{0} {1}");
            public static readonly ModString East = new ModString("Scanner.East", "east");
            public static readonly ModString EastShort = new ModString("Scanner.EastShort", "e");
            public static readonly ModString Enemy = new ModString("Scanner.Enemy", "Enemy");
            public static readonly ModString EnemyGates = new ModString("Scanner.EnemyGates", "Enemy gates");
            public static readonly ModString Entities = new ModString("Scanner.Entities", "Entities");
            public static readonly ModString Friendly = new ModString("Scanner.Friendly", "Friendly");
            public static readonly ModString FriendlyGates = new ModString("Scanner.FriendlyGates", "Friendly gates");
            public static readonly ModString Exploration = new ModString("Scanner.Exploration", "Exploration");
            public static readonly ModString Knowledge = new ModString("Scanner.Knowledge", "Knowledge");
            public static readonly ModString LookAround = new ModString("Scanner.LookAround", "Look around");
            public static readonly ModString LookAroundRadius = new ModString("Scanner.LookAroundRadius", "Look around radius {0}");
            public static readonly ModString Merchants = new ModString("Scanner.Merchants", "Merchants");
            public static readonly ModString Neutral = new ModString("Scanner.Neutral", "Neutral");
            public static readonly ModString NoTileToReturnTo = new ModString("Scanner.NoTileToReturnTo", "No tile to return to");
            public static readonly ModString North = new ModString("Scanner.North", "north");
            public static readonly ModString NorthShort = new ModString("Scanner.NorthShort", "n");
            public static readonly ModString Northeast = new ModString("Scanner.Northeast", "northeast");
            public static readonly ModString NortheastShort = new ModString("Scanner.NortheastShort", "ne");
            public static readonly ModString Northwest = new ModString("Scanner.Northwest", "northwest");
            public static readonly ModString NorthwestShort = new ModString("Scanner.NorthwestShort", "nw");
            public static readonly ModString Objectives = new ModString("Scanner.Objectives", "Objectives");
            public static readonly ModString OpenGround = new ModString("Scanner.OpenGround", "Open ground");
            public static readonly ModString Obstacles = new ModString("Scanner.Obstacles", "Obstacles");
            public static readonly ModString Pickups = new ModString("Scanner.Pickups", "Pickups");
            public static readonly ModString Power = new ModString("Scanner.Power", "Power");
            public static readonly ModString Revealed = new ModString("Scanner.Revealed", "Revealed");
            public static readonly ModString ReachableSummary = new ModString("Scanner.ReachableSummary", "Reachable: {0}");
            public static readonly ModString ReachableSummaryCount = new ModString("Scanner.ReachableSummaryCount", "{0} {1}");
            public static readonly ModString ReachableSummaryNone = new ModString("Scanner.ReachableSummaryNone", "Reachable: none");
            public static readonly ModString ResourceGenerators = new ModString("Scanner.ResourceGenerators", "Resource generators");
            public static readonly ModString Riches = new ModString("Scanner.Riches", "Riches");
            public static readonly ModString RoadsAndCrossings = new ModString("Scanner.RoadsAndCrossings", "Roads and crossings");
            public static readonly ModString Search = new ModString("Scanner.Search", "Search");
            public static readonly ModString SearchNoResults = new ModString("Scanner.SearchNoResults", "No results");
            public static readonly ModString SearchResults = new ModString("Scanner.SearchResults", "Search Results");
            public static readonly ModString SettlementsAndBuildSites = new ModString("Scanner.SettlementsAndBuildSites", "Settlements and Build sites");
            public static readonly ModString South = new ModString("Scanner.South", "south");
            public static readonly ModString SouthShort = new ModString("Scanner.SouthShort", "s");
            public static readonly ModString Southeast = new ModString("Scanner.Southeast", "southeast");
            public static readonly ModString SoutheastShort = new ModString("Scanner.SoutheastShort", "se");
            public static readonly ModString Southwest = new ModString("Scanner.Southwest", "southwest");
            public static readonly ModString SouthwestShort = new ModString("Scanner.SouthwestShort", "sw");
            public static readonly ModString SpawnPointEmpty = new ModString("Scanner.SpawnPointEmpty", "empty");
            public static readonly ModString SpawnPoints = new ModString("Scanner.SpawnPoints", "Spawn points");
            public static readonly ModString SpecialSites = new ModString("Scanner.SpecialSites", "Special sites");
            public static readonly ModString Teleport = new ModString("Scanner.Teleport", "Teleport");
            public static readonly ModString Terrain = new ModString("Scanner.Terrain", "Terrain");
            // ONE GROUP OF BATTLEFIELD GROUND, as the Terrain category names it. A group is one
            // item and its cells are that item's instances, so the count is the group's and the
            // height is its highest cell. A lone impassable cell borrows Spatial.Impassable, and a
            // tower or a flight of stairs Spatial.TowerHeight and Spatial.StairsHeight: one tower
            // is one tower however many cells it covers.
            public static readonly ModString TerrainChokePoint = new ModString("Scanner.TerrainChokePoint", "choke point");
            public static readonly ModPluralString TerrainChokePointCells = new ModPluralString("Scanner.TerrainChokePointCells", "choke point, {0} cell", "choke point, {0} cells");
            public static readonly ModString TerrainCliff = new ModString("Scanner.TerrainCliff", "cliff");
            public static readonly ModPluralString TerrainCliffCells = new ModPluralString("Scanner.TerrainCliffCells", "cliff of {0} cell", "cliff of {0} cells");
            public static readonly ModPluralString TerrainImpassableCells = new ModPluralString("Scanner.TerrainImpassableCells", "impassable, {0} cell", "impassable, {0} cells");
            public static readonly ModPluralString TerrainImpassableWall = new ModPluralString("Scanner.TerrainImpassableWall", "wall of {0} impassable cell", "wall of {0} impassable cells");
            // The same two groups where the cells have a name: the count and the words for what is
            // standing there, most of it first.
            public static readonly ModPluralString TerrainObstacleCells = new ModPluralString("Scanner.TerrainObstacleCells", "{1}, {0} cell, impassable", "{1}, {0} cells, impassable");
            public static readonly ModPluralString TerrainObstacleWall = new ModPluralString("Scanner.TerrainObstacleWall", "wall of {0} {1}, impassable", "wall of {0} {1}, impassable");
            public static readonly ModPluralString TerrainPatch = new ModPluralString("Scanner.TerrainPatch", "patch of {0} cell, height {1}", "patch of {0} cells, height {1}");
            public static readonly ModPluralString TerrainRidge = new ModPluralString("Scanner.TerrainRidge", "ridge of {0} cell, height {1}", "ridge of {0} cells, height {1}");
            public static readonly ModPluralString TerrainRidgeDiagonal = new ModPluralString("Scanner.TerrainRidgeDiagonal", "diagonal ridge of {0} cell, height {1}", "diagonal ridge of {0} cells, height {1}");
            public static readonly ModPluralString TerrainRidgeVertical = new ModPluralString("Scanner.TerrainRidgeVertical", "vertical ridge of {0} cell, height {1}", "vertical ridge of {0} cells, height {1}");
            public static readonly ModPluralString TerrainSiegeWall = new ModPluralString("Scanner.TerrainSiegeWall", "wall of {0} cell, height {1}", "wall of {0} cells, height {1}");
            public static readonly ModString TerrainSingleCell = new ModString("Scanner.TerrainSingleCell", "single cell, height {0}");
            public static readonly ModString TerrainUnreachable = new ModString("Scanner.TerrainUnreachable", "unreachable ground");
            public static readonly ModPluralString TerrainUnreachableCells = new ModPluralString("Scanner.TerrainUnreachableCells", "unreachable ground, {0} cell", "unreachable ground, {0} cells");
            public static readonly ModString TroopSources = new ModString("Scanner.TroopSources", "Troop sources");
            public static readonly ModString Troops = new ModString("Scanner.Troops", "Troops");
            public static readonly ModString Unexplored = new ModString("Scanner.Unexplored", "Unexplored");
            public static readonly ModString Unvisited = new ModString("Scanner.Unvisited", "Unvisited");
            public static readonly ModString West = new ModString("Scanner.West", "west");
            public static readonly ModString WestShort = new ModString("Scanner.WestShort", "w");
            public static readonly ModString Wielders = new ModString("Scanner.Wielders", "Wielders");
            public static readonly ModString ZoneOfControl = new ModString("Scanner.ZoneOfControl", "Zone of control");
        }
    }
}

using System;
using System.Globalization;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class AdventureMapTileSpeechFormatter
    {
        private readonly Func<AnnouncementGroupDefinition, IReadOnlyList<string>> _getOrder;
        private readonly Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> _isEnabled;
        private readonly Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> _includeSuffix;
        private readonly Func<bool> _readsRoadDirections;
        private readonly Func<bool> _usesLongRoadDirections;

        public AdventureMapTileSpeechFormatter()
            : this(
                ModSettings.GetAnnouncementOrder,
                ModSettings.GetAnnouncementElementEnabled,
                ModSettings.GetAnnouncementElementSuffix,
                () => ModSettings.AdventureMapReadsRoadDirections,
                () => ModSettings.AdventureMapUsesLongRoadDirections)
        {
        }

        internal AdventureMapTileSpeechFormatter(
            Func<AnnouncementGroupDefinition, IReadOnlyList<string>> getOrder,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> isEnabled,
            Func<AnnouncementGroupDefinition, AnnouncementElementDefinition, bool> includeSuffix,
            Func<bool> readsRoadDirections = null,
            Func<bool> usesLongRoadDirections = null)
        {
            _getOrder = getOrder;
            _isEnabled = isEnabled;
            _includeSuffix = includeSuffix;
            _readsRoadDirections = readsRoadDirections ?? (() => true);
            _usesLongRoadDirections = usesLongRoadDirections ?? (() => false);
        }

        public string DescribeTile(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return ModText.Get(ModStrings.Screens.AdventureMap);
            }

            string text = Compose(
                AdventureMapAnnouncementDefinitions.Tile,
                BuildTileParts(tile));
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text + ".";
        }

        private string DescribeWielder(AdventureMapTile tile)
        {
            if (tile == null || !tile.IsExplored || !tile.IsVisible || tile.Commander == null)
            {
                return string.Empty;
            }

            AdventureMapTile.CommanderInfo commander = tile.Commander;
            return Compose(
                AdventureMapAnnouncementDefinitions.Wielder,
                BuildWielderParts(commander));
        }

        private string DescribeMapEntity(AdventureMapTile tile)
        {
            if (tile == null || !tile.IsExplored || tile.MapEntity == null)
            {
                return string.Empty;
            }

            return Compose(
                AdventureMapAnnouncementDefinitions.MapEntity,
                BuildMapEntityParts(tile));
        }

        private string Compose(AnnouncementGroupDefinition group, IEnumerable<AnnouncementPart> parts)
        {
            return ConfigurableAnnouncementComposer.Compose(group, parts, _getOrder, _isEnabled, _includeSuffix);
        }

        private string DescribeInteractionPoint(AdventureMapTile tile)
        {
            return tile != null && tile.IsExplored && tile.IsVisible && tile.IsInteractionPoint
                ? ModText.Get(ModStrings.Spatial.InteractionPoint)
                : string.Empty;
        }

        private static string FormatPossessive(string name)
        {
            return ModText.FormatPossessiveName(name, ModStrings.Spatial.CommanderPossessive);
        }

        public string DescribeCoordinates(AdventureMapTile tile)
        {
            return tile == null ? string.Empty : tile.Position.x + ", " + tile.Position.y;
        }

        private IEnumerable<AnnouncementPart> BuildTileParts(AdventureMapTile tile)
        {
            if (tile == null)
            {
                yield break;
            }

            string explorationState = DescribeExplorationState(tile);
            if (!string.IsNullOrWhiteSpace(explorationState))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.ExplorationState, explorationState);
            }

            string wielder = DescribeWielder(tile);
            if (!string.IsNullOrWhiteSpace(wielder))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.Wielder, wielder);
            }

            string mapEntity = string.IsNullOrWhiteSpace(wielder) ? DescribeMapEntity(tile) : string.Empty;
            if (!string.IsNullOrWhiteSpace(mapEntity))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.MapEntity, mapEntity);
            }

            string interactionPoint = string.IsNullOrWhiteSpace(wielder) && string.IsNullOrWhiteSpace(mapEntity)
                ? DescribeInteractionPoint(tile)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(interactionPoint))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.InteractionPoint, interactionPoint);
            }

            string route = DescribeReachabilityOrRoutePreview(tile);
            bool hasContent = !string.IsNullOrWhiteSpace(wielder)
                || !string.IsNullOrWhiteSpace(mapEntity)
                || !string.IsNullOrWhiteSpace(interactionPoint);
            string terrain = AppendRoadDirections(DescribeTerrain(tile.Terrain), tile);
            bool appendRouteToTerrain = tile.IsExplored
                && !hasContent
                && !string.IsNullOrWhiteSpace(terrain)
                && !string.IsNullOrWhiteSpace(route);

            if (!appendRouteToTerrain && !string.IsNullOrWhiteSpace(route))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.ReachabilityOrRoutePreview, route);
            }

            string zoneOfControl = DescribeZoneOfControl(tile);
            if (!string.IsNullOrWhiteSpace(zoneOfControl))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.ZoneOfControl, zoneOfControl);
            }

            if (appendRouteToTerrain)
            {
                terrain += ", " + route;
            }

            if (!string.IsNullOrWhiteSpace(terrain))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.Terrain, terrain);
            }

            yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.Coordinates, DescribeCoordinates(tile));

            string movementCost = DescribeMovementCost(tile);
            if (!string.IsNullOrWhiteSpace(movementCost))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.TileKeys.MovementCost, movementCost);
            }
        }

        private IEnumerable<AnnouncementPart> BuildWielderParts(AdventureMapTile.CommanderInfo commander)
        {
            yield return new AnnouncementPart(
                AdventureMapAnnouncementDefinitions.WielderKeys.Name,
                FirstNonEmpty(commander.Name, ModText.Get(ModStrings.Events.Wielder)));

            if (!string.IsNullOrWhiteSpace(commander.Relationship))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.WielderKeys.Affiliation, commander.Relationship);
            }

            if (commander.IsSelected)
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.WielderKeys.Selected,
                    ModText.Get(ModStrings.UI.Selected));
            }

            if (commander.IsOwnedByLocalTeam)
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.WielderKeys.Movement,
                    FormatWielderMovement(commander));
            }

            if (commander.IsOwnedByLocalTeam && commander.HasDestination)
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.WielderKeys.Destination,
                    ModText.Get(ModStrings.Spatial.DestinationAt, FormatPoint(commander.Destination)));
            }

            if (commander.IsOwnedByLocalTeam
                && commander.HasThisTurnDestination
                && commander.ThisTurnDestination != commander.Destination)
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.WielderKeys.ThisTurnDestination,
                    ModText.Get(ModStrings.Spatial.ThisTurnAt, FormatPoint(commander.ThisTurnDestination)));
            }
        }

        private IEnumerable<AnnouncementPart> BuildMapEntityParts(AdventureMapTile tile)
        {
            string name = FirstNonEmpty(tile.MapEntityName, string.Empty);
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return new AnnouncementPart(AdventureMapAnnouncementDefinitions.MapEntityKeys.Name, name);
            }

            if (tile.MapEntityVisited)
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.MapEntityKeys.Visited,
                    ModText.Get(ModStrings.Spatial.Visited));
            }

            if (!string.IsNullOrWhiteSpace(tile.MapEntityRelationship))
            {
                yield return new AnnouncementPart(
                    AdventureMapAnnouncementDefinitions.MapEntityKeys.Affiliation,
                    tile.MapEntityRelationship);
            }
        }

        private static string DescribeExplorationState(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            if (!tile.IsExplored)
            {
                return ModText.Get(ModStrings.Spatial.Unexplored);
            }

            return !tile.IsVisible ? ModText.Get(ModStrings.Spatial.Unseen) : string.Empty;
        }

        private static string DescribeZoneOfControl(AdventureMapTile tile)
        {
            if (tile == null || tile.ZoneOfControlNames.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < tile.ZoneOfControlNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tile.ZoneOfControlNames[i]))
                {
                    parts.Add(ModText.Get(ModStrings.Spatial.WithinZoneOfControl, FormatPossessive(tile.ZoneOfControlNames[i])));
                }
            }

            return string.Join(". ", parts.ToArray());
        }

        public static string DescribeReachabilityOrRoutePreview(AdventureMapTile tile)
        {
            List<string> details = GetMovementDetails(tile);
            return details.Count > 0 ? string.Join(", ", details.ToArray()) : string.Empty;
        }

        private static List<string> GetMovementDetails(AdventureMapTile tile)
        {
            List<string> details = new List<string>();
            if (tile == null)
            {
                return details;
            }

            string pathIndicator = DescribePathIndicator(tile != null ? tile.PathIndicator : null);
            if (!string.IsNullOrWhiteSpace(pathIndicator))
            {
                details.Add(pathIndicator);
                return details;
            }

            if (tile.IsReachable)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Reachable));
            }
            else if (tile.IsImpassable)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Impassable));
            }
            else if (tile.IsBlocked)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Blocked));
            }

            return details;
        }

        private static string DescribePathIndicator(AdventureMapTile.PathIndicatorInfo indicator)
        {
            if (indicator == null)
            {
                return string.Empty;
            }

            List<string> details = new List<string>();
            if (indicator.Kind == AdventureMapTile.PathIndicatorKind.Destination)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Destination));
                if (!indicator.HasRoutePreview)
                {
                    details.Add(ModText.Get(ModStrings.Spatial.NoRoutePreview));
                    return string.Join(", ", details.ToArray());
                }

                string arrival = DescribeArrivalTurns(indicator.TravelTurns);
                if (!string.IsNullOrWhiteSpace(arrival))
                {
                    details.Add(arrival);
                }

                if (indicator.IsInteractable)
                {
                    details.Add(indicator.TravelTurns <= 1 && !indicator.CanInteractThisTurn
                        ? ModText.Get(ModStrings.Spatial.InteractableNextTurn)
                        : ModText.Get(ModStrings.Spatial.Interactable));
                }
            }
            else
            {
                details.Add(ModText.Get(ModStrings.Spatial.OnRoute));
                if (indicator.FurthestReachableTurns.HasValue)
                {
                    details.Add(DescribeFurthestReachableTurns(indicator.FurthestReachableTurns.Value));
                }
                else
                {
                    string routeArrival = DescribeArrivalTurns(indicator.TravelTurns);
                    if (!string.IsNullOrWhiteSpace(routeArrival))
                    {
                        details.Add(routeArrival);
                    }
                }
            }

            if (indicator.CostMark.HasValue)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Cost, indicator.CostMark.Value));
            }

            return string.Join(", ", details.ToArray());
        }

        // Travel turns are ordinals counting the current turn as 1, matching the
        // numbers the game paints on its path markers. Speech says how long the
        // wait is instead, so ordinal 2 is next turn and ordinal 3 is two turns.
        private static string DescribeArrivalTurns(int travelTurns)
        {
            if (travelTurns <= 1)
            {
                return string.Empty;
            }

            return travelTurns == 2
                ? ModText.Get(ModStrings.Spatial.NextTurn)
                : ModText.Get(ModStrings.Spatial.TurnsIn, travelTurns - 1);
        }

        private static string DescribeFurthestReachableTurns(int turns)
        {
            if (turns <= 1)
            {
                return ModText.Get(ModStrings.Spatial.FurthestReachableThisTurn);
            }

            return turns == 2
                ? ModText.Get(ModStrings.Spatial.FurthestReachableNextTurn)
                : ModText.Get(ModStrings.Spatial.FurthestReachableInTurns, turns - 1);
        }

        /// <summary>
        /// A movement number as it is spoken, to at most two decimals. Shared with the
        /// route readout so that one cost is never spoken two ways depending on which
        /// part of the mod happens to be saying it.
        /// </summary>
        public static string FormatMovementNumber(float value)
        {
            return Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Movement a wielder has, with less than half a point spoken as none: that much
        /// is left over rather than held, since it buys no step.
        /// </summary>
        private static string FormatMovementValue(float value)
        {
            return FormatMovementNumber(value < 0.5f ? 0f : value);
        }

        public static string DescribeMovementCost(AdventureMapTile tile)
        {
            return tile != null && tile.IsExplored && tile.ReachableMovementCost.HasValue
                ? ModText.Get(ModStrings.Spatial.MovementCost, FormatMovementNumber(tile.ReachableMovementCost.Value))
                : string.Empty;
        }

        private static string FormatWielderMovement(AdventureMapTile.CommanderInfo commander)
        {
            return ModText.Get(
                ModStrings.UI.LabelValue,
                FirstNonEmpty(commander.MovementLabel, ModText.Get(ModStrings.Spatial.Movement)),
                FormatMovementValue(commander.MovesLeft) + " / " + FormatMovementValue(commander.MaxMovement));
        }

        private static string FormatPoint(UnityEngine.Vector2Int point)
        {
            return point.x + ", " + point.y;
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        }

        /// <summary>
        /// Adds the neighbouring tiles a road carries on into, as bare direction words after the
        /// terrain name, so a road reads as "Dirt road e w". Every direction named is a neighbour
        /// that is road as well, which is what lets a curve be followed a step at a time. Naming
        /// a shape on top of that would only make the tile slower to hear, so the directions are
        /// left to speak for themselves.
        /// </summary>
        internal string AppendRoadDirections(string terrain, AdventureMapTile tile)
        {
            // Checked before reading the tile, which only works out its road directions when
            // something asks, so turning this off costs nothing rather than costing it anyway.
            if (tile == null || string.IsNullOrWhiteSpace(terrain) || !_readsRoadDirections())
            {
                return terrain;
            }

            IReadOnlyList<ScannerDirection> directions = tile.RoadDirections;
            if (directions == null || directions.Count == 0)
            {
                return terrain;
            }

            bool useLongForm = _usesLongRoadDirections();
            string text = terrain;
            for (int i = 0; i < directions.Count; i++)
            {
                text = ModText.Get(
                    ModStrings.Spatial.RoadDirectionJoin,
                    text,
                    ScannerDirectionUtility.FormatDirection(directions[i], useLongForm));
            }

            return text;
        }

        private static string DescribeTerrain(AdventureTerrainKind terrain)
        {
            switch (terrain)
            {
                case AdventureTerrainKind.Road:
                    return ModText.Get(ModStrings.Spatial.Road);
                case AdventureTerrainKind.DirtRoad:
                    return ModText.Get(ModStrings.Spatial.DirtRoad);
                case AdventureTerrainKind.CobblestoneRoad:
                    return ModText.Get(ModStrings.Spatial.CobblestoneRoad);
                case AdventureTerrainKind.Wall:
                    return ModText.Get(ModStrings.Spatial.Wall);
                case AdventureTerrainKind.Obstruction:
                    return ModText.Get(ModStrings.Spatial.Obstruction);
                case AdventureTerrainKind.Grass:
                    return ModText.Get(ModStrings.Spatial.Grass);
                case AdventureTerrainKind.Sand:
                    return ModText.Get(ModStrings.Spatial.Sand);
                case AdventureTerrainKind.Dirt:
                    return ModText.Get(ModStrings.Spatial.Dirt);
                case AdventureTerrainKind.Bridge:
                    return ModText.Get(ModStrings.Spatial.Bridge);
                case AdventureTerrainKind.Water:
                    return ModText.Get(ModStrings.Spatial.Water);
                case AdventureTerrainKind.ShallowWater:
                    return ModText.Get(ModStrings.Spatial.ShallowWater);
                case AdventureTerrainKind.DeepWater:
                    return ModText.Get(ModStrings.Spatial.DeepWater);
                case AdventureTerrainKind.WaterEdge:
                    return ModText.Get(ModStrings.Spatial.WaterEdge);
                case AdventureTerrainKind.AridTrees:
                    return ModText.Get(ModStrings.Spatial.AridTrees);
                case AdventureTerrainKind.TemperateTrees:
                    return ModText.Get(ModStrings.Spatial.TemperateTrees);
                case AdventureTerrainKind.Mountain:
                    return ModText.Get(ModStrings.Spatial.Mountain);
                case AdventureTerrainKind.Deforestation:
                    return ModText.Get(ModStrings.Spatial.Deforestation);
                case AdventureTerrainKind.Farmland:
                    return ModText.Get(ModStrings.Spatial.Farmland);
                default:
                    return string.Empty;
            }
        }
    }
}

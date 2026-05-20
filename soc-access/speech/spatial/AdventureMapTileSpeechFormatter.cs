using System;
using System.Globalization;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class AdventureMapTileSpeechFormatter : ISpatialTileSpeechFormatter<AdventureMapTile>
    {
        public string DescribeTile(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return ModText.Get(ModStrings.Screens.AdventureMap);
            }

            List<string> parts = new List<string>();
            if (!tile.IsExplored)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Unexplored));
                for (int i = 0; i < tile.ZoneOfControlNames.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tile.ZoneOfControlNames[i]))
                    {
                        parts.Add(ModText.Get(ModStrings.Spatial.WithinZoneOfControl, FormatPossessive(tile.ZoneOfControlNames[i])));
                    }
                }

                parts.Add(DescribeCoordinates(tile));
                return string.Join(". ", parts.ToArray()) + ".";
            }

            if (!tile.IsVisible)
            {
                parts.Add(ModText.Get(ModStrings.Spatial.Unseen));
            }

            string primary = DescribePrimaryContent(tile);
            bool hasContent = !string.IsNullOrWhiteSpace(primary);
            bool addedMovementStatus = hasContent;
            if (hasContent)
            {
                parts.Add(primary);
            }

            for (int i = 0; i < tile.ZoneOfControlNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tile.ZoneOfControlNames[i]))
                {
                    parts.Add(ModText.Get(ModStrings.Spatial.WithinZoneOfControl, FormatPossessive(tile.ZoneOfControlNames[i])));
                }
            }

            for (int i = 0; i < tile.Environment.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(tile.Environment[i]))
                {
                    continue;
                }

                if (!addedMovementStatus)
                {
                    parts.Add(AppendDetails(tile.Environment[i], GetMovementDetails(tile)));
                    addedMovementStatus = true;
                }
                else
                {
                    parts.Add(tile.Environment[i]);
                }
            }

            if (!addedMovementStatus)
            {
                List<string> movementDetails = GetMovementDetails(tile);
                if (movementDetails.Count > 0)
                {
                    parts.Add(string.Join(", ", movementDetails.ToArray()));
                }
            }

            if (tile.Terrain.HasValue)
            {
                parts.Add(FormatEnumName(tile.Terrain.Value.ToString()));
            }

            parts.Add(DescribeCoordinates(tile));
            return string.Join(". ", parts.ToArray()) + ".";
        }

        public string DescribePrimaryContent(AdventureMapTile tile)
        {
            if (tile == null || !tile.IsExplored)
            {
                return string.Empty;
            }

            if (tile.Commander != null && tile.IsVisible)
            {
                List<string> details = new List<string>();
                AdventureMapTile.CommanderInfo commander = tile.Commander;
                if (!string.IsNullOrWhiteSpace(commander.Relationship))
                {
                    details.Add(commander.Relationship);
                }

                if (commander.IsSelected)
                {
                    details.Add(ModText.Get(ModStrings.UI.Selected));
                }

                AddCommanderMovementDetails(commander, details);
                details.AddRange(GetMovementDetails(tile));
                return AppendDetails(FirstNonEmpty(commander.Name, ModText.Get(ModStrings.Spatial.Commander)), details);
            }

            if (tile.MapEntity != null)
            {
                string mapEntityName = FirstNonEmpty(tile.MapEntityName, string.Empty);
                if (tile.MapEntityVisited)
                {
                    mapEntityName = ModText.Get(ModStrings.Spatial.VisitedSuffix, mapEntityName);
                }

                List<string> details = new List<string>();
                if (!string.IsNullOrWhiteSpace(tile.MapEntityRelationship))
                {
                    details.Add(tile.MapEntityRelationship);
                }

                for (int i = 0; i < tile.MapEntityDetails.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(tile.MapEntityDetails[i]))
                    {
                        details.Add(SpeechTextSanitizer.Normalize(tile.MapEntityDetails[i]));
                    }
                }

                details.AddRange(GetMovementDetails(tile));
                return AppendDetails(mapEntityName, details);
            }

            if (tile.IsInteractionPoint && tile.IsVisible)
            {
                return AppendDetails(ModText.Get(ModStrings.Spatial.InteractionPoint), GetMovementDetails(tile));
            }

            return string.Empty;
        }

        public string DescribeTileContext(AdventureMapTile tile)
        {
            if (tile == null || !tile.IsExplored)
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

            for (int i = 0; i < tile.Environment.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tile.Environment[i]))
                {
                    parts.Add(tile.Environment[i]);
                }
            }

            if (tile.Terrain.HasValue)
            {
                parts.Add(FormatEnumName(tile.Terrain.Value.ToString()));
            }

            return string.Join(". ", parts.ToArray());
        }

        private static string FormatPossessive(string name)
        {
            return ModText.FormatPossessiveName(name, ModStrings.Spatial.CommanderPossessive);
        }

        public string DescribeCoordinates(AdventureMapTile tile)
        {
            return tile == null ? string.Empty : tile.Position.x + ", " + tile.Position.y;
        }

        private static List<string> GetMovementDetails(AdventureMapTile tile)
        {
            List<string> details = new List<string>();
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

                if (indicator.TravelTurns > 1)
                {
                    details.Add(ModText.Get(ModStrings.Spatial.TurnsIn, indicator.TravelTurns));
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
                    int turns = indicator.FurthestReachableTurns.Value;
                    details.Add(turns <= 1
                        ? ModText.Get(ModStrings.Spatial.FurthestReachableThisTurn)
                        : ModText.Get(ModStrings.Spatial.FurthestReachableInTurns, turns));
                }
                else if (indicator.TravelTurns > 1)
                {
                    details.Add(ModText.Get(ModStrings.Spatial.TurnsIn, indicator.TravelTurns));
                }
            }

            if (indicator.CostMark.HasValue)
            {
                details.Add(ModText.Get(ModStrings.Spatial.Cost, indicator.CostMark.Value));
            }

            return string.Join(", ", details.ToArray());
        }

        private static void AddCommanderMovementDetails(AdventureMapTile.CommanderInfo commander, List<string> details)
        {
            if (commander == null || details == null || !commander.IsOwnedByLocalTeam)
            {
                return;
            }

            details.Add(ModText.Get(
                ModStrings.UI.LabelValue,
                FirstNonEmpty(commander.MovementLabel, ModText.Get(ModStrings.Spatial.Movement)),
                FormatMovementValue(commander.MovesLeft) + " / " + FormatMovementValue(commander.MaxMovement)));

            if (!commander.HasDestination)
            {
                return;
            }

            details.Add(ModText.Get(ModStrings.Spatial.DestinationAt, FormatPoint(commander.Destination)));
            if (commander.HasThisTurnDestination && commander.ThisTurnDestination != commander.Destination)
            {
                details.Add(ModText.Get(ModStrings.Spatial.ThisTurnAt, FormatPoint(commander.ThisTurnDestination)));
            }
        }

        private static string FormatMovementValue(float value)
        {
            float normalized = value < 0.5f ? 0f : value;
            return Math.Round(normalized, 2).ToString("g2", CultureInfo.InvariantCulture);
        }

        private static string FormatPoint(UnityEngine.Vector2Int point)
        {
            return point.x + ", " + point.y;
        }

        private static string AppendDetails(string name, List<string> details)
        {
            if (details == null || details.Count == 0)
            {
                return name;
            }

            return name + ", " + string.Join(", ", details.ToArray());
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(current);
            }

            return new string(chars.ToArray());
        }
    }
}

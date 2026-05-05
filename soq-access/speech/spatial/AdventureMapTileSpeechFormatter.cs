using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Speech.Spatial
{
    internal sealed class AdventureMapTileSpeechFormatter : ISpatialTileSpeechFormatter<AdventureMapTile>
    {
        public string DescribeTile(AdventureMapTile tile)
        {
            if (tile == null)
            {
                return "Adventure map";
            }

            List<string> parts = new List<string>();
            if (!tile.IsExplored)
            {
                parts.Add("Unexplored");
                parts.Add(DescribeCoordinates(tile));
                return string.Join(". ", parts.ToArray()) + ".";
            }

            if (!tile.IsVisible)
            {
                parts.Add("Not visible");
            }

            string primary = DescribePrimaryContent(tile);
            bool hasContent = !string.IsNullOrWhiteSpace(primary);
            bool addedMovementStatus = hasContent;
            if (hasContent)
            {
                parts.Add(primary);
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
                if (!string.IsNullOrWhiteSpace(tile.CommanderRelationship))
                {
                    details.Add(tile.CommanderRelationship);
                }

                if (tile.IsSelectedCommander)
                {
                    details.Add("selected");
                }

                details.AddRange(GetMovementDetails(tile));
                return AppendDetails(FirstNonEmpty(tile.CommanderName, "Commander"), details);
            }

            if (tile.MapEntity != null)
            {
                string mapEntityName = FirstNonEmpty(tile.MapEntityName, "Map entity");
                if (tile.MapEntityVisited)
                {
                    mapEntityName += " (visited)";
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
                return AppendDetails("Interaction point", GetMovementDetails(tile));
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

        public string DescribeCoordinates(AdventureMapTile tile)
        {
            return tile == null ? string.Empty : "(" + tile.Position.x + ", " + tile.Position.y + ")";
        }

        private static List<string> GetMovementDetails(AdventureMapTile tile)
        {
            List<string> details = new List<string>();
            if (tile.IsReachable)
            {
                details.Add("reachable");
            }
            else if (tile.IsBlocked)
            {
                details.Add("blocked");
            }

            return details;
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

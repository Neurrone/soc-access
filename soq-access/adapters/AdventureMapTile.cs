using System.Collections.Generic;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureMapTile
    {
        public AdventureMapTile(Vector2Int position)
        {
            Position = position;
        }

        public Vector2Int Position { get; private set; }

        public bool IsExplored { get; set; }

        public bool IsVisible { get; set; }

        public bool IsReachable { get; set; }

        public bool IsBlocked { get; set; }

        public bool IsInteractionPoint { get; set; }

        public MapGroundType? Terrain { get; set; }

        public List<string> Environment { get; private set; } = new List<string>();

        public ICommanderState Commander { get; set; }

        public string CommanderName { get; set; }

        public bool IsSelectedCommander { get; set; }

        public string CommanderRelationship { get; set; }

        public IMapEntity MapEntity { get; set; }

        public string MapEntityName { get; set; }

        public string MapEntityHint { get; set; }

        public List<string> MapEntityDetails { get; private set; } = new List<string>();

        public string MapEntityRelationship { get; set; }

        public string ToSpeech()
        {
            List<string> parts = new List<string>();

            if (!IsExplored)
            {
                parts.Add("Unexplored");
                parts.Add(FormatCoordinates());
                return string.Join(". ", parts.ToArray()) + ".";
            }

            if (!IsVisible)
            {
                parts.Add("Not visible");
            }

            bool hasContent = AddContents(parts);
            bool addedMovementStatus = hasContent;

            for (int i = 0; i < Environment.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(Environment[i]))
                {
                    if (!addedMovementStatus)
                    {
                        parts.Add(AppendDetails(Environment[i], GetMovementDetails()));
                        addedMovementStatus = true;
                    }
                    else
                    {
                        parts.Add(Environment[i]);
                    }
                }
            }

            if (!addedMovementStatus)
            {
                List<string> movementDetails = GetMovementDetails();
                if (movementDetails.Count > 0)
                {
                    parts.Add(string.Join(", ", movementDetails.ToArray()));
                }
            }

            if (Terrain.HasValue)
            {
                parts.Add(FormatEnumName(Terrain.Value.ToString()));
            }

            parts.Add(FormatCoordinates());
            return string.Join(". ", parts.ToArray()) + ".";
        }

        private bool AddContents(List<string> parts)
        {
            if (Commander != null && IsVisible)
            {
                string commander = FirstNonEmpty(CommanderName, "Commander");
                List<string> details = new List<string>();
                if (!string.IsNullOrWhiteSpace(CommanderRelationship))
                {
                    details.Add(CommanderRelationship);
                }

                if (IsSelectedCommander)
                {
                    details.Add("selected");
                }

                details.AddRange(GetMovementDetails());

                parts.Add(AppendDetails(commander, details));
                return true;
            }

            if (MapEntity != null)
            {
                string mapEntity = FirstNonEmpty(MapEntityName, "Map entity");
                List<string> details = new List<string>();
                if (!string.IsNullOrWhiteSpace(MapEntityRelationship))
                {
                    details.Add(MapEntityRelationship);
                }

                for (int i = 0; i < MapEntityDetails.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(MapEntityDetails[i]))
                    {
                        details.Add(SpeechTextSanitizer.Normalize(MapEntityDetails[i]));
                    }
                }

                details.AddRange(GetMovementDetails());

                parts.Add(AppendDetails(mapEntity, details));
                return true;
            }

            if (IsInteractionPoint && IsVisible)
            {
                parts.Add(AppendDetails("Interaction point", GetMovementDetails()));
                return true;
            }

            if (IsVisible)
            {
                return false;
            }

            return false;
        }

        private List<string> GetMovementDetails()
        {
            List<string> details = new List<string>();
            if (IsReachable)
            {
                details.Add("reachable");
            }
            else if (IsBlocked)
            {
                details.Add("blocked");
            }

            return details;
        }

        private string FormatCoordinates()
        {
            return Position.x + ", " + Position.y;
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

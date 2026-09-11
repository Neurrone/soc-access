using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess.Scanner
{
    public sealed class CombatScannerSpeechContext : ScannerSpeechContext
    {
        public CombatScannerSpeechContext(
            ScannerResult result,
            CombatTile tile,
            CombatAdapter adapter,
            IReadOnlyList<ScannerDirectionStep> directions,
            int resultIndex,
            int resultCount,
            bool includeItemName)
            : base(
                result,
                CombatAnnouncementDefinitions.ScannerContent,
                BuildTileParts(tile, result),
                () => new CombatTileSpeechFormatter(adapter, null, includeEnemyInfluence: false).DescribeCoordinates(tile),
                directions,
                resultIndex,
                resultCount,
                includeItemName)
        {
        }

        /// <summary>
        /// Whether the acting troop can get there, and how high the ground is,
        /// both decide what the player would do with a result, so the scanner
        /// keeps them even though the rest of the tile belongs to the cursor.
        /// A terrain result already names one of these as its subject, so it
        /// does not get it twice.
        /// </summary>
        private static IEnumerable<AnnouncementPart> BuildTileParts(CombatTile tile, ScannerResult result)
        {
            if (tile == null)
            {
                yield break;
            }

            if (tile.IsReachable)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Reachable,
                    ModText.Get(ModStrings.Spatial.Reachable));
            }

            if (tile.Elevation > 0 && result != null && result.Kind != ScannerResultKind.TerrainPoint)
            {
                yield return new AnnouncementPart(
                    CombatAnnouncementDefinitions.TileKeys.Elevation,
                    ModText.Get(ModStrings.Spatial.ElevatedGroundHeight, tile.Elevation));
            }
        }
    }
}

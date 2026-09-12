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
                BuildTileParts(tile, result, adapter),
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
        /// does not get it twice. The stack's own restrictions and effects join
        /// them for the same reason: they decide what the player would do with
        /// the result, and they change under a label built once per sweep.
        /// </summary>
        private static IEnumerable<AnnouncementPart> BuildTileParts(
            CombatTile tile,
            ScannerResult result,
            CombatAdapter adapter)
        {
            if (tile == null)
            {
                yield break;
            }

            // What the stack the result points at cannot do and what has been done to it, read here
            // rather than folded into the result's own label: the label is built once for the whole
            // sweep, and these change with every turn that passes under it. A terrain result names
            // the ground, not whoever happens to stand on it.
            if (tile.Troop != null && adapter != null && result != null && result.Kind != ScannerResultKind.TerrainPoint)
            {
                CombatTroopFacts troop = adapter.GetTroopFacts(tile.Troop);
                string restrictions = CombatTileSpeechFormatter.DescribeRestrictions(troop);
                if (!string.IsNullOrWhiteSpace(restrictions))
                {
                    yield return new AnnouncementPart(
                        CombatAnnouncementDefinitions.TroopKeys.Restrictions,
                        restrictions);
                }

                string effects = CombatTileSpeechFormatter.DescribeEffects(troop);
                if (!string.IsNullOrWhiteSpace(effects))
                {
                    yield return new AnnouncementPart(
                        CombatAnnouncementDefinitions.TroopKeys.Effects,
                        effects);
                }
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

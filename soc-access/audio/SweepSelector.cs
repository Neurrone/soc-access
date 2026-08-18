using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    /// <summary>
    /// How a scanner entry sounds. The sweep and scanner-result browsing both come through
    /// here, so a ping and the sound of landing on the same entry are one gesture.
    /// </summary>
    internal static class SweepSelector
    {
        private static readonly SweepEntry[] NoEntries = new SweepEntry[0];
        private static readonly TileCue[] NoCues = new TileCue[0];

        /// <summary>
        /// The category voice the adapter stamped on this result plus the affiliation marker,
        /// or empty for entries that are not categorised entities. The category comes from the
        /// result rather than its position because a multi-tile entity's result can sit on a
        /// tile that does not carry it.
        /// </summary>
        public static IReadOnlyList<TileCue> ForScannerResult(
            ScannerResult result,
            Func<Vector2Int, AdventureMapTile> tileLookup)
        {
            if (result == null)
            {
                return NoCues;
            }

            return TileCueSelector.ForEntityCategory(
                result.EntityCategory,
                tileLookup != null ? tileLookup(result.Position) : null);
        }

        /// <summary>
        /// One ping per occupied tile: results are deduplicated by position, and entries that
        /// name nothing the sweep covers (obstacles, objectives, teleports) are skipped.
        /// </summary>
        public static IReadOnlyList<SweepEntry> ForLookAround(
            ScannerSnapshot snapshot,
            Func<Vector2Int, AdventureMapTile> tileLookup)
        {
            if (snapshot == null)
            {
                return NoEntries;
            }

            List<SweepEntry> entries = new List<SweepEntry>();
            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            for (int i = 0; i < snapshot.Categories.Count; i++)
            {
                ScannerCategory category = snapshot.Categories[i];
                if (category == null)
                {
                    continue;
                }

                for (int j = 0; j < category.Subcategories.Count; j++)
                {
                    ScannerSubcategory subcategory = category.Subcategories[j];
                    if (subcategory != null)
                    {
                        AddResults(entries, seen, subcategory.AllResults, tileLookup);
                    }
                }
            }

            return entries;
        }

        private static void AddResults(
            List<SweepEntry> entries,
            HashSet<Vector2Int> seen,
            IEnumerable<ScannerResult> results,
            Func<Vector2Int, AdventureMapTile> tileLookup)
        {
            foreach (ScannerResult result in results)
            {
                if (result == null || seen.Contains(result.Position))
                {
                    continue;
                }

                // Only a ping claims the tile, so an uncategorised entry sharing it — an
                // obstacle, an objective — never silences the entity standing there.
                IReadOnlyList<TileCue> cues = ForScannerResult(result, tileLookup);
                if (cues.Count > 0)
                {
                    seen.Add(result.Position);
                    entries.Add(new SweepEntry(result.Position, cues));
                }
            }
        }
    }
}

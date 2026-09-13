using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// ONE GROUP OF BATTLEFIELD GROUND AS SCANNER RESULTS, the same on the placement page and in
    /// the fight: the group is the item and its cells are that item's instances, so a ridge is one
    /// stop in the item cycle and its cells are what Alt+Page Down walks. The item key is the
    /// group's own identity, which the terrain of a battle never changes, so two scans of the same
    /// board land on the same item.
    /// </summary>
    public static class BattlefieldTerrainResults
    {
        private const string KeyPrefix = "terrain:";

        public static void Add(ScannerSnapshot snapshot, BattlefieldRegion region)
        {
            if (snapshot == null || region == null || region.Count == 0)
            {
                return;
            }

            string itemKey = KeyPrefix + region.Key;
            // Composed once for the group, not once per cell: every cell of a ridge is that ridge.
            string label = BattlefieldText.Region(region);
            for (int i = 0; i < region.Cells.Count; i++)
            {
                Vector2Int point = region.Cells[i];
                snapshot.Add(
                    ScannerCategoryKeys.Terrain,
                    ScannerSubcategoryKeys.All,
                    new ScannerResult(ScannerTileKeys.For(itemKey, point), label, point)
                    {
                        Kind = ScannerResultKind.TerrainPoint,
                        ItemKey = itemKey
                    });
            }
        }
    }
}

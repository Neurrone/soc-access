using Lavapotion.Cartography;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>The static travel cost of one battlefield cell, summed the way the game sums it
    /// (<c>AbstractStaticMapCache.UpdatePoint</c>, battle branch: no road factor). An infinite cost
    /// is what <c>IsWalkableStatic</c> reads as impassable, so a page that asks this question here
    /// gets the same walkable ground as the fight and as the dumped layout.</summary>
    public static class BattlefieldCellCosts
    {
        /// <summary>The manifest the battle costs are priced from, or null before it is loaded.</summary>
        public static CartographyManifest BattleManifest()
        {
            return CartographyManifestLoader.Instance != null ? CartographyManifestLoader.Instance.BattleManifest : null;
        }

        /// <summary>The cost of one cell from its layer bytes. Without a manifest nothing can be
        /// priced, so the caller's own blocker rule decides instead: water or a blocking decoration
        /// is impassable and everything else costs one.</summary>
        public static float StaticTravelCost(
            CartographyManifest manifest,
            int theme,
            int terrainType,
            int customType,
            int decoration,
            int standaloneDecoration,
            int effect,
            int water,
            int bridge,
            bool blockedWithoutManifest)
        {
            if (manifest == null)
            {
                return water > 0 || blockedWithoutManifest ? float.PositiveInfinity : 1f;
            }

            float typeCost = customType > 0 ? manifest.GetCustomTypeTravelCost(customType) : manifest.GetTypeTravelCost(theme, terrainType);
            float decorationCost = decoration == 0 ? 0f : manifest.GetDecorationTravelCost(theme, decoration);
            float effectCost = effect == 0 ? 0f : manifest.GetEffectTravelCost(effect);
            float bridgeCost = bridge == 0 ? 0f : manifest.GetBridgeTravelCost(bridge);
            float standaloneCost = standaloneDecoration == 0 ? 0f : manifest.GetStandaloneDecorationTravelCost(standaloneDecoration);
            float waterCost = water > 0 ? float.PositiveInfinity : 0f;
            return typeCost + (standaloneDecoration > 0 ? standaloneCost : decorationCost + waterCost) + effectCost + bridgeCost;
        }
    }
}

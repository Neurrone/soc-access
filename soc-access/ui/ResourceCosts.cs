using System.Collections.Generic;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// What a price costs, said once for every page that draws one. The game draws a price as bare
    /// numbers beside resource icons - "3500" under a gold coin, "2" under an amber - so the amount
    /// is the game's and the resource's name is the game's own word for it, and only the joining is
    /// the mod's.
    /// </summary>
    public static class ResourceCosts
    {
        /// <summary>The whole price as one phrase: "3500 gold and 2 ancient amber". Empty where the
        /// control draws no price at all.</summary>
        public static string Text(IReadOnlyList<PurchaseTroopsSubMenuAdapter.ResourceCostLine> costs)
        {
            List<string> parts = new List<string>();
            for (int i = 0; costs != null && i < costs.Count; i++)
            {
                PurchaseTroopsSubMenuAdapter.ResourceCostLine cost = costs[i];
                if (cost != null)
                {
                    parts.Add(ModText.Get(ModStrings.Common.ResourceAmount, cost.Amount, Name(cost.ResourceType)));
                }
            }

            return parts.Count == 0 ? string.Empty : ModText.JoinList(parts);
        }

        /// <summary>The game's own name for a resource, through the localization handler the game
        /// currently holds.</summary>
        public static string Name(ResourceType resourceType)
        {
            return SpokenLines.Clean(GameText.Get(Key(resourceType), string.Empty));
        }

        /// <summary>The game's own name for a resource, through a menu's own handler.</summary>
        public static string Name(ILocalizationHandler localization, ResourceType resourceType)
        {
            return SpokenLines.Clean(GameText.Get(localization, Key(resourceType), string.Empty));
        }

        /// <summary>The game's own name for a resource in the plural form it uses for an amount:
        /// the tables carry one form per count, and the game asks for them this way itself
        /// (<c>Resource.ToString</c> reads GetPluralText off the same key).</summary>
        public static string Name(ILocalizationHandler localization, ResourceType resourceType, int amount)
        {
            return SpokenLines.Clean(GameText.Plural(localization, Key(resourceType), amount, string.Empty));
        }

        /// <summary>The key the game itself composes for a resource's name
        /// (<c>ResourceExtensions.GetLocalizationKey</c>).</summary>
        public static string Key(ResourceType resourceType)
        {
            return "Common/Resource/" + resourceType;
        }
    }
}

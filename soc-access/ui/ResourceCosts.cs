using System.Collections.Generic;
using SongsOfConquest.Common.Economy;
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

        /// <summary>The game's own name for a resource.</summary>
        public static string Name(ResourceType resourceType)
        {
            return GameText.Get("Common/Resource/" + resourceType, FormatEnumName(resourceType.ToString()));
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(char.ToLowerInvariant(c));
            }

            return new string(chars.ToArray());
        }
    }
}

using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// What a spell costs and what a spell is called, said once for the spellbook and once for the
    /// quick bar. The amounts and the essence names are the game's own; only the joining is the
    /// mod's, which is why it is spelled here and not in the adapter that reads them.
    /// </summary>
    public static class SpellCosts
    {
        /// <summary>The whole price as one phrase: "3 Order, 2 Chaos". Empty where the spell costs
        /// nothing the game draws.</summary>
        public static string Text(IReadOnlyList<SpellbookAdapter.SpellCost> costs)
        {
            List<string> parts = new List<string>();
            for (int i = 0; costs != null && i < costs.Count; i++)
            {
                SpellbookAdapter.SpellCost cost = costs[i];
                if (cost != null)
                {
                    parts.Add(ModText.Get(ModStrings.Common.ResourceAmount, cost.Amount, cost.EssenceName));
                }
            }

            return Join(parts);
        }

        /// <summary>A spell as one line: its name, the tier the commander casts it at, and its price.
        /// </summary>
        public static string Label(string name, string tierLabel, IReadOnlyList<SpellbookAdapter.SpellCost> costs)
        {
            if (name == null)
            {
                return ModText.Get(ModStrings.Screens.UnknownSpell);
            }

            List<string> parts = new List<string> { name };
            if (!string.IsNullOrWhiteSpace(tierLabel))
            {
                parts.Add(tierLabel);
            }

            string cost = Text(costs);
            if (!string.IsNullOrWhiteSpace(cost))
            {
                parts.Add(cost);
            }

            return Join(parts);
        }

        /// <summary>The parts one after another, as a list of equals rather than a sentence: the
        /// spellbook's own lines read that way ("Fireball, Tier 2, 3 Chaos").</summary>
        private static string Join(IReadOnlyList<string> parts)
        {
            if (parts == null || parts.Count == 0)
            {
                return string.Empty;
            }

            string text = parts[0];
            for (int i = 1; i < parts.Count; i++)
            {
                text = ModText.Get(ModStrings.Common.ListSeparator, text, parts[i]);
            }

            return text;
        }
    }
}

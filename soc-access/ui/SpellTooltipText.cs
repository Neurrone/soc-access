using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// A SPELL'S TOOLTIP, as lines. The game draws the same thing as one block of marked-up text
    /// with its headers inline; read aloud it wants a line per thing said, so the pieces the adapter
    /// read from the game - the name, the tier, the lore, the tier's description and duration, the
    /// cost and what a click would do - are joined here, where the wording belongs.
    /// </summary>
    public static class SpellTooltipText
    {
        public static IReadOnlyList<string> Lines(BattleHudAdapter.SpellTooltipFacts spell)
        {
            List<string> lines = new List<string>();
            if (spell == null)
            {
                return lines;
            }

            // The name with its tier beside it, as the spellbook titles a spell.
            Add(lines, ModText.JoinListWithCommas(new[] { Name(spell.Name), spell.TierLabel }));
            Add(lines, spell.Lore);
            if (!string.IsNullOrWhiteSpace(spell.TierDescription))
            {
                Add(lines, ModText.Get(ModStrings.Common.Parenthetical, spell.DescriptionHeader, spell.DescriptionTierLabel));
                Add(lines, spell.TierDescription);
            }

            if (!string.IsNullOrWhiteSpace(spell.Duration))
            {
                Add(lines, ModText.Get(ModStrings.UI.LabelValue, spell.DurationHeader, spell.Duration));
            }

            string cost = FormatCost(spell.Cost);
            if (!string.IsNullOrWhiteSpace(cost))
            {
                Add(lines, ModText.Get(ModStrings.UI.LabelValue, spell.CostHeader, cost));
            }

            Add(lines, spell.CastText);
            return lines;
        }

        /// <summary>A spell's name, or the general word where the game has none for it.</summary>
        public static string Name(string spellName)
        {
            return string.IsNullOrWhiteSpace(spellName) ? ModText.Get(ModStrings.Combat.Spell) : spellName;
        }

        private static string FormatCost(IReadOnlyList<BattleHudAdapter.EssenceCost> cost)
        {
            if (cost == null || cost.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < cost.Count; i++)
            {
                parts.Add(ModText.Get(ModStrings.Common.ResourceAmount, cost[i].Amount, cost[i].EssenceName));
            }

            return ModText.JoinListWithCommas(parts);
        }

        private static void Add(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }
    }
}

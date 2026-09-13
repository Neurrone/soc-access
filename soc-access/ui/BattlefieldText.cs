using System.Collections.Generic;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE WORDING AROUND AN AUTHORED BATTLEFIELD DESCRIPTION. The table holds three pieces of raw
    /// text and nothing else (<see cref="BattlefieldDescriptions"/>); the labels, the order and what
    /// is said about a layout nobody has described yet are the mod's, so they live here rather than
    /// beside the lookup.
    ///
    /// The placement page reads all three, labelled, because nothing has been placed yet and where
    /// each side starts is what the player is deciding about. In combat the spawns are already
    /// behind them, so the gesture there says the terrain alone and does not label it.
    /// </summary>
    public static class BattlefieldText
    {
        /// <summary>The three lines of a description, one per field so the review buffer holds three
        /// and the node speaks them as one body. Empty where the layout has no description.</summary>
        public static List<string> Lines(BattlefieldDescription description)
        {
            List<string> lines = new List<string>(3);
            if (description == null)
            {
                return lines;
            }

            AddLine(lines, ModStrings.Screens.DescriptionTerrain, description.Terrain);
            AddLine(lines, ModStrings.Screens.DescriptionAttacker, description.Attacker);
            AddLine(lines, ModStrings.Screens.DescriptionDefender, description.Defender);
            return lines;
        }

        /// <summary>What the describe-battlefield gesture says on the placement board: the three
        /// labelled lines as one breath, or that nobody has described this layout.</summary>
        public static string Spoken(string layoutKey)
        {
            BattlefieldDescription description;
            if (!BattlefieldDescriptions.TryGet(layoutKey, out description))
            {
                return ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
            }

            List<string> lines = Lines(description);
            return lines.Count == 0
                ? ModText.Get(ModStrings.Screens.NoBattlefieldDescription)
                : ModText.JoinList(ModStrings.Common.PhraseSeparator, lines);
        }

        /// <summary>What the same gesture says in combat: the terrain alone, unlabelled.</summary>
        public static string SpokenTerrain(string layoutKey)
        {
            BattlefieldDescription description;
            return BattlefieldDescriptions.TryGet(layoutKey, out description)
                && !string.IsNullOrWhiteSpace(description.Terrain)
                ? description.Terrain
                : ModText.Get(ModStrings.Screens.NoBattlefieldDescription);
        }

        private static void AddLine(List<string> lines, ModString label, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(ModText.Get(label, text));
            }
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The game's attack previews as lines to read. The preview on the troop aimed at reads first
    /// and without a name, since the tile has just said it; the game draws it last when an attack
    /// also hits other stacks. Every other preview is headed by its stack ("5 Roots of the Mother
    /// at 3.5, 1: damage 119–149, kills 1."), friendly stacks marked as such, so an attack that
    /// hits several stacks, or an area aimed at an empty hex, says whose numbers are whose. A
    /// spell's previews read the same way, and a map entity it would hit off the tile aimed at is
    /// headed by its name and position.
    /// </summary>
    public static class CombatAttackPreviewText
    {
        public static IList<string> Compose(IList<CombatAttackPreviewFacts> previews)
        {
            List<string> lines = new List<string>();
            if (previews == null)
            {
                return lines;
            }

            foreach (CombatAttackPreviewFacts preview in previews)
            {
                if (preview.IsOnTargetTroop || preview.IsOnTargetEntity)
                {
                    AddLines(lines, preview, named: false);
                }
            }

            foreach (CombatAttackPreviewFacts preview in previews)
            {
                if (!preview.IsOnTargetTroop && !preview.IsOnTargetEntity)
                {
                    AddLines(lines, preview, named: preview.Troop != null || preview.IsEntity);
                }
            }

            // Split where the game drew a line: an additional sentence it wrote over two lines is
            // two lines to read, exactly as a tooltip's are.
            return SpokenLines.Of(lines);
        }

        private static void AddLines(List<string> lines, CombatAttackPreviewFacts preview, bool named)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(preview.Damage))
            {
                parts.Add(ModText.Get(ModStrings.Spatial.DamagePreview, preview.Damage));
            }

            if (!string.IsNullOrWhiteSpace(preview.Kills))
            {
                parts.Add(preview.IsEntity || (preview.TargetIsEntity && !named)
                    ? FormatEntityDestruction(preview.Kills)
                    : ModText.Get(ModStrings.Spatial.Kills, preview.Kills));
            }

            List<string> previewLines = new List<string>();
            if (parts.Count > 0)
            {
                previewLines.Add(ModText.Get(ModStrings.Common.Sentence, ModText.JoinListWithCommas(parts)));
            }

            if (!string.IsNullOrWhiteSpace(preview.Additional))
            {
                // A sentence per line the game drew, so "Spell Damage Resistance: -40% Damage" and
                // the Mother's Embrace line under it do not run together.
                foreach (string line in SpokenLines.Of(new[] { preview.Additional }))
                {
                    previewLines.Add(ModText.Get(ModStrings.Common.Sentence, line));
                }
            }

            if (named && previewLines.Count > 0)
            {
                string heading = preview.IsEntity ? FormatEntity(preview) : FormatStack(preview.Troop);
                previewLines[0] = ModText.Get(ModStrings.UI.LabelValue, heading, previewLines[0]);
            }

            lines.AddRange(previewLines);
        }

        /// <summary>"38 Piercers at 1.5, 3" for an enemy stack, "6 friendly Grenadier at 3.5, 3"
        /// for one of the player's own.</summary>
        private static string FormatStack(TroopRef troop)
        {
            string label = troop.LocalTeamId >= 0 && !troop.IsEnemy
                ? ModText.Get(ModStrings.Combat.FriendlyTroop, troop.Count, troop.Name)
                : ModText.Get(ModStrings.Combat.TroopQuantity, troop.Count, troop.Name);
            return ModText.Get(ModStrings.Combat.TroopAt, label, CombatText.FormatPoint(troop.Position));
        }

        /// <summary>"Explosive barrel at 4, 2" for a map entity a spell would hit off the tile
        /// aimed at.</summary>
        private static string FormatEntity(CombatAttackPreviewFacts preview)
        {
            string name = string.IsNullOrWhiteSpace(preview.EntityName)
                ? ModText.Get(ModStrings.Combat.AttackableEntity)
                : preview.EntityName;
            return ModText.Get(ModStrings.Combat.TroopAt, name, CombatText.FormatPoint(preview.EntityPoint));
        }

        private static string FormatEntityDestruction(string killsText)
        {
            int min;
            int max;
            if (TryParseRange(killsText, out min, out max))
            {
                return min <= 0 && max > 0
                    ? ModText.Get(ModStrings.Spatial.MayDestroy)
                    : ModText.Get(ModStrings.Spatial.Destroys);
            }

            return ModText.Get(ModStrings.Spatial.Destroys);
        }

        private static bool TryParseRange(string text, out int min, out int max)
        {
            min = 0;
            max = 0;
            MatchCollection matches = Regex.Matches(text ?? string.Empty, "\\d+");
            if (matches.Count == 0)
            {
                return false;
            }

            min = int.Parse(matches[0].Value);
            max = matches.Count > 1 ? int.Parse(matches[1].Value) : min;
            return true;
        }
    }
}

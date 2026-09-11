using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// How a stack on the battlefield, or a thing on it that can be attacked, is SAID: the quantity
    /// and the name, the health under them, and the word that marks the troop whose turn it is. The
    /// board's rows, the scanner's results, the inspection's opening line and the tile readout all
    /// name a stack the same way, so the wording lives here and the adapter answers with facts.
    /// </summary>
    public static class CombatTroopText
    {
        /// <summary>"5 Footmen" for one of yours, "5 enemy Footmen" for theirs.</summary>
        public static string Quantity(CombatTroopFacts troop)
        {
            return ModText.Get(
                troop.IsEnemy ? ModStrings.Combat.EnemyTroop : ModStrings.Combat.TroopQuantity,
                troop.Size,
                troop.Name);
        }

        /// <summary>The stack's name on its own, with the general word where the game gives none.
        /// </summary>
        public static string Name(CombatTroopFacts troop)
        {
            return string.IsNullOrWhiteSpace(troop.Name) ? ModText.Get(ModStrings.Combat.Troop) : troop.Name;
        }

        public static string Health(int current, int max)
        {
            return ModText.Get(ModStrings.Spatial.Health, current, max);
        }

        /// <summary>A whole stack row: the quantity, the health, and "Acting" in front of the troop
        /// whose turn it is.</summary>
        public static string Stack(CombatTroopFacts troop)
        {
            string label = MenuButtonTextUtility.JoinParts(
                Quantity(troop),
                Health(troop.CurrentHealth, troop.MaxHealth));
            return troop.IsActing
                ? MenuButtonTextUtility.JoinParts(ModText.Get(ModStrings.Spatial.Acting), label)
                : label;
        }

        /// <summary>A thing on the board that can be attacked: its name, or the general word where
        /// the game gives it none, and the health it has left.</summary>
        public static string Entity(CombatEntityFacts entity)
        {
            string name = string.IsNullOrWhiteSpace(entity.Name)
                ? ModText.Get(ModStrings.Combat.AttackableEntity)
                : entity.Name;
            return entity.HasHealth
                ? MenuButtonTextUtility.JoinParts(name, Health(entity.HealthLeft, entity.MaxHealth))
                : name;
        }
    }
}

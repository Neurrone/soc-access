using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Speech.Spatial
{
    public enum CombatInfluenceGroup
    {
        ZoneOfControl,
        DeadlyRange,
        AttackRange,
        MovementRange
    }

    public sealed class CombatInfluenceSource
    {
        public CombatInfluenceSource(TroopRef troop, IEnumerable<CombatRangeIndicator> indicators)
        {
            Troop = troop;
            Indicators = indicators != null
                ? new HashSet<CombatRangeIndicator>(indicators)
                : new HashSet<CombatRangeIndicator>();
        }

        public TroopRef Troop { get; private set; }
        public HashSet<CombatRangeIndicator> Indicators { get; private set; }
    }

    public static class CombatInfluenceFormatter
    {
        private static readonly CombatInfluenceGroup[] GroupOrder =
        {
            CombatInfluenceGroup.ZoneOfControl,
            CombatInfluenceGroup.DeadlyRange,
            CombatInfluenceGroup.AttackRange,
            CombatInfluenceGroup.MovementRange
        };

        public static string Format(IList<CombatInfluenceSource> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return string.Empty;
            }

            Dictionary<CombatInfluenceGroup, List<TroopRef>> groups = BuildGroups(sources);
            List<string> sentences = new List<string>();
            for (int i = 0; i < GroupOrder.Length; i++)
            {
                CombatInfluenceGroup group = GroupOrder[i];
                List<TroopRef> troops;
                if (!groups.TryGetValue(group, out troops) || troops.Count == 0)
                {
                    continue;
                }

                sentences.Add(ModText.Get(ModStrings.UI.LabelValue, FormatGroupLabel(group), FormatTroops(troops)));
            }

            return sentences.Count > 0 ? string.Join(". ", sentences.ToArray()) + "." : string.Empty;
        }

        private static Dictionary<CombatInfluenceGroup, List<TroopRef>> BuildGroups(IList<CombatInfluenceSource> sources)
        {
            Dictionary<CombatInfluenceGroup, List<TroopRef>> groups = new Dictionary<CombatInfluenceGroup, List<TroopRef>>();
            for (int i = 0; i < sources.Count; i++)
            {
                CombatInfluenceSource source = sources[i];
                if (source == null || source.Troop == null || source.Indicators == null || source.Indicators.Count == 0)
                {
                    continue;
                }

                if (source.Indicators.Contains(CombatRangeIndicator.ZoneOfControl))
                {
                    Add(groups, CombatInfluenceGroup.ZoneOfControl, source.Troop);
                    continue;
                }

                if (source.Indicators.Contains(CombatRangeIndicator.Deadly))
                {
                    Add(groups, CombatInfluenceGroup.DeadlyRange, source.Troop);
                }
                else if (source.Indicators.Contains(CombatRangeIndicator.Attack) || source.Indicators.Contains(CombatRangeIndicator.Melee))
                {
                    Add(groups, CombatInfluenceGroup.AttackRange, source.Troop);
                }

                if (source.Indicators.Contains(CombatRangeIndicator.Movement))
                {
                    Add(groups, CombatInfluenceGroup.MovementRange, source.Troop);
                }
            }

            return groups;
        }

        private static void Add(Dictionary<CombatInfluenceGroup, List<TroopRef>> groups, CombatInfluenceGroup group, TroopRef troop)
        {
            List<TroopRef> troops;
            if (!groups.TryGetValue(group, out troops))
            {
                troops = new List<TroopRef>();
                groups[group] = troops;
            }

            troops.Add(troop);
        }

        private static string FormatGroupLabel(CombatInfluenceGroup group)
        {
            switch (group)
            {
                case CombatInfluenceGroup.ZoneOfControl:
                    return ModText.Get(ModStrings.Spatial.ZoneOfControl);
                case CombatInfluenceGroup.DeadlyRange:
                    return ModText.Get(ModStrings.Spatial.DeadlyRange);
                case CombatInfluenceGroup.AttackRange:
                    return ModText.Get(ModStrings.Spatial.AttackRange);
                default:
                    return ModText.Get(ModStrings.Spatial.MovementRange);
            }
        }

        private static string FormatTroops(IList<TroopRef> troops)
        {
            return string.Join(", ", troops.Select(FormatTroop).ToArray());
        }

        private static string FormatTroop(TroopRef troop)
        {
            string text = ModText.Get(ModStrings.Combat.TroopQuantity, troop.Count, troop.Name);
            return ModText.Get(ModStrings.Combat.TroopAt, text, CombatText.FormatPoint(troop.Position));
        }
    }
}

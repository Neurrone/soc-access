using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Adapters
{
    // THE GAME'S OWN LARGE COST SECTION, READ BACK. The build menu and the purchase-wielder menu
    // both draw a price through a LargeCostSection, and both read it back the same way: one entry
    // per resource in the game's own order, skipped when the game has not drawn that entry or has
    // drawn it blank, with the amount taken from the text the entry shows. Only the wording differs
    // between the two, so the amount comes back as the game's own string and the caller composes
    // the phrase.
    public static class LargeCostSectionText
    {
        private static readonly CostEntryFields[] Entries =
        {
            new CostEntryFields("_goldCostEntry", "_goldAmountText", ResourceType.Gold),
            new CostEntryFields("_stoneCostEntry", "_stoneAmountText", ResourceType.Stone),
            new CostEntryFields("_woodCostEntry", "_woodAmountText", ResourceType.Wood),
            new CostEntryFields("_glimmerWeaveCostEntry", "_glimmerWeaveAmountText", ResourceType.Glimmerweave),
            new CostEntryFields("_ancientAmberCostEntry", "_ancientAmberAmountText", ResourceType.AncientAmber),
            new CostEntryFields("_celestialOreCostEntry", "_celestialOreAmountText", ResourceType.CelestialOre),
        };

        /// <summary>Every resource the section is currently drawing, worded by <paramref name="format"/>,
        /// which is handed the resource and the amount exactly as the entry shows it.</summary>
        public static List<string> Parts(LargeCostSection section, Func<ResourceType, string, string> format)
        {
            List<string> parts = new List<string>();
            if (section == null || format == null)
            {
                return parts;
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                CostEntryFields fields = Entries[i];
                UITransform entry = Reflect.Get<UITransform>(section, fields.EntryField);
                if (entry == null || !entry.Active)
                {
                    continue;
                }

                string amount = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(section, fields.TextField));
                if (string.IsNullOrWhiteSpace(amount))
                {
                    continue;
                }

                parts.Add(format(fields.Type, amount));
            }

            return parts;
        }

        private sealed class CostEntryFields
        {
            public CostEntryFields(string entryFieldName, string textFieldName, ResourceType type)
            {
                EntryField = AccessTools.Field(typeof(LargeCostSection), entryFieldName);
                TextField = AccessTools.Field(typeof(LargeCostSection), textFieldName);
                Type = type;
            }

            public FieldInfo EntryField { get; private set; }

            public FieldInfo TextField { get; private set; }

            public ResourceType Type { get; private set; }
        }
    }
}

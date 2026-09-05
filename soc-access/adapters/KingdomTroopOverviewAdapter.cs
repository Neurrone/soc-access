using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class KingdomTroopOverviewAdapter
    {
        private static readonly FieldInfo TownNameTextField =
            AccessTools.Field(typeof(KingdomTroopOverviewTownEntry), "_townNameText");
        private static readonly FieldInfo UpgradeTextField =
            AccessTools.Field(typeof(KingdomTroopOverviewTownEntry), "_upgradeText");
        private static readonly FieldInfo IncomeTextField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_text");
        private static readonly FieldInfo IncomeAmountField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_amount");
        private static readonly FieldInfo IncomeButtonField =
            AccessTools.Field(typeof(KingdomTroopOverviewIncomeEntry), "_button");
        private static readonly MethodInfo TownClickMethod =
            AccessTools.Method(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked");
        private static readonly MethodInfo IncomeClickMethod =
            AccessTools.Method(typeof(KingdomTroopOverviewIncomeEntry), "HandleButtonClicked");

        private readonly KingdomTroopOverviewMenu _menu;

        public KingdomTroopOverviewAdapter(KingdomTroopOverviewMenu menu)
        {
            _menu = menu;
        }

        public bool IsPresent()
        {
            return _menu != null && _menu.IsVisible;
        }

        public string Title
        {
            get
            {
                if (_menu == null)
                {
                    return "Troop overview";
                }

                UITextMesh[] texts = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: false);
                for (int i = 0; i < texts.Length; i++)
                {
                    UITextMesh text = texts[i];
                    if (text == null || IsOverviewEntryText(text))
                    {
                        continue;
                    }

                    string candidate = NormalizeText(text);
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        return candidate;
                    }
                }

                return "Troop overview";
            }
        }

        public IReadOnlyList<GroupItem> GetGroups()
        {
            List<GroupItem> groups = new List<GroupItem>();
            if (!IsPresent())
            {
                return groups;
            }

            KingdomTroopOverviewTownEntry[] entries =
                ((Component)_menu).GetComponentsInChildren<KingdomTroopOverviewTownEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                KingdomTroopOverviewTownEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                GroupItem group = BuildGroup(entry, i);
                if (group != null)
                {
                    groups.Add(group);
                }
            }

            return groups;
        }

        public void HideNativeTooltip()
        {
        }

        private static GroupItem BuildGroup(KingdomTroopOverviewTownEntry entry, int index)
        {
            string town = NormalizeText(GetText(entry, TownNameTextField));
            string tier = NormalizeText(GetText(entry, UpgradeTextField));
            string title = MenuButtonTextUtility.JoinParts(town, tier);
            string label = GetShortCategoryLabel(town);

            List<RowItem> rows = new List<RowItem>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                rows.Add(new RowItem(
                    title,
                    string.Empty,
                    () => ClickTown(entry),
                    () => true));
            }

            KingdomTroopOverviewIncomeEntry[] incomeEntries =
                entry.GetComponentsInChildren<KingdomTroopOverviewIncomeEntry>(includeInactive: false);
            for (int i = 0; i < incomeEntries.Length; i++)
            {
                KingdomTroopOverviewIncomeEntry income = incomeEntries[i];
                if (income == null || !income.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string troop = NormalizeText(GetText(income, IncomeTextField));
                if (string.IsNullOrWhiteSpace(troop))
                {
                    continue;
                }

                string amount = NormalizeText(GetText(income, IncomeAmountField));
                rows.Add(new RowItem(
                    troop,
                    amount,
                    () => ClickIncome(income),
                    () => FocusIncome(income)));
            }

            return rows.Count > 0 ? new GroupItem(label, rows) : null;
        }

        private static bool ClickTown(KingdomTroopOverviewTownEntry entry)
        {
            if (entry == null || TownClickMethod == null)
            {
                return false;
            }

            try
            {
                TownClickMethod.Invoke(entry, new object[] { Vector2.zero });
                return true;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("KingdomTroopOverviewAdapter failed to click town row: " + ex.Message);
                return false;
            }
        }

        private static bool ClickIncome(KingdomTroopOverviewIncomeEntry entry)
        {
            if (entry == null || IncomeClickMethod == null)
            {
                return false;
            }

            try
            {
                IncomeClickMethod.Invoke(entry, null);
                return true;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("KingdomTroopOverviewAdapter failed to click troop row: " + ex.Message);
                return false;
            }
        }

        private static bool FocusIncome(KingdomTroopOverviewIncomeEntry entry)
        {
            UIButton button = GetField<UIButton>(entry, IncomeButtonField);
            Selectable selectable = button != null ? button.GetSelectable() : null;
            return NativeSelectionUtility.Select(selectable);
        }

        private static UITextMesh GetText(object target, FieldInfo field)
        {
            return GetField<UITextMesh>(target, field);
        }

        private static T GetField<T>(object target, FieldInfo field) where T : class
        {
            if (target == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target) as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string NormalizeText(UITextMesh text)
        {
            return text != null
                ? SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text))
                : string.Empty;
        }

        private static bool IsOverviewEntryText(UITextMesh text)
        {
            if (text == null)
            {
                return true;
            }

            return text.GetComponentInParent<KingdomTroopOverviewTownEntry>() != null
                || text.GetComponentInParent<KingdomTroopOverviewIncomeEntry>() != null;
        }

        private static string GetShortCategoryLabel(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return string.Empty;
            }

            int separator = category.IndexOf(" - ", StringComparison.Ordinal);
            if (separator < 0)
            {
                separator = category.IndexOf(" \u2013 ", StringComparison.Ordinal);
            }

            return separator > 0 ? category.Substring(0, separator).Trim() : category.Trim();
        }

        public sealed class GroupItem
        {
            public GroupItem(string label, IReadOnlyList<RowItem> rows)
            {
                Label = label ?? string.Empty;
                Rows = rows ?? new RowItem[0];
            }

            public string Label { get; private set; }
            public IReadOnlyList<RowItem> Rows { get; private set; }
        }

        public sealed class RowItem
        {
            public RowItem(string label, string status, Func<bool> activate, Func<bool> focus)
            {
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                Activate = activate;
                Focus = focus;
            }

            public string Label { get; private set; }
            public string Status { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Func<bool> Focus { get; private set; }
        }
    }
}

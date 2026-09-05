using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class KingdomEntityOverviewAdapter
    {
        private static readonly FieldInfo CategoryTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_categoryText");
        private static readonly FieldInfo UpgradeTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_upgradeText");
        private static readonly FieldInfo ParentField =
            AccessTools.Field(typeof(KingdomEntityOverviewCategoryEntry), "_parent");
        private static readonly FieldInfo AmountTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_amount");
        private static readonly FieldInfo NameTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_text");
        private static readonly FieldInfo LevelTextField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_level");
        private static readonly FieldInfo ButtonField =
            AccessTools.Field(typeof(KingdomEntityOverviewClaimedEntry), "_button");
        private static readonly MethodInfo CategoryClickMethod =
            AccessTools.Method(typeof(KingdomEntityOverviewCategoryEntry), "HandleCategoryTextClicked");
        private static readonly MethodInfo RowClickMethod =
            AccessTools.Method(typeof(KingdomEntityOverviewClaimedEntry), "HandleButtonClicked");

        private readonly KingdomEntityOverviewMenu _menu;

        public KingdomEntityOverviewAdapter(KingdomEntityOverviewMenu menu)
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
                    return string.Empty;
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

                return string.Empty;
            }
        }

        public IReadOnlyList<GroupItem> GetGroups()
        {
            List<GroupItem> groups = new List<GroupItem>();
            if (!IsPresent())
            {
                return groups;
            }

            KingdomEntityOverviewCategoryEntry[] entries =
                ((Component)_menu).GetComponentsInChildren<KingdomEntityOverviewCategoryEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                KingdomEntityOverviewCategoryEntry entry = entries[i];
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

        private static GroupItem BuildGroup(KingdomEntityOverviewCategoryEntry entry, int index)
        {
            string category = NormalizeText(GetText(entry, CategoryTextField));
            string tier = NormalizeText(GetText(entry, UpgradeTextField));
            bool hasParent = GetParent(entry) != null;
            string title = MenuButtonTextUtility.JoinParts(category, tier);
            string label = hasParent ? GetShortCategoryLabel(category) : category;

            List<RowItem> rows = new List<RowItem>();
            if (hasParent && !string.IsNullOrWhiteSpace(title))
            {
                rows.Add(new RowItem(
                    title,
                    string.Empty,
                    () => ClickCategory(entry),
                    () => true));
            }

            KingdomEntityOverviewClaimedEntry[] buildingEntries =
                entry.GetComponentsInChildren<KingdomEntityOverviewClaimedEntry>(includeInactive: false);
            for (int i = 0; i < buildingEntries.Length; i++)
            {
                KingdomEntityOverviewClaimedEntry building = buildingEntries[i];
                if (building == null || !building.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string labelText = BuildBuildingLabel(building);
                if (string.IsNullOrWhiteSpace(labelText))
                {
                    continue;
                }

                string status = NormalizeText(GetText(building, LevelTextField));
                rows.Add(new RowItem(
                    labelText,
                    status,
                    () => ClickBuilding(building),
                    () => FocusBuilding(building)));
            }

            return rows.Count > 0 ? new GroupItem(label, rows) : null;
        }

        private static string BuildBuildingLabel(KingdomEntityOverviewClaimedEntry entry)
        {
            string amount = NormalizeText(GetText(entry, AmountTextField));
            string name = NormalizeText(GetText(entry, NameTextField));
            if (string.IsNullOrWhiteSpace(amount))
            {
                return name;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return amount;
            }

            return amount + " " + name;
        }

        private static bool ClickCategory(KingdomEntityOverviewCategoryEntry entry)
        {
            if (entry == null || CategoryClickMethod == null || GetParent(entry) == null)
            {
                return false;
            }

            try
            {
                CategoryClickMethod.Invoke(entry, new object[] { Vector2.zero });
                return true;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("KingdomEntityOverviewAdapter failed to click category: " + ex.Message);
                return false;
            }
        }

        private static bool ClickBuilding(KingdomEntityOverviewClaimedEntry entry)
        {
            if (entry == null || RowClickMethod == null)
            {
                return false;
            }

            try
            {
                RowClickMethod.Invoke(entry, null);
                return true;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("KingdomEntityOverviewAdapter failed to click building row: " + ex.Message);
                return false;
            }
        }

        private static bool FocusBuilding(KingdomEntityOverviewClaimedEntry entry)
        {
            UIButton button = GetField<UIButton>(entry, ButtonField);
            Selectable selectable = button != null ? button.GetSelectable() : null;
            return NativeSelectionUtility.Select(selectable);
        }

        private static IMapEntity GetParent(KingdomEntityOverviewCategoryEntry entry)
        {
            return GetField<IMapEntity>(entry, ParentField);
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

            return text.GetComponentInParent<KingdomEntityOverviewCategoryEntry>() != null
                || text.GetComponentInParent<KingdomEntityOverviewClaimedEntry>() != null;
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

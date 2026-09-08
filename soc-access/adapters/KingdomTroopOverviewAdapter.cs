using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The kingdom's troop overview, read off the game's own entries. One
    /// <see cref="KingdomTroopOverviewTownEntry"/> per settlement, each drawing its name and its
    /// tier, and one <see cref="KingdomTroopOverviewIncomeEntry"/> per recruitable troop.
    /// </summary>
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
        private static readonly MethodInfo TownClickMethod =
            AccessTools.Method(typeof(KingdomTroopOverviewTownEntry), "HandleTownNameClicked");

        private readonly KingdomTroopOverviewMenu _menu;

        public KingdomTroopOverviewAdapter(KingdomTroopOverviewMenu menu)
        {
            _menu = menu;
        }

        public bool IsPresent()
        {
            return _menu != null && _menu.IsVisible;
        }

        /// <summary>The menu's own drawn title. Empty when the menu draws none.</summary>
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

        /// <summary>The towns in hierarchy order, which is the order the menu draws them.</summary>
        public IReadOnlyList<TownItem> GetTowns()
        {
            List<TownItem> towns = new List<TownItem>();
            if (!IsPresent())
            {
                return towns;
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

                towns.Add(BuildTown(entry));
            }

            return towns;
        }

        /// <summary>The game's own hide path, which is what clicking the blocker behind the menu
        /// runs.</summary>
        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        private static TownItem BuildTown(KingdomTroopOverviewTownEntry entry)
        {
            List<RowItem> rows = new List<RowItem>();
            KingdomTroopOverviewIncomeEntry[] incomes =
                entry.GetComponentsInChildren<KingdomTroopOverviewIncomeEntry>(includeInactive: false);
            for (int i = 0; i < incomes.Length; i++)
            {
                KingdomTroopOverviewIncomeEntry income = incomes[i];
                if (income == null || !income.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string troop = NormalizeText(GetText(income, IncomeTextField));
                if (string.IsNullOrWhiteSpace(troop))
                {
                    continue;
                }

                rows.Add(new RowItem(
                    income,
                    income.Button,
                    troop,
                    NormalizeText(GetText(income, IncomeAmountField)),
                    () => ClickIncome(income),
                    () => FocusButton(income.Button)));
            }

            return new TownItem(
                entry,
                NormalizeText(GetText(entry, TownNameTextField)),
                NormalizeText(GetText(entry, UpgradeTextField)),
                () => ClickTown(entry),
                rows);
        }

        // The town's name is a UITextMesh whose click is delivered by UITransform.Update from the real
        // mouse position (decompiled), so there is no native call to make: the handler is invoked
        // directly.
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

        // The row IS a UIButton, and the entry wires HandleButtonClicked onto its OnClicked in
        // OnEnable, so the native click reaches the same handler the mouse does.
        private static bool ClickIncome(KingdomTroopOverviewIncomeEntry entry)
        {
            return entry != null && NativeSelectionUtility.Click(entry.Button);
        }

        private static bool FocusButton(UIButton button)
        {
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
                ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text))
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

        /// <summary>One settlement's entry.</summary>
        public sealed class TownItem
        {
            public TownItem(
                Component entry,
                string name,
                string tier,
                Func<bool> moveCamera,
                IReadOnlyList<RowItem> rows)
            {
                Entry = entry;
                Name = name ?? string.Empty;
                Tier = tier ?? string.Empty;
                MoveCamera = moveCamera;
                Rows = rows ?? new RowItem[0];
            }

            /// <summary>The drawn entry: what the row stands on and what it is scrolled by.</summary>
            public Component Entry { get; private set; }

            /// <summary>The full drawn town text ("Hazelpoint - Small Settlement").</summary>
            public string Name { get; private set; }

            /// <summary>The drawn upgrade text ("Tier: 2/2").</summary>
            public string Tier { get; private set; }

            /// <summary>Moves the camera onto the settlement, as clicking the name does.</summary>
            public Func<bool> MoveCamera { get; private set; }

            public IReadOnlyList<RowItem> Rows { get; private set; }
        }

        /// <summary>One recruitable troop of a town.</summary>
        public sealed class RowItem
        {
            public RowItem(
                Component entry,
                Component button,
                string name,
                string amount,
                Func<bool> activate,
                Func<bool> focus)
            {
                Entry = entry;
                Button = button;
                Name = name ?? string.Empty;
                Amount = amount ?? string.Empty;
                Activate = activate;
                Focus = focus;
            }

            /// <summary>The drawn entry: what the row stands on and what it is scrolled by.</summary>
            public Component Entry { get; private set; }

            /// <summary>The drawn button the row's click runs through.</summary>
            public Component Button { get; private set; }

            /// <summary>The troop's localized name.</summary>
            public string Name { get; private set; }

            /// <summary>The figure the game draws for the troop, which is the number available and the
            /// per-round income in one text ("10 (+2)"): <c>KingdomTroopOverviewIncomeEntry.SetTroop</c>
            /// composes them into the label and keeps neither number.</summary>
            public string Amount { get; private set; }

            /// <summary>Cycles the camera through the buildings producing this troop, as the mouse
            /// does.</summary>
            public Func<bool> Activate { get; private set; }

            public Func<bool> Focus { get; private set; }
        }
    }
}

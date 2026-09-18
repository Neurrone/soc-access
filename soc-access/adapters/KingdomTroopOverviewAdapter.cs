using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The kingdom's troop overview, read off the game's own entries. One
    /// <see cref="KingdomTroopOverviewTownEntry"/> per settlement, each drawing its name and its
    /// tier, and one <see cref="KingdomTroopOverviewIncomeEntry"/> per recruitable troop.
    /// </summary>
    public sealed class KingdomTroopOverviewAdapter : IPresent
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

        /// <summary>The menu's own per-opening token: <c>Show</c> assigns a fresh <c>Async</c> to
        /// <c>_async</c> before it respawns the entries, and <c>Hide</c> clears it.</summary>
        private static readonly FieldInfo AsyncField =
            AccessTools.Field(typeof(KingdomTroopOverviewMenu), "_async");

        /// <summary>What GetTowns answers before the menu has drawn.</summary>
        private static readonly TownItem[] NoTowns = new TownItem[0];

        private readonly KingdomTroopOverviewMenu _menu;

        // What the menu drew, read once PER OPENING. The game fills the whole page inside
        // KingdomTroopOverviewMenu.Show and never touches it again until Hide, so reading the
        // entries every frame would re-read a page that cannot change; but Hide only deactivates
        // the object, and the next Show reuses this same menu instance and respawns the entries
        // from its pool (so entry identities repeat), which is why the snapshot is keyed on the
        // opening rather than on the adapter's lifetime.
        private object _opening;
        private ILanguageDefinition _language;
        private List<TownItem> _towns;
        private string _title;

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
                SyncOpening();
                if (_title == null)
                {
                    _title = KingdomOverviewRead.FindTitle<
                        KingdomTroopOverviewTownEntry, KingdomTroopOverviewIncomeEntry>((Component)_menu);
                }

                return _title;
            }
        }

        /// <summary>The towns in hierarchy order, which is the order the menu draws them. Read off
        /// the page once per opening: the game builds it in Show and leaves it alone until
        /// Hide.</summary>
        public IReadOnlyList<TownItem> GetTowns()
        {
            SyncOpening();
            if (_towns == null && IsPresent())
            {
                _towns = ReadTowns();
            }

            return (IReadOnlyList<TownItem>)_towns ?? NoTowns;
        }

        /// <summary>Drop what the last opening drew. One field read a frame: the menu's <c>_async</c>
        /// is the object <c>Show</c> makes for the opening it is about to draw and <c>Hide</c> clears,
        /// so it differs whenever the page's content can - a close and a reopen, another team's
        /// kingdom in hot-seat, a Hide and a Show inside one frame. Show refuses to redraw while that
        /// object is still uncompleted, so there is no refresh in place to key on as well. The
        /// language is read beside it, because the game re-localizes the drawn page where it stands
        /// and leaves that object alone.</summary>
        private void SyncOpening()
        {
            object opening = Reflect.Get<object>(_menu, AsyncField);
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            ILanguageDefinition language = localization != null ? localization.CurrentLanguage : null;
            if (ReferenceEquals(opening, _opening) && ReferenceEquals(language, _language))
            {
                return;
            }

            _opening = opening;
            _language = language;
            _towns = null;
            _title = null;
        }

        private List<TownItem> ReadTowns()
        {
            return KingdomOverviewRead.FindEntries<KingdomTroopOverviewTownEntry, TownItem>(
                (Component)_menu, BuildTown);
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
            List<RowItem> rows =
                KingdomOverviewRead.FindEntries<KingdomTroopOverviewIncomeEntry, RowItem>(entry, BuildRow);

            return new TownItem(
                entry,
                KingdomOverviewRead.ReadText(entry, TownNameTextField),
                KingdomOverviewRead.ReadText(entry, UpgradeTextField),
                () => ClickTown(entry),
                rows);
        }

        /// <summary>One recruitable troop line, or null where the game drew no name.</summary>
        private static RowItem BuildRow(KingdomTroopOverviewIncomeEntry income)
        {
            string troop = KingdomOverviewRead.ReadText(income, IncomeTextField);
            if (string.IsNullOrWhiteSpace(troop))
            {
                return null;
            }

            return new RowItem(
                income,
                income.Button,
                troop,
                KingdomOverviewRead.ReadText(income, IncomeAmountField),
                () => ClickIncome(income),
                () => KingdomOverviewRead.FocusButton(income.Button));
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

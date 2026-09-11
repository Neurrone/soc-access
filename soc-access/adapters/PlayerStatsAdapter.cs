using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PlayerStatsAdapter : IPresent
    {
        private const int OverallTabIndex = 0;
        private const int BattleTabIndex = 1;

        private static readonly FieldInfo NavigationOverallMenuField = AccessTools.Field(typeof(PlayerStatsMenuNavigation), "_overallMenu");
        private static readonly FieldInfo NavigationBattleMenuField = AccessTools.Field(typeof(PlayerStatsMenuNavigation), "_battleMenu");
        private static readonly FieldInfo NavigationCanvasGroupField = AccessTools.Field(typeof(PlayerStatsMenuNavigation), "_canvasGroup");
        private static readonly FieldInfo NavigationCurrentTabField = AccessTools.Field(typeof(PlayerStatsMenuNavigation), "_currentTab");
        private static readonly FieldInfo NavigationManagerContainerField = AccessTools.Field(typeof(PlayerStatsMenuNavigation), "_mainMenuManagerContainer");
        private static readonly FieldInfo MainMenuSettingsField = AccessTools.Field(typeof(MainMenuManager), "_settings");
        private static readonly MethodInfo HandleSwitchedTabMethod = AccessTools.Method(typeof(PlayerStatsMenuNavigation), "HandleSwitchedTab", new[] { typeof(int) });

        private static readonly FieldInfo OverallGamesPlayedField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_gamesPlayedEntry");
        private static readonly FieldInfo OverallGamesWonField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_gamesWonEntry");
        private static readonly FieldInfo OverallGamesLostField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_gamesLostEntry");
        private static readonly FieldInfo OverallHoursPlayedField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_hoursPlayed");
        private static readonly FieldInfo OverallAdventureTurnsPlayedField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_adventureTurnsPlayed");
        private static readonly FieldInfo OverallAdventureTurnsOnlinePlayedField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_adventureTurnsOnlinePlayed");
        private static readonly FieldInfo OverallArtifactsField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_artifacts");
        private static readonly FieldInfo OverallFactionEntriesField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_factionEntries");
        private static readonly FieldInfo OverallTopMapsField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_topMaps");
        private static readonly FieldInfo OverallTopWieldersField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_topWielders");
        private static readonly FieldInfo OverallWielderMaxLevelField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_wielderMaxLevel");
        private static readonly FieldInfo OverallPlayedWieldersField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_playedWielders");
        private static readonly FieldInfo OverallTopTroopsField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_topTroop");
        private static readonly FieldInfo OverallTotalUnitsField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_totalUnits");
        private static readonly FieldInfo OverallUniqueUnitsField = AccessTools.Field(typeof(PlayerStatsOverallMenu), "_uniqueUnits");

        private static readonly FieldInfo BattleBattlesPlayedField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_battlesPlayedEntry");
        private static readonly FieldInfo BattleBattlesWonField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_battlesWonEntry");
        private static readonly FieldInfo BattleBattlesLostField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_battlesLostEntry");
        private static readonly FieldInfo BattleManualBattlesField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_manualBattles");
        private static readonly FieldInfo BattleQuickBattlesField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_quickBattles");
        private static readonly FieldInfo BattleRoundsPlayedField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_battleRoundsPlayed");
        private static readonly FieldInfo BattleEnemyUnitsKilledField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_enemyUnitsKilled");
        private static readonly FieldInfo BattleUnitsLostField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_unitsLost");
        private static readonly FieldInfo BattleTotalDamageField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_totalDamage");
        private static readonly FieldInfo BattleRangedDamageField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_rangedDamage");
        private static readonly FieldInfo BattleMeleeDamageField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_meleeDamage");
        private static readonly FieldInfo BattleSpellsDamageField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_spellsDamage");
        private static readonly FieldInfo BattleTopSpellsField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_topSpells");
        private static readonly FieldInfo BattleDifferentSpellsField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_differentSpells");
        private static readonly FieldInfo BattleTotalSpellsField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_totalSpells");
        private static readonly FieldInfo BattleTopEnemyTroopsField = AccessTools.Field(typeof(PlayerStatsBattleMenu), "_topEnemyTroops");

        private static readonly FieldInfo FactionTextField = AccessTools.Field(typeof(PlayerStatsFactionEntry), "_factionText");
        private static readonly FieldInfo FactionPercentTextField = AccessTools.Field(typeof(PlayerStatsFactionEntry), "_percentText");

        private static readonly FieldInfo MapNameTextField = AccessTools.Field(typeof(PlayerStatsMapEntry), "_mapNameText");
        private static readonly FieldInfo MapDetailsTextField = AccessTools.Field(typeof(PlayerStatsMapEntry), "_mapDetailstext");
        private static readonly FieldInfo MapTimesPlayedTextField = AccessTools.Field(typeof(PlayerStatsMapEntry), "_timesPlayedText");

        private static readonly FieldInfo WielderNameField = AccessTools.Field(typeof(PlayerStatsWielderEntry), "_wielderName");
        private static readonly FieldInfo WielderFactionNameField = AccessTools.Field(typeof(PlayerStatsWielderEntry), "_factionName");
        private static readonly FieldInfo WielderTimesPlayedField = AccessTools.Field(typeof(PlayerStatsWielderEntry), "_timesPlayed");

        private static readonly FieldInfo TroopNameField = AccessTools.Field(typeof(PlayerStatsTroopEntry), "_troopName");
        private static readonly FieldInfo TroopFactionNameField = AccessTools.Field(typeof(PlayerStatsTroopEntry), "_factionName");
        private static readonly FieldInfo TroopAmountTextField = AccessTools.Field(typeof(PlayerStatsTroopEntry), "_amountText");

        private static readonly FieldInfo SpellNameField = AccessTools.Field(typeof(PlayerStatsSpellEntry), "_name");
        private static readonly FieldInfo SpellAmountField = AccessTools.Field(typeof(PlayerStatsSpellEntry), "_amount");

        private readonly PlayerStatsMenuNavigation _navigation;

        // Which mesh each label was found on, kept so the page is not walked again for it every
        // frame. Every entry is re-resolved the moment its mesh stops answering the search that
        // found it, so a kept answer can never be one the walk would not have found.
        private readonly Dictionary<int, UITextMesh> _tabLabels = new Dictionary<int, UITextMesh>();
        private readonly Dictionary<string, UITextMesh> _titleTexts = new Dictionary<string, UITextMesh>();
        private readonly Dictionary<string, UITextMesh> _prefixedTexts = new Dictionary<string, UITextMesh>();
        private readonly Dictionary<UITextMesh, string[]> _siblingLabels = new Dictionary<UITextMesh, string[]>();

        public PlayerStatsAdapter(PlayerStatsMenuNavigation navigation)
        {
            _navigation = navigation;

            MainMenuManager.Settings settings = GetMainMenuSettings();
            BackButton = settings != null
                ? new StandardMenuButtonAdapter(
                    settings.BackButton,
                    () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                    () => NativeSelectionUtility.Click(settings.BackButton))
                : null;
            OptionsButton = settings != null
                ? new OptionsMenuButtonAdapter(
                    settings.OptionsButton,
                    () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                    () => NativeSelectionUtility.Click(settings.OptionsButton))
                : null;
        }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public int SelectedTabIndex
        {
            get
            {
                object value = NavigationCurrentTabField != null ? NavigationCurrentTabField.GetValue(_navigation) : null;
                return value is int ? (int)value : OverallTabIndex;
            }
        }

        public bool IsOverallTabSelected
        {
            get { return SelectedTabIndex == OverallTabIndex; }
        }

        public string Title
        {
            get { return GameText.Get("PlayerStats/TopTitle", ModText.Get(ModStrings.Screens.PlayerStats)); }
        }

        /// <summary>The page as the game leaves it once it is workable. <c>PlayerStatsMenuNavigation</c>
        /// sets its canvas group to alpha 0 in <c>Awake</c> and fades it to 1 at the end of its
        /// <c>Start</c> coroutine, after nine end-of-frame waits, so an alpha at 1 with both tab views
        /// resolved IS the end state - it is what the deleted 600-frame poll was waiting for. The tab
        /// switch animates the two views and not this group, so the gate stays true while the player
        /// moves between the tabs.</summary>
        public bool IsPresent()
        {
            if (_navigation == null
                || !IsLoadedPlayerStatsScene()
                || !GameObjects.IsLiveSceneObject(_navigation.gameObject)
                || !_navigation.gameObject.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup canvasGroup = Reflect.Get<CanvasGroup>(_navigation, NavigationCanvasGroupField);
            return canvasGroup != null
                && canvasGroup.alpha >= 0.95f
                && GetOverallMenu() != null
                && GetBattleMenu() != null;
        }

        /// <summary>The page's two tabs, each with whatever the game drew on it - empty where the
        /// page names it nowhere the mod can read, which the screen words.</summary>
        public IReadOnlyList<TabItem> GetTabs()
        {
            return new[]
            {
                new TabItem("player-stats-tab-overall", OverallTabIndex, true, FindTabLabel(OverallTabIndex)),
                new TabItem("player-stats-tab-battle", BattleTabIndex, false, FindTabLabel(BattleTabIndex))
            };
        }

        public bool ActivateTab(int index)
        {
            if (_navigation == null || HandleSwitchedTabMethod == null)
            {
                return false;
            }

            if (index != OverallTabIndex && index != BattleTabIndex)
            {
                return false;
            }

            HandleSwitchedTabMethod.Invoke(_navigation, new object[] { index });
            return true;
        }

        public IReadOnlyList<LabeledItem> GetOverallGeneralItems()
        {
            PlayerStatsOverallMenu menu = GetOverallMenu();
            List<LabeledItem> items = new List<LabeledItem>();
            AddLabelValueItem(items, "games-played", Reflect.Get<UITextMesh>(menu, OverallGamesPlayedField));
            AddLabelValueItem(items, "games-won", Reflect.Get<UITextMesh>(menu, OverallGamesWonField));
            AddLabelValueItem(items, "games-lost", Reflect.Get<UITextMesh>(menu, OverallGamesLostField));
            AddFullTextItem(items, "hours-played", Reflect.Get<UITextMesh>(menu, OverallHoursPlayedField));
            AddFullTextItem(items, "adventure-turns-played", Reflect.Get<UITextMesh>(menu, OverallAdventureTurnsPlayedField));
            AddFullTextItem(items, "owned-artifacts", Reflect.Get<UITextMesh>(menu, OverallArtifactsField));
            AddFullTextItem(items, "online-games", Reflect.Get<UITextMesh>(menu, OverallAdventureTurnsOnlinePlayedField));
            return items.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetFactionRows()
        {
            PlayerStatsFactionEntry[] entries = Reflect.Get<PlayerStatsFactionEntry[]>(GetOverallMenu(), OverallFactionEntriesField);
            return _factionRows.Get(entries, () => ReadFactionRows(entries));
        }

        private IReadOnlyList<TableRowItem> ReadFactionRows(PlayerStatsFactionEntry[] entries)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                PlayerStatsFactionEntry entry = entries[i];
                if (!GameObjects.IsLive(entry))
                {
                    continue;
                }

                UITextMesh faction = Reflect.Get<UITextMesh>(entry, FactionTextField);
                UITextMesh percent = Reflect.Get<UITextMesh>(entry, FactionPercentTextField);
                if (string.IsNullOrWhiteSpace(GetText(faction)) && string.IsNullOrWhiteSpace(GetText(percent)))
                {
                    continue;
                }

                rows.Add(new TableRowItem(i, GetRectTransform(entry), faction, percent));
            }

            return rows.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetMapRows()
        {
            PlayerStatsMapEntry[] entries = Reflect.Get<PlayerStatsMapEntry[]>(GetOverallMenu(), OverallTopMapsField);
            return _mapRows.Get(entries, () => ReadMapRows(entries));
        }

        private IReadOnlyList<TableRowItem> ReadMapRows(PlayerStatsMapEntry[] entries)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                PlayerStatsMapEntry entry = entries[i];
                if (!GameObjects.IsLive(entry))
                {
                    continue;
                }

                UITextMesh map = Reflect.Get<UITextMesh>(entry, MapNameTextField);
                UITextMesh details = Reflect.Get<UITextMesh>(entry, MapDetailsTextField);
                UITextMesh games = Reflect.Get<UITextMesh>(entry, MapTimesPlayedTextField);
                if (string.IsNullOrWhiteSpace(GetText(map))
                    && string.IsNullOrWhiteSpace(GetText(details))
                    && string.IsNullOrWhiteSpace(GetText(games)))
                {
                    continue;
                }

                rows.Add(new TableRowItem(i, GetRectTransform(entry), map, details, games));
            }

            return rows.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetWielderRows()
        {
            PlayerStatsWielderEntry[] entries = Reflect.Get<PlayerStatsWielderEntry[]>(GetOverallMenu(), OverallTopWieldersField);
            return _wielderRows.Get(entries, () => ReadWielderRows(entries));
        }

        private IReadOnlyList<TableRowItem> ReadWielderRows(PlayerStatsWielderEntry[] entries)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                PlayerStatsWielderEntry entry = entries[i];
                if (!GameObjects.IsLive(entry))
                {
                    continue;
                }

                rows.Add(new TableRowItem(
                    i,
                    GetRectTransform(entry),
                    Reflect.Get<UITextMesh>(entry, WielderNameField),
                    Reflect.Get<UITextMesh>(entry, WielderFactionNameField),
                    Reflect.Get<UITextMesh>(entry, WielderTimesPlayedField)));
            }

            return rows.ToArray();
        }

        public string WielderSummary
        {
            get
            {
                return JoinLines(
                    GetText(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallWielderMaxLevelField)),
                    GetText(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallPlayedWieldersField)),
                    FindVisibleTextStartingWith(GetOverallMenu(), "*"));
            }
        }

        public RectTransform WielderSummaryTransform
        {
            get { return GetRectTransform(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallWielderMaxLevelField)); }
        }

        public IReadOnlyList<TableRowItem> GetTroopRows()
        {
            PlayerStatsTroopEntry[] entries = Reflect.Get<PlayerStatsTroopEntry[]>(GetOverallMenu(), OverallTopTroopsField);
            return _troopRows.Get(entries, () => ReadTroopRows(entries));
        }

        public string TroopSummary
        {
            get
            {
                return JoinLines(
                    GetText(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallTotalUnitsField)),
                    GetText(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallUniqueUnitsField)),
                    FindVisibleTextStartingWith(GetOverallMenu(), "**"));
            }
        }

        public RectTransform TroopSummaryTransform
        {
            get { return GetRectTransform(Reflect.Get<UITextMesh>(GetOverallMenu(), OverallTotalUnitsField)); }
        }

        public IReadOnlyList<LabeledItem> GetBattleGeneralItems()
        {
            PlayerStatsBattleMenu menu = GetBattleMenu();
            List<LabeledItem> items = new List<LabeledItem>();
            AddLabelValueItem(items, "battles-played", Reflect.Get<UITextMesh>(menu, BattleBattlesPlayedField));
            AddLabelValueItem(items, "battles-won", Reflect.Get<UITextMesh>(menu, BattleBattlesWonField));
            AddLabelValueItem(items, "battles-lost", Reflect.Get<UITextMesh>(menu, BattleBattlesLostField));
            AddLabelValueItem(items, "manual-battles", Reflect.Get<UITextMesh>(menu, BattleManualBattlesField));
            AddLabelValueItem(items, "quick-battles", Reflect.Get<UITextMesh>(menu, BattleQuickBattlesField));
            AddLabelValueItem(items, "battle-rounds-played", Reflect.Get<UITextMesh>(menu, BattleRoundsPlayedField));
            AddLabelValueItem(items, "enemy-units-killed", Reflect.Get<UITextMesh>(menu, BattleEnemyUnitsKilledField));
            AddLabelValueItem(items, "units-lost", Reflect.Get<UITextMesh>(menu, BattleUnitsLostField));
            AddLabelValueItem(items, "total-damage", Reflect.Get<UITextMesh>(menu, BattleTotalDamageField));
            AddLabelValueItem(items, "ranged-damage", Reflect.Get<UITextMesh>(menu, BattleRangedDamageField));
            AddLabelValueItem(items, "melee-damage", Reflect.Get<UITextMesh>(menu, BattleMeleeDamageField));
            AddLabelValueItem(items, "spells-damage", Reflect.Get<UITextMesh>(menu, BattleSpellsDamageField));
            return items.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetSpellRows()
        {
            List<PlayerStatsSpellEntry> entries = Reflect.Get<List<PlayerStatsSpellEntry>>(GetBattleMenu(), BattleTopSpellsField);
            return _spellRows.Get(entries, () => ReadSpellRows(entries));
        }

        private IReadOnlyList<TableRowItem> ReadSpellRows(List<PlayerStatsSpellEntry> entries)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                PlayerStatsSpellEntry entry = entries[i];
                if (!GameObjects.IsLive(entry))
                {
                    continue;
                }

                rows.Add(new TableRowItem(
                    i,
                    GetRectTransform(entry),
                    Reflect.Get<UITextMesh>(entry, SpellNameField),
                    Reflect.Get<UITextMesh>(entry, SpellAmountField)));
            }

            return rows.ToArray();
        }

        public string SpellSummary
        {
            get
            {
                return JoinLines(
                    GetText(Reflect.Get<UITextMesh>(GetBattleMenu(), BattleDifferentSpellsField)),
                    GetText(Reflect.Get<UITextMesh>(GetBattleMenu(), BattleTotalSpellsField)));
            }
        }

        public RectTransform SpellSummaryTransform
        {
            get { return GetRectTransform(Reflect.Get<UITextMesh>(GetBattleMenu(), BattleDifferentSpellsField)); }
        }

        public IReadOnlyList<TableRowItem> GetEnemyTroopRows()
        {
            List<PlayerStatsTroopEntry> entries = Reflect.Get<List<PlayerStatsTroopEntry>>(GetBattleMenu(), BattleTopEnemyTroopsField);
            return _enemyTroopRows.Get(
                entries,
                () => ReadTroopRows(entries != null ? entries.ToArray() : null));
        }

        public string OverallGeneralLabel
        {
            get { return ReadRequiredTitle(GetOverallMenu(), "GeneralContainer", "Title"); }
        }

        public string FactionsLabel
        {
            get { return ReadRequiredTitle(GetOverallMenu(), "FactionContainer", "Title"); }
        }

        public string TopMapsLabel
        {
            get { return ReadRequiredTitle(GetOverallMenu(), "TopMapsContainer", "Title"); }
        }

        public string TopWieldersLabel
        {
            get { return ReadRequiredTitle(GetOverallMenu(), "WieldersAndTroopsContainer", "Title"); }
        }

        public string TopTroopsLabel
        {
            get { return ReadRequiredTitle(GetOverallMenu(), "WieldersAndTroopsContainer", "Title"); }
        }

        public string BattleGeneralLabel
        {
            get { return ReadRequiredTitle(GetBattleMenu(), "BattleGeneralContainer", "Title"); }
        }

        public string SpellsLabel
        {
            get { return ReadRequiredTitle(GetBattleMenu(), "BattleSpellsContainer", "Title"); }
        }

        public string EnemyTroopsLabel
        {
            get { return ReadRequiredTitle(GetBattleMenu(), "TroopContainer", "Title"); }
        }

        public void ScrollIntoView(RectTransform source)
        {
            if (source == null)
            {
                return;
            }

            ScrollView.Reveal(FindScrollRect(), source);
        }

        private IReadOnlyList<TableRowItem> ReadTroopRows(PlayerStatsTroopEntry[] entries)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                PlayerStatsTroopEntry entry = entries[i];
                if (!GameObjects.IsLive(entry))
                {
                    continue;
                }

                rows.Add(new TableRowItem(
                    i,
                    GetRectTransform(entry),
                    Reflect.Get<UITextMesh>(entry, TroopNameField),
                    Reflect.Get<UITextMesh>(entry, TroopFactionNameField),
                    Reflect.Get<UITextMesh>(entry, TroopAmountTextField)));
            }

            return rows.ToArray();
        }

        // One memo per table. The page's tables are read whole on every build, and a row was an
        // object, a dictionary and four game-text reads each; now the rows are built only when the
        // game's own entries change, and each one reads its text when it is asked (AGENTS.md,
        // Performance).
        private readonly RowMemo _factionRows = new RowMemo();
        private readonly RowMemo _mapRows = new RowMemo();
        private readonly RowMemo _wielderRows = new RowMemo();
        private readonly RowMemo _troopRows = new RowMemo();
        private readonly RowMemo _spellRows = new RowMemo();
        private readonly RowMemo _enemyTroopRows = new RowMemo();

        /// <summary>One table's rows, kept while the game is drawing the same entries: how many of
        /// them are live, and which the first and last live ones are, all read from the game on every
        /// call.</summary>
        private sealed class RowMemo
        {
            private static readonly TableRowItem[] None = new TableRowItem[0];

            private IReadOnlyList<TableRowItem> _rows;
            private int _count = -1;
            private object _first;
            private object _last;

            public IReadOnlyList<TableRowItem> Get(System.Collections.IList entries, Func<IReadOnlyList<TableRowItem>> read)
            {
                int count = 0;
                object first = null;
                object last = null;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    Component entry = entries[i] as Component;
                    if (!GameObjects.IsLive(entry))
                    {
                        continue;
                    }

                    count++;
                    first = first ?? entry;
                    last = entry;
                }

                if (_rows != null
                    && count == _count
                    && ReferenceEquals(first, _first)
                    && ReferenceEquals(last, _last))
                {
                    return _rows;
                }

                _count = count;
                _first = first;
                _last = last;
                _rows = read() ?? None;
                return _rows;
            }
        }

        private void AddFullTextItem(List<LabeledItem> items, string id, UITextMesh text)
        {
            string label = GetText(text);
            if (!string.IsNullOrWhiteSpace(label))
            {
                items.Add(new LabeledItem(id, label, GetRectTransform(text)));
            }
        }

        private void AddLabelValueItem(List<LabeledItem> items, string id, UITextMesh valueText)
        {
            string label = BuildLabelValue(valueText);
            if (!string.IsNullOrWhiteSpace(label))
            {
                items.Add(new LabeledItem(id, label, GetRectTransform(valueText)));
            }
        }

        private string BuildLabelValue(UITextMesh valueText)
        {
            string value = GetText(valueText);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.IndexOf(':') >= 0)
            {
                return value;
            }

            string label = FindSiblingLabel(valueText, value);
            return !string.IsNullOrWhiteSpace(label)
                ? ModText.Get(ModStrings.UI.LabelValue, label, value)
                : value;
        }

        private string FindSiblingLabel(UITextMesh valueText, string value)
        {
            if (valueText == null)
            {
                return string.Empty;
            }

            // The caption beside a figure is drawn once and never redrawn, so it is remembered
            // against the figure it was found for; a figure that has changed searches again.
            string[] remembered;
            if (_siblingLabels.TryGetValue(valueText, out remembered)
                && string.Equals(remembered[0], value, StringComparison.Ordinal))
            {
                return remembered[1];
            }

            Transform current = valueText.transform;
            for (int depth = 0; current != null && depth < 4; depth++, current = current.parent)
            {
                List<string> parts = new List<string>();
                UITextMesh[] textMeshes = current.GetComponentsInChildren<UITextMesh>(false);
                for (int i = 0; i < textMeshes.Length; i++)
                {
                    UITextMesh candidate = textMeshes[i];
                    if (candidate == null || ReferenceEquals(candidate, valueText) || !candidate.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    string text = GetText(candidate);
                    if (string.IsNullOrWhiteSpace(text) || string.Equals(text, value, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!parts.Contains(text))
                    {
                        parts.Add(text);
                    }
                }

                if (parts.Count > 0 && parts.Count <= 3)
                {
                    string found = string.Join(" ", parts.ToArray());
                    _siblingLabels[valueText] = new[] { value, found };
                    return found;
                }
            }

            _siblingLabels[valueText] = new[] { value, string.Empty };
            return string.Empty;
        }

        private string ReadRequiredTitle(Component root, params string[] path)
        {
            if (root == null)
            {
                SocAccessMod.Instance?.LogWarning("PlayerStats title lookup failed because the root component is null.");
                return string.Empty;
            }

            // The Find chain, and the warnings a missing one writes, run once per container rather
            // than once a frame. The text is still read live off the mesh they settle on.
            string key = root.GetInstanceID() + "/" + string.Join("/", path);
            UITextMesh kept;
            if (_titleTexts.TryGetValue(key, out kept))
            {
                return kept == null ? string.Empty : GetText(kept);
            }

            Transform current = root.transform;
            for (int i = 0; i < path.Length; i++)
            {
                current = current != null ? current.Find(path[i]) : null;
                if (current == null)
                {
                    SocAccessMod.Instance?.LogWarning(
                        "PlayerStats title lookup failed under "
                        + root.GetType().Name
                        + " at "
                        + string.Join("/", path)
                        + ".");
                    _titleTexts[key] = null;
                    return string.Empty;
                }
            }

            UITextMesh titleText = current.GetComponent<UITextMesh>();
            _titleTexts[key] = titleText;
            string text = GetText(titleText);
            if (string.IsNullOrWhiteSpace(text))
            {
                SocAccessMod.Instance?.LogWarning(
                    "PlayerStats title lookup found empty text at "
                    + root.GetType().Name
                    + "/"
                    + string.Join("/", path)
                    + ".");
            }

            return text;
        }

        private string FindVisibleTextStartingWith(Component root, string prefix)
        {
            if (root == null || string.IsNullOrEmpty(prefix))
            {
                return string.Empty;
            }

            string key = root.GetInstanceID() + "/" + prefix;
            UITextMesh kept;
            if (_prefixedTexts.TryGetValue(key, out kept))
            {
                // A page that draws no such line is remembered as drawing none: searching for it
                // again is a walk of the whole page, twice a frame, for the same answer.
                if (kept == null)
                {
                    return string.Empty;
                }

                string keptText = GetText(kept);
                if (!string.IsNullOrWhiteSpace(keptText) && keptText.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
                {
                    return keptText;
                }
            }

            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
                {
                    _prefixedTexts[key] = textMeshes[i];
                    return text;
                }
            }

            _prefixedTexts[key] = null;
            return string.Empty;
        }

        private string FindTabLabel(int index)
        {
            string expectedSuffix = index == OverallTabIndex ? "Overall" : "Battle";
            UITextMesh kept;
            if (_tabLabels.TryGetValue(index, out kept))
            {
                // The same for the tab bar, which matters most where the search cannot succeed at
                // all: the words looked for are English, so a game in another language would walk
                // the whole page for both tabs on every frame and take the fallback anyway.
                if (kept == null)
                {
                    return string.Empty;
                }

                string keptText = GetText(kept);
                if (!string.IsNullOrWhiteSpace(keptText)
                    && keptText.IndexOf(expectedSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return keptText;
                }
            }

            UITextMesh[] textMeshes = _navigation != null ? _navigation.GetComponentsInChildren<UITextMesh>(false) : new UITextMesh[0];
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && text.IndexOf(expectedSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _tabLabels[index] = textMeshes[i];
                    return text;
                }
            }

            _tabLabels[index] = null;
            return string.Empty;
        }

        private ScrollRect FindScrollRect()
        {
            if (_navigation == null)
            {
                return null;
            }

            ScrollRect scrollRect = _navigation.GetComponentInChildren<ScrollRect>(false);
            if (scrollRect != null)
            {
                return scrollRect;
            }

            Transform parent = _navigation.transform.parent;
            while (parent != null)
            {
                scrollRect = parent.GetComponentInChildren<ScrollRect>(false);
                if (scrollRect != null)
                {
                    return scrollRect;
                }

                parent = parent.parent;
            }

            return null;
        }

        private PlayerStatsOverallMenu GetOverallMenu()
        {
            return Reflect.Get<PlayerStatsOverallMenu>(_navigation, NavigationOverallMenuField);
        }

        private PlayerStatsBattleMenu GetBattleMenu()
        {
            return Reflect.Get<PlayerStatsBattleMenu>(_navigation, NavigationBattleMenuField);
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = Reflect.Get<MainMenuManagerContainer>(_navigation, NavigationManagerContainerField);
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return Reflect.Get<MainMenuManager.Settings>(manager, MainMenuSettingsField);
        }

        private static bool IsLoadedPlayerStatsScene()
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == MainMenuSceneType.PlayerStats;
        }

        private static RectTransform GetRectTransform(Component component)
        {
            return component != null ? component.GetComponent<RectTransform>() : null;
        }

        private static string GetText(UITextMesh text)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static string JoinLines(params string[] lines)
        {
            List<string> result = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    result.Add(lines[i]);
                }
            }

            return string.Join("\n", result.ToArray());
        }

        public sealed class TabItem
        {
            public TabItem(string id, int index, bool isOverall, string label)
            {
                Id = id ?? string.Empty;
                Index = index;
                IsOverall = isOverall;
                Label = label ?? string.Empty;
            }

            public string Id { get; private set; }
            public int Index { get; private set; }

            /// <summary>Which of the two pages this tab shows, so a screen that has to name it
            /// itself knows which one it is naming.</summary>
            public bool IsOverall { get; private set; }

            /// <summary>What the page drew on the tab, empty where it drew nothing readable.</summary>
            public string Label { get; private set; }
        }

        public sealed class LabeledItem
        {
            public LabeledItem(string id, string label, RectTransform sourceTransform)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                SourceTransform = sourceTransform;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public RectTransform SourceTransform { get; private set; }
        }

        /// <summary>One row of one of the page's tables: the entry's own name, the figures the game
        /// draws across it IN THE ORDER IT DRAWS THEM, and where the row sits in the table. What the
        /// figures are CALLED, and how the row's place is said, belong to the screen.</summary>
        public sealed class TableRowItem
        {
            private static readonly UITextMesh[] NoValues = new UITextMesh[0];

            private readonly UITextMesh _label;
            private readonly UITextMesh[] _values;

            public TableRowItem(int index, RectTransform sourceTransform, UITextMesh label, params UITextMesh[] values)
            {
                Index = index;
                SourceTransform = sourceTransform;
                _label = label;
                _values = values ?? NoValues;
            }

            /// <summary>Where the row sits in the table the game drew, counted from zero.</summary>
            public int Index { get; private set; }

            public RectTransform SourceTransform { get; private set; }

            /// <summary>The entry's own name, read off the game when it is asked for.</summary>
            public string Label
            {
                get { return GetText(_label); }
            }

            /// <summary>How many figures the row draws beside its name.</summary>
            public int ValueCount
            {
                get { return _values.Length; }
            }

            /// <summary>One of those figures, read off the game when it is asked for.</summary>
            public string Value(int index)
            {
                return index >= 0 && index < _values.Length ? GetText(_values[index]) : string.Empty;
            }
        }
    }
}

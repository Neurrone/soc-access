using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PlayerStatsAdapter
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

        public PlayerStatsMenuNavigation Source
        {
            get { return _navigation; }
        }

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

        public bool IsPresent()
        {
            return _navigation != null
                && IsLoadedPlayerStatsScene()
                && IsLiveSceneObject(_navigation.gameObject)
                && _navigation.gameObject.activeInHierarchy;
        }

        public bool IsReadyAfterAnimation()
        {
            CanvasGroup canvasGroup = GetField<CanvasGroup>(_navigation, NavigationCanvasGroupField);
            return IsPresent()
                && canvasGroup != null
                && canvasGroup.alpha >= 0.95f
                && GetOverallMenu() != null
                && GetBattleMenu() != null;
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            return new[]
            {
                new TabItem("player-stats-tab-overall", OverallTabIndex, FindTabLabel(OverallTabIndex, ModText.Get(ModStrings.Screens.PlayerStatsOverall))),
                new TabItem("player-stats-tab-battle", BattleTabIndex, FindTabLabel(BattleTabIndex, ModText.Get(ModStrings.Screens.PlayerStatsBattle)))
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
            AddLabelValueItem(items, "games-played", GetTextField(menu, OverallGamesPlayedField));
            AddLabelValueItem(items, "games-won", GetTextField(menu, OverallGamesWonField));
            AddLabelValueItem(items, "games-lost", GetTextField(menu, OverallGamesLostField));
            AddFullTextItem(items, "hours-played", GetTextField(menu, OverallHoursPlayedField));
            AddFullTextItem(items, "adventure-turns-played", GetTextField(menu, OverallAdventureTurnsPlayedField));
            AddFullTextItem(items, "owned-artifacts", GetTextField(menu, OverallArtifactsField));
            AddFullTextItem(items, "online-games", GetTextField(menu, OverallAdventureTurnsOnlinePlayedField));
            return items.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetFactionRows()
        {
            PlayerStatsFactionEntry[] entries = GetField<PlayerStatsFactionEntry[]>(GetOverallMenu(), OverallFactionEntriesField);
            List<TableRowItem> rows = new List<TableRowItem>();
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PlayerStatsFactionEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string faction = GetText(GetField<UITextMesh>(entry, FactionTextField));
                string percent = GetText(GetField<UITextMesh>(entry, FactionPercentTextField));
                if (string.IsNullOrWhiteSpace(faction) && string.IsNullOrWhiteSpace(percent))
                {
                    continue;
                }

                rows.Add(new TableRowItem(
                    "faction-" + i,
                    faction,
                    GetRectTransform(entry),
                    new Dictionary<string, string>
                    {
                        { "rank", "#" + (i + 1) },
                        { "faction", faction },
                        { "play-distribution", percent }
                    }));
            }

            return rows.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetMapRows()
        {
            PlayerStatsMapEntry[] entries = GetField<PlayerStatsMapEntry[]>(GetOverallMenu(), OverallTopMapsField);
            List<TableRowItem> rows = new List<TableRowItem>();
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PlayerStatsMapEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string map = GetText(GetField<UITextMesh>(entry, MapNameTextField));
                string details = GetText(GetField<UITextMesh>(entry, MapDetailsTextField));
                string games = GetText(GetField<UITextMesh>(entry, MapTimesPlayedTextField));
                if (string.IsNullOrWhiteSpace(map) && string.IsNullOrWhiteSpace(details) && string.IsNullOrWhiteSpace(games))
                {
                    continue;
                }

                rows.Add(new TableRowItem(
                    "map-" + i,
                    map,
                    GetRectTransform(entry),
                    new Dictionary<string, string>
                    {
                        { "rank", "#" + (i + 1) },
                        { "map", map },
                        { "details", details },
                        { "games", games }
                    }));
            }

            return rows.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetWielderRows()
        {
            PlayerStatsWielderEntry[] entries = GetField<PlayerStatsWielderEntry[]>(GetOverallMenu(), OverallTopWieldersField);
            List<TableRowItem> rows = new List<TableRowItem>();
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PlayerStatsWielderEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string wielder = GetText(GetField<UITextMesh>(entry, WielderNameField));
                string faction = GetText(GetField<UITextMesh>(entry, WielderFactionNameField));
                string amount = GetText(GetField<UITextMesh>(entry, WielderTimesPlayedField));
                rows.Add(new TableRowItem(
                    "wielder-" + i,
                    wielder,
                    GetRectTransform(entry),
                    new Dictionary<string, string>
                    {
                        { "rank", "#" + (i + 1) },
                        { "wielder", wielder },
                        { "faction", faction },
                        { "times-recruited", amount }
                    }));
            }

            return rows.ToArray();
        }

        public string WielderSummary
        {
            get
            {
                return JoinLines(
                    GetText(GetTextField(GetOverallMenu(), OverallWielderMaxLevelField)),
                    GetText(GetTextField(GetOverallMenu(), OverallPlayedWieldersField)),
                    FindVisibleTextStartingWith(GetOverallMenu(), "*"));
            }
        }

        public RectTransform WielderSummaryTransform
        {
            get { return GetRectTransform(GetTextField(GetOverallMenu(), OverallWielderMaxLevelField)); }
        }

        public IReadOnlyList<TableRowItem> GetTroopRows()
        {
            PlayerStatsTroopEntry[] entries = GetField<PlayerStatsTroopEntry[]>(GetOverallMenu(), OverallTopTroopsField);
            return GetTroopRows(entries, "troop", "times-trained");
        }

        public string TroopSummary
        {
            get
            {
                return JoinLines(
                    GetText(GetTextField(GetOverallMenu(), OverallTotalUnitsField)),
                    GetText(GetTextField(GetOverallMenu(), OverallUniqueUnitsField)),
                    FindVisibleTextStartingWith(GetOverallMenu(), "**"));
            }
        }

        public RectTransform TroopSummaryTransform
        {
            get { return GetRectTransform(GetTextField(GetOverallMenu(), OverallTotalUnitsField)); }
        }

        public IReadOnlyList<LabeledItem> GetBattleGeneralItems()
        {
            PlayerStatsBattleMenu menu = GetBattleMenu();
            List<LabeledItem> items = new List<LabeledItem>();
            AddLabelValueItem(items, "battles-played", GetTextField(menu, BattleBattlesPlayedField));
            AddLabelValueItem(items, "battles-won", GetTextField(menu, BattleBattlesWonField));
            AddLabelValueItem(items, "battles-lost", GetTextField(menu, BattleBattlesLostField));
            AddLabelValueItem(items, "manual-battles", GetTextField(menu, BattleManualBattlesField));
            AddLabelValueItem(items, "quick-battles", GetTextField(menu, BattleQuickBattlesField));
            AddLabelValueItem(items, "battle-rounds-played", GetTextField(menu, BattleRoundsPlayedField));
            AddLabelValueItem(items, "enemy-units-killed", GetTextField(menu, BattleEnemyUnitsKilledField));
            AddLabelValueItem(items, "units-lost", GetTextField(menu, BattleUnitsLostField));
            AddLabelValueItem(items, "total-damage", GetTextField(menu, BattleTotalDamageField));
            AddLabelValueItem(items, "ranged-damage", GetTextField(menu, BattleRangedDamageField));
            AddLabelValueItem(items, "melee-damage", GetTextField(menu, BattleMeleeDamageField));
            AddLabelValueItem(items, "spells-damage", GetTextField(menu, BattleSpellsDamageField));
            return items.ToArray();
        }

        public IReadOnlyList<TableRowItem> GetSpellRows()
        {
            List<PlayerStatsSpellEntry> entries = GetField<List<PlayerStatsSpellEntry>>(GetBattleMenu(), BattleTopSpellsField);
            List<TableRowItem> rows = new List<TableRowItem>();
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PlayerStatsSpellEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string spell = GetText(GetField<UITextMesh>(entry, SpellNameField));
                string amount = GetText(GetField<UITextMesh>(entry, SpellAmountField));
                rows.Add(new TableRowItem(
                    "spell-" + i,
                    spell,
                    GetRectTransform(entry),
                    new Dictionary<string, string>
                    {
                        { "rank", "#" + (i + 1) },
                        { "spell", spell },
                        { "times-cast", amount }
                    }));
            }

            return rows.ToArray();
        }

        public string SpellSummary
        {
            get
            {
                return JoinLines(
                    GetText(GetTextField(GetBattleMenu(), BattleDifferentSpellsField)),
                    GetText(GetTextField(GetBattleMenu(), BattleTotalSpellsField)));
            }
        }

        public RectTransform SpellSummaryTransform
        {
            get { return GetRectTransform(GetTextField(GetBattleMenu(), BattleDifferentSpellsField)); }
        }

        public IReadOnlyList<TableRowItem> GetEnemyTroopRows()
        {
            List<PlayerStatsTroopEntry> entries = GetField<List<PlayerStatsTroopEntry>>(GetBattleMenu(), BattleTopEnemyTroopsField);
            return GetTroopRows(entries != null ? entries.ToArray() : null, "enemy-troop", "kills");
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

            ScrollRect scrollRect = FindScrollRect();
            if (scrollRect == null || scrollRect.content == null)
            {
                return;
            }

            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : ((Component)scrollRect).GetComponent<RectTransform>();
            if (viewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            Bounds itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, source);
            Rect viewportRect = viewport.rect;
            float scrollableHeight = scrollRect.content.rect.height - viewportRect.height;
            if (scrollableHeight <= 0f)
            {
                return;
            }

            float normalized = scrollRect.verticalNormalizedPosition;
            if (itemBounds.max.y > viewportRect.max.y)
            {
                normalized += (itemBounds.max.y - viewportRect.max.y) / scrollableHeight;
            }
            else if (itemBounds.min.y < viewportRect.min.y)
            {
                normalized -= (viewportRect.min.y - itemBounds.min.y) / scrollableHeight;
            }
            else
            {
                return;
            }

            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        private IReadOnlyList<TableRowItem> GetTroopRows(PlayerStatsTroopEntry[] entries, string idPrefix, string amountColumnId)
        {
            List<TableRowItem> rows = new List<TableRowItem>();
            if (entries == null)
            {
                return rows;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                PlayerStatsTroopEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string troop = GetText(GetField<UITextMesh>(entry, TroopNameField));
                string faction = GetText(GetField<UITextMesh>(entry, TroopFactionNameField));
                string amount = GetText(GetField<UITextMesh>(entry, TroopAmountTextField));
                rows.Add(new TableRowItem(
                    idPrefix + "-" + i,
                    troop,
                    GetRectTransform(entry),
                    new Dictionary<string, string>
                    {
                        { "rank", "#" + (i + 1) },
                        { "troop", troop },
                        { "faction", faction },
                        { amountColumnId, amount }
                    }));
            }

            return rows.ToArray();
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

        private static string FindSiblingLabel(UITextMesh valueText, string value)
        {
            if (valueText == null)
            {
                return string.Empty;
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
                    return string.Join(" ", parts.ToArray());
                }
            }

            return string.Empty;
        }

        private static string ReadRequiredTitle(Component root, params string[] path)
        {
            if (root == null)
            {
                SocAccessMod.Instance?.LogWarning("PlayerStats title lookup failed because the root component is null.");
                return string.Empty;
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
                    return string.Empty;
                }
            }

            string text = GetText(current.GetComponent<UITextMesh>());
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

            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && text.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private string FindTabLabel(int index, string fallback)
        {
            string expectedSuffix = index == OverallTabIndex ? "Overall" : "Battle";
            UITextMesh[] textMeshes = _navigation != null ? _navigation.GetComponentsInChildren<UITextMesh>(false) : new UITextMesh[0];
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && text.IndexOf(expectedSuffix, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return text;
                }
            }

            return fallback;
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
            return GetField<PlayerStatsOverallMenu>(_navigation, NavigationOverallMenuField);
        }

        private PlayerStatsBattleMenu GetBattleMenu()
        {
            return GetField<PlayerStatsBattleMenu>(_navigation, NavigationBattleMenuField);
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = GetField<MainMenuManagerContainer>(_navigation, NavigationManagerContainerField);
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return GetField<MainMenuManager.Settings>(manager, MainMenuSettingsField);
        }

        private static bool IsLoadedPlayerStatsScene()
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == MainMenuSceneType.PlayerStats;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsComponentVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static UITextMesh GetTextField(object owner, FieldInfo field)
        {
            return GetField<UITextMesh>(owner, field);
        }

        private static RectTransform GetRectTransform(Component component)
        {
            return component != null ? component.GetComponent<RectTransform>() : null;
        }

        private static string GetText(UITextMesh text)
        {
            return CleanText(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static string CleanText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string withoutTags = Regex.Replace(value, "<.*?>", string.Empty);
            return withoutTags.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
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

        private static T FirstEntry<T>(IReadOnlyList<T> entries) where T : Component
        {
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (IsComponentVisible(entries[i]))
                {
                    return entries[i];
                }
            }

            return null;
        }

        private static T FirstEntry<T>(T[] entries) where T : Component
        {
            return FirstEntry((IReadOnlyList<T>)entries);
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class TabItem
        {
            public TabItem(string id, int index, string label)
            {
                Id = id ?? string.Empty;
                Index = index;
                Label = label ?? string.Empty;
            }

            public string Id { get; private set; }
            public int Index { get; private set; }
            public string Label { get; private set; }
        }

        internal sealed class LabeledItem
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

        internal sealed class TableRowItem
        {
            private readonly Dictionary<string, string> _values;

            public TableRowItem(string id, string label, RectTransform sourceTransform, Dictionary<string, string> values)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                SourceTransform = sourceTransform;
                _values = values ?? new Dictionary<string, string>();
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public RectTransform SourceTransform { get; private set; }

            public string GetCellValue(string columnId)
            {
                string value;
                return !string.IsNullOrWhiteSpace(columnId) && _values.TryGetValue(columnId, out value)
                    ? value
                    : string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PlayerStatsScreen : Screen
    {
        private readonly PlayerStatsAdapter _adapter;

        public PlayerStatsScreen(PlayerStatsAdapter adapter)
            : base(BuildRoot(adapter, null))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PlayerStatsMenuNavigation[] menus = Resources.FindObjectsOfTypeAll<PlayerStatsMenuNavigation>();
            for (int i = 0; i < menus.Length; i++)
            {
                PlayerStatsAdapter adapter = new PlayerStatsAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new PlayerStatsScreen(adapter);
                }
            }

            return null;
        }

        public bool Matches(PlayerStatsMenuNavigation menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.Source, menu);
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            FocusState focusState = CaptureFocusState();
            RootWidget = BuildRoot(_adapter, focusState);
            if (focusState != null && !string.IsNullOrWhiteSpace(focusState.RootChildId))
            {
                RootWidget?.SetFocusedChildById(focusState.RootChildId);
            }

            UIManager.RequestFocus(RootWidget);
        }

        private FocusState CaptureFocusState()
        {
            Widget focusedChild = RootWidget != null ? RootWidget.FocusedChild : null;
            MenuWidget menu = focusedChild as MenuWidget;
            MenuItemWidget menuItem = menu != null ? menu.FocusedItem : null;
            TableWidget table = focusedChild as TableWidget;
            return new FocusState(
                focusedChild != null ? focusedChild.Id : null,
                menuItem != null ? menuItem.Id : null,
                table != null,
                table != null ? table.FocusedRowIndex : 0,
                table != null ? table.FocusedColumnIndex : 0);
        }

        private static ContainerWidget BuildRoot(PlayerStatsAdapter adapter, FocusState focusState)
        {
            ContainerWidget root = new ContainerWidget("player-stats-screen", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(BuildTabs(adapter, focusState));
            if (adapter.IsOverallTabSelected)
            {
                AddOverallContent(root, adapter, focusState);
            }
            else
            {
                AddBattleContent(root, adapter, focusState);
            }

            AddOptionalButton(root, "options", adapter.OptionsButton);
            AddOptionalButton(root, "back", adapter.BackButton);
            return root;
        }

        private static MenuWidget BuildTabs(PlayerStatsAdapter adapter, FocusState focusState)
        {
            MenuWidget menu = new MenuWidget(
                "player-stats-tabs",
                ModText.Get(ModStrings.Screens.NamedTabs, adapter.Title, ModText.Get(ModStrings.Screens.Tabs)));
            IReadOnlyList<PlayerStatsAdapter.TabItem> tabs = adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                PlayerStatsAdapter.TabItem tab = tabs[i];
                PlayerStatsAdapter.TabItem captured = tab;
                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    () => adapter.SelectedTabIndex == captured.Index ? ModText.Get(ModStrings.UI.Selected) : string.Empty,
                    () =>
                    {
                        if (!adapter.ActivateTab(captured.Index))
                        {
                            return false;
                        }

                        SocAccessPlugin.Instance?.ScreenDetector?.OnPlayerStatsChanged();
                        return true;
                    },
                    null,
                    () => true));
            }

            if (focusState != null
                && focusState.RootChildId == "player-stats-tabs"
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }
            else
            {
                menu.SetFocusedItemById(adapter.SelectedTabIndex == 1 ? "player-stats-tab-battle" : "player-stats-tab-overall");
            }

            return menu;
        }

        private static void AddOverallContent(ContainerWidget root, PlayerStatsAdapter adapter, FocusState focusState)
        {
            root.AddChild(BuildMenu(
                "player-stats-overall-general",
                adapter.OverallGeneralLabel,
                adapter.GetOverallGeneralItems(),
                adapter,
                focusState));

            root.AddChild(BuildTable(
                "player-stats-factions",
                adapter.FactionsLabel,
                BuildFactionColumns(),
                adapter.GetFactionRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));

            root.AddChild(BuildTable(
                "player-stats-maps",
                adapter.TopMapsLabel,
                BuildMapColumns(),
                adapter.GetMapRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));

            root.AddChild(BuildTable(
                "player-stats-wielders",
                adapter.TopWieldersLabel,
                BuildWielderColumns(),
                adapter.GetWielderRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));

            AddText(root, "player-stats-wielder-summary", () => adapter.WielderSummary, () => adapter.ScrollIntoView(adapter.WielderSummaryTransform));

            root.AddChild(BuildTable(
                "player-stats-troops",
                adapter.TopTroopsLabel,
                BuildTroopColumns(),
                adapter.GetTroopRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));

            AddText(root, "player-stats-troop-summary", () => adapter.TroopSummary, () => adapter.ScrollIntoView(adapter.TroopSummaryTransform));
        }

        private static void AddBattleContent(ContainerWidget root, PlayerStatsAdapter adapter, FocusState focusState)
        {
            root.AddChild(BuildMenu(
                "player-stats-battle-general",
                adapter.BattleGeneralLabel,
                adapter.GetBattleGeneralItems(),
                adapter,
                focusState));

            root.AddChild(BuildTable(
                "player-stats-spells",
                adapter.SpellsLabel,
                BuildSpellColumns(),
                adapter.GetSpellRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));

            AddText(root, "player-stats-spell-summary", () => adapter.SpellSummary, () => adapter.ScrollIntoView(adapter.SpellSummaryTransform));

            root.AddChild(BuildTable(
                "player-stats-enemy-troops",
                adapter.EnemyTroopsLabel,
                BuildEnemyTroopColumns(),
                adapter.GetEnemyTroopRows(),
                adapter,
                focusState,
                defaultColumnIndex: 1));
        }

        private static MenuWidget BuildMenu(
            string id,
            string label,
            IReadOnlyList<PlayerStatsAdapter.LabeledItem> items,
            PlayerStatsAdapter adapter,
            FocusState focusState)
        {
            MenuWidget menu = new MenuWidget(id, label);
            for (int i = 0; i < items.Count; i++)
            {
                PlayerStatsAdapter.LabeledItem item = items[i];
                PlayerStatsAdapter.LabeledItem captured = item;
                menu.AddItem(new MenuItemWidget(
                    id + "-" + captured.Id,
                    () => captured.Label,
                    null,
                    () => false,
                    () => adapter.ScrollIntoView(captured.SourceTransform),
                    () => true));
            }

            if (focusState != null
                && focusState.RootChildId == id
                && !string.IsNullOrWhiteSpace(focusState.MenuItemId))
            {
                menu.SetFocusedItemById(focusState.MenuItemId);
            }

            return menu;
        }

        private static TableWidget BuildTable(
            string id,
            string label,
            IEnumerable<TableWidget.Column> columns,
            IReadOnlyList<PlayerStatsAdapter.TableRowItem> rows,
            PlayerStatsAdapter adapter,
            FocusState focusState,
            int defaultColumnIndex)
        {
            TableWidget table = new TableWidget(
                id,
                label,
                columns,
                BuildRows(rows, adapter));

            if (focusState != null && focusState.RootChildId == id && focusState.HasTableFocus)
            {
                table.SetFocusedCell(focusState.TableRowIndex, focusState.TableColumnIndex);
            }
            else
            {
                table.SetFocusedColumn(defaultColumnIndex);
            }

            return table;
        }

        private static IReadOnlyList<TableWidget.Row> BuildRows(IReadOnlyList<PlayerStatsAdapter.TableRowItem> rows, PlayerStatsAdapter adapter)
        {
            List<TableWidget.Row> result = new List<TableWidget.Row>();
            if (rows == null)
            {
                return result;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                PlayerStatsAdapter.TableRowItem row = rows[i];
                PlayerStatsAdapter.TableRowItem captured = row;
                result.Add(new TableWidget.Row(
                    captured.Id,
                    captured.Label,
                    columnId => captured.GetCellValue(columnId),
                    null,
                    () => adapter.ScrollIntoView(captured.SourceTransform),
                    null));
            }

            return result;
        }

        private static IEnumerable<TableWidget.Column> BuildFactionColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("faction", ModText.Get(ModStrings.UI.ColumnFaction));
            yield return Column("play-distribution", ModText.Get(ModStrings.UI.ColumnPlayDistribution));
        }

        private static IEnumerable<TableWidget.Column> BuildMapColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("map", ModText.Get(ModStrings.UI.ColumnMap));
            yield return Column("details", ModText.Get(ModStrings.UI.ColumnDetails));
            yield return Column("games", ModText.Get(ModStrings.UI.ColumnGames));
        }

        private static IEnumerable<TableWidget.Column> BuildWielderColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("wielder", ModText.Get(ModStrings.UI.ColumnWielder));
            yield return Column("faction", ModText.Get(ModStrings.UI.ColumnFaction));
            yield return Column("times-recruited", ModText.Get(ModStrings.UI.ColumnTimesRecruited));
        }

        private static IEnumerable<TableWidget.Column> BuildTroopColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("troop", ModText.Get(ModStrings.UI.ColumnTroop));
            yield return Column("faction", ModText.Get(ModStrings.UI.ColumnFaction));
            yield return Column("times-trained", ModText.Get(ModStrings.UI.ColumnTimesTrained));
        }

        private static IEnumerable<TableWidget.Column> BuildSpellColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("spell", ModText.Get(ModStrings.UI.ColumnSpell));
            yield return Column("times-cast", ModText.Get(ModStrings.UI.ColumnTimesCast));
        }

        private static IEnumerable<TableWidget.Column> BuildEnemyTroopColumns()
        {
            yield return Column("rank", ModText.Get(ModStrings.UI.ColumnRank));
            yield return Column("troop", ModText.Get(ModStrings.UI.ColumnTroop));
            yield return Column("faction", ModText.Get(ModStrings.UI.ColumnFaction));
            yield return Column("kills", ModText.Get(ModStrings.UI.ColumnKills));
        }

        private static TableWidget.Column Column(string id, string label)
        {
            return new TableWidget.Column(id, label, null, null);
        }

        private static void AddText(ContainerWidget root, string id, Func<string> getText, Action onFocus)
        {
            root.AddChild(new TextWidget(
                id,
                getText,
                onFocus,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(getText())));
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                "player-stats-" + id,
                button.GetLabel,
                button.Activate,
                () => FocusNativeButton(button.Button),
                button.IsEnabled,
                button.IsVisible));
        }

        private static void FocusNativeButton(UIButton button)
        {
            if (button != null)
            {
                NativeSelectionUtility.Select(button);
            }
        }

        private sealed class FocusState
        {
            public FocusState(string rootChildId, string menuItemId, bool hasTableFocus, int tableRowIndex, int tableColumnIndex)
            {
                RootChildId = rootChildId;
                MenuItemId = menuItemId;
                HasTableFocus = hasTableFocus;
                TableRowIndex = tableRowIndex;
                TableColumnIndex = tableColumnIndex;
            }

            public string RootChildId { get; private set; }
            public string MenuItemId { get; private set; }
            public bool HasTableFocus { get; private set; }
            public int TableRowIndex { get; private set; }
            public int TableColumnIndex { get; private set; }
        }
    }
}

using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Skills;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CommanderSheetScreen : Screen
    {
        private readonly CommanderSheetAdapter _adapter;
        private Action<int, bool> _artifactChangedHandler;
        private Action<int> _statisticsChangedHandler;
        private Action<ICommanderState, SkillReference> _skillAddedHandler;

        public CommanderSheetScreen(CommanderSheetAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommanderSheet[] sheets = Resources.FindObjectsOfTypeAll<CommanderSheet>();
            for (int i = 0; i < sheets.Length; i++)
            {
                CommanderSheetAdapter adapter = new CommanderSheetAdapter(sheets[i]);
                if (adapter.IsPresent())
                {
                    return new CommanderSheetScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            AttachListeners();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
            DetachListeners();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            InventoryGridFocus inventoryGridFocus = CaptureInventoryGridFocus();
            RootWidget = BuildRoot(_adapter);
            RestoreInventoryGridFocus(focusedIndex, inventoryGridFocus);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private InventoryGridFocus CaptureInventoryGridFocus()
        {
            InventoryGridWidget grid = RootWidget != null ? RootWidget.FocusedChild as InventoryGridWidget : null;
            return grid != null
                ? new InventoryGridFocus(grid.FocusedColumnIndex, grid.FocusedRowIndex)
                : null;
        }

        private void RestoreInventoryGridFocus(int rootChildIndex, InventoryGridFocus focus)
        {
            if (focus == null || RootWidget == null)
            {
                return;
            }

            InventoryGridWidget grid = RootWidget.GetChildAt(rootChildIndex) as InventoryGridWidget;
            grid?.SetFocusedCell(focus.ColumnIndex, focus.RowIndex);
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            _artifactChangedHandler = HandleArtifactChanged;
            _statisticsChangedHandler = HandleStatisticsChanged;
            _skillAddedHandler = HandleSkillAdded;
            commands.OnArtifactChanged = (Action<int, bool>)Delegate.Combine(commands.OnArtifactChanged, _artifactChangedHandler);
            commands.OnCommanderStatisticsChanged = (Action<int>)Delegate.Combine(commands.OnCommanderStatisticsChanged, _statisticsChangedHandler);
            commands.OnCommanderSkillAdded = (Action<ICommanderState, SkillReference>)Delegate.Combine(commands.OnCommanderSkillAdded, _skillAddedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            if (_artifactChangedHandler != null)
            {
                commands.OnArtifactChanged = (Action<int, bool>)Delegate.Remove(commands.OnArtifactChanged, _artifactChangedHandler);
                _artifactChangedHandler = null;
            }

            if (_statisticsChangedHandler != null)
            {
                commands.OnCommanderStatisticsChanged = (Action<int>)Delegate.Remove(commands.OnCommanderStatisticsChanged, _statisticsChangedHandler);
                _statisticsChangedHandler = null;
            }

            if (_skillAddedHandler != null)
            {
                commands.OnCommanderSkillAdded = (Action<ICommanderState, SkillReference>)Delegate.Remove(commands.OnCommanderSkillAdded, _skillAddedHandler);
                _skillAddedHandler = null;
            }
        }

        private void HandleArtifactChanged(int artifactId, bool isNewArtifact)
        {
            RequestDetectorRefresh();
        }

        private void HandleStatisticsChanged(int commanderId)
        {
            if (_adapter != null && commanderId == _adapter.CommanderId)
            {
                RequestDetectorRefresh();
            }
        }

        private void HandleSkillAdded(ICommanderState commander, SkillReference skill)
        {
            if (_adapter != null && commander != null && commander.Id == _adapter.CommanderId)
            {
                RequestDetectorRefresh();
            }
        }

        private void RequestDetectorRefresh()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCommanderSheetChanged();
        }

        private static ContainerWidget BuildRoot(CommanderSheetAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("commander-sheet-screen", "Character sheet");
            if (adapter == null)
            {
                return root;
            }

            if (adapter.IsTutorialButtonVisible())
            {
                root.AddChild(new ButtonWidget(
                    "commander-sheet-tutorial",
                    adapter.GetTutorialButtonLabel(),
                    adapter.ActivateTutorial,
                    adapter.HideNativeTooltip,
                    adapter.IsTutorialButtonVisible,
                    adapter.IsTutorialButtonVisible));
            }

            root.AddChild(new TextWidget(
                "commander-sheet-identity",
                adapter.GetCommanderIdentity,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            IReadOnlyList<CommanderSheetAdapter.LabeledItem> stats = GetItemsSafely("Stats", adapter.GetStats);
            root.AddChild(BuildMenu("commander-sheet-stats", "Stats", stats));

            IReadOnlyList<CommanderSheetAdapter.LabeledItem> specializations = GetItemsSafely("Specializations", adapter.GetSpecializations);
            root.AddChild(BuildMenu("commander-sheet-specializations", "Specializations", specializations, adapter.HideNativeTooltip));

            root.AddChild(BuildModifierCategoryMenu(adapter));

            string activeModifierLabel = adapter.GetActiveModifierListLabel();
            IReadOnlyList<CommanderSheetAdapter.LabeledItem> activeModifiers = GetItemsSafely(activeModifierLabel, adapter.GetActiveModifiers);
            root.AddChild(BuildMenu("commander-sheet-active-modifiers", activeModifierLabel, activeModifiers, adapter.HideNativeTooltip));

            root.AddChild(new InventoryGridWidget(
                "commander-sheet-inventory-grid",
                BuildInventoryGridColumns(adapter),
                adapter.DropInventoryArtifact));

            IReadOnlyList<CommanderSheetAdapter.LabeledItem> skills = GetItemsSafely("Skills", () => adapter.GetSkills(powers: false));
            root.AddChild(BuildMenu("commander-sheet-skills", "Skills", skills));

            IReadOnlyList<CommanderSheetAdapter.LabeledItem> powers = GetItemsSafely("Powers", () => adapter.GetSkills(powers: true));
            root.AddChild(BuildMenu("commander-sheet-powers", "Powers", powers, adapter.HideNativeTooltip));

            root.AddChild(new ButtonWidget(
                "commander-sheet-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));
            return root;
        }

        private static IReadOnlyList<InventoryGridWidget.Column> BuildInventoryGridColumns(CommanderSheetAdapter adapter)
        {
            return new[]
            {
                new InventoryGridWidget.Column(
                    "commander-sheet-inventory-equipped",
                    adapter.EquipmentLabel,
                    BuildInventoryCells("equipment", adapter.GetEquipmentSlots(), includeOwnerName: false)),
                new InventoryGridWidget.Column(
                    "commander-sheet-inventory-backpack",
                    adapter.InventoryLabel,
                    BuildInventoryCells("inventory", adapter.GetBackpackSlots(), includeOwnerName: false))
            };
        }

        private static IReadOnlyList<InventoryGridWidget.Cell> BuildInventoryCells(
            string idPrefix,
            IReadOnlyList<InventorySlotInfo> slots,
            bool includeOwnerName)
        {
            List<InventoryGridWidget.Cell> cells = new List<InventoryGridWidget.Cell>();
            if (slots == null)
            {
                return cells;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlotInfo slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                cells.Add(new InventoryGridWidget.Cell(
                    idPrefix + "-" + i,
                    BuildInventorySlotLabel(slot, includeOwnerName),
                    slot));
            }

            return cells;
        }

        private static string BuildInventorySlotLabel(InventorySlotInfo slot, bool includeOwnerName)
        {
            string name = !slot.IsEmpty ? slot.ArtifactName : "empty";
            string location = slot.IsBackpackSlot
                ? slot.InventoryName + " slot " + (slot.PositionIndex + 1)
                : slot.SlotName;
            string ownerName = includeOwnerName ? slot.OwnerName : string.Empty;
            return MenuButtonTextUtility.JoinParts(name, location, ownerName);
        }

        private static IReadOnlyList<CommanderSheetAdapter.LabeledItem> GetItemsSafely(
            string section,
            Func<IReadOnlyList<CommanderSheetAdapter.LabeledItem>> getter)
        {
            try
            {
                IReadOnlyList<CommanderSheetAdapter.LabeledItem> items = getter != null ? getter() : null;
                return items ?? new CommanderSheetAdapter.LabeledItem[0];
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("CommanderSheetScreen section " + section + " failed to build: " + ex);
                return new CommanderSheetAdapter.LabeledItem[]
                {
                    new CommanderSheetAdapter.LabeledItem(section.ToLowerInvariant() + "-error", "Unavailable")
                };
            }
        }

        private static MenuWidget BuildModifierCategoryMenu(CommanderSheetAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("commander-sheet-modifier-tabs", "Modifier category tabs");
            string activeId = null;
            foreach (CommanderSheetAdapter.ModifierCategory category in adapter.GetModifierCategories())
            {
                CommanderSheetAdapter.ModifierCategory captured = category;
                if (captured.Index == adapter.GetActiveModifierCategoryIndex())
                {
                    activeId = captured.Id;
                }

                menu.AddItem(new MenuItemWidget(
                    captured.Id,
                    () => captured.Label,
                    null,
                    () => adapter.FocusModifierCategory(captured.Index),
                    () => adapter.FocusModifierCategory(captured.Index),
                    () => true,
                    captured.Tooltip));
            }

            menu.SetFocusedItemById(activeId);
            return menu;
        }

        private static MenuWidget BuildMenu(
            string id,
            string label,
            System.Collections.Generic.IReadOnlyList<CommanderSheetAdapter.LabeledItem> items,
            Action emptyItemFocus = null)
        {
            MenuWidget menu = new MenuWidget(id, label);
            if (items == null || items.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    id + "-none",
                    () => "None",
                    null,
                    () => false,
                    emptyItemFocus,
                    () => true));
                return menu;
            }

            for (int i = 0; i < items.Count; i++)
            {
                CommanderSheetAdapter.LabeledItem item = items[i];
                menu.AddItem(new MenuItemWidget(
                    item.Id,
                    () => item.Label,
                    () => item.Status,
                    item.Activate ?? (() => false),
                    item.OnFocus ?? emptyItemFocus,
                    () => true,
                    item.Tooltip));
            }

            return menu;
        }

        private sealed class InventoryGridFocus
        {
            public InventoryGridFocus(int columnIndex, int rowIndex)
            {
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
            }

            public int ColumnIndex { get; private set; }
            public int RowIndex { get; private set; }
        }
    }
}

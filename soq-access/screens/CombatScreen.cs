using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;
using System;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatScreen : Screen
    {
        private const int GridIndex = 0;
        private const int TimelineIndex = 7;
        private readonly CombatAdapter _adapter;
        private readonly CombatHexGrid _grid;
        private Action<TroopAbilityTargeting> _abilityTargetingBeginHandler;

        public CombatScreen(CombatAdapter adapter)
            : this(adapter, new CombatHexGrid(adapter))
        {
        }

        private CombatScreen(CombatAdapter adapter, CombatHexGrid grid)
            : base(BuildRoot(adapter, grid))
        {
            _adapter = adapter;
            _grid = grid;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public CombatAdapter Adapter
        {
            get { return _adapter; }
        }

        public override void OnPush()
        {
            AccessibilityEventBus.Subscribe(HandleAccessibilityEvent);
            _adapter?.AttachSpellCastBegin(HandleSpellCastBegin);
            _adapter?.AttachSpellTargetingNarration();
            _abilityTargetingBeginHandler = HandleAbilityTargetingBegin;
            _adapter?.AttachAbilityTargetingBegin(_abilityTargetingBeginHandler);
            _adapter?.AttachAbilityTargetingEnd(HandleAbilityTargetingEnd);
            _adapter?.AnnounceVisibleSpellTargetInstruction();
        }

        public void MoveCursorToLocalActingTroop(int troopId)
        {
            MoveCursorToTroop(troopId, focusGrid: true, requireLocalCurrentTurn: true);
        }

        public bool MoveCursorToTroop(int troopId, bool focusGrid, bool requireLocalCurrentTurn)
        {
            Vector2Int position;
            if (_adapter == null
                || _grid == null
                || !_adapter.TryGetTroopPosition(troopId, out position, requireLocalCurrentTurn))
            {
                return false;
            }

            if (focusGrid)
            {
                RootWidget?.SetFocusByIndexSilently(GridIndex);
            }

            bool moved = _grid.MoveToTroop(position);
            if (focusGrid)
            {
                FocusGridIfNeeded();
            }

            return moved;
        }

        public void FocusGridIfNeeded()
        {
            if (ReferenceEquals(RootWidget?.FocusedChild, _grid) && ReferenceEquals(UIManager.CurrentWidget, _grid))
            {
                return;
            }

            RootWidget?.SetFocusByIndex(GridIndex);
        }

        public bool FocusTimeline()
        {
            Widget timeline = RootWidget?.GetChildAt(TimelineIndex);
            if (timeline == null || !timeline.IsVisible)
            {
                return true;
            }

            return RootWidget.SetFocusByIndex(TimelineIndex);
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            _adapter?.DetachSpellCastBegin();
            _adapter?.DetachSpellTargetingNarration();
            _adapter?.DetachAbilityTargetingBegin(_abilityTargetingBeginHandler);
            _adapter?.DetachAbilityTargetingEnd();
            _abilityTargetingBeginHandler = null;
            _adapter?.Hud.ClearSpellTargetInstructionText();
            _adapter?.Hud.ClearAbilityTargetInstructionText();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private void HandleSpellCastBegin()
        {
            bool wasGridFocused = ReferenceEquals(RootWidget?.FocusedChild, _grid) && ReferenceEquals(UIManager.CurrentWidget, _grid);
            if (!wasGridFocused)
            {
                RootWidget?.SetFocusByIndexSilently(GridIndex);
            }

            _grid?.HandleTargetingBegin();
            FocusGridIfNeeded();
        }

        private void HandleAbilityTargetingBegin(TroopAbilityTargeting targeting)
        {
            bool wasGridFocused = ReferenceEquals(RootWidget?.FocusedChild, _grid) && ReferenceEquals(UIManager.CurrentWidget, _grid);
            if (!wasGridFocused)
            {
                RootWidget?.SetFocusByIndexSilently(GridIndex);
            }

            _grid?.HandleTargetingBegin();
            FocusGridIfNeeded();

            string instruction = _adapter != null ? _adapter.BuildAbilityTargetInstruction(targeting) : string.Empty;
            _adapter?.Hud.SetAbilityTargetInstructionText(instruction);
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                SpeechPipeline.Output(new SpeechRequest(instruction, interrupt: false));
            }
        }

        private void HandleAbilityTargetingEnd()
        {
            _adapter?.Hud.ClearAbilityTargetInstructionText();
        }

        private void HandleAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null && !hudVisibility.IsVisible)
            {
                FocusGridIfNeeded();
            }
        }

        private static ContainerWidget BuildRoot(CombatAdapter adapter, CombatHexGrid grid)
        {
            ContainerWidget root = new ContainerWidget("combat-screen", "Combat");
            root.AddChild(grid);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "combat-targeting-instruction",
                () => adapter.Hud.TargetingInstructionText,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.Hud.IsTargetingInstructionVisible));
            root.AddChild(new ButtonWidget(
                "combat-cancel-spell",
                () => adapter.Hud.CancelSpellButtonLabel,
                adapter.Hud.ClickCancelSpellButton,
                adapter.Hud.FocusCancelSpellButton,
                adapter.Hud.IsCancelSpellButtonEnabled,
                adapter.Hud.IsCancelSpellButtonVisible,
                () => adapter.Hud.CancelSpellButtonTooltip));
            root.AddChild(new ButtonWidget(
                "combat-cancel-ability",
                () => adapter.Hud.CancelAbilityButtonLabel,
                () => ActivateCancelAbilityButton(adapter),
                adapter.Hud.FocusCancelAbilityButton,
                adapter.Hud.IsCancelAbilityButtonEnabled,
                adapter.Hud.IsCancelAbilityButtonVisible,
                () => adapter.Hud.CancelAbilityButtonTooltip));
            root.AddChild(BuildQuickbarMenu(adapter));
            root.AddChild(new TextWidget(
                "combat-current-troop",
                () => adapter.Hud.CurrentTroopLabel,
                null,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.Hud.IsCurrentTroopIndicatorVisible));
            root.AddChild(new ButtonWidget(
                "combat-current-troop-ability",
                () => adapter.Hud.AbilityButtonLabel,
                () => ActivateAbilityButton(adapter),
                adapter.Hud.FocusAbilityButton,
                adapter.Hud.IsAbilityButtonEnabled,
                adapter.Hud.IsAbilityButtonVisible,
                () => adapter.Hud.AbilityButtonTooltip));
            root.AddChild(BuildQueueMenu(adapter));
            root.AddChild(Portrait.StaticNative(
                "combat-attacker-portrait",
                () => adapter.Hud.Commanders.GetPortraitLabel(CombatHudSide.Attacker),
                () => adapter.Hud.Commanders.GetPortraitButton(CombatHudSide.Attacker),
                adapter.Hud.Commanders.Localization,
                isVisible: () => adapter.Hud.Commanders.IsPortraitVisible(CombatHudSide.Attacker)));
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Attacker, "combat-attacker-essence", "Attacker essence"));
            root.AddChild(BuildAiControlButton(adapter, CombatHudSide.Attacker, "combat-attacker-ai-control"));
            root.AddChild(Portrait.StaticNative(
                "combat-defender-portrait",
                () => adapter.Hud.Commanders.GetPortraitLabel(CombatHudSide.Defender),
                () => adapter.Hud.Commanders.GetPortraitButton(CombatHudSide.Defender),
                adapter.Hud.Commanders.Localization,
                isVisible: () => adapter.Hud.Commanders.IsPortraitVisible(CombatHudSide.Defender)));
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Defender, "combat-defender-essence", "Defender essence"));
            root.AddChild(BuildAiControlButton(adapter, CombatHudSide.Defender, "combat-defender-ai-control"));
            root.AddChild(new ButtonWidget(
                "combat-spellbook",
                () => adapter.Hud.SpellbookButtonLabel,
                adapter.Hud.ClickSpellbookButton,
                adapter.Hud.FocusSpellbookButton,
                adapter.Hud.IsSpellbookButtonEnabled,
                adapter.Hud.IsSpellbookButtonVisible,
                () => adapter.Hud.SpellbookButtonTooltip));
            root.AddChild(new ButtonWidget(
                "combat-end-turn",
                () => adapter.Hud.EndTurnButtonLabel,
                adapter.Hud.ClickEndTurnButton,
                adapter.Hud.FocusEndTurnButton,
                adapter.Hud.IsEndTurnButtonEnabled,
                adapter.Hud.IsEndTurnButtonVisible,
                () => adapter.Hud.EndTurnButtonTooltip));
            root.AddChild(BuildBattleLogMenu(adapter));
            return root;
        }

        private static MenuWidget BuildQuickbarMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-quickbar", "Quickbar", adapter.Hud.IsQuickbarMenuVisible);
            int slotCount = SafeGetCount("quickbar slots", adapter.Hud.GetQuickbarSlotCount);
            for (int i = 0; i < slotCount; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "combat-quickbar-" + capturedIndex,
                    () => GetQuickbarItem(adapter, capturedIndex)?.Label,
                    null,
                    () => ActivateQuickbarItem(adapter, capturedIndex),
                    () => GetQuickbarItem(adapter, capturedIndex)?.Focus(),
                    () => GetQuickbarItem(adapter, capturedIndex)?.IsVisible ?? false,
                    () => GetQuickbarItem(adapter, capturedIndex)?.Tooltip,
                    () => GetQuickbarItem(adapter, capturedIndex)?.Unfocus(),
                    () => GetQuickbarItem(adapter, capturedIndex)?.IsEnabled ?? false));
            }

            return menu;
        }

        private static BattleHudAdapter.QuickbarItem GetQuickbarItem(CombatAdapter adapter, int index)
        {
            return adapter != null ? adapter.Hud.GetQuickbarItem(index) : null;
        }

        private static bool ActivateQuickbarItem(CombatAdapter adapter, int index)
        {
            BattleHudAdapter.QuickbarItem item = GetQuickbarItem(adapter, index);
            bool activated = item != null && item.Activate();
            CombatScreen screen = SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            screen?.FocusGridIfNeeded();
            return activated;
        }

        private static bool ActivateAbilityButton(CombatAdapter adapter)
        {
            bool activated = adapter != null && adapter.Hud.ClickAbilityButton();
            CombatScreen screen = SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            if (adapter != null && adapter.GetTargetingMode() == CombatTargetingMode.Ability)
            {
                screen?.FocusGridIfNeeded();
            }

            return activated;
        }

        private static bool ActivateCancelAbilityButton(CombatAdapter adapter)
        {
            bool activated = adapter != null && adapter.Hud.ClickCancelAbilityButton();
            CombatScreen screen = SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            screen?.FocusGridIfNeeded();
            return activated;
        }

        private static MenuWidget BuildQueueMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-queue", "Turn order", adapter.Hud.IsQueueMenuVisible);
            const int MaxQueueItems = 32;
            for (int i = 0; i < MaxQueueItems; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "combat-queue-" + capturedIndex,
                    () => GetQueueItem(adapter, capturedIndex)?.Label,
                    null,
                    () => ActivateQueueItem(adapter, capturedIndex),
                    () => GetQueueItem(adapter, capturedIndex)?.Focus(),
                    () => GetQueueItem(adapter, capturedIndex)?.IsVisible ?? false,
                    () => GetQueueItem(adapter, capturedIndex)?.Tooltip,
                    () => GetQueueItem(adapter, capturedIndex)?.Unfocus()));
            }

            return menu;
        }

        private static BattleHudAdapter.QueueItem GetQueueItem(CombatAdapter adapter, int index)
        {
            return adapter != null ? adapter.Hud.GetQueueItem(index) : null;
        }

        private static bool ActivateQueueItem(CombatAdapter adapter, int index)
        {
            BattleHudAdapter.QueueItem item = GetQueueItem(adapter, index);
            if (item == null || item.IsRoundMarker)
            {
                return false;
            }

            CombatScreen screen = SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            return screen != null && screen.MoveCursorToTroop(item.TroopId, focusGrid: true, requireLocalCurrentTurn: false);
        }

        private static MenuWidget BuildBattleLogMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-battle-log", "Battle log", adapter.Hud.IsBattleLogMenuVisible);
            const int MaxBattleLogEntries = 32;
            for (int i = 0; i < MaxBattleLogEntries; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "combat-battle-log-" + capturedIndex,
                    () => adapter.Hud.GetBattleLogEntry(capturedIndex),
                    null,
                    () => false,
                    adapter.Hud.FocusBattleLog,
                    () => capturedIndex < adapter.Hud.GetBattleLogEntryCount(),
                    onUnfocus: adapter.Hud.UnfocusBattleLog));
            }

            return menu;
        }

        private static Widget BuildAiControlButton(CombatAdapter adapter, CombatHudSide side, string id)
        {
            CombatHudSide capturedSide = side;
            return new ButtonWidget(
                id,
                () => adapter.Hud.Commanders.GetAiControlButtonLabel(capturedSide),
                () => adapter.Hud.Commanders.ClickAiControlButton(capturedSide),
                () => adapter.Hud.Commanders.FocusAiControlButton(capturedSide),
                () => adapter.Hud.Commanders.IsAiControlButtonEnabled(capturedSide),
                () => adapter.Hud.Commanders.IsAiControlButtonVisible(capturedSide),
                () => adapter.Hud.Commanders.GetAiControlButtonTooltip(capturedSide));
        }

        private static MenuWidget BuildEssenceMenu(CombatAdapter adapter, CombatHudSide side, string id, string label)
        {
            MenuWidget menu = new MenuWidget(id, label, () => adapter.Hud.Commanders.IsEssenceMenuVisible(side));
            AddEssenceItem(menu, adapter, side, EssenceType.Order);
            AddEssenceItem(menu, adapter, side, EssenceType.Creation);
            AddEssenceItem(menu, adapter, side, EssenceType.Chaos);
            AddEssenceItem(menu, adapter, side, EssenceType.Arcana);
            AddEssenceItem(menu, adapter, side, EssenceType.Destruction);
            return menu;
        }

        private static void AddEssenceItem(MenuWidget menu, CombatAdapter adapter, CombatHudSide side, EssenceType essenceType)
        {
            CombatHudSide capturedSide = side;
            EssenceType capturedType = essenceType;
            string sideId = capturedSide == CombatHudSide.Attacker ? "attacker" : "defender";
            menu.AddItem(new MenuItemWidget(
                "combat-" + sideId + "-essence-" + capturedType.ToString().ToLowerInvariant(),
                () => adapter.Hud.Commanders.GetEssenceLabel(capturedSide, capturedType),
                null,
                null,
                () => adapter.Hud.Commanders.FocusEssence(capturedSide, capturedType),
                () => adapter.Hud.Commanders.IsEssenceMenuVisible(capturedSide),
                () => adapter.Hud.Commanders.GetEssenceTooltip(capturedSide, capturedType)));
        }

        private static System.Collections.Generic.IReadOnlyList<T> SafeGet<T>(string section, System.Func<System.Collections.Generic.IReadOnlyList<T>> getter)
        {
            try
            {
                System.Collections.Generic.IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (System.Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("CombatScreen section " + section + " failed to build: " + exception);
                return new T[0];
            }
        }

        private static int SafeGetCount(string section, System.Func<int> getter)
        {
            try
            {
                return getter != null ? getter() : 0;
            }
            catch (System.Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("CombatScreen section " + section + " failed to count: " + exception);
                return 0;
            }
        }
    }
}

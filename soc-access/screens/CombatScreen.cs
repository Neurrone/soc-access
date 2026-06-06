using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Utilities;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(BattleSceneInstaller), "Container");
        private static string _lastProbeDiagnostic;

        private const string ReturnToGridSoundKey = "Common_ClosePauseMenu";
        private const string FocusWrapCueKey = "Common_ClickUnfold";
        private const int GridIndex = 0;
        private const int TimelineIndex = 7;
        private readonly CombatAdapter _adapter;
        private readonly CombatHexGrid _grid;
        private readonly CombatTroopCycle _localActingTroopCycle = new CombatTroopCycle();
        private readonly CombatTroopCycle _enemyActingTroopCycle = new CombatTroopCycle();
        private int _lastCycleCurrentTroopId = -1;
        private Action<TroopAbilityTargeting> _abilityTargetingBeginHandler;

        public CombatScreen(CombatAdapter adapter)
            : this(adapter, new CombatHexGrid(adapter))
        {
        }

        public static Screen TryBuildActiveScreen()
        {
            return FindActiveCombatScreen();
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

        public override System.Collections.Generic.IEnumerable<ReviewBufferKind> VisibleReviewBuffers
        {
            get
            {
                foreach (ReviewBufferKind kind in base.VisibleReviewBuffers)
                {
                    yield return kind;
                }

                yield return ReviewBufferKind.CombatEvents;
            }
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

        public bool CanFocusActingTroop()
        {
            return HasActingTroops(enemy: false);
        }

        public bool CanNavigateLocalActingTroops()
        {
            return HasActingTroops(enemy: false);
        }

        public bool CanNavigateEnemyActingTroops()
        {
            return HasActingTroops(enemy: true);
        }

        public bool FocusActingTroop()
        {
            SyncTroopCyclesWithCurrent();
            IReadOnlyList<int> troopIds = GetActingTroopIds(enemy: false);
            CombatTroopCycleResult result = _localActingTroopCycle.AnchorFirst(troopIds);
            return ApplyCycleResult(_localActingTroopCycle, result);
        }

        public bool NavigateLocalActingTroop(int delta)
        {
            return NavigateActingTroop(_localActingTroopCycle, enemy: false, delta: delta);
        }

        public bool NavigateEnemyActingTroop(int delta)
        {
            return NavigateActingTroop(_enemyActingTroopCycle, enemy: true, delta: delta);
        }

        public void FocusGridIfNeeded()
        {
            if (IsGridFocused())
            {
                return;
            }

            RootWidget?.SetFocusByIndex(GridIndex);
        }

        public override bool HasClaimed(string actionKey)
        {
            if (actionKey != AccessibilityActions.Cancel.Key)
            {
                return base.HasClaimed(actionKey);
            }

            return base.HasClaimed(actionKey) || !IsGridFocused();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action == null || action.Key != AccessibilityActions.Cancel.Key)
            {
                return base.OnActionJustPressed(action);
            }

            // Let focused controls cancel their own state first. Otherwise Escape
            // returns HUD focus to the grid; on the grid, combat cancel or native pause owns it.
            if (RootWidget != null && RootWidget.HandleAction(action))
            {
                return true;
            }

            if (IsGridFocused())
            {
                return false;
            }

            FocusGridIfNeeded();
            NativeSoundUtility.PostEvent(ReturnToGridSoundKey);
            return true;
        }

        public bool FocusTimeline()
        {
            MenuWidget timeline = RootWidget?.GetChildAt(TimelineIndex) as MenuWidget;
            if (timeline == null || !timeline.IsVisible)
            {
                return true;
            }

            bool focusedTimeline = RootWidget.SetFocusByIndex(TimelineIndex);
            bool focusedFirstItem = timeline.SetFocusByIndex(0);
            return focusedTimeline || focusedFirstItem;
        }

        public bool SummarizeResources()
        {
            string summary = _adapter != null ? _adapter.BuildLocalEssenceSummary() : string.Empty;
            if (string.IsNullOrWhiteSpace(summary))
            {
                return false;
            }

            SpeechPipeline.Output(new SpeechRequest(summary, interrupt: false));
            return true;
        }

        public bool SummarizeEnemyResources()
        {
            string summary = _adapter != null ? _adapter.BuildEnemyEssenceSummary() : string.Empty;
            if (!string.IsNullOrWhiteSpace(summary))
            {
                SpeechPipeline.Output(new SpeechRequest(summary, interrupt: false));
            }

            return true;
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

        private bool IsGridFocused()
        {
            return ReferenceEquals(RootWidget?.FocusedChild, _grid)
                && ReferenceEquals(UIManager.CurrentWidget, _grid);
        }

        private bool HasActingTroops(bool enemy)
        {
            if (_adapter == null || !_adapter.IsLocalTurn())
            {
                return false;
            }

            IReadOnlyList<int> troopIds = GetActingTroopIds(enemy);
            return troopIds != null && troopIds.Count > 0;
        }

        private bool NavigateActingTroop(CombatTroopCycle cycle, bool enemy, int delta)
        {
            if (cycle == null)
            {
                return false;
            }

            SyncTroopCyclesWithCurrent();
            CombatTroopCycleResult result = cycle.Move(GetActingTroopIds(enemy), delta);
            return ApplyCycleResult(cycle, result);
        }

        private bool ApplyCycleResult(CombatTroopCycle cycle, CombatTroopCycleResult result)
        {
            if (!result.Moved)
            {
                return true;
            }

            bool moved = MoveCursorToTroop(result.TroopId, focusGrid: true, requireLocalCurrentTurn: false);
            if (!moved)
            {
                cycle?.Reset();
                return true;
            }

            if (result.Wrapped)
            {
                NativeSoundUtility.PostEvent(FocusWrapCueKey);
            }

            return true;
        }

        private IReadOnlyList<int> GetActingTroopIds(bool enemy)
        {
            if (_adapter == null || !_adapter.IsLocalTurn())
            {
                return new int[0];
            }

            return enemy ? _adapter.GetEnemyActingTroopIds() : _adapter.GetLocalActingTroopIds();
        }

        private void SyncTroopCyclesWithCurrent()
        {
            int currentTroopId = _adapter != null ? _adapter.GetCurrentTroopId() : -1;
            if (currentTroopId == _lastCycleCurrentTroopId)
            {
                return;
            }

            _lastCycleCurrentTroopId = currentTroopId;
            _localActingTroopCycle.Reset();
            _enemyActingTroopCycle.Reset();
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
            if (accessibilityEvent is QueueChangedEvent)
            {
                RefreshTimelineAfterQueueChanged();
                return;
            }

            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null && !hudVisibility.IsVisible)
            {
                FocusGridIfNeeded();
            }
        }

        private void RefreshTimelineAfterQueueChanged()
        {
            if (RootWidget == null || _adapter == null)
            {
                return;
            }

            MenuWidget previousTimeline = RootWidget.GetChildAt(TimelineIndex) as MenuWidget;
            bool wasTimelineFocused = ReferenceEquals(RootWidget.FocusedChild, previousTimeline);
            MenuWidget timeline = BuildQueueMenu(_adapter);
            if (!RootWidget.ReplaceChildAt(TimelineIndex, timeline))
            {
                return;
            }

            if (wasTimelineFocused)
            {
                RootWidget.SetFocusByIndex(TimelineIndex);
            }
        }

        private static ContainerWidget BuildRoot(CombatAdapter adapter, CombatHexGrid grid)
        {
            ContainerWidget root = new ContainerWidget("combat-screen", ModText.Get(ModStrings.Screens.Combat));
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
                () => ModText.Get(ModStrings.Screens.CancelSpell),
                adapter.Hud.ClickCancelSpellButton,
                adapter.Hud.FocusCancelSpellButton,
                adapter.Hud.IsCancelSpellButtonEnabled,
                adapter.Hud.IsCancelSpellButtonVisible,
                () => adapter.Hud.CancelSpellButtonTooltip));
            root.AddChild(new ButtonWidget(
                "combat-cancel-ability",
                () => ModText.Get(ModStrings.Screens.CancelAbility),
                () => ActivateCancelAbilityButton(adapter),
                adapter.Hud.FocusCancelAbilityButton,
                adapter.Hud.IsCancelAbilityButtonEnabled,
                adapter.Hud.IsCancelAbilityButtonVisible,
                () => adapter.Hud.CancelAbilityButtonTooltip));
            root.AddChild(BuildQuickbarMenu(adapter));
            root.AddChild(new ButtonWidget(
                "combat-current-troop",
                () => ModText.Get(ModStrings.Screens.CurrentTroop, BuildTroopLabel(adapter.Hud.GetCurrentTroopInfo())),
                () => ActivateCurrentTroop(adapter),
                null,
                () => adapter.Hud.GetCurrentTroopId() >= 0,
                adapter.Hud.IsCurrentTroopIndicatorVisible));
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
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Attacker, "combat-attacker-essence", ModText.Get(ModStrings.Screens.AttackerEssence)));
            root.AddChild(BuildAiControlButton(adapter, CombatHudSide.Attacker, "combat-attacker-ai-control"));
            root.AddChild(Portrait.StaticNative(
                "combat-defender-portrait",
                () => adapter.Hud.Commanders.GetPortraitLabel(CombatHudSide.Defender),
                () => adapter.Hud.Commanders.GetPortraitButton(CombatHudSide.Defender),
                adapter.Hud.Commanders.Localization,
                isVisible: () => adapter.Hud.Commanders.IsPortraitVisible(CombatHudSide.Defender)));
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Defender, "combat-defender-essence", ModText.Get(ModStrings.Screens.DefenderEssence)));
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
            root.AddChild(new ButtonWidget(
                "combat-chat",
                () => ChatPatches.CurrentAdapter != null ? ChatPatches.CurrentAdapter.ButtonLabel : ModText.Get(ModStrings.Screens.Chat),
                () => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.Open(),
                () => ChatPatches.CurrentAdapter?.FocusButton(),
                () => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.IsButtonEnabled(),
                () => ChatPatches.CurrentAdapter != null && ChatPatches.CurrentAdapter.IsButtonVisible(),
                () => ChatPatches.CurrentAdapter != null ? ChatPatches.CurrentAdapter.ButtonTooltip : null));
            root.AddChild(new ButtonWidget(
                "combat-options",
                () => adapter.Hud.OptionsButtonLabel,
                adapter.Hud.ClickOptionsButton,
                adapter.Hud.FocusOptionsButton,
                adapter.Hud.IsOptionsButtonEnabled,
                adapter.Hud.IsOptionsButtonVisible,
                () => adapter.Hud.OptionsButtonTooltip));
            root.AddChild(BuildBattleLogMenu(adapter));
            return root;
        }

        private static MenuWidget BuildQuickbarMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-quickbar", ModText.Get(ModStrings.Screens.Quickbar), adapter.Hud.IsQuickbarMenuVisible);
            int slotCount = SafeGetCount("quickbar slots", adapter.Hud.GetQuickbarSlotCount);
            for (int i = 0; i < slotCount; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "combat-quickbar-" + capturedIndex,
                    () => BuildQuickbarItemLabel(GetQuickbarItem(adapter, capturedIndex)),
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
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            screen?.FocusGridIfNeeded();
            return activated;
        }

        private static bool ActivateAbilityButton(CombatAdapter adapter)
        {
            bool activated = adapter != null && adapter.Hud.ClickAbilityButton();
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            if (adapter != null && adapter.GetTargetingMode() == CombatTargetingMode.Ability)
            {
                screen?.FocusGridIfNeeded();
            }

            return activated;
        }

        private static bool ActivateCancelAbilityButton(CombatAdapter adapter)
        {
            bool activated = adapter != null && adapter.Hud.ClickCancelAbilityButton();
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            screen?.FocusGridIfNeeded();
            return activated;
        }

        private static MenuWidget BuildQueueMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-queue", ModText.Get(ModStrings.Screens.TurnOrder), adapter.Hud.IsQueueMenuVisible);
            const int MaxQueueItems = 32;
            for (int i = 0; i < MaxQueueItems; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "combat-queue-" + capturedIndex,
                    () => BuildQueueItemLabel(GetQueueItem(adapter, capturedIndex)),
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

        private static string BuildQuickbarItemLabel(BattleHudAdapter.QuickbarItem item)
        {
            if (item == null || !item.HasSpell)
            {
                return string.Empty;
            }

            return item.SpellName + ", " + GameText.Get("Spells/Spellbook/SpellTierHeader", "tier " + item.SpellTier, item.SpellTier);
        }

        private static string BuildQueueItemLabel(BattleHudAdapter.QueueItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return item.IsRoundMarker ? ModText.Get(ModStrings.Screens.Round, item.RoundNumber) : BuildTroopLabel(item.Troop);
        }

        private static string BuildTroopLabel(BattleHudAdapter.TroopInfo troop)
        {
            if (troop == null || !troop.IsKnown || string.IsNullOrWhiteSpace(troop.Name))
            {
                return string.Empty;
            }

            string label;
            if (troop.HasSize)
            {
                label = troop.IsEnemy
                    ? ModText.Get(ModStrings.Combat.EnemyTroop, troop.Size, troop.Name)
                    : ModText.Get(ModStrings.Combat.TroopQuantity, troop.Size, troop.Name);
            }
            else
            {
                label = troop.Name;
            }

            return troop.HasPosition
                ? ModText.Get(ModStrings.Combat.TroopAt, label, CombatAdapter.FormatPoint(troop.Position))
                : label;
        }

        private static bool ActivateQueueItem(CombatAdapter adapter, int index)
        {
            BattleHudAdapter.QueueItem item = GetQueueItem(adapter, index);
            if (item == null || item.IsRoundMarker)
            {
                return false;
            }

            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            return screen != null && screen.MoveCursorToTroop(item.TroopId, focusGrid: true, requireLocalCurrentTurn: false);
        }

        private static bool ActivateCurrentTroop(CombatAdapter adapter)
        {
            if (adapter == null)
            {
                return false;
            }

            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            return screen != null && screen.MoveCursorToTroop(adapter.GetCurrentTroopId(), focusGrid: true, requireLocalCurrentTurn: false);
        }

        private static MenuWidget BuildBattleLogMenu(CombatAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("combat-battle-log", ModText.Get(ModStrings.Screens.BattleLog), adapter.Hud.IsBattleLogMenuVisible);
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
                SocAccessPlugin.Instance?.LogWarning("CombatScreen section " + section + " failed to build: " + exception);
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
                SocAccessPlugin.Instance?.LogWarning("CombatScreen section " + section + " failed to count: " + exception);
                return 0;
            }
        }

        private static CombatScreen FindActiveCombatScreen()
        {
            BattleSceneInstaller[] installers = Resources.FindObjectsOfTypeAll<BattleSceneInstaller>();
            if (installers.Length == 0)
            {
                LogProbeDiagnostic("Combat probe found no BattleSceneInstaller instances");
                return null;
            }

            int liveInstallers = 0;
            for (int i = 0; i < installers.Length; i++)
            {
                BattleSceneInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                liveInstallers++;
                DiContainer container = GetContainer(installer);
                IClientBattleFacade facade = TryResolve<IClientBattleFacade>(container);
                IBattleCursorManager cursorManager = TryResolve<IBattleCursorManager>(container);
                IBattleGridManager gridManager = TryResolve<IBattleGridManager>(container);
                IBattlePathManager pathManager = TryResolve<IBattlePathManager>(container);
                IBattleHighlightManager highlightManager = TryResolve<IBattleHighlightManager>(container);
                IBattleAttackPreviewHandler attackPreviewHandler = TryResolve<IBattleAttackPreviewHandler>(container);
                IBattleTooltipUtility tooltipUtility = TryResolve<IBattleTooltipUtility>(container);
                IInputManager inputManager = TryResolve<IInputManager>(container);
                ILocalizationHandler localization = TryResolve<ILocalizationHandler>(container);
                ICameraLookup cameraLookup = TryResolve<ICameraLookup>(container);
                IHumanBattleControllerFacade humanBattleController = TryResolve<IHumanBattleControllerFacade>(container);
                MouseKeyboardHumanBattleControllerModule mouseKeyboardInputModule = TryResolve<MouseKeyboardHumanBattleControllerModule>(container);
                IHumanBattleSpellController battleSpellController = TryResolve<IHumanBattleSpellController>(container);
                MouseKeyboardHumanBattleSpellModule mouseKeyboardSpellInputModule = TryResolve<MouseKeyboardHumanBattleSpellModule>(container);
                IBattleHudSignals battleHudSignals = TryResolve<IBattleHudSignals>(container);
                ITroopAbilityUtility abilityUtility = TryResolve<ITroopAbilityUtility>(container);
                object cartographyConverter = TryResolveByTypeName(container, "Lavapotion.Cartography.ICartographyConverter");

                CombatAdapter adapter = new CombatAdapter(
                    installer,
                    container,
                    facade,
                    cursorManager,
                    gridManager,
                    pathManager,
                    highlightManager,
                    attackPreviewHandler,
                    tooltipUtility,
                    inputManager,
                    localization,
                    cameraLookup,
                    cartographyConverter,
                    humanBattleController,
                    mouseKeyboardInputModule,
                    battleSpellController,
                    mouseKeyboardSpellInputModule,
                    battleHudSignals,
                    abilityUtility);
                if (adapter.IsPresent())
                {
                    CombatEventNarrator.SetActiveAdapter(adapter);
                    CombatEventNarrator.SyncCurrentTurnTroop(adapter);
                    LogProbeDiagnostic("Combat probe found ready battle");
                    return new CombatScreen(adapter);
                }

                LogProbeDiagnostic("Combat probe found installer but adapter is not ready: " + adapter.GetReadinessDiagnostic());
            }

            if (liveInstallers == 0)
            {
                LogProbeDiagnostic("Combat probe found " + installers.Length + " installer instances but none in a loaded scene");
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(BattleSceneInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(BattleSceneInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object TryResolveByTypeName(DiContainer container, string typeName)
        {
            if (container == null || string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            try
            {
                return container.Resolve(type);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void LogProbeDiagnostic(string message)
        {
            if (message == _lastProbeDiagnostic)
            {
                return;
            }

            _lastProbeDiagnostic = message;
            SocAccessPlugin.Instance?.LogInfo(message);
        }
    }
}

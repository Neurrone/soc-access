using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class HostileJoinMenuAdapter : IDisposable
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(HostileJoinMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(HostileJoinMenu), "_async");
        private static readonly FieldInfo StageField = AccessTools.Field(typeof(HostileJoinMenu), "_stage");
        private static readonly FieldInfo AttackingCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_attackingCommander");
        private static readonly FieldInfo JoiningCommanderField = AccessTools.Field(typeof(HostileJoinMenu), "_joiningCommander");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(HostileJoinMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(HostileJoinMenu), "_localizationHandler");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo TroopHudEntriesField = AccessTools.Field(typeof(TroopHUD), "_troops");
        private static readonly FieldInfo MovableTroopField = AccessTools.Field(typeof(TroopHUD), "_movableHudTroop");
        private static readonly FieldInfo CurrentHoverEntryField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentHoverEntry");
        private static readonly FieldInfo IsDraggingRightField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_isDraggingRight");
        private static readonly FieldInfo DragDirectionField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_dragDirection");
        private static readonly MethodInfo CanDropHereMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "CanDropHere");
        private static readonly MethodInfo CanMergeMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "CanMerge");
        private static readonly MethodInfo IsEmptyAndUnlockedMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "IsEmptyAndUnlocked");
        private static readonly MethodInfo CanSwapMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "CanSwap");
        private static readonly MethodInfo DecideAmountMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "DecideAmount");
        private static readonly MethodInfo SwapMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "Swap");

        private readonly HostileJoinMenu _menu;
        private readonly HostileJoinMenu.Settings _settings;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private bool _disposed;

        public HostileJoinMenuAdapter(HostileJoinMenu menu)
        {
            _menu = menu;
            _settings = GetField<HostileJoinMenu.Settings>(menu, SettingsField);
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int AttackingCommanderId
        {
            get
            {
                ICommanderState commander = GetField<ICommanderState>(_menu, AttackingCommanderField);
                return commander != null ? commander.Id : -1;
            }
        }

        public int JoiningCommanderId
        {
            get
            {
                ICommanderState commander = GetField<ICommanderState>(_menu, JoiningCommanderField);
                return commander != null ? commander.Id : -1;
            }
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.TitleText : null); }
        }

        public string Instructions
        {
            get { return GetText(_settings != null ? _settings.JoinText : null); }
        }

        public string DiscardLabel
        {
            get { return GetButtonText(_settings != null ? _settings.DoneButton : null, "Discard"); }
        }

        public string MassMoveLabel
        {
            get { return GetButtonText(_settings != null ? _settings.MassMoveButton : null, "Mass move"); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && GetField<object>(_menu, AsyncField) != null
                && IsJoinStage()
                && _settings.JoinStageContainer != null
                && _settings.JoinStageContainer.activeInHierarchy;
        }

        public ArmyExchangeGridWidget BuildArmyExchangeGrid()
        {
            return new ArmyExchangeGridWidget(
                "hostile-join-army-exchange-grid",
                GetWielderArmyLabel(),
                BuildSlotData(GetWielderTroopHud(), GetWielderArmyLabel()),
                BuildSlotData(_settings != null ? _settings.TroopHUD : null, "joining army"),
                Drop);
        }

        public bool ActivateDiscard()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.DoneButton : null);
        }

        public bool IsDiscardEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.DoneButton : null);
        }

        public bool ActivateMassMove()
        {
            return NativeSelectionUtility.Click(_settings != null ? _settings.MassMoveButton : null);
        }

        public bool IsMassMoveEnabled()
        {
            return IsButtonEnabled(_settings != null ? _settings.MassMoveButton : null);
        }

        public void FocusMassMove()
        {
            NativeSelectionUtility.Select(_settings != null ? _settings.MassMoveButton : null);
        }

        public Tooltip MassMoveTooltip
        {
            get { return Tooltip.ForComponent(_settings != null ? _settings.MassMoveButton : null, _localization); }
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        private bool Drop(ArmyExchangeGridWidget.SlotWidget source, ArmyExchangeGridWidget.SlotWidget target)
        {
            TroopHUDEntry sourceEntry = source != null ? source.NativeSource as TroopHUDEntry : null;
            TroopHUDEntry targetEntry = target != null ? target.NativeSource as TroopHUDEntry : null;
            if (sourceEntry == null || targetEntry == null || ReferenceEquals(sourceEntry, targetEntry))
            {
                return false;
            }

            TroopHUD sourceHud = GetTroopHudForEntry(sourceEntry);
            TroopHUDEntryMovable movable = GetMovable(sourceHud);
            if (movable == null || sourceEntry.Troop == null)
            {
                return false;
            }

            Vector3 sourceContainerPosition = sourceEntry.Container.Position;
            movable.BeginDrag(sourceEntry, new Vector2(sourceContainerPosition.x, sourceContainerPosition.y));
            CurrentHoverEntryField?.SetValue(movable, targetEntry);
            Vector3 sourcePosition = ((Component)sourceEntry).transform.position;
            Vector3 targetPosition = ((Component)targetEntry).transform.position;
            IsDraggingRightField?.SetValue(movable, sourcePosition.x < targetPosition.x);
            Vector3 dragDirection = targetPosition - sourcePosition;
            DragDirectionField?.SetValue(movable, new Vector2(dragDirection.x, dragDirection.y));

            if (!InvokeBool(CanDropHereMethod, movable))
            {
                movable.Reset();
                AccessibilityEventBus.Publish(new ArmyExchangeInvalidDestinationEvent(source.Id, target.Id));
                return false;
            }

            if (InvokeBool(CanMergeMethod, movable) || InvokeBool(IsEmptyAndUnlockedMethod, movable))
            {
                DecideAmountMethod?.Invoke(movable, new object[] { targetEntry.FormationIndex });
                return true;
            }

            if (InvokeBool(CanSwapMethod, movable))
            {
                SwapMethod?.Invoke(movable, null);
                return true;
            }

            return true;
        }

        private TroopHUD GetTroopHudForEntry(TroopHUDEntry entry)
        {
            TroopHUD wielderHud = GetWielderTroopHud();
            if (ContainsEntry(wielderHud, entry))
            {
                return wielderHud;
            }

            TroopHUD joiningHud = _settings != null ? _settings.TroopHUD : null;
            if (ContainsEntry(joiningHud, entry))
            {
                return joiningHud;
            }

            return null;
        }

        private TroopHUD GetWielderTroopHud()
        {
            return _settings != null && _settings.WielderInteractHeader != null
                ? GetField<TroopHUD>(_settings.WielderInteractHeader, HeaderTroopHudField)
                : null;
        }

        private TroopHUDEntryMovable GetMovable(TroopHUD hud)
        {
            return GetField<TroopHUDEntryMovable>(hud, MovableTroopField);
        }

        private bool ContainsEntry(TroopHUD hud, TroopHUDEntry entry)
        {
            List<TroopHUDEntry> entries = GetEntries(hud);
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i], entry))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<ArmyExchangeGridWidget.SlotData> BuildSlotData(TroopHUD hud, string armyLabel)
        {
            List<TroopHUDEntry> entries = GetEntries(hud);
            for (int i = 0; i < entries.Count; i++)
            {
                TroopHUDEntry entry = entries[i];
                if (entry == null || !((Component)entry).gameObject.activeInHierarchy || !entry.IsUnlocked)
                {
                    continue;
                }

                string id = armyLabel.Replace(" ", "-").ToLowerInvariant() + "-slot-" + (entry.FormationIndex + 1);
                string troopName = entry.Troop != null ? GetTroopName(entry.Troop.Id) : string.Empty;
                int currentSize = entry.Troop != null ? entry.Troop.Stats.Size : 0;
                int maxSize = entry.Troop != null ? entry.Troop.Stats.MaxTroopSize.GetValue() : 0;
                TroopHUDEntry capturedEntry = entry;
                yield return new ArmyExchangeGridWidget.SlotData(
                    id,
                    armyLabel,
                    entry.FormationIndex + 1,
                    troopName,
                    currentSize,
                    maxSize,
                    entry.Troop != null,
                    capturedEntry,
                    () => FocusSlot(capturedEntry),
                    entry.Troop != null ? BuildTroopTooltip(entry) : null);
            }
        }

        private Tooltip BuildTroopTooltip(TroopHUDEntry entry)
        {
            Tooltip tooltip = Tooltip.ForComponent(entry != null ? entry.GetSelectable() : null, _localization);
            AdventureTroopDetails details = entry != null ? entry.TroopDetails : null;
            if (tooltip == null || details == null || !details.ShowDisbandInstruction || !details.CanDisband || _localization == null)
            {
                return tooltip;
            }

            string disbandLine = GetLocalizedText("Adventure/TroopHUD/DisbandInstruction", "Disband Troop");
            List<string> instructionLines = new List<string> { disbandLine };
            List<TooltipAction> actions = new List<TooltipAction>
            {
                new TooltipAction(disbandLine, () => InvokeTroopRightClick(entry))
            };

            // AdventureTroopDetails also draws the disband action as a native
            // tooltip instruction row. Remove only the exact localized line we
            // are replacing with a structured action; if CanDisband is false,
            // the native "cannot disband" status row remains normal tooltip text.
            // Keep this explicit instead of using input metadata alone because
            // the troop adapter also relies on CanDisband and invokes the TroopHUD
            // right-click callback directly.
            return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
        }

        private static bool InvokeTroopRightClick(TroopHUDEntry entry)
        {
            if (entry == null || entry.OnRightClick == null)
            {
                return false;
            }

            entry.OnRightClick(entry);
            return true;
        }

        private void FocusSlot(TroopHUDEntry entry)
        {
            if (entry == null || entry.Troop == null)
            {
                HideNativeTooltip();
                return;
            }

            NativeSelectionUtility.Select(entry.GetSelectable());
        }

        private List<TroopHUDEntry> GetEntries(TroopHUD hud)
        {
            return GetField<List<TroopHUDEntry>>(hud, TroopHudEntriesField) ?? new List<TroopHUDEntry>();
        }

        private bool IsJoinStage()
        {
            object value = StageField != null ? StageField.GetValue(_menu) : null;
            return value != null && value.ToString() == "Join";
        }

        private string GetWielderArmyLabel()
        {
            ICommanderState commander = GetField<ICommanderState>(_menu, AttackingCommanderField);
            string name = commander != null && _facade != null ? _facade.Commanders.GetName(commander.Id) : "wielder";
            return name + "'s army";
        }

        private string GetTroopName(int troopId)
        {
            return SpeechTextSanitizer.Normalize(_facade != null ? _facade.Troops.GetName(troopId) : string.Empty);
        }

        private static bool InvokeBool(MethodInfo method, object instance)
        {
            object value = method != null ? method.Invoke(instance, null) : null;
            return value is bool && (bool)value;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(UIButton button, string fallback)
        {
            string text = MenuButtonTextUtility.GetStandardButtonLabel(button);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            string text = _localization != null ? _localization.GetText(key) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }

        private static IReadOnlyList<string> RemoveExactLines(IReadOnlyList<string> lines, IReadOnlyList<string> linesToRemove)
        {
            if (lines == null || lines.Count == 0 || linesToRemove == null || linesToRemove.Count == 0)
            {
                return lines ?? new string[0];
            }

            List<string> result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (!ContainsExact(linesToRemove, line))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        private static bool ContainsExact(IReadOnlyList<string> lines, string candidate)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }
    }
}

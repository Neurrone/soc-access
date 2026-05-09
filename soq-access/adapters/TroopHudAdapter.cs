using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TroopHudAdapter
    {
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

        private readonly TroopHUD _hud;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly string _label;

        public TroopHudAdapter(TroopHUD hud, IClientAdventureFacade facade, ILocalizationHandler localization, string label)
        {
            _hud = hud;
            _facade = facade;
            _localization = localization;
            _label = string.IsNullOrWhiteSpace(label) ? "army" : label;
        }

        public IReadOnlyList<SlotItem> GetSlots()
        {
            List<SlotItem> result = new List<SlotItem>();
            List<TroopHUDEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                TroopHUDEntry entry = entries[i];
                if (!IsVisibleUnlockedEntry(entry))
                {
                    continue;
                }

                result.Add(new SlotItem(this, entry, _label));
            }

            return result;
        }

        public IEnumerable<ArmyExchangeGridWidget.SlotData> BuildArmyExchangeSlots()
        {
            IReadOnlyList<SlotItem> slots = GetSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                SlotItem slot = slots[i];
                yield return new ArmyExchangeGridWidget.SlotData(
                    slot.Id,
                    slot.ArmyLabel,
                    slot.SlotNumber,
                    slot.TroopName,
                    slot.CurrentSize,
                    slot.MaxSize,
                    slot.IsOccupied,
                    slot,
                    slot.Focus,
                    slot.IsOccupied ? slot.Tooltip : null);
            }
        }

        public bool Drop(ArmyExchangeGridWidget.SlotWidget source, ArmyExchangeGridWidget.SlotWidget target)
        {
            return Drop(source != null ? source.NativeSource as SlotItem : null, target != null ? target.NativeSource as SlotItem : null);
        }

        public bool Drop(SlotItem source, SlotItem target)
        {
            TroopHUDEntry sourceEntry = source != null ? source.Entry : null;
            TroopHUDEntry targetEntry = target != null ? target.Entry : null;
            if (sourceEntry == null || targetEntry == null || ReferenceEquals(sourceEntry, targetEntry))
            {
                return false;
            }

            TroopHUDEntryMovable movable = GetMovable();
            if (movable == null || sourceEntry.Troop == null)
            {
                return false;
            }

            Vector3 sourcePosition = ((Component)sourceEntry).transform.position;
            Vector3 targetPosition = ((Component)targetEntry).transform.position;
            Vector3 dragDirection = targetPosition - sourcePosition;

            using (NativeScreenInputPositionOverride inputOverride = NativeScreenInputPositionOverride.Apply(GetScreenCenter(sourceEntry)))
            {
                Vector3 sourceContainerPosition = sourceEntry.Container.Position;
                movable.BeginDrag(sourceEntry, new Vector2(sourceContainerPosition.x, sourceContainerPosition.y));
                inputOverride?.SetPosition(GetScreenCenter(targetEntry));
                CurrentHoverEntryField?.SetValue(movable, targetEntry);
                IsDraggingRightField?.SetValue(movable, sourcePosition.x < targetPosition.x);
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
                    PushMoveTroopPopupIfPresent(movable);
                    return true;
                }

                if (InvokeBool(CanSwapMethod, movable))
                {
                    SwapMethod?.Invoke(movable, null);
                    return true;
                }
            }

            return true;
        }

        private TroopHUDEntryMovable GetMovable()
        {
            return GetField<TroopHUDEntryMovable>(_hud, MovableTroopField);
        }

        private List<TroopHUDEntry> GetEntries()
        {
            return GetField<List<TroopHUDEntry>>(_hud, TroopHudEntriesField) ?? new List<TroopHUDEntry>();
        }

        private static bool IsVisibleUnlockedEntry(TroopHUDEntry entry)
        {
            return entry != null
                && entry.IsUnlocked
                && ((Component)entry).gameObject != null
                && ((Component)entry).gameObject.activeInHierarchy;
        }

        private string GetTroopName(int troopId)
        {
            return SpeechTextSanitizer.Normalize(_facade != null ? _facade.Troops.GetName(troopId) : string.Empty);
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

        private string GetLocalizedText(string key, string fallback)
        {
            string text = _localization != null ? _localization.GetText(key) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }

        private static bool InvokeBool(MethodInfo method, object instance)
        {
            object value = method != null ? method.Invoke(instance, null) : null;
            return value is bool && (bool)value;
        }

        private static void PushMoveTroopPopupIfPresent(TroopHUDEntryMovable movable)
        {
            if (movable == null)
            {
                return;
            }

            MoveTroopPopupAdapter adapter = new MoveTroopPopupAdapter(movable);
            if (adapter.IsPresent())
            {
                SoqAccessPlugin.Instance?.ScreenDetector?.OnMoveTroopPopupReady(movable);
            }
        }

        private static Vector2 GetScreenCenter(TroopHUDEntry entry)
        {
            Component component = entry as Component;
            RectTransform rectTransform = component != null ? component.GetComponent<RectTransform>() : null;
            if (rectTransform != null)
            {
                Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
                return RectTransformUtility.WorldToScreenPoint(null, worldCenter);
            }

            Vector3 position = component != null ? component.transform.position : Vector3.zero;
            return new Vector2(position.x, position.y);
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
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

        internal sealed class SlotItem
        {
            private readonly TroopHudAdapter _adapter;

            public SlotItem(TroopHudAdapter adapter, TroopHUDEntry entry, string armyLabel)
            {
                _adapter = adapter;
                Entry = entry;
                ArmyLabel = armyLabel ?? string.Empty;
            }

            public TroopHUDEntry Entry { get; private set; }

            public string ArmyLabel { get; private set; }

            public int SlotNumber
            {
                get { return Entry != null ? Entry.FormationIndex + 1 : 0; }
            }

            public string Id
            {
                get { return ArmyLabel.Replace(" ", "-").Replace("'", string.Empty).ToLowerInvariant() + "-slot-" + SlotNumber; }
            }

            public bool IsOccupied
            {
                get { return Entry != null && Entry.Troop != null; }
            }

            public string TroopName
            {
                get { return IsOccupied ? _adapter.GetTroopName(Entry.Troop.Id) : string.Empty; }
            }

            public int CurrentSize
            {
                get { return IsOccupied && Entry.Troop.Stats != null ? Entry.Troop.Stats.Size : 0; }
            }

            public int MaxSize
            {
                get { return IsOccupied && Entry.Troop.Stats != null && Entry.Troop.Stats.MaxTroopSize != null ? Entry.Troop.Stats.MaxTroopSize.GetValue() : 0; }
            }

            public string Label
            {
                get
                {
                    string slotLabel = "slot " + SlotNumber;
                    if (!IsOccupied)
                    {
                        return "Empty troop " + slotLabel;
                    }

                    if (CurrentSize > 0 && MaxSize > 0)
                    {
                        return TroopName + ", " + CurrentSize + " / " + MaxSize + ", " + slotLabel;
                    }

                    return TroopName + ", " + slotLabel;
                }
            }

            public Tooltip Tooltip
            {
                get { return _adapter.BuildTroopTooltip(Entry); }
            }

            public void Focus()
            {
                if (Entry != null)
                {
                    NativeSelectionUtility.Select(Entry.GetSelectable());
                }
            }

            public bool DropTo(SlotItem target)
            {
                return _adapter != null && _adapter.Drop(this, target);
            }
        }

        private sealed class NativeScreenInputPositionOverride : IDisposable
        {
            private readonly object _response;
            private readonly PropertyInfo _positionProperty;
            private readonly object _oldPosition;
            private bool _disposed;

            private NativeScreenInputPositionOverride(object response, PropertyInfo positionProperty, Vector2 position)
            {
                _response = response;
                _positionProperty = positionProperty;
                _oldPosition = _positionProperty.GetValue(_response, null);
                SetPosition(position);
            }

            public static NativeScreenInputPositionOverride Apply(Vector2 position)
            {
                object response = ResolveWritablePrimaryResponse();
                if (response == null)
                {
                    SoqAccessPlugin.Instance?.LogWarning("TroopHudAdapter could not override native screen input position");
                    return null;
                }

                PropertyInfo positionProperty = AccessTools.Property(response.GetType(), "Position");
                if (positionProperty == null || !positionProperty.CanWrite)
                {
                    SoqAccessPlugin.Instance?.LogWarning("TroopHudAdapter could not override native screen input position because Position was not writable on " + response.GetType().FullName);
                    return null;
                }

                return new NativeScreenInputPositionOverride(response, positionProperty, position);
            }

            public void SetPosition(Vector2 position)
            {
                if (_response != null && _positionProperty != null)
                {
                    _positionProperty.SetValue(_response, position, null);
                }
            }

            public void Dispose()
            {
                if (_disposed || _response == null || _positionProperty == null)
                {
                    return;
                }

                _positionProperty.SetValue(_response, _oldPosition, null);
                _disposed = true;
            }

            private static object ResolveWritablePrimaryResponse()
            {
                IInputManager inputManager = InputManagerStaticAccessUnsafe.Current;
                object response = inputManager != null && inputManager.Screen != null
                    ? inputManager.Screen.Primary
                    : null;
                if (response == null)
                {
                    return null;
                }

                if (HasWritablePosition(response))
                {
                    return response;
                }

                FieldInfo currentResponseField = AccessTools.Field(response.GetType(), "_currentResponse");
                object currentResponse = currentResponseField != null ? currentResponseField.GetValue(response) : null;
                if (HasWritablePosition(currentResponse))
                {
                    return currentResponse;
                }

                FieldInfo mouseResponseField = AccessTools.Field(response.GetType(), "_mouseResponse");
                object mouseResponse = mouseResponseField != null ? mouseResponseField.GetValue(response) : null;
                return HasWritablePosition(mouseResponse) ? mouseResponse : null;
            }

            private static bool HasWritablePosition(object response)
            {
                if (response == null)
                {
                    return false;
                }

                PropertyInfo property = AccessTools.Property(response.GetType(), "Position");
                return property != null && property.CanWrite;
            }
        }
    }
}

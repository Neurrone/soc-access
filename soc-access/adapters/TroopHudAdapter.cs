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
using SongsOfConquest.Common;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TroopHudAdapter
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
        private static readonly FieldInfo MovableMainRectField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_mainRectTransform");
        private static readonly FieldInfo SourceTroopEntryField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_sourceTroopEntry");
        private static readonly FieldInfo DecideTargetEntryField = AccessTools.Field(typeof(TroopHUDEntryMovable), "_decideTargetTroopEntry");
        private static readonly MethodInfo CanSplitMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "CanSplit");
        private static readonly MethodInfo MoveTroopsMethod = AccessTools.Method(typeof(TroopHUDEntryMovable), "MoveTroops");
        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(TroopHUDEntry), "_button");
        private static readonly FieldInfo HudParentIdField = AccessTools.Field(typeof(TroopHUD), "_parentId");
        private static readonly FieldInfo HudParentTypeField = AccessTools.Field(typeof(TroopHUD), "_parentType");
        private static readonly MethodInfo HoverEntryMethod = AccessTools.Method(typeof(TroopHUD), "HandleTroopEntryHoverEntry");
        private static readonly MethodInfo QuickSplitMethod = AccessTools.Method(typeof(TroopHUD), "QuickSplitTroop");

        private readonly TroopHUD _hud;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public TroopHudAdapter(TroopHUD hud, IClientAdventureFacade facade, ILocalizationHandler localization)
        {
            _hud = hud;
            _facade = facade;
            _localization = localization;
            WakeMovable();
        }

        /// <summary>
        /// Let the game's drag ghost run its <c>Start()</c> before the first replayed drop. The ghost of
        /// a wielder band (the artifact market, the world choice menu) is inactive until a drag begins,
        /// and <c>TroopHUDEntryMovable.Start()</c> is where the game assigns <c>_mainRectTransform</c>
        /// and wires the slider and button handlers. A mouse drag ends frames after it begins, so the
        /// game never sees the gap; the mod's replay calls <c>BeginDrag</c> and <c>DecideAmount</c> in
        /// one frame, and <c>DecideAmount</c> then threw on the unassigned rect. Activating the ghost
        /// once here is what <c>BeginDrag</c> itself does; <c>Start()</c> runs on the next frame and,
        /// finding the ghost idle, deactivates it again. Calling <c>Start()</c> by reflection instead
        /// would wire every handler twice. Only while the bar is drawn: Unity runs <c>Start()</c>
        /// only in an active hierarchy, and a ghost woken inside a closed menu would stay awake
        /// until the menu opened.
        /// </summary>
        private void WakeMovable()
        {
            TroopHUDEntryMovable movable = GetMovable();
            if (movable == null || movable.gameObject.activeSelf || _hud == null || !_hud.gameObject.activeInHierarchy)
            {
                return;
            }

            // A ghost whose Start() has run keeps its rect; waking that one again would leave it
            // drawn and idle, since Unity never runs Start() twice.
            if (MovableMainRectField != null && MovableMainRectField.GetValue(movable) != null)
            {
                return;
            }

            movable.gameObject.SetActive(true);
        }

        /// <summary>The bar this adapter reads, so an owner can tell whether the one it kept is still
        /// the one the game draws.</summary>
        public TroopHUD Hud
        {
            get { return _hud; }
        }

        /// <summary>The wielder whose army the bar draws, in the game's own words; empty where the bar
        /// belongs to a map entity rather than to a commander.</summary>
        public string OwnerName
        {
            get
            {
                if (_hud == null || _facade == null || HudParentIdField == null || HudParentTypeField == null)
                {
                    return string.Empty;
                }

                object parentType = HudParentTypeField.GetValue(_hud);
                if (!(parentType is TroopParentType) || (TroopParentType)parentType != TroopParentType.Commander)
                {
                    return string.Empty;
                }

                object parentId = HudParentIdField.GetValue(_hud);
                return parentId is int
                    ? SpokenLines.Clean(_facade.Commanders.GetName((int)parentId))
                    : string.Empty;
            }
        }

        public enum DropResult
        {
            None,
            Completed,
            InvalidDestination,
            MoveAmountPopupOpened
        }

        /// <summary>The slots the bar DRAWS, in the order it draws them.
        /// <paramref name="includeLocked"/> keeps the ones drawn with a lock on them
        /// (<c>TroopHUDEntry.IsUnlocked</c> false), which a wielder band draws and a bar built with
        /// <c>hideLockedSlots</c> does not.</summary>
        public IReadOnlyList<SlotItem> GetSlots(bool includeLocked = false)
        {
            List<SlotItem> result = new List<SlotItem>();
            List<TroopHUDEntry> entries = GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                TroopHUDEntry entry = entries[i];
                if (!IsDrawnEntry(entry) || (!includeLocked && !entry.IsUnlocked))
                {
                    continue;
                }

                result.Add(new SlotItem(this, entry));
            }

            return result;
        }

        public DropResult Drop(SlotItem source, SlotItem target)
        {
            TroopHUDEntry sourceEntry = source != null ? source.Entry : null;
            TroopHUDEntry targetEntry = target != null ? target.Entry : null;
            if (sourceEntry == null || targetEntry == null || ReferenceEquals(sourceEntry, targetEntry))
            {
                return DropResult.None;
            }

            if (!BeginNativeDrag(sourceEntry))
            {
                return DropResult.None;
            }

            return CompleteNativeDrop(sourceEntry, targetEntry);
        }

        /// <summary>The whole of the game's drag on one troop, begun and finished in the one call the
        /// keyboard's drop is: the same two halves a mouse press and release run frames apart. A drop
        /// on the troop's own slot is allowed here - with Ctrl held that is the game's force split.
        /// </summary>
        public DropResult DropOn(TroopHUDEntry sourceEntry, TroopHUDEntry targetEntry)
        {
            if (sourceEntry == null || targetEntry == null || !BeginNativeDrag(sourceEntry))
            {
                return DropResult.None;
            }

            return CompleteNativeDrop(sourceEntry, targetEntry);
        }

        public bool BeginDrag(SlotItem source)
        {
            TroopHUDEntry sourceEntry = source != null ? source.Entry : null;
            return BeginNativeDrag(sourceEntry);
        }

        public DropResult CompleteDrop(SlotItem source, SlotItem target)
        {
            TroopHUDEntry sourceEntry = source != null ? source.Entry : null;
            TroopHUDEntry targetEntry = target != null ? target.Entry : null;
            if (sourceEntry == null || targetEntry == null || ReferenceEquals(sourceEntry, targetEntry))
            {
                return DropResult.None;
            }

            return CompleteNativeDrop(sourceEntry, targetEntry);
        }

        public void CancelDrag()
        {
            TroopHUDEntryMovable movable = GetMovable();
            if (movable != null)
            {
                movable.Reset();
            }
        }

        private bool BeginNativeDrag(TroopHUDEntry sourceEntry)
        {
            TroopHUDEntryMovable movable = GetMovable();
            if (sourceEntry == null || movable == null || sourceEntry.Troop == null)
            {
                return false;
            }

            using (ApplyScreenInputPositionOverride(GetScreenCenter(sourceEntry)))
            {
                Vector3 sourceContainerPosition = sourceEntry.Container.Position;
                movable.BeginDrag(sourceEntry, new Vector2(sourceContainerPosition.x, sourceContainerPosition.y));
            }

            return true;
        }

        /// <summary>
        /// The rest of the game's own drag, replayed onto <paramref name="targetEntry"/>: the hover
        /// state the pointer would have left behind, then the very branch
        /// <c>TroopHUDEntryMovable.Update</c> takes at mouse-up - merge, empty slot, swap - with the
        /// physical Ctrl read by the game's own input manager, exactly as that method reads it.
        /// </summary>
        private DropResult CompleteNativeDrop(TroopHUDEntry sourceEntry, TroopHUDEntry targetEntry)
        {
            TroopHUDEntryMovable movable = GetMovable();
            if (movable == null || sourceEntry.Troop == null)
            {
                return DropResult.None;
            }

            Vector3 sourcePosition = ((Component)sourceEntry).transform.position;
            Vector3 targetPosition = ((Component)targetEntry).transform.position;
            Vector3 dragDirection = targetPosition - sourcePosition;
            bool ctrlHeld = IsCtrlHeld();

            using (ApplyScreenInputPositionOverride(GetScreenCenter(targetEntry)))
            {
                CurrentHoverEntryField?.SetValue(movable, targetEntry);
                IsDraggingRightField?.SetValue(movable, sourcePosition.x < targetPosition.x);
                DragDirectionField?.SetValue(movable, new Vector2(dragDirection.x, dragDirection.y));

                // The drop on the troop's OWN slot: with Ctrl held the game splits it in half into the
                // next unlocked slot and ends the drag, and does nothing at all without.
                if (ReferenceEquals(sourceEntry, targetEntry))
                {
                    if (ctrlHeld && InvokeBool(CanSplitMethod, movable))
                    {
                        Action<TroopHUDEntry> forceSplit = movable.OnForceSplit;
                        movable.Reset();
                        if (forceSplit != null)
                        {
                            forceSplit(sourceEntry);
                        }

                        return DropResult.Completed;
                    }

                    movable.Reset();
                    return DropResult.InvalidDestination;
                }

                if (!InvokeBool(CanDropHereMethod, movable))
                {
                    movable.Reset();
                    return DropResult.InvalidDestination;
                }

                bool wholeStackFits = ctrlHeld
                    && sourceEntry.Troop.Stats.Size <= GetMaxTroopSize(sourceEntry, targetEntry);

                if (InvokeBool(CanMergeMethod, movable))
                {
                    if (wholeStackFits)
                    {
                        return MoveTroops(
                            movable,
                            Mathf.Min(targetEntry.Troop.AmountOfRoomLeft(), sourceEntry.Troop.Stats.Size),
                            sourceEntry,
                            targetEntry);
                    }

                    DecideAmountMethod?.Invoke(movable, new object[] { targetEntry.FormationIndex });
                    return DropResult.MoveAmountPopupOpened;
                }

                if (InvokeBool(IsEmptyAndUnlockedMethod, movable))
                {
                    if (wholeStackFits)
                    {
                        return MoveTroops(movable, sourceEntry.Troop.Stats.Size, sourceEntry, targetEntry);
                    }

                    DecideAmountMethod?.Invoke(movable, new object[] { targetEntry.FormationIndex });
                    return DropResult.MoveAmountPopupOpened;
                }

                if (InvokeBool(CanSwapMethod, movable))
                {
                    SwapMethod?.Invoke(movable, null);
                    return DropResult.Completed;
                }

                // CanDropHere said yes and every branch behind it then said no - the state changed
                // under the replay. The drag is put down rather than left hanging, and nothing moved.
                movable.Reset();
                return DropResult.InvalidDestination;
            }
        }

        /// <summary>The game's own move, for the Ctrl branches: the whole stack onto an empty slot, or
        /// as much of it as the target has room for.</summary>
        private DropResult MoveTroops(
            TroopHUDEntryMovable movable,
            int amountToMove,
            TroopHUDEntry sourceEntry,
            TroopHUDEntry targetEntry)
        {
            if (MoveTroopsMethod == null)
            {
                movable.Reset();
                return DropResult.InvalidDestination;
            }

            DecideTargetEntryField?.SetValue(movable, targetEntry);
            MoveTroopsMethod.Invoke(movable, new object[] { amountToMove, sourceEntry.Troop, targetEntry });
            return DropResult.Completed;
        }

        private int GetMaxTroopSize(TroopHUDEntry sourceEntry, TroopHUDEntry targetEntry)
        {
            return _facade == null
                ? 0
                : _facade.GetMaxTroopSize(sourceEntry.Troop.Reference, targetEntry.ParentType, targetEntry.ParentId);
        }

        /// <summary>Whether Ctrl is physically down, asked of the game's own input manager - the same
        /// question <c>TroopHUDEntryMovable.Update</c> asks at mouse-up.</summary>
        public static bool IsCtrlHeld()
        {
            IInputManager inputManager = InputManagerStaticAccessUnsafe.Current;
            return inputManager != null && inputManager.IsCtrlHeld();
        }

        /// <summary>
        /// Whether the game would take a drop of <paramref name="sourceEntry"/> on
        /// <paramref name="targetEntry"/>: its own <c>CanDropHere</c>, asked with the two entries a
        /// drag would have left in the movable and with them put back afterwards. A pure question - no
        /// drag begins, no sound plays, nothing is left behind.
        /// </summary>
        public bool CanDropOn(TroopHUDEntry sourceEntry, TroopHUDEntry targetEntry)
        {
            TroopHUDEntryMovable movable = GetMovable();
            if (movable == null
                || sourceEntry == null
                || targetEntry == null
                || sourceEntry.Troop == null
                || ReferenceEquals(sourceEntry, targetEntry)
                || SourceTroopEntryField == null
                || CurrentHoverEntryField == null)
            {
                return false;
            }

            object oldSource = SourceTroopEntryField.GetValue(movable);
            object oldHover = CurrentHoverEntryField.GetValue(movable);
            try
            {
                SourceTroopEntryField.SetValue(movable, sourceEntry);
                CurrentHoverEntryField.SetValue(movable, targetEntry);
                return InvokeBool(CanDropHereMethod, movable);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning(
                    "TroopHudAdapter could not ask the game whether a troop may be dropped: " + exception);
                return false;
            }
            finally
            {
                SourceTroopEntryField.SetValue(movable, oldSource);
                CurrentHoverEntryField.SetValue(movable, oldHover);
            }
        }

        /// <summary>
        /// The game's quick split (<c>Adventure.SplitTroopSize1</c> to <c>SplitTroopSize10</c>), which
        /// acts on the entry the POINTER is over: the entry is handed to the same hover handler the
        /// mouse would have run first, and the game's own handler then splits.
        /// </summary>
        public bool QuickSplit(TroopHUDEntry entry, int size)
        {
            if (_hud == null || entry == null || QuickSplitMethod == null)
            {
                return false;
            }

            HoverEntryMethod?.Invoke(_hud, new object[] { entry });
            QuickSplitMethod.Invoke(_hud, new object[] { size });
            return true;
        }

        /// <summary>The button the entry hangs its own click handlers on
        /// (<c>TroopHUDEntry._button</c>).</summary>
        private IUIButton GetEntryButton(TroopHUDEntry entry)
        {
            return entry != null && EntryButtonField != null ? EntryButtonField.GetValue(entry) as IUIButton : null;
        }

        private TroopHUDEntryMovable GetMovable()
        {
            return GetField<TroopHUDEntryMovable>(_hud, MovableTroopField);
        }

        private List<TroopHUDEntry> GetEntries()
        {
            return GetField<List<TroopHUDEntry>>(_hud, TroopHudEntriesField) ?? new List<TroopHUDEntry>();
        }

        private static bool IsDrawnEntry(TroopHUDEntry entry)
        {
            return entry != null
                && ((Component)entry).gameObject != null
                && ((Component)entry).gameObject.activeInHierarchy;
        }

        private string GetTroopName(int troopId)
        {
            return SpokenLines.Clean(_facade != null ? _facade.Troops.GetName(troopId) : string.Empty);
        }

        /// <summary>
        /// The troop's own details, as the game draws them on hover. The line telling the player to
        /// right click to disband is dropped - it describes a gesture rather than the troop - and the
        /// graph rows say the gesture as a usage hint on the right-click key instead.
        /// </summary>
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
            return new Tooltip(
                () => RemoveExactLines(tooltip.TextLines, instructionLines),
                tooltip.VisualMetadata,
                isLong: () => tooltip.IsLong);
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return GameText.Get(_localization, key, fallback);
        }

        private static bool InvokeBool(MethodInfo method, object instance)
        {
            object value = method != null ? method.Invoke(instance, null) : null;
            return value is bool && (bool)value;
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

        public sealed class SlotItem
        {
            private readonly TroopHudAdapter _adapter;

            public SlotItem(TroopHudAdapter adapter, TroopHUDEntry entry)
            {
                _adapter = adapter;
                Entry = entry;
            }

            public TroopHUDEntry Entry { get; private set; }

            public int SlotNumber
            {
                get { return Entry != null ? Entry.FormationIndex + 1 : 0; }
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

            /// <summary>The troop's details, minus the disband instruction row: the graph rows say
            /// the disband gesture as a usage hint instead.</summary>
            public Tooltip Details
            {
                get { return _adapter.BuildTroopTooltip(Entry); }
            }

            /// <summary>False for a slot the bar draws with a lock on it - a slot the wielder has not
            /// earned yet, which holds nothing and takes nothing.</summary>
            public bool IsUnlocked
            {
                get { return Entry != null && Entry.IsUnlocked; }
            }

            /// <summary>Whether the game would let this troop be disbanded, which is what decides
            /// whether its right click does anything (<c>TroopHUDEntry.HandleRightClick</c>).</summary>
            /// <summary>Whether the game would take a right click on this troop: its own disband rule,
            /// and the entry's button being one the game lets a click reach (<c>UIButton.OnPointerClick</c>
            /// returns on a non-interactable button, which a band inside a menu whose canvas group the
            /// game never enabled draws; seen 2026-09-08 on the world choice prompt variant).</summary>
            public bool CanDisband
            {
                get
                {
                    if (Entry == null || Entry.TroopDetails == null || !Entry.TroopDetails.CanDisband)
                    {
                        return false;
                    }

                    IUIButton button = _adapter.GetEntryButton(Entry);
                    return button != null && button.Active && button.Interactable;
                }
            }

            /// <summary>The entry's own left click, delivered as the pointer delivers it. The game
            /// starts its drag on button DOWN, so a click on a troop does nothing.</summary>
            public bool Click()
            {
                return NativeSelectionUtility.Click(_adapter.GetEntryButton(Entry));
            }

            /// <summary>The entry's own right click, which the game answers with its disband confirm
            /// dialog where <c>CanDisband</c> allows.</summary>
            public bool RightClick()
            {
                return NativeSelectionUtility.RightClick(_adapter.GetEntryButton(Entry));
            }

            public void Focus()
            {
                if (Entry != null)
                {
                    NativeSelectionUtility.Select(Entry.GetSelectable());
                }
            }

            public DropResult DropTo(SlotItem target)
            {
                return _adapter != null ? _adapter.Drop(this, target) : DropResult.None;
            }

            public bool BeginDrag()
            {
                return _adapter != null && _adapter.BeginDrag(this);
            }

            public DropResult CompleteDropTo(SlotItem target)
            {
                return _adapter != null ? _adapter.CompleteDrop(this, target) : DropResult.None;
            }

            public void CancelDrag()
            {
                if (_adapter != null)
                {
                    _adapter.CancelDrag();
                }
            }
        }

        /// <summary>
        /// The game's own pointer moved to a screen point for the length of a native drag call,
        /// so the drag reads the position the mod means rather than where the mouse happens to be.
        /// </summary>
        private static ScreenInputOverride ApplyScreenInputPositionOverride(Vector2 position)
        {
            IInputManager inputManager = InputManagerStaticAccessUnsafe.Current;
            object response = ScreenInputOverride.ResolveWritableResponse(
                inputManager != null && inputManager.Screen != null ? inputManager.Screen.Primary : null);
            if (response == null)
            {
                SocAccessMod.Instance?.LogWarning("TroopHudAdapter could not override native screen input position");
                return null;
            }

            return ScreenInputOverride.ApplyPosition(response, position, "TroopHudAdapter");
        }
    }
}

using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.UI
{
    public static class TroopHudMenu
    {
        public static MenuWidget Build(string id, string label, TroopHudAdapter adapter, Func<bool> isVisible, bool readOnly = false)
        {
            return readOnly
                ? BuildReadOnly(id, label, adapter, isVisible)
                : BuildDraggable(id, label, adapter, isVisible);
        }

        private static MenuWidget BuildReadOnly(string id, string label, TroopHudAdapter adapter, Func<bool> isVisible)
        {
            MenuWidget menu = new MenuWidget(id, label, isVisible);
            IReadOnlyList<TroopHudAdapter.SlotItem> slots = adapter != null
                ? adapter.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            for (int i = 0; i < slots.Count; i++)
            {
                TroopHudAdapter.SlotItem item = slots[i];
                menu.AddItem(new ReadOnlyTroopSlotWidget(
                    id + "-slot-" + item.SlotNumber,
                    () => BuildSlotLabel(item),
                    item.Focus,
                    () => true,
                    () => item.IsOccupied ? item.Tooltip : null));
            }

            return menu;
        }

        private static MenuWidget BuildDraggable(string id, string label, TroopHudAdapter adapter, Func<bool> isVisible)
        {
            Dictionary<MenuItemWidget, TroopHudAdapter.SlotItem> slotByWidget = new Dictionary<MenuItemWidget, TroopHudAdapter.SlotItem>();
            DraggableMenuWidget menu = null;
            menu = new DraggableMenuWidget(
                id,
                label,
                isVisible,
                (source, target) =>
                {
                    TroopHudAdapter.SlotItem sourceSlot;
                    TroopHudAdapter.SlotItem targetSlot;
                    if (!slotByWidget.TryGetValue(source, out sourceSlot) || !slotByWidget.TryGetValue(target, out targetSlot))
                    {
                        return false;
                    }

                    TroopHudAdapter.DropResult result = sourceSlot.DropTo(targetSlot);
                    if (result == TroopHudAdapter.DropResult.InvalidDestination)
                    {
                        Speak(ModText.Get(ModStrings.UI.CannotDropThere));
                    }

                    return result == TroopHudAdapter.DropResult.Completed
                        || result == TroopHudAdapter.DropResult.MoveAmountPopupOpened;
                });

            IReadOnlyList<TroopHudAdapter.SlotItem> slots = adapter != null
                ? adapter.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            for (int i = 0; i < slots.Count; i++)
            {
                TroopHudAdapter.SlotItem item = slots[i];
                DraggableMenuItemWidget widget = null;
                widget = new DraggableMenuItemWidget(
                    id + "-slot-" + item.SlotNumber,
                    () => BuildSlotLabel(item),
                    null,
                    null,
                    item.Focus,
                    () => true,
                    () => item.IsOccupied,
                    () => ReferenceEquals(menu.DragSource, widget),
                    () => item.IsOccupied ? item.Tooltip : null);
                slotByWidget.Add(widget, item);
                menu.AddItem(widget);
            }

            return menu;
        }

        private static string BuildSlotLabel(TroopHudAdapter.SlotItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            string slotLabel = ModText.Get(ModStrings.UI.Slot, item.SlotNumber);
            if (!item.IsOccupied)
            {
                return ModText.Get(ModStrings.UI.EmptyTroopSlot, slotLabel);
            }

            if (item.CurrentSize > 0 && item.MaxSize > 0)
            {
                return ModText.Get(ModStrings.UI.TroopSlotWithSize, item.TroopName, item.CurrentSize, item.MaxSize, slotLabel);
            }

            return ModText.Get(ModStrings.UI.TroopSlot, item.TroopName, slotLabel);
        }

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }

        private sealed class ReadOnlyTroopSlotWidget : MenuItemWidget
        {
            public ReadOnlyTroopSlotWidget(
                string id,
                Func<string> getLabel,
                Action onFocus,
                Func<bool> isVisible,
                Func<Tooltip> getTooltip)
                : base(id, getLabel, null, null, onFocus, isVisible, getTooltip)
            {
            }

            public override bool ClaimsAction(string actionKey)
            {
                return false;
            }

            public override bool HandleAction(InputAction action)
            {
                return false;
            }
        }
    }
}

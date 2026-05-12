using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.UI
{
    internal static class TroopHudMenu
    {
        public static MenuWidget Build(string id, string label, TroopHudAdapter adapter, Func<bool> isVisible)
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

                    return sourceSlot.DropTo(targetSlot);
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
                    () => item.Label,
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
    }
}

using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The pieces a settlement's defences are read as, shared by the two pages that draw them: the
    /// town's landing page (<c>screens/SettlementScreen.cs</c>) and the defence menu
    /// (<c>screens/DefenceMenuScreen.cs</c>). Both hang the same <c>DefencePanelWielder</c> band and
    /// the same garrison and ballista slot lists off their defence panel, so both read them here.
    ///
    /// The garrison and the ballistae are READ-ONLY: the game gives their entries no drag and no
    /// click of any kind, so they are lines rather than controls, and an entry the game has switched
    /// off is the empty slot it draws in that place.
    /// </summary>
    public static class SettlementNodes
    {
        /// <summary>
        /// The band naming the wielder defending the settlement, under the header the game draws over
        /// it: the line saying who is in there (or the game's own prompt while nobody is), then
        /// whichever of Store, Eject and Trade the game draws for the page it is on.
        /// </summary>
        public static void WielderBand(GraphBuilder builder, string keyPrefix, DefencePanelWielderAdapter panel)
        {
            if (builder == null || panel == null || !panel.IsPresent)
            {
                return;
            }

            string caption = panel.HeaderText;
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(keyPrefix + ":defending-wielder");
            }

            AddStatus(builder, keyPrefix, panel);
            Button(
                builder,
                panel.StoreButton,
                keyPrefix + ":store",
                // The button draws an icon and the game has no key for the action, so the name
                // is the mod's unless the button ever carries text of its own.
                () => string.IsNullOrWhiteSpace(panel.StoreLabel)
                    ? ModText.Get(ModStrings.Screens.StoreWielder)
                    : panel.StoreLabel,
                () => panel.ActivateStore(),
                panel.IsStoreEnabled,
                panel.StoreTooltip,
                panel.FocusStore);
            Button(
                builder,
                panel.EjectButton,
                keyPrefix + ":eject",
                () => panel.EjectLabel,
                () => panel.ActivateEject(),
                panel.IsEjectEnabled,
                panel.EjectTooltip,
                panel.FocusEject);
            Button(
                builder,
                panel.TradeButton,
                keyPrefix + ":trade",
                () => panel.TradeLabel,
                () => panel.ActivateTrade(),
                panel.IsTradeEnabled,
                panel.TradeTooltip,
                panel.FocusTrade);

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>The settlement's own defences: the troops the towers keep and the ballistae, each
        /// under the caption the page names it by, one line per slot in the order the panel draws
        /// them.</summary>
        public static void SlotBands(
            GraphBuilder builder,
            string keyPrefix,
            Component drawnBy,
            IReadOnlyList<DefenceSlotListAdapter.Slot> garrison,
            IReadOnlyList<DefenceSlotListAdapter.Slot> ballista)
        {
            SlotBand(
                builder,
                keyPrefix + ":garrison",
                GameText.Get("Adventure/BuildMenu/Garrison", string.Empty),
                drawnBy,
                garrison);
            SlotBand(
                builder,
                keyPrefix + ":ballista",
                ModText.Get(ModStrings.Screens.Ballista),
                drawnBy,
                ballista);
        }

        /// <summary>One button the game draws on these pages, declared only while it draws it: the
        /// game hides the ones that do not apply to the page rather than turning them off, and turns
        /// off the ones that apply but would be refused.</summary>
        public static void Button(
            GraphBuilder builder,
            Component button,
            string key,
            Func<string> label,
            Action activate,
            Func<bool> enabled,
            Tooltip tooltip,
            Action focus)
        {
            if (builder == null || !GameObjects.IsLive(button))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(label, activate, enabled, tooltip);
            if (focus != null)
            {
                vtable.OnFocusVisual = () => focus();
            }

            builder.AddItem(new DrawnNode(ControlId.For(button, key), vtable, button));
        }

        /// <summary>Who is defending the settlement, watched: the line turns from the game's prompt
        /// into the wielder's name under a cursor standing right here, as the Store button is
        /// pressed.</summary>
        private static void AddStatus(GraphBuilder builder, string keyPrefix, DefencePanelWielderAdapter panel)
        {
            Component drawnBy = panel.Panel;
            if (drawnBy == null || string.IsNullOrWhiteSpace(Status(panel)))
            {
                return;
            }

            DefencePanelWielderAdapter it = panel;
            NodeVtable vtable = GraphNodes.Text(() => Status(it));
            vtable.Announcements[0].Live = true;
            builder.AddItem(new DrawnNode(
                ControlId.Structural(keyPrefix + ":defending-wielder/status"),
                vtable,
                drawnBy));
        }

        /// <summary>The stored wielder's name alone, or the game's own prompt: the band is already
        /// named by the game's header, so a "Defending wielder:" prefix would say it twice.</summary>
        private static string Status(DefencePanelWielderAdapter panel)
        {
            string name = panel.StoredWielderName;
            return string.IsNullOrWhiteSpace(name) ? panel.NoStoredWielderText : name;
        }

        private static void SlotBand(
            GraphBuilder builder,
            string key,
            string caption,
            Component drawnBy,
            IReadOnlyList<DefenceSlotListAdapter.Slot> slots)
        {
            if (builder == null || slots == null || slots.Count == 0)
            {
                return;
            }

            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(key);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                AddSlot(builder, key + "/" + i, drawnBy, slots[i]);
            }

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        private static void AddSlot(
            GraphBuilder builder,
            string key,
            Component drawnBy,
            DefenceSlotListAdapter.Slot slot)
        {
            DefenceSlotListAdapter.Slot it = slot;
            NodeVtable vtable = GraphNodes.Text(() => SlotLabel(it), null, it == null ? null : it.Tooltip);
            if (it != null)
            {
                vtable.OnFocusVisual = () => it.Focus();
            }

            ControlId id = ControlId.Structural(key);
            builder.AddItem(drawnBy == null
                ? (NodeDeclaration)new SyntheticNode(id, vtable)
                : new DrawnNode(id, vtable, drawnBy));
        }

        /// <summary>What is in a slot: the troop and how many of them, exactly as a troop row reads,
        /// or the mod's word for an empty one. Where it sits is the graph's to say.</summary>
        private static string SlotLabel(DefenceSlotListAdapter.Slot slot)
        {
            if (slot == null || !slot.IsOccupied)
            {
                return ModText.Get(ModStrings.Screens.Empty);
            }

            return slot.CurrentSize > 0 && slot.MaxSize > 0
                ? ModText.Get(ModStrings.UI.TroopWithSize, slot.TroopName, slot.CurrentSize, slot.MaxSize)
                : slot.TroopName;
        }
    }
}

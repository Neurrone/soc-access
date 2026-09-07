using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The popup that trades towns with an ally, made navigable as a graph. Three places to be, in the
    /// order the popup draws them: the towns you can give, the towns you can ask for, and the close.
    ///
    /// Measured 2026-09-07: the gift header over a strip of portrait buttons, one per settlement and
    /// town you own; then - against an AI ally only - the request header over the same strip of that
    /// ally's towns (<c>GiftTownPopup.Show</c> turns both the header and the strip on for an AI and off
    /// for a human); and a close cross at the top right, which the game draws outside gamepad mode.
    ///
    /// Each button is a BUTTON whose label is the town's type name and the name the player gave it,
    /// both of which the game writes into the button's tooltip (<c>GiftTownButton.SetTown</c> passes
    /// the entity's name key as the title and the custom name as the text). Where the game refuses the
    /// trade it appends the reason to that same tooltip in its negative colour, and the reason reads
    /// after "unavailable" as a part of the button rather than as a line of its own - the rule the
    /// campaign menu set. The whole tooltip is still in the review buffer, as every native tooltip is.
    ///
    /// Activation is the game's own click, which opens its confirmation popup.
    ///
    /// ESCAPE IS THE MOD'S here and presses the drawn cross: <c>GiftTownPopup.Show</c> registers only
    /// <c>UI.Cancel</c>, the gamepad binding, so the keyboard's Escape would otherwise do nothing.
    /// </summary>
    public sealed class GiftTownPopupScreen : GraphScreen
    {
        private const string GiftStop = "gift-town-gift";
        private const string RequestStop = "gift-town-request";
        private const string CloseStop = "gift-town-close";

        private readonly GiftTownPopupAdapter _adapter;

        public GiftTownPopupScreen(GiftTownPopupAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            GiftTownPopup[] popups = Resources.FindObjectsOfTypeAll<GiftTownPopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                GiftTownPopupAdapter adapter = new GiftTownPopupAdapter(popups[i]);
                if (adapter.IsPresent())
                {
                    return new GiftTownPopupScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "gift-town-popup"; }
        }

        /// <summary>The popup's own drawn heading ("Gift towns/settlements to Nealuchi").</summary>
        public override string ScreenName
        {
            get
            {
                string header = _adapter != null ? _adapter.GiftHeader : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.IsCloseVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.ActivateClose();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(GiftStop);
            BuildTowns(builder, _adapter.GetGiftTowns());

            if (_adapter.IsRequestMenuVisible())
            {
                builder.BeginStop(RequestStop);
                BuildTowns(builder, _adapter.GetRequestTowns());
            }

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- a strip of towns ----

        private static void BuildTowns(GraphBuilder builder, IReadOnlyList<GiftTownPopupAdapter.TownItem> towns)
        {
            for (int i = 0; i < towns.Count; i++)
            {
                GiftTownPopupAdapter.TownItem town = towns[i];
                if (town == null || town.Component == null)
                {
                    continue;
                }

                GiftTownPopupAdapter.TownItem it = town;
                // No tooltip section: the native tooltip is where the name, custom name and reason
                // come from, and the parts already say all of it (a section would say it twice).
                NodeVtable vtable = GraphNodes.Button(
                    () => Label(it),
                    () => it.Activate(),
                    () => it.IsEnabled,
                    (Tooltip)null);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.Reason));
                vtable.OnFocusVisual = it.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(it.Component, it.Id), vtable, it.Component));
            }
        }

        /// <summary>Both names the button's tooltip carries, in the order it draws them: the town's own
        /// type first, then the name the player gave it.</summary>
        private static string Label(GiftTownPopupAdapter.TownItem town)
        {
            string custom = town.CustomName;
            return string.IsNullOrWhiteSpace(custom)
                ? town.TypeName
                : ModText.Get(ModStrings.Common.ListSeparator, town.TypeName, custom);
        }

        // ---- the close ----

        private void BuildClose(GraphBuilder builder)
        {
            Component close = _adapter.CloseButton;
            if (close == null || !_adapter.IsCloseVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.ActivateClose(),
                null,
                _adapter.CloseTooltip);
            vtable.OnFocusVisual = _adapter.FocusClose;
            builder.AddItem(new DrawnNode(ControlId.For(close, "gift-town:close"), vtable, close));
        }
    }
}

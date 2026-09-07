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
    /// The popup that trades resources with an ally, made navigable as a graph. Three places to be, in
    /// the order the popup draws them: what you can send, what you can ask for, and the close.
    ///
    /// Measured 2026-09-07: the send header over a row of six buttons, one per resource, with the fixed
    /// amount one press moves DRAWN on the button (500 gold, 1 of everything else - the game reads them
    /// out of its config in <c>SendResourcePopup.GetRequestAmount</c>, so nothing here is adjustable);
    /// then - against an AI ally only - the request header over the same six; and a close cross at the
    /// top right, which the game draws outside gamepad mode.
    ///
    /// Each button is a BUTTON labelled with the resource's own name, with the drawn amount after it.
    /// Where the game refuses the transfer it replaces the resource's name in the tooltip with the
    /// reason ("Not enough of this resource"), which reads after "unavailable" as a part of the button.
    /// A request button the game is taking clicks for carries what the ALLY holds of that resource as a
    /// second tooltip line, which reads as a part too. The whole tooltip is still in the review buffer.
    ///
    /// ESCAPE IS THE MOD'S here and presses the drawn cross: <c>SendResourcePopup.Show</c> registers
    /// only <c>UI.Cancel</c>, the gamepad binding, so the keyboard's Escape would otherwise do nothing.
    /// </summary>
    public sealed class SendResourcePopupScreen : GraphScreen
    {
        private const string SendStop = "send-resource-send";
        private const string RequestStop = "send-resource-request";
        private const string CloseStop = "send-resource-close";

        private readonly SendResourcePopupAdapter _adapter;

        public SendResourcePopupScreen(SendResourcePopupAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            SendResourcePopup[] popups = Resources.FindObjectsOfTypeAll<SendResourcePopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                SendResourcePopupAdapter adapter = new SendResourcePopupAdapter(popups[i]);
                if (adapter.IsPresent())
                {
                    return new SendResourcePopupScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "send-resource-popup"; }
        }

        /// <summary>The popup's own drawn heading ("Send resources to Nealuchi").</summary>
        public override string ScreenName
        {
            get
            {
                string header = _adapter != null ? _adapter.SendHeader : null;
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

            builder.BeginStop(SendStop);
            BuildResources(builder, _adapter.GetSendResources());

            if (_adapter.IsRequestMenuVisible())
            {
                builder.BeginStop(RequestStop);
                BuildResources(builder, _adapter.GetRequestResources());
            }

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- a band of resource buttons ----

        private static void BuildResources(
            GraphBuilder builder,
            IReadOnlyList<SendResourcePopupAdapter.ResourceItem> resources)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                SendResourcePopupAdapter.ResourceItem resource = resources[i];
                if (resource == null || !resource.IsVisible || resource.Component == null)
                {
                    continue;
                }

                SendResourcePopupAdapter.ResourceItem it = resource;
                // No tooltip section: the native tooltip is where the reason and the ally's stock
                // come from, and the parts already say all of it (a section would say it twice).
                NodeVtable vtable = GraphNodes.Button(
                    () => it.Name,
                    () => it.Activate(),
                    () => it.IsEnabled,
                    (Tooltip)null);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.Amount));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.Reason));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.OtherTeamAmount));
                vtable.OnFocusVisual = it.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(it.Component, it.Id), vtable, it.Component));
            }
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
            builder.AddItem(new DrawnNode(ControlId.For(close, "send-resource:close"), vtable, close));
        }
    }
}

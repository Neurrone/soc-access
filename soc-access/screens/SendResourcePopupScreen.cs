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
    public sealed class SendResourcePopupScreen : LiveScreen<SendResourcePopupAdapter>
    {
        private const string SendStop = "send-resource-send";
        private const string RequestStop = "send-resource-request";
        private const string CloseStop = "send-resource-close";

        /// <summary>The player menu holds this popup in a field of its own
        /// (<see cref="HudSources"/>).</summary>
        private readonly ScreenSource<SendResourcePopup> _source =
            ScreenSource<SendResourcePopup>.FromOwner(HudSources.PlayerMenu, HudSources.SendResource);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override SendResourcePopupAdapter Adapt(object menu)
        {
            return new SendResourcePopupAdapter((SendResourcePopup)menu);
        }

        public override string Key
        {
            get { return "send-resource-popup"; }
        }

        /// <summary>Layer 21: a popup over the player menu that opens it.</summary>
        public override int Layer
        {
            get { return 21; }
        }

        /// <summary>None: each band's stop is named by its drawn header, and the first is what
        /// arrival lands in.</summary>
        public override string ScreenName
        {
            get { return null; }
        }

        public override bool ConsumesBack
        {
            get { return Live != null && Live.IsCloseVisible(); }
        }

        public override bool Back()
        {
            return Live != null && Live.ActivateClose();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            // Each band is named by the header the game draws over it, said once on entering the
            // stop (owner ruling 2026-09-07); the popup has no name of its own beyond them.
            builder.BeginStop(SendStop);
            builder.PushContext(Live.SendHeader);
            BuildResources(builder, Live.GetSendResources());
            builder.PopContext();

            if (Live.IsRequestMenuVisible())
            {
                builder.BeginStop(RequestStop);
                builder.PushContext(Live.RequestHeader);
                BuildResources(builder, Live.GetRequestResources());
                builder.PopContext();
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
            Component close = Live.CloseButton;
            if (close == null || !Live.IsCloseVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => Live.ActivateClose(),
                null,
                Live.CloseTooltip);
            vtable.OnFocusVisual = Live.FocusClose;
            builder.AddItem(new DrawnNode(ControlId.For(close, "send-resource:close"), vtable, close));
        }
    }
}

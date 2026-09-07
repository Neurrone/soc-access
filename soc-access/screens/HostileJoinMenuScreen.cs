using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The offer a hostile army makes when a wielder walks into it. TWO SHAPES over the same window,
    /// keyed on the game's own stage, both of them two places to be and both named by the title the
    /// menu draws. There is no close cross anywhere on this menu.
    ///
    /// THE CHOICE STAGE is the attacking wielder's band and then the offer: what the menu says about
    /// it, the army being offered as troop rows, and the two answers. The offered rows read exactly as
    /// any army's rows do and say "unavailable" as well, because the game draws its lock over them
    /// until the offer is taken; nothing may be carried out of them while it does.
    ///
    /// THE JOIN STAGE is the same band, now a set of drop targets, and then the troops to be moved:
    /// what the menu says about moving them, the offered rows as carry sources, the Move all button,
    /// and Done - which the game itself rewrites from Discard to Close as the last troop leaves, so
    /// the label is watched under a cursor waiting on it.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): <c>HostileJoinMenu.ReregisterInput</c>
    /// registers <c>UI.ExitMenu</c> in BOTH stages outside its gamepad branch - No in the choice
    /// stage, Done in the join stage (measured 2026-09-07 in the decompiled source). The navigator
    /// claims the key only while something is being carried.
    ///
    /// A STAGE CHANGE SWAPS THE WHOLE PAGE, so the cursor is given up and seated afresh rather than
    /// recovered onto whatever survived.
    /// </summary>
    public sealed class HostileJoinMenuScreen : GraphScreen
    {
        private const string WielderStop = "hostile-join-wielder";
        private const string OfferStop = "hostile-join-offer";
        private const string JoinStop = "hostile-join-joining";
        private const string WielderKey = "hostile-join:wielder";
        private const string JoiningKey = "hostile-join:joining";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(HostileJoinMenuInstaller), "Container");

        private readonly HostileJoinMenuAdapter _adapter;

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the menu's two bodies of text are not drawn as controls.
        private readonly object _offerTextMarker = new object();
        private readonly object _joinTextMarker = new object();

        private HostileJoinMenuStage _stage;

        public HostileJoinMenuScreen(HostileJoinMenuAdapter adapter)
        {
            _adapter = adapter;
            _stage = adapter != null ? adapter.Stage : HostileJoinMenuStage.None;
        }

        public static Screen TryBuildActiveScreen()
        {
            HostileJoinMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<HostileJoinMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                HostileJoinMenu menu = TryResolveHostileJoinMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new HostileJoinMenuScreen(adapter);
                }

                adapter.Dispose();
            }

            return null;
        }

        public override string Key
        {
            get { return "hostile-join"; }
        }

        /// <summary>The title the menu draws, which it writes once and keeps through both stages.
        /// </summary>
        public override string ScreenName
        {
            get { return _adapter == null ? null : _adapter.Title; }
        }

        /// <summary>The offer, not the band: the band is who is asking, and the offer is the business.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return _adapter != null && _adapter.Stage == HostileJoinMenuStage.Join ? JoinStop : OfferStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Called by the detector whenever the menu changes. The graph is declared afresh on
        /// every operation, so the only thing to do here is give up the cursor when the menu has
        /// swapped one whole page for the other.</summary>
        public void Refresh()
        {
            HostileJoinMenuStage stage = _adapter == null ? HostileJoinMenuStage.None : _adapter.Stage;
            if (stage == _stage)
            {
                return;
            }

            _stage = stage;
            GraphNavigator navigator = Navigator;
            if (navigator != null && ReferenceEquals(navigator.Screen, this))
            {
                navigator.Blur();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            TroopHudRows.WielderStop(builder, WielderStop, WielderKey, _adapter.Wielder);

            if (_adapter.Stage == HostileJoinMenuStage.Join)
            {
                builder.BeginStop(JoinStop);
                BuildBody(builder, _joinTextMarker, "hostile-join:join-text", _adapter.JoinText);
                BuildJoiningTroops(builder, null);
                BuildMassMove(builder);
                BuildDone(builder);
                return;
            }

            builder.BeginStop(OfferStop);
            BuildBody(builder, _offerTextMarker, "hostile-join:offer-text", _adapter.OfferText);
            BuildJoiningTroops(builder, () => !_adapter.IsOfferLocked);
            BuildReject(builder);
            BuildAccept(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on whichever band's rows the cursor is on.
        /// </summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, WielderTroops, WielderKey)
                || TroopHudRows.ClaimsAction(actionKey, Navigator, JoiningTroops, JoiningKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, WielderTroops, WielderKey)
                || TroopHudRows.OnAction(actionKey, Navigator, JoiningTroops, JoiningKey);
        }

        private TroopHudAdapter WielderTroops
        {
            get
            {
                WielderInteract wielder = _adapter == null ? null : _adapter.Wielder;
                return wielder == null ? null : wielder.Troops;
            }
        }

        private TroopHudAdapter JoiningTroops
        {
            get { return _adapter == null ? null : _adapter.JoiningTroops; }
        }

        /// <summary>What the menu says about the offer or about moving the troops, as one node of its
        /// paragraphs.</summary>
        private void BuildBody(GraphBuilder builder, object marker, string key, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(marker, key),
                GraphNodes.Paragraphs(() => SpokenLines.Of(new[] { body }))));
        }

        /// <summary>The army being offered, under the game's own word for an army.</summary>
        private void BuildJoiningTroops(GraphBuilder builder, Func<bool> available)
        {
            string caption = GameText.Get("Commanders/Tooltip/Troops", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
                builder.SetRegion(JoiningKey);
            }

            TroopHudRows.Rows(builder, JoiningTroops, TroopHudRows.RowPrefix(JoiningKey), available);

            if (named)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        // ---- the choice stage's two answers ----

        private void BuildReject(GraphBuilder builder)
        {
            Component button = _adapter.RejectButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.RejectText,
                () => _adapter.ActivateReject(),
                _adapter.IsRejectEnabled);
            vtable.OnFocusVisual = () => _adapter.FocusReject();
            builder.AddItem(new DrawnNode(ControlId.For(button, "hostile-join:reject"), vtable, button));
        }

        /// <summary>The answer that takes the offer, with the price the game draws on it as its value.
        /// The game turns it off when the local team cannot afford that price, and says nothing else
        /// about why, so neither does the mod.</summary>
        private void BuildAccept(GraphBuilder builder)
        {
            Component button = _adapter.AcceptButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.AcceptText,
                () => _adapter.ActivateAccept(),
                _adapter.IsAcceptEnabled);
            vtable.Announcements.Add(GraphNodes.ValuePart(AcceptPrice));
            vtable.OnFocusVisual = () => _adapter.FocusAccept();
            builder.AddItem(new DrawnNode(ControlId.For(button, "hostile-join:accept"), vtable, button));
        }

        private string AcceptPrice()
        {
            string amount = _adapter.AcceptGoldAmount;
            return string.IsNullOrWhiteSpace(amount)
                ? string.Empty
                : ModText.Get(ModStrings.Common.ResourceAmount, amount, _adapter.GoldName);
        }

        // ---- the join stage's two buttons ----

        private void BuildMassMove(GraphBuilder builder)
        {
            Component button = _adapter.MassMoveButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.MassMoveText,
                () => _adapter.ActivateMassMove(),
                _adapter.IsMassMoveEnabled);
            vtable.OnFocusVisual = () => _adapter.FocusMassMove();
            builder.AddItem(new DrawnNode(ControlId.For(button, "hostile-join:mass-move"), vtable, button));
        }

        /// <summary>The way out. The game rewrites it from Discard to Close as the last troop leaves,
        /// so its label is watched under a cursor waiting on it.</summary>
        private void BuildDone(GraphBuilder builder)
        {
            Component button = _adapter.DoneButton;
            if (button == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.DoneText,
                () => _adapter.ActivateDone(),
                _adapter.IsDoneEnabled);
            vtable.Announcements[0].Live = true;
            vtable.OnFocusVisual = () => _adapter.FocusDone();
            builder.AddItem(new DrawnNode(ControlId.For(button, "hostile-join:done"), vtable, button));
        }

        private static HostileJoinMenu TryResolveHostileJoinMenu(HostileJoinMenuInstaller installer)
        {
            if (!IsLiveSceneInstaller(installer) || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<HostileJoinMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(HostileJoinMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}

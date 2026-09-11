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
    /// The menu the map puts up when a wielder walks into something that offers a choice - a reward
    /// to pick, a penalty to take, a question to answer. Three places to be, in the order the menu
    /// draws them: the wielder band across the top, the choice itself, and the close cross.
    ///
    /// THE CARDS ARE A RADIO GROUP that never chooses on arrival: walking onto a card must not commit
    /// the player to it, and the game's own model is select-then-confirm - a click on a card selects
    /// it and turns the Confirm button on, and only Confirm closes the menu. So Enter is the card's
    /// own pointer click and arriving on it only draws it (the widget screen selected on focus, which
    /// this replaces). A card the game will not take is unavailable and says the game's own red
    /// reason, which the card draws as part of its text.
    ///
    /// The wielder band comes from the shared contributor (<c>ui/TroopHudRows.cs</c>), because the
    /// menu rebuilds its cards whenever the army changes - which is why it draws the army at all -
    /// and because every wielder band in the game reads the same way.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu's <c>AdventureMenuBackground</c>
    /// registers its own exit and closes on it. The navigator claims the key only while something is
    /// being carried.
    /// </summary>
    public sealed class WorldChoiceMenuScreen : LiveScreen<WorldChoiceMenuAdapter>
    {
        private const string WielderStop = "world-choice-wielder";
        private const string ChoiceStop = "world-choice";
        private const string CloseStop = "world-choice-close";
        private const string WielderKey = "world-choice:wielder";

        // A subject of its own for the body, which the menu draws as a plain text rather than as a
        // control, kept across rebuilds so the reconciler seats the cursor on the same one.
        // A section the game has stopped answering for is reported once and then costs only its own
        // rows. What has been reported is mod-owned and outlives any one menu instance.
        private readonly SectionItems _sections = new SectionItems("WorldChoiceMenuScreen");

        private readonly object _bodyMarker = new object();

        /// <summary>The one world choice window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<IWorldChoiceMenu> _source =
            ScreenSource<IWorldChoiceMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override WorldChoiceMenuAdapter Adapt(object menu)
        {
            return new WorldChoiceMenuAdapter((WorldChoiceMenu)menu);
        }

        public override string Key
        {
            get { return "world-choice-menu"; }
        }

        /// <summary>Layer 30: a story follow-up over the map.</summary>
        public override int Layer
        {
            get { return 30; }
        }

        /// <summary>The title the menu draws.</summary>
        public override string ScreenName
        {
            get
            {
                return Live == null
                    ? null
                    : TroopHudRows.NameWithPlace(Live.Title, Live.Wielder);
            }
        }

        public override object InitialFocusStop
        {
            get { return ChoiceStop; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            TroopHudRows.WielderStop(builder, WielderStop, WielderKey, Live.Wielder);

            builder.BeginStop(ChoiceStop);
            BuildBody(builder);
            BuildChoices(builder);
            BuildConfirm(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on the band's troop rows.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, Troops, WielderKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, Troops, WielderKey);
        }

        private TroopHudAdapter Troops
        {
            get { return Live == null || Live.Wielder == null ? null : Live.Wielder.Troops; }
        }

        /// <summary>What the menu says the choice is about, as one node of its paragraphs.</summary>
        private void BuildBody(GraphBuilder builder)
        {
            string body = Live.Body;
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            // Broken into paragraphs ONCE: the node counts them at build and would break the same
            // string again on every readout.
            IList<string> lines = SpokenLines.Of(new[] { body });
            builder.AddItem(new SyntheticNode(
                ControlId.For(_bodyMarker, "world-choice:body"),
                GraphNodes.Paragraphs(() => lines)));
        }

        /// <summary>One card per row, walked with Up and Down: exactly one of them is the choice, and
        /// arriving is not choosing.</summary>
        private void BuildChoices(GraphBuilder builder)
        {
            IReadOnlyList<WorldChoiceMenuAdapter.ChoiceItem> choices = _sections.Of("choices", Live.GetChoices);
            for (int i = 0; i < choices.Count; i++)
            {
                WorldChoiceMenuAdapter.ChoiceItem it = choices[i];
                if (it == null || it.Button == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Radio(
                    () => it.Label,
                    () => it.IsSelected,
                    () => it.Choose(),
                    () => it.IsEnabled,
                    it.Tooltip);
                vtable.OnFocusVisual = () => it.Select();
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Button, "world-choice:card/" + i),
                    vtable,
                    it.Button));
            }
        }

        /// <summary>The button that commits the choice. The game turns it on when a card is chosen,
        /// so it is watched live under a cursor waiting here.</summary>
        private void BuildConfirm(GraphBuilder builder)
        {
            Component confirm = Live.ConfirmButton;
            if (confirm == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => Live.ConfirmLabel,
                () => Live.ActivateConfirm(),
                Live.IsConfirmEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(confirm);
            builder.AddItem(new DrawnNode(
                ControlId.For(confirm, "world-choice:confirm"),
                vtable,
                confirm));
        }

        /// <summary>The cross the wielder band draws, which is the game's own way out of the menu.
        /// </summary>
        private void BuildClose(GraphBuilder builder)
        {
            WielderInteract wielder = Live.Wielder;
            GraphNodes.DrawnClose(
                builder,
                "world-choice:close",
                wielder == null ? null : wielder.CloseButton,
                () => wielder.IsCloseVisible,
                () => wielder.ActivateClose());
        }

    }
}

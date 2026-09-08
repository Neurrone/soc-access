using System;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The game's split popup, which opens over whichever screen's troop rows dropped a troop on an
    /// empty slot or onto the same troop (<c>ui/TroopHudRows.cs</c>). One place to be, in the order
    /// the popup draws it: the maximum troop size the game states, the slider that divides the troops
    /// between the two portraits, and the three buttons under it.
    ///
    /// ENTER ON THE SLIDER IS THE COMMIT, because that is what the game does: the popup has no OK and
    /// no Cancel of its own, and the mouse commits by RELEASING the handle
    /// (<c>TroopHUDEntryMovable.HandleSliderPointerUp</c>), which is the call Enter makes here. Left
    /// and Right move the handle through the game's own <c>HandleSliderChanged</c>, so the amounts the
    /// value reads back are the game's own two numbers.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the movable polls <c>UI.Cancel</c> in its own
    /// <c>Update</c> while it is deciding and puts the troops back itself. A carry cannot be live here
    /// - it ended at the drop that opened this popup - so the navigator never wants the key either.
    ///
    /// The three buttons are ONE ROW because the game draws them in one, side by side under the
    /// slider; the split button in the middle is the game's own <c>Common/MoveTroops/SplitHalf</c>
    /// text, read from the localization rather than off its tooltip, which has the key that presses it
    /// appended to it.
    /// </summary>
    public sealed class MoveTroopPopupScreen : LiveScreen<MoveTroopPopupAdapter>
    {
        private const string Stop = "move-troop";
        private const string SliderKey = "move-troop:slider";

        /// <summary>How many single steps a coarse one is worth - the shape the options window's
        /// sliders established.</summary>
        private const int CoarseSteps = 10;

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<MoveTroopPopupScreen>(FindActive());
        }

        public static MoveTroopPopupAdapter FindActive()
        {
            TroopHUDEntryMovable[] movables = Resources.FindObjectsOfTypeAll<TroopHUDEntryMovable>();
            for (int i = 0; i < movables.Length; i++)
            {
                TroopHUDEntryMovable movable = movables[i];
                MoveTroopPopupAdapter adapter = new MoveTroopPopupAdapter(movable);
                if (adapter.IsPresent())
                {
                    return (adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "move-troop-popup"; }
        }

        /// <summary>Layer 34: over every page that draws a troop row.</summary>
        public override int Layer
        {
            get { return 34; }
        }

        /// <summary>The header the popup draws ("Move troops").</summary>
        public override string ScreenName
        {
            get { return Live == null ? null : Live.Title; }
        }

        public override object InitialFocusStop
        {
            get { return Stop; }
        }

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(Stop);
            AddMaxTroopSize(builder);
            AddSlider(builder);
            AddButtons(builder);
        }

        /// <summary>The line the popup states above the slider, as the game draws it: its caption and
        /// the number beside it.</summary>
        private void AddMaxTroopSize(GraphBuilder builder)
        {
            Component drawnBy = Live.MaxTroopSizeText;
            string line = ModText.JoinListWithCommas(Live.MaxTroopSizeTexts);
            if (drawnBy == null || string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            builder.AddItem(new DrawnNode(
                ControlId.For(drawnBy, "move-troop:max-size"),
                GraphNodes.Text(() => line),
                drawnBy));
        }

        /// <summary>The slider, read as the two amounts the game draws under the portraits either side
        /// of it. The hotkeys the popup answers to are the game's own list, in the buffer.</summary>
        private void AddSlider(GraphBuilder builder)
        {
            Component drawnBy = Live.SliderComponent;
            if (drawnBy == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Slider(
                () => ModText.Get(ModStrings.Screens.TroopDistribution),
                Distribution,
                Adjust,
                Live.IsSliderEnabled,
                Live.HotkeysTooltip,
                activate: () => Live.Confirm());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(drawnBy);
            ControlId id = ControlId.For(drawnBy, SliderKey);
            builder.AddItem(new DrawnNode(id, vtable, drawnBy));
            builder.SetStart(id);
        }

        /// <summary>The three buttons the popup draws in one row under the slider, in the order it
        /// draws them.</summary>
        private void AddButtons(GraphBuilder builder)
        {
            builder.StartRow("move-troop:buttons");
            AddButton(
                builder,
                "move-all-left",
                Live.MoveAllLeftButton,
                () => ModText.Get(ModStrings.Screens.MoveAllLeft),
                Live.IsMoveAllLeftEnabled,
                Live.MoveAllLeftTooltip,
                () => Live.MoveAllLeft());
            AddButton(
                builder,
                "split-equal",
                Live.SplitEqualButton,
                () => Live.SplitEqualLabel,
                Live.IsSplitEqualEnabled,
                Live.SplitEqualTooltip,
                () => Live.SplitEqual());
            AddButton(
                builder,
                "move-all-right",
                Live.MoveAllRightButton,
                () => ModText.Get(ModStrings.Screens.MoveAllRight),
                Live.IsMoveAllRightEnabled,
                Live.MoveAllRightTooltip,
                () => Live.MoveAllRight());
            builder.EndRow();
        }

        private static void AddButton(
            GraphBuilder builder,
            string key,
            Component drawnBy,
            Func<string> label,
            Func<bool> enabled,
            Tooltip tooltip,
            Action activate)
        {
            if (drawnBy == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(label, activate, enabled, tooltip);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(drawnBy);
            builder.AddItem(new DrawnNode(ControlId.For(drawnBy, "move-troop:" + key), vtable, drawnBy));
        }

        /// <summary>What the slider currently divides the troops into: the two amounts the game draws
        /// under the left and right portraits.</summary>
        private string Distribution()
        {
            string left = Live.LeftAmount;
            string right = Live.RightAmount;
            return ModText.Get(
                ModStrings.Screens.LeftRightDistribution,
                string.IsNullOrWhiteSpace(left) ? "0" : left,
                string.IsNullOrWhiteSpace(right) ? "0" : right);
        }

        /// <summary>
        /// Move the handle, through the game's own value change. The native value is the RIGHT-hand
        /// balance size and the game maps it onto the drawn left and right amounts by the direction the
        /// drag went, so a step is reported by re-reading those two amounts rather than by the mod
        /// working out which way the troops went.
        /// </summary>
        private void Adjust(int sign, bool large)
        {
            int step = large ? CoarseSteps : 1;
            Live.SetSliderValue(Live.GetSliderValue() + sign * step);
        }
    }
}

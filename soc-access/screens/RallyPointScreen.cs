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
    /// A rally point a wielder has walked into: a building that recruits out of the pools of the
    /// towns the player owns, one town at a time or all of them at once. Three places to be: the
    /// band of the wielder who walked in, the page, and the close cross.
    ///
    /// THE PAGE IS TWO BANDS. The strip of towns across the top is a choice among alternatives -
    /// each town by its name and its level, and the game's own entry for taking from every town at
    /// once - and picking one is the game's own click on that entry, which is what refreshes the
    /// grid underneath. Under it, the line the menu writes to say where the recruits are coming
    /// from, watched, since picking a town rewrites it under a still cursor; then the same recruit
    /// cards every draft page draws (<c>ui/RecruitGroups.cs</c>).
    ///
    /// A RALLY POINT NEVER HAS AN UPGRADE PAGE: its pool is the towns', and
    /// <c>UpgradeTroopsSubMenu</c> returns early for one.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false), and here it closes rather than goes back:
    /// the menu registers no exit action of its own, so the one its window registered stands
    /// (<c>AdventureMenuBackground.AnimateEntry</c>, <c>UI.ExitMenu</c> at <c>InputLevel.Popup</c>,
    /// measured 2026-09-08 in the decompiled source). The navigator claims the key only while
    /// something is being carried.
    ///
    /// Against the widget page it replaced (walked 2026-09-08), nothing spoken was lost: the
    /// building's name is now the screen's name rather than a line of its own, the towns read as
    /// what they are - alternatives, one of them chosen - the buy button says "Purchase" with its
    /// price as the value, and the slider and the price live inside the recruit's own group. The
    /// wielder's rows lost "slot 5": the graph says where a row sits.
    /// </summary>
    public sealed class RallyPointScreen : LiveScreen<RallyPointInteractionMenuAdapter>
    {
        private const string KeyPrefix = "rally-point";
        private const string WielderKey = "rally-point:wielder";

        /// <summary>The one rally point window the adventure scene holds for the whole game.</summary>
        private readonly ScreenSource<RallyPointInteractionMenu> _source =
            ScreenSource<RallyPointInteractionMenu>.FromScene(LoadedScenes.AdventureScene);

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override RallyPointInteractionMenuAdapter Adapt(object menu)
        {
            return new RallyPointInteractionMenuAdapter((RallyPointInteractionMenu)menu);
        }

        public override string Key
        {
            get { return KeyPrefix; }
        }

        /// <summary>Layer 22: a dwelling sub-page.</summary>
        public override int Layer
        {
            get { return 22; }
        }

        /// <summary>The building's own name, which the menu draws over the page.</summary>
        public override string ScreenName
        {
            get
            {
                return Live == null
                    ? null
                    : TroopHudRows.NameWithPlace(Live.Title, Live.Wielder);
            }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            TroopHudRows.WielderStop(builder, KeyPrefix + ":wielder-stop", WielderKey, Live.Wielder);

            builder.BeginStop(KeyPrefix + ":page");
            BuildSources(builder);
            RecruitGroups.Region(builder, Live.PurchaseTroops, KeyPrefix, BuildSelectedSource);

            builder.BeginStop(KeyPrefix + ":close");
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on the wielder's own rows.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, WielderTroops, WielderKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, WielderTroops, WielderKey);
        }

        private TroopHudAdapter WielderTroops
        {
            get
            {
                WielderInteract wielder = Live == null ? null : Live.Wielder;
                return wielder == null ? null : wielder.Troops;
            }
        }

        /// <summary>Where to recruit from: one entry per town the player owns, by its name and its
        /// level, and the game's own entry for every town at once. Picking one is the entry's own
        /// click, which the game answers by refreshing the grid; focus alone picks nothing.</summary>
        private void BuildSources(GraphBuilder builder)
        {
            IReadOnlyList<RallyPointInteractionMenuAdapter.SourceItem> sources = Live.GetSourceItems();
            builder.PushContext(ModText.Get(ModStrings.Screens.RecruitFrom));
            builder.SetRegion(KeyPrefix + ":sources");

            ControlId landing = null;
            for (int i = 0; i < sources.Count; i++)
            {
                RallyPointInteractionMenuAdapter.SourceItem it = sources[i];
                Component button = it == null ? null : it.Button;
                if (button == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Radio(
                    () => SourceLabel(it),
                    () => it.IsSelected,
                    () => it.Select(),
                    null,
                    it.Tooltip);
                vtable.OnFocusVisual = () => it.Focus();
                ControlId id = ControlId.For(button, KeyPrefix + ":source/" + i);
                builder.AddItem(new DrawnNode(id, vtable, button));
                if (it.IsSelected)
                {
                    landing = id;
                }
            }

            builder.PopContext();
            builder.SetRegion(null);

            // Tab into the page lands on the town it is recruiting from rather than at the top of a
            // strip the player would have to walk to find it in.
            builder.LandStopOn(landing);
        }

        /// <summary>The town an entry stands for, and the level the strip draws on it.</summary>
        private static string SourceLabel(RallyPointInteractionMenuAdapter.SourceItem source)
        {
            string name = source.Name;
            return source.IsLevelVisible && !string.IsNullOrWhiteSpace(source.Level)
                ? ModText.Get(ModStrings.Screens.NamedLevel, name, source.Level)
                : name;
        }

        /// <summary>The line the menu writes over the grid, watched: it is rewritten as a town is
        /// picked, under a cursor that may be standing right here.</summary>
        private void BuildSelectedSource(GraphBuilder builder)
        {
            Component line = Live.SelectedSourceLine;
            if (line == null || string.IsNullOrWhiteSpace(Live.SelectedSourceName))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(
                () => ModText.Get(ModStrings.Screens.RecruitingFrom, Live.SelectedSourceName));
            vtable.Announcements[0].Live = true;
            builder.AddItem(new DrawnNode(
                ControlId.For(line, KeyPrefix + ":recruiting-from"),
                vtable,
                line));
        }

        /// <summary>The cross on the wielder band, which is the one the menu itself listens to.
        /// An icon with no text of its own, so the mod names it.</summary>
        private void BuildClose(GraphBuilder builder)
        {
            WielderInteract wielder = Live.Wielder;
            GraphNodes.DrawnClose(
                builder,
                KeyPrefix + ":close-button",
                wielder == null ? null : wielder.CloseButton,
                () => wielder.IsCloseVisible,
                () => wielder.ActivateClose());
        }
    }
}

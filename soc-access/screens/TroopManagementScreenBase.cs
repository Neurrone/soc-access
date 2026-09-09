using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// What the draft page and the upgrade page have in common, on all three menus that draw them -
    /// the dwelling, the town and the defence menu. Up to four places to be, in the order the host
    /// draws them: the tutorial button in the corner where the host draws one, the band of the
    /// wielder who walked in where there is one, the page itself, and the way out.
    ///
    /// ESCAPE IS THE GAME'S, and on these sub-pages it goes BACK rather than closing: the town and
    /// the dwelling register <c>UI.ExitMenu</c> at <c>InputLevel.PopupInPopup</c> outside their
    /// gamepad branch, which shadows the close binding their window registered at <c>Popup</c>, and
    /// the defence menu swaps its own <c>Popup</c> handler from <c>Hide</c> to <c>ShowTopLevel</c>
    /// while a sub-page is up (all three measured 2026-09-08 in the decompiled source). So
    /// <c>ConsumesBack</c> stays false, no Back node is invented, and the back button each host
    /// DRAWS over its sub-page is declared as the button it is, before the close cross.
    ///
    /// THE WIELDER'S ARMY is the same band every other menu draws (<c>ui/TroopHudRows.cs</c>), carry
    /// and quick splits included; the defence menu is opened without a wielder and draws none.
    ///
    /// The host writes no title of its own over a sub-page - the building's name stays where it was
    /// on the landing page - so that is what the page is called.
    /// </summary>
    public abstract class TroopManagementScreenBase : LiveScreen<ITroopManagementHostAdapter>
    {
        /// <summary>The host that drew this page - the slot, under the name the page reads it by.
        /// </summary>
        protected ITroopManagementHostAdapter Host
        {
            get { return Live; }
        }

        /// <summary>Which host drew this page, which is how the detector tells one menu's sub-page
        /// from another's.</summary>
        public string HostIdPrefix
        {
            get { return Live != null ? Live.IdPrefix : string.Empty; }
        }

        /// <summary>What this page is, in control keys: "draft-troops" or "upgrade-troops".</summary>
        protected abstract string ScreenSuffix { get; }

        /// <summary>Whether this host is drawing THIS page now, read off the game.</summary>
        protected abstract bool IsContentPresent(ITroopManagementHostAdapter host);

        // The three menus that draw this page, each resolved from the adventure scene's container
        // and adapted once per menu instance. The page belongs to whichever of them is drawing it,
        // asked in the order the detector's own handlers used to write them; nothing is remembered,
        // so a host going back to its landing page empties the slot on the next tick.
        private readonly AdaptedSource<DwellingInteractionMenu, ITroopManagementHostAdapter> _dwelling =
            new AdaptedSource<DwellingInteractionMenu, ITroopManagementHostAdapter>(
                ScreenSource<DwellingInteractionMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu)));

        private readonly AdaptedSource<TownInteractionMenu, ITroopManagementHostAdapter> _settlement =
            new AdaptedSource<TownInteractionMenu, ITroopManagementHostAdapter>(
                ScreenSource<TownInteractionMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new SettlementTroopManagementHostAdapter(new TownInteractionMenuAdapter(menu)));

        private readonly AdaptedSource<DefenceMenu, ITroopManagementHostAdapter> _defence =
            new AdaptedSource<DefenceMenu, ITroopManagementHostAdapter>(
                ScreenSource<DefenceMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new DefenceTroopManagementHostAdapter(new DefenceMenuAdapter(menu)));

        /// <summary>The host adapter itself is what the slot holds here: the three hosts are three
        /// different menus behind one page, so the "menu" a source answers with IS the adapter over
        /// it, built once per menu instance.</summary>
        protected override object ResolveMenu()
        {
            return Drawing(_dwelling.Current) ?? Drawing(_settlement.Current) ?? Drawing(_defence.Current);
        }

        protected override ITroopManagementHostAdapter Adapt(object menu)
        {
            return (ITroopManagementHostAdapter)menu;
        }

        private ITroopManagementHostAdapter Drawing(ITroopManagementHostAdapter host)
        {
            return host != null && IsContentPresent(host) ? host : null;
        }

        /// <summary>The page itself, declared into the stop the base has already opened.</summary>
        protected abstract void BuildContent(GraphBuilder builder);

        /// <summary>The host's prefix and the page: "settlement-draft-troops". With no host - the
        /// page is not showing - the suffix alone, so the screen still has a name to be found by.
        /// </summary>
        public override string Key
        {
            get
            {
                string prefix = HostIdPrefix;
                return string.IsNullOrEmpty(prefix) ? ScreenSuffix : prefix + "-" + ScreenSuffix;
            }
        }

        public override string ScreenName
        {
            get
            {
                return Host == null ? null : TroopHudRows.NameWithPlace(Host.Title, Host.Wielder);
            }
        }

        public override bool IsActive()
        {
            SyncLive();
            return Live != null && IsContentPresent(Live);
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildTutorial(builder);
            TroopHudRows.WielderStop(builder, Key + ":wielder-stop", Key + ":wielder", Host.Wielder);

            builder.BeginStop(Key + ":content");
            BuildContent(builder);

            builder.BeginStop(Key + ":close");
            BuildBack(builder);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on the wielder's own rows.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, WielderTroops, Key + ":wielder");
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, WielderTroops, Key + ":wielder");
        }

        private TroopHudAdapter WielderTroops
        {
            get
            {
                WielderInteract wielder = Host == null ? null : Host.Wielder;
                return wielder == null ? null : wielder.Troops;
            }
        }

        private void BuildTutorial(GraphBuilder builder)
        {
            if (!Host.IsTutorialVisible())
            {
                return;
            }

            builder.BeginStop(Key + ":tutorial");
            SettlementNodes.Button(
                builder,
                Host.TutorialButton,
                Key + ":tutorial-button",
                () => Host.TutorialLabel,
                () => Host.ActivateTutorial(),
                null,
                null,
                null);
        }

        /// <summary>The button the host draws over a sub-page to go back to its landing page - the
        /// same thing Escape does here, and the only one of the two the mod has to name.</summary>
        private void BuildBack(GraphBuilder builder)
        {
            if (!Host.IsBackVisible())
            {
                return;
            }

            SettlementNodes.Button(
                builder,
                Host.BackButton,
                Key + ":back",
                () => Host.BackLabel,
                () => Host.Back(),
                null,
                null,
                () => NativeSelectionUtility.Select(Host.BackButton));
        }

        /// <summary>The cross that shuts the whole menu: the wielder band's on the town and the
        /// dwelling, the menu's own on the defence menu. An icon with no text of its own, so the mod
        /// names it.</summary>
        private void BuildClose(GraphBuilder builder)
        {
            Component close = Host.CloseButton;
            if (close == null || !Host.IsCloseVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => Host.Close());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, Key + ":close-button"), vtable, close));
        }
    }
}

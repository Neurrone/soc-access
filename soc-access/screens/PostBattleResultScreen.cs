using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Battle;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The page a battle ends on, made navigable as a graph. Four places to be, in the order the menu
    /// draws them: the attacker's side, the defender's side, the loot, and the two buttons.
    ///
    /// Measured 2026-09-07 (<c>PostBattleMenu</c>, opened by <c>AdventureBattleMenu</c>): the
    /// attacker's name and portrait top LEFT (the name at x 325 beside the portrait button, with the
    /// level and essence figures), the defender's name top RIGHT (x 681), the outcome header
    /// ("Defeat!") centred between them, and a "Troops Lost" column under each name - the attacker's
    /// on the left, the defender's on the right. The XP gained ("+310") is drawn under the name of
    /// whichever side is the local team (<c>_XPGainContainer</c> is moved to the attacker's or the
    /// defender's position in <c>AnimateResults</c>), which is why it is declared inside that side
    /// rather than as a page of its own. The buttons sit at the bottom, "Manual Battle" (x 483) LEFT
    /// of "Accept" (x 660), and are declared in the order of their drawn left edges, measured every
    /// build.
    ///
    /// THE OUTCOME HEADER IS THE SCREEN'S NAME, so arrival says "Defeat!" once and then goes on to
    /// read the attacker's commander, which is where focus starts: the page is read top-left first,
    /// and the header has already been said.
    ///
    /// The commander is a LINE rather than a control - the portrait's click opens nothing from here -
    /// carrying the portrait's own native tooltip, which is where the commander's stats and skills
    /// are read from. Each troop lost is a line too, named "N X lost" out of the amount the entry
    /// draws and the troop's name, with the entry's tooltip behind it; a side that lost nothing says
    /// "None", as it did before. The loot band is skipped entirely when the battle dropped none.
    ///
    /// The redo button COUNTS DOWN while turn timers are on (<c>InitiateDelayedFinalize</c> rewrites
    /// its text once a second, "Manual Battle (9)", and turns it non-interactable at zero), so its
    /// label is watched live and the count reads under the cursor.
    ///
    /// ESCAPE IS THE GAME'S AND IT CONFIRMS (<c>ConsumesBack</c> false): <c>PostBattleMenu.Show</c>
    /// registers <c>UI.ExitMenu</c> on <c>HandleConfirmClicked</c>, so the key accepts the result
    /// rather than dismissing the page.
    /// </summary>
    public sealed class PostBattleResultScreen : LiveScreen<PostBattleResultAdapter>
    {
        private const string AttackerStop = "post-battle-attacker";
        private const string DefenderStop = "post-battle-defender";
        private const string LootStop = "post-battle-loot";
        private const string ButtonsStop = "post-battle-buttons";

        private static readonly System.Reflection.FieldInfo PostBattleMenuResultField =
            AccessTools.Field(typeof(PostBattleMenu), "_result");
        private static readonly System.Reflection.FieldInfo PostBattleMenuOnHideField =
            AccessTools.Field(typeof(PostBattleMenu), "OnHidePostBattle");

        // A subject of its own per synthesized line, kept across rebuilds so the reconciler seats the
        // cursor back on the same one: the menu gives no component the screen can key the XP figure,
        // the returned-troops line or the "None" row on.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        // Resolving a portrait walks the menu's parents and the scene root, so each side is looked
        // for once per menu, hit or miss: a defender without a commander has no portrait to find,
        // and the search would otherwise run again every frame. A new menu starts both over.
        private CommanderHudPortraitAdapter _attackerPortrait;
        private CommanderHudPortraitAdapter _defenderPortrait;
        private bool _attackerPortraitProbed;
        private bool _defenderPortraitProbed;

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<PostBattleResultScreen>(FindActive());
        }

        public static PostBattleResultAdapter FindActive()
        {
            return FindActivePostBattleResultScreen();
        }

        public override string Key
        {
            get { return "post-battle-result"; }
        }

        /// <summary>Layer 28: the battle result, over what raised the battle.</summary>
        public override int Layer
        {
            get { return 28; }
        }

        /// <summary>The outcome the menu draws ("Defeat!"), which is the one thing that names this
        /// page.</summary>
        public override string ScreenName
        {
            get
            {
                string header = Live != null ? Live.HeaderText : null;
                return string.IsNullOrWhiteSpace(header) ? null : header;
            }
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

            // Each side's stop is NAMED, "Attacker" and "Defender" (owner ruling 2026-09-07): the
            // game draws no such caption, so the words are the mod's, said once on entering the stop.
            builder.BeginStop(AttackerStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Attacker));
            ControlId start = BuildSide(builder, attacker: true);
            builder.PopContext();

            builder.BeginStop(DefenderStop);
            builder.PushContext(ModText.Get(ModStrings.Screens.Defender));
            BuildSide(builder, attacker: false);
            builder.PopContext();

            builder.BeginStop(LootStop);
            BuildLoot(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);

            if (start != null)
            {
                // Top left, where the page is read from.
                builder.SetStart(start);
            }
        }

        // ---- one side of the page ----

        /// <summary>One side's column, in drawn order: the commander, the XP where it belongs to this
        /// side, the troops lost under it, and the eternal troops the battle returned. Answers the
        /// commander's node, which is where the page starts.</summary>
        private ControlId BuildSide(GraphBuilder builder, bool attacker)
        {
            string key = attacker ? "attacker" : "defender";
            ControlId commander = BuildCommander(builder, attacker);

            if (Live.XpBelongsToAttacker == attacker && Live.XpVisible)
            {
                AddLine(builder, key + "-xp", () => Live.XpText);
            }

            // The troops lost, under the caption the game draws over the column, as a region: its
            // name is said once on entering it, and each stack reads as the count and the name the
            // entry draws, with no suffix (owner ruling 2026-09-07).
            builder.PushContext(attacker ? Live.AttackerTroopsCaption : Live.DefenderTroopsCaption);
            builder.SetRegion("post-battle:" + key + "-troops");
            BuildEntries(
                builder,
                key + "-troop",
                attacker ? Live.AttackerTroopsLost : Live.DefenderTroopsLost,
                addNoneWhenEmpty: true);
            builder.SetRegion(null);
            builder.PopContext();

            bool returned = attacker ? Live.AttackerReturnedTroopsVisible : Live.DefenderReturnedTroopsVisible;
            if (returned)
            {
                AddLine(
                    builder,
                    key + "-returned",
                    () => attacker ? Live.AttackerReturnedTroopsText : Live.DefenderReturnedTroopsText);
            }

            return commander;
        }

        /// <summary>The commander's name, as the portrait the game draws it beside: a line carrying
        /// the portrait's native tooltip, which is where its stats, skills and status are read. Where
        /// the portrait cannot be resolved the name is still said, off the menu's own label.</summary>
        private ControlId BuildCommander(GraphBuilder builder, bool attacker)
        {
            CommanderHudPortraitAdapter portrait = attacker ? AttackerPortrait : DefenderPortrait;
            string key = attacker ? "attacker-commander" : "defender-commander";
            Func<string> name = () => portrait != null
                ? portrait.Name
                : (attacker ? Live.AttackerCommanderText : Live.DefenderCommanderText);
            Tooltip tooltip = portrait != null
                ? Portrait.BuildNativeTooltip(() => portrait.TooltipTarget, portrait.Localization, portrait.RefreshTooltip)
                : null;
            NodeVtable vtable = GraphNodes.Text(name, null, tooltip);

            Component target = portrait != null ? portrait.TooltipTarget : null;
            if (target == null)
            {
                ControlId synthetic = ControlId.For(Marker(key), "post-battle:" + key);
                builder.AddItem(new SyntheticNode(synthetic, vtable));
                return synthetic;
            }

            // The mouse resting on the portrait is what makes the game draw its tooltip, and the
            // portrait refreshes its own contents on the way.
            vtable.OnFocusVisual = portrait.Focus;
            ControlId id = ControlId.For(target, "post-battle:" + key);
            builder.AddItem(new DrawnNode(id, vtable, target));
            return id;
        }

        // ---- the troops lost and the loot ----

        /// <summary>The loot band, named by the caption the game draws over it ("Battle Loot"): the
        /// artifacts and resources the winner picks up, which are the only equipment this page draws.
        /// </summary>
        private void BuildLoot(GraphBuilder builder)
        {
            builder.PushContext(GameText.Get("Adventure/AdventurePostBattleMenu/BattleLoot", null));
            BuildEntries(builder, "loot", Live.Loot, addNoneWhenEmpty: false);
            builder.PopContext();
        }

        /// <summary>A band of read-only lines, one per entry the menu is drawing, each carrying the
        /// entry's own tooltip. A band the menu drew nothing in says "None" where the page always
        /// draws the band (the troops-lost columns) and is skipped where it does not (the loot).
        /// </summary>
        private void BuildEntries(
            GraphBuilder builder,
            string key,
            IReadOnlyList<PostBattleResultAdapter.ResultEntry> entries,
            bool addNoneWhenEmpty)
        {
            int declared = 0;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                PostBattleResultAdapter.ResultEntry entry = entries[i];
                if (entry == null || !entry.IsVisible)
                {
                    continue;
                }

                string label = EntryLabel(entry);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Text(() => label, null, entry.Tooltip);
                Component subject = entry.Subject;
                if (subject != null)
                {
                    // The structural key carries the index: a ControlId is equal on its structural
                    // key alone, so two entries under one key would be one duplicate id.
                    builder.AddItem(new DrawnNode(
                        ControlId.For(subject, "post-battle:" + key + "/" + i),
                        vtable,
                        subject));
                }
                else
                {
                    builder.AddItem(new SyntheticNode(
                        ControlId.Structural("post-battle:" + key + "/" + i),
                        vtable));
                }

                declared++;
            }

            if (declared == 0 && addNoneWhenEmpty)
            {
                AddLine(builder, key + "-none", () => ModText.Get(ModStrings.Screens.None));
            }
        }

        /// <summary>What a line says: a lost troop is the amount the entry drew and the troop's name,
        /// under the "Troops Lost" region that says the loss; anything else is the name alone.</summary>
        private static string EntryLabel(PostBattleResultAdapter.ResultEntry entry)
        {
            if (!entry.IsLostTroop)
            {
                return entry.Name;
            }

            string label;
            if (string.IsNullOrWhiteSpace(entry.Amount))
            {
                label = entry.Name;
            }
            else if (string.IsNullOrWhiteSpace(entry.Name))
            {
                label = entry.Amount;
            }
            else
            {
                label = ModText.Get(ModStrings.Common.ResourceAmount, entry.Amount, entry.Name);
            }

            return string.IsNullOrWhiteSpace(label) ? string.Empty : label;
        }

        // ---- the buttons ----

        /// <summary>The two buttons in the order their left edges are drawn in, measured every build:
        /// Manual Battle then Accept.</summary>
        private void BuildButtons(GraphBuilder builder)
        {
            List<KeyValuePair<float, NodeDeclaration>> drawn = new List<KeyValuePair<float, NodeDeclaration>>(2);
            Component accept = Live.IsAcceptButtonVisible() ? Live.AcceptButton : null;
            if (accept != null)
            {
                drawn.Add(new KeyValuePair<float, NodeDeclaration>(Left(accept), AcceptNode(accept)));
            }

            Component redo = Live.IsRedoManualBattleButtonVisible() ? Live.RedoManualBattleButton : null;
            if (redo != null)
            {
                drawn.Add(new KeyValuePair<float, NodeDeclaration>(Left(redo), RedoNode(redo)));
            }

            if (drawn.Count == 2 && drawn[1].Key < drawn[0].Key)
            {
                KeyValuePair<float, NodeDeclaration> first = drawn[0];
                drawn[0] = drawn[1];
                drawn[1] = first;
            }

            for (int i = 0; i < drawn.Count; i++)
            {
                builder.AddItem(drawn[i].Value);
            }
        }

        private NodeDeclaration AcceptNode(Component button)
        {
            NodeVtable vtable = GraphNodes.Button(
                () => Live.AcceptButtonLabel,
                () => Live.Accept(),
                Live.IsAcceptButtonEnabled,
                Live.AcceptButtonTooltip);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            return new DrawnNode(ControlId.For(button, "post-battle:accept"), vtable, button);
        }

        private NodeDeclaration RedoNode(Component button)
        {
            NodeVtable vtable = GraphNodes.Button(
                () => Live.RedoManualBattleButtonLabel,
                () => Live.RedoManualBattle(),
                Live.IsRedoManualBattleButtonEnabled,
                Live.RedoManualBattleButtonTooltip);
            // The game rewrites this button's text once a second while the turn timer runs it down, so
            // the count is read under the cursor rather than only on arrival.
            vtable.Announcements[0] = new NodeAnnouncement(
                () => Live.RedoManualBattleButtonLabel,
                live: true,
                kind: AnnouncementKinds.Label);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button);
            return new DrawnNode(ControlId.For(button, "post-battle:redo"), vtable, button);
        }

        private static float Left(Component button)
        {
            return button != null && button.transform != null ? button.transform.position.x : 0f;
        }

        // ---- the lines the menu gives nothing to key on ----

        private void AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(text()))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "post-battle:" + key),
                GraphNodes.Text(text)));
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }

        public override void OnLiveChanged(PostBattleResultAdapter previous)
        {
            _attackerPortrait = null;
            _defenderPortrait = null;
            _attackerPortraitProbed = false;
            _defenderPortraitProbed = false;
        }

        private CommanderHudPortraitAdapter AttackerPortrait
        {
            get
            {
                if (!_attackerPortraitProbed)
                {
                    _attackerPortrait = Live.AttackerCommanderPortrait;
                    _attackerPortraitProbed = true;
                }

                return _attackerPortrait;
            }
        }

        private CommanderHudPortraitAdapter DefenderPortrait
        {
            get
            {
                if (!_defenderPortraitProbed)
                {
                    _defenderPortrait = Live.DefenderCommanderPortrait;
                    _defenderPortraitProbed = true;
                }

                return _defenderPortrait;
            }
        }

        // ---- finding the live menu ----

        private static PostBattleResultAdapter FindActivePostBattleResultScreen()
        {
            PostBattleMenu menu = FindActivePostBattleMenu();
            if (!IsActive(menu) || GetResult(menu) == null)
            {
                return null;
            }

            AdventureBattleMenu battleMenu = ResolveOwningBattleMenu(menu);
            PostBattleResultAdapter adapter = new PostBattleResultAdapter(battleMenu, menu);
            return adapter.IsPresent() ? adapter : null;
        }

        private static PostBattleMenu FindActivePostBattleMenu()
        {
            PostBattleMenu[] menus = Resources.FindObjectsOfTypeAll<PostBattleMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                if (IsActive(menus[i]) && GetResult(menus[i]) != null)
                {
                    return menus[i];
                }
            }

            return null;
        }

        private static IBattleResult GetResult(PostBattleMenu menu)
        {
            return menu != null && PostBattleMenuResultField != null
                ? PostBattleMenuResultField.GetValue(menu) as IBattleResult
                : null;
        }

        private static AdventureBattleMenu ResolveOwningBattleMenu(PostBattleMenu menu)
        {
            Action<PostBattleMenu.HideAction> onHidePostBattle = menu != null && PostBattleMenuOnHideField != null
                ? PostBattleMenuOnHideField.GetValue(menu) as Action<PostBattleMenu.HideAction>
                : null;
            if (onHidePostBattle == null)
            {
                return null;
            }

            Delegate[] invocationList = onHidePostBattle.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                AdventureBattleMenu battleMenu = invocationList[i]?.Target as AdventureBattleMenu;
                if (battleMenu != null)
                {
                    return battleMenu;
                }
            }

            return null;
        }

        private static bool IsActive(PostBattleMenu menu)
        {
            return menu != null
                && menu.gameObject != null
                && menu.gameObject.activeInHierarchy;
        }
    }
}
